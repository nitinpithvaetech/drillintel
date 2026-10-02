using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DrillIntel.Converters;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Models;
using DrillIntel.Projects;
using DrillIntel.ViewModels;
using System.Windows;
using Xunit;

namespace DrillIntel.Tests;

public class EditTimeLogTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ProjectSession _session;
    private readonly WellDataRepository _repo;

    public EditTimeLogTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"edittimelog_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(_tempDbPath);

        _session = new ProjectSession();
        _session.Load(_tempDbPath);

        _repo = new WellDataRepository(_session);
    }

    public void Dispose()
    {
        _session.Dispose();
        try
        {
            if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
        }
        catch { }
    }

    private async Task<(string wellId, string wellboreId, string logId, string tableName)> SeedTimelogAsync(
        string logName = "Timelog1",
        string serviceCompany = "Baker Hughes",
        string edrProvider = "Pason",
        string runNo = "42",
        string description = "Test timelog description",
        bool primaryLog = true,
        bool remarksLog = false,
        bool noAutoCalc = true,
        double startingHoleDepth = 1500.5,
        bool linkToParent = false)
    {
        var conn = _session.GetConnection();
        string wellId = "WELL-001";
        string wellboreId = "WB-001";
        string logId = "TL-001";
        string tableName = "timeLog_test_data";

        // Seed Well
        await conn.ExecuteAsync(
            "INSERT OR REPLACE INTO VMX_WELL (WELL_ID, WELL_NAME, FIELD) VALUES (@wId, @wName, 'Field A');",
            new { wId = wellId, wName = "Test Well Alpha" });

        // Seed Wellbore
        await conn.ExecuteAsync(
            "INSERT OR REPLACE INTO VMX_WELLBORE (WELLBORE_ID, WELL_ID, WELLBORE_NAME) VALUES (@wbId, @wId, 'Wellbore 1');",
            new { wbId = wellboreId, wId = wellId });

        // Seed VMX_TIME_LOG
        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO VMX_TIME_LOG (
                WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, RUN_NO, SERVICE_COMPANY,
                DATA_TABLE_NAME, DESCRIPTION, EDR_PROVIDER, PRIMARY_LOG, REMARKS_LOG,
                DONT_CALC_HDTH, STARTING_HDTH, LINK_TO_PARENT, DONT_MOVE_AHEAD, DUPLICATE_ACTION
            ) VALUES (
                @wellId, @wellboreId, @logId, @logName, @runNo, @serviceCompany,
                @tableName, @description, @edrProvider, @primaryLog, @remarksLog,
                @noAutoCalc, @startingHoleDepth, @linkToParent, 0, 2
            );", new
        {
            wellId,
            wellboreId,
            logId,
            logName,
            runNo,
            serviceCompany,
            tableName,
            description,
            edrProvider,
            primaryLog = primaryLog ? 1 : 0,
            remarksLog = remarksLog ? 1 : 0,
            noAutoCalc = noAutoCalc ? 1 : 0,
            startingHoleDepth,
            linkToParent = linkToParent ? 1 : 0
        });

        // Seed VMX_TIME_LOG_COLUMNS
        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO VMX_TIME_LOG_COLUMNS (
                WELL_ID, WELLBORE_ID, LOG_ID, MNEMONIC, CHANNEL_NAME, DATA_TYPE,
                UNIT, VUMAX_UNIT_ID, VALUE_TYPE, VALUE_QUERY, WITSML_MNEMONIC,
                WRITE_BACK, NO_INTERPOLATE, COLUMN_ORDER
            ) VALUES 
            (@wellId, @wellboreId, @logId, 'DATETIME', 'Date and Time', 'DateTime', '', '', 0, '', 'DATETIME', 1, 0, 1),
            (@wellId, @wellboreId, @logId, 'DEPTH', 'Bit Depth', 'Double', 'm', 'm', 0, '', 'DEPT', 1, 0, 2),
            (@wellId, @wellboreId, @logId, 'HKLD', 'Hook Load', 'Double', 'kN', 'kN', 0, '', 'HKLD', 1, 0, 3);",
            new { wellId, wellboreId, logId });

        // Seed data table
        await conn.ExecuteAsync($@"
            CREATE TABLE IF NOT EXISTS [{tableName}] (
                DATA_INDEX DECIMAL(8),
                [DATETIME] DATETIME,
                [DEPTH] REAL,
                [HKLD] REAL,
                [ROP] REAL
            );");

        await conn.ExecuteAsync($@"
            INSERT INTO [{tableName}] (DATA_INDEX, [DATETIME], [DEPTH], [HKLD], [ROP])
            VALUES (1, '2026-03-01 10:00:00', 1500.5, 450.0, 25.0);");

        return (wellId, wellboreId, logId, tableName);
    }

    [Fact]
    public async Task DialogInitialization_PopulatesLogInformation_FromTimeLog()
    {
        await SeedTimelogAsync(
            logName: "Timelog1",
            serviceCompany: "Schlumberger (SLB)",
            edrProvider: "Pason",
            runNo: "12",
            description: "High pressure test log",
            primaryLog: true,
            remarksLog: false,
            noAutoCalc: true,
            startingHoleDepth: 2500.75);

        var vm = new EditTimeLogViewModel(_session, _repo, "TL-001");
        await vm.InitializeAsync();

        Assert.Equal("Timelog1", vm.LogName);
        Assert.Equal("Schlumberger (SLB)", vm.ServiceCompany);
        Assert.Equal("Pason", vm.EdrProvider);
        Assert.Equal("12", vm.RunNo);
        Assert.Equal("High pressure test log", vm.Description);
        Assert.True(vm.PrimaryLog);
        Assert.False(vm.RemarksLog);
        Assert.True(vm.NoAutoCalc);
        Assert.Equal("2500.75", vm.StartingHoleDepth);
        Assert.Equal("timeLog_test_data", vm.DataTableName);
    }

    [Fact]
    public async Task DialogInitialization_PopulatesChannelsGrid_FromDataTableAndColumns()
    {
        await SeedTimelogAsync();

        var vm = new EditTimeLogViewModel(_session, _repo, "TL-001");
        await vm.InitializeAsync();

        Assert.NotEmpty(vm.Channels);

        // Verify required columns exist
        var depthChannel = vm.Channels.FirstOrDefault(c => c.Mnemonic == "DEPTH");
        Assert.NotNull(depthChannel);
        Assert.True(depthChannel.Upload);
        Assert.Equal("m", depthChannel.Unit);
        Assert.Equal("m", depthChannel.VuMaxUnitId);
        Assert.Equal("Bit Depth", depthChannel.Description);
        Assert.Equal("DEPT", depthChannel.UploadMnemonic);
        Assert.Equal("0", depthChannel.ValueType);
        Assert.False(depthChannel.DoNotInterpol);

        // Also verify ROP was picked up from table columns
        var ropChannel = vm.Channels.FirstOrDefault(c => c.Mnemonic == "ROP");
        Assert.NotNull(ropChannel);
    }

    [Fact]
    public async Task DialogInitialization_WhenDataTableStoresChannelRows_PopulatesDirectly()
    {
        var conn = _session.GetConnection();
        string logId = "TL-CHAN-TBL";
        string chanTable = "custom_channel_data_tbl";

        // Insert into VMX_TIME_LOG
        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO VMX_TIME_LOG (
                WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, DATA_TABLE_NAME, SERVICE_COMPANY, EDR_PROVIDER, RUN_NO
            ) VALUES ('W1', 'WB1', @logId, 'DirectChannelLog', @chanTable, 'Halliburton', 'NOV', '5');",
            new { logId, chanTable });

        // Create table that stores channels directly as rows
        await conn.ExecuteAsync($@"
            CREATE TABLE [{chanTable}] (
                Upload INTEGER,
                Mnemonic TEXT,
                Unit TEXT,
                [VuMax Unit ID] TEXT,
                Description TEXT,
                [Upload Mnemonic] TEXT,
                [Value Type] TEXT,
                Expression TEXT,
                [Do Not Interpol] INTEGER
            );");

        await conn.ExecuteAsync($@"
            INSERT INTO [{chanTable}] 
            (Upload, Mnemonic, Unit, [VuMax Unit ID], Description, [Upload Mnemonic], [Value Type], Expression, [Do Not Interpol])
            VALUES 
            (1, 'SPP', 'psi', 'psi', 'Standpipe Pressure', 'SPP', '0', '', 0),
            (1, 'TORQUE', 'ft-lbf', 'ft-lbf', 'Drillstring Torque', 'TORQ', '1', 'HKLD * 0.1', 1);");

        var vm = new EditTimeLogViewModel(_session, _repo, logId);
        await vm.InitializeAsync();

        Assert.Equal("DirectChannelLog", vm.LogName);
        Assert.Equal("Halliburton", vm.ServiceCompany);
        Assert.Equal("NOV", vm.EdrProvider);
        Assert.Equal("5", vm.RunNo);
        Assert.Equal(2, vm.Channels.Count);

        var spp = vm.Channels.First(c => c.Mnemonic == "SPP");
        Assert.True(spp.Upload);
        Assert.Equal("psi", spp.Unit);
        Assert.Equal("psi", spp.VuMaxUnitId);
        Assert.Equal("Standpipe Pressure", spp.Description);
        Assert.Equal("SPP", spp.UploadMnemonic);
        Assert.Equal("0", spp.ValueType);
        Assert.False(spp.DoNotInterpol);

        var torq = vm.Channels.First(c => c.Mnemonic == "TORQUE");
        Assert.Equal("TORQ", torq.UploadMnemonic);
        Assert.Equal("1", torq.ValueType);
        Assert.Equal("HKLD * 0.1", torq.Expression);
        Assert.True(torq.DoNotInterpol);
    }

    [Fact]
    public async Task ChannelsTab_AddChannel_AddsNewItemWithUniqueMnemonic()
    {
        await SeedTimelogAsync();
        var vm = new EditTimeLogViewModel(_session, _repo, "TL-001");
        await vm.InitializeAsync();

        int initialCount = vm.Channels.Count;
        vm.AddChannelCommand.Execute(null);

        Assert.Equal(initialCount + 1, vm.Channels.Count);
        var added = vm.SelectedChannel;
        Assert.NotNull(added);
        Assert.StartsWith("CHAN_", added.Mnemonic);
        Assert.True(added.Upload);
    }

    [Fact]
    public async Task ChannelsTab_RemoveChannel_RemovesSelectedItem()
    {
        await SeedTimelogAsync();
        var vm = new EditTimeLogViewModel(_session, _repo, "TL-001");
        await vm.InitializeAsync();

        var depthChannel = vm.Channels.First(c => c.Mnemonic == "DEPTH");
        vm.SelectedChannel = depthChannel;

        vm.RemoveChannelCommand.Execute(depthChannel);

        Assert.DoesNotContain(vm.Channels, c => c.Mnemonic == "DEPTH");
    }

    [Fact]
    public async Task ChannelsTab_ApplyUnitConversionProfile_StandardizesUnits()
    {
        await SeedTimelogAsync();
        var vm = new EditTimeLogViewModel(_session, _repo, "TL-001");
        await vm.InitializeAsync();

        // Apply Imperial profile
        vm.ApplyUnitConversionProfileCommand.Execute("Imperial");
        var depthCh = vm.Channels.First(c => c.Mnemonic == "DEPTH");
        var hkldCh = vm.Channels.First(c => c.Mnemonic == "HKLD");
        Assert.Equal("ft", depthCh.Unit);
        Assert.Equal("klbf", hkldCh.Unit);

        // Apply Metric profile
        vm.ApplyUnitConversionProfileCommand.Execute("Metric");
        Assert.Equal("m", depthCh.Unit);
        Assert.Equal("kN", hkldCh.Unit);
    }

    [Fact]
    public async Task LinkTimeLogTab_LoadsAvailableWells_Wellbores_AndLogs_ExcludingCurrent()
    {
        await SeedTimelogAsync();
        var conn = _session.GetConnection();

        // Add a second timelog to link to
        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO VMX_TIME_LOG (
                WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME
            ) VALUES ('WELL-001', 'WB-001', 'TL-002', 'ParentTimelog');");

        var vm = new EditTimeLogViewModel(_session, _repo, "TL-001");
        await vm.InitializeAsync();

        Assert.NotEmpty(vm.AvailableWells);
        Assert.NotEmpty(vm.AvailableWellbores);
        Assert.NotEmpty(vm.AvailableTimeLogs);

        // Current log TL-001 should not be in the list of available parent logs
        Assert.DoesNotContain(vm.AvailableTimeLogs, tl => tl.LogId == "TL-001");
        Assert.Contains(vm.AvailableTimeLogs, tl => tl.LogId == "TL-002");
    }

    [Fact]
    public async Task Validation_EmptyLogName_SetsStatusErrorAndDoesNotSave()
    {
        await SeedTimelogAsync();
        var vm = new EditTimeLogViewModel(_session, _repo, "TL-001");
        await vm.InitializeAsync();

        vm.LogName = ""; // Empty name
        bool? closeResult = null;
        vm.RequestClose += res => closeResult = res;

        await vm.SaveAsync();

        Assert.Null(closeResult);
        Assert.True(vm.IsStatusError);
        Assert.Contains("Log Name is required", vm.StatusMessage);
    }

    [Fact]
    public async Task Validation_InvalidRunNo_SetsStatusErrorAndDoesNotSave()
    {
        await SeedTimelogAsync();
        var vm = new EditTimeLogViewModel(_session, _repo, "TL-001");
        await vm.InitializeAsync();

        vm.RunNo = "ABC_NOT_NUMERIC";
        bool? closeResult = null;
        vm.RequestClose += res => closeResult = res;

        await vm.SaveAsync();

        Assert.Null(closeResult);
        Assert.True(vm.IsStatusError);
        Assert.Contains("Run No. must be numeric", vm.StatusMessage);
    }

    [Fact]
    public async Task Validation_InvalidStartingHoleDepth_SetsStatusErrorAndDoesNotSave()
    {
        await SeedTimelogAsync();
        var vm = new EditTimeLogViewModel(_session, _repo, "TL-001");
        await vm.InitializeAsync();

        vm.StartingHoleDepth = "XYZ";
        bool? closeResult = null;
        vm.RequestClose += res => closeResult = res;

        await vm.SaveAsync();

        Assert.Null(closeResult);
        Assert.True(vm.IsStatusError);
        Assert.Contains("Starting Hole Depth must be a valid number", vm.StatusMessage);
    }

    [Fact]
    public async Task Validation_DuplicateChannelMnemonic_SetsStatusErrorAndDoesNotSave()
    {
        await SeedTimelogAsync();
        var vm = new EditTimeLogViewModel(_session, _repo, "TL-001");
        await vm.InitializeAsync();

        // Introduce duplicate mnemonic
        // --- [OLD LOGIC (TimelogChannelItem)] ---
        // vm.Channels.Add(new TimelogChannelItem { Mnemonic = "DEPTH", Unit = "m", Description = "Duplicate depth" });
        // --- [NEW LOGIC (LogChannel from DrillIntel.Data.Objects)] ---
        vm.Channels.Add(new LogChannel
        {
            Mnemonic = "DEPTH",
            Unit = "m",
            Description = "Duplicate depth"
        });

        bool? closeResult = null;
        vm.RequestClose += res => closeResult = res;

        await vm.SaveAsync();

        Assert.Null(closeResult);
        Assert.True(vm.IsStatusError);
        Assert.Contains("Duplicate channel mnemonic", vm.StatusMessage);
    }

    [Fact]
    public async Task SaveAsync_PersistsAllChangesToTimeLogAndColumns()
    {
        await SeedTimelogAsync();
        var vm = new EditTimeLogViewModel(_session, _repo, "TL-001");
        await vm.InitializeAsync();

        // Update fields
        vm.LogName = "UpdatedTimelogName";
        vm.ServiceCompany = "Halliburton";
        vm.EdrProvider = "NOV";
        vm.RunNo = "99";
        vm.Description = "Updated description";
        vm.PrimaryLog = false;
        vm.RemarksLog = true;
        vm.NoAutoCalc = false;
        vm.StartingHoleDepth = "3200.5";
        vm.LinkToParent = true;
        vm.DontMoveAhead = true;
        vm.SelectedDuplicateAction = "Replace";

        // Modify a channel
        var hkld = vm.Channels.First(c => c.Mnemonic == "HKLD");
        hkld.Unit = "klbf";
        hkld.Description = "Modified Hookload";
        hkld.DoNotInterpol = true;

        bool? closeResult = null;
        vm.RequestClose += res => closeResult = res;

        await vm.SaveAsync();

        Assert.True(closeResult);

        // Verify database updated
        var conn = _session.GetConnection();
        var row = await conn.QuerySingleAsync<dynamic>("SELECT * FROM VMX_TIME_LOG WHERE LOG_ID = 'TL-001';");
        Assert.Equal("UpdatedTimelogName", (string)row.LOG_NAME);
        Assert.Equal("Halliburton", (string)row.SERVICE_COMPANY);
        Assert.Equal("NOV", (string)row.EDR_PROVIDER);
        Assert.Equal("99", (string)row.RUN_NO);
        Assert.Equal("Updated description", (string)row.DESCRIPTION);
        Assert.Equal(0, Convert.ToInt32(row.PRIMARY_LOG));
        Assert.Equal(1, Convert.ToInt32(row.REMARKS_LOG));
        Assert.Equal(0, Convert.ToInt32(row.DONT_CALC_HDTH));
        Assert.Equal(3200.5, Convert.ToDouble(row.STARTING_HDTH));
        Assert.Equal(1, Convert.ToInt32(row.LINK_TO_PARENT));
        Assert.Equal(1, Convert.ToInt32(row.DONT_MOVE_AHEAD));
        Assert.Equal(0, Convert.ToInt32(row.DUPLICATE_ACTION)); // 0 = Replace

        // Verify VMX_TIME_LOG_COLUMNS
        var colRow = await conn.QuerySingleAsync<dynamic>(
            "SELECT * FROM VMX_TIME_LOG_COLUMNS WHERE LOG_ID = 'TL-001' AND MNEMONIC = 'HKLD';");
        Assert.Equal("klbf", (string)colRow.UNIT);
        Assert.Equal("Modified Hookload", (string)colRow.CHANNEL_NAME);
        Assert.Equal(1, Convert.ToInt32(colRow.NO_INTERPOLATE));
    }

    [Fact]
    public async Task CancelCommand_InvokesRequestCloseWithFalse_AndDoesNotPersist()
    {
        await SeedTimelogAsync(logName: "OriginalName");
        var vm = new EditTimeLogViewModel(_session, _repo, "TL-001");
        await vm.InitializeAsync();

        vm.LogName = "DiscardedName";

        bool? closeResult = null;
        vm.RequestClose += res => closeResult = res;

        vm.CancelCommand.Execute(null);

        Assert.False(closeResult);

        // Verify database was NOT updated
        var conn = _session.GetConnection();
        var logName = await conn.ExecuteScalarAsync<string>("SELECT LOG_NAME FROM VMX_TIME_LOG WHERE LOG_ID = 'TL-001';");
        Assert.Equal("OriginalName", logName);
    }

    [Fact]
    public async Task DashboardViewModel_EditTimeLogCommand_OpensDialogWithSelectedNode()
    {
        await SeedTimelogAsync(logName: "TimelogNodeTest");
        var dashboard = new DashboardViewModel(_session, _repo);
        await dashboard.LoadDataAsync();

        var wellNode = dashboard.WellTree[0];
        var timeFolder = wellNode.Children.First(c => c.Name == "Timelogs");
        var timeNode = timeFolder.Children.First();

        EditTimeLogViewModel? openedVm = null;
        dashboard.OpenEditTimeLogDialogHandler = (vm) =>
        {
            openedVm = vm;
            return false;
        };

        await dashboard.EditTimeLogAsync(timeNode);

        Assert.NotNull(openedVm);
        Assert.Equal("TimelogNodeTest", openedVm.LogName);
        Assert.Equal("TL-001", openedVm.LogId);
    }

    [Fact]
    public void ContextMenu_HasEditTimeLogOption_ForTimeLogType()
    {
        var converter = new EnumToVisibilityConverter();
        var visibleForTimeLog = converter.Convert(WellTreeNodeType.TimeLog, typeof(Visibility), "TimeLog", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Visible, visibleForTimeLog);

        var hiddenForDepthLog = converter.Convert(WellTreeNodeType.DepthLog, typeof(Visibility), "TimeLog", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Collapsed, hiddenForDepthLog);

        var hiddenForWell = converter.Convert(WellTreeNodeType.Well, typeof(Visibility), "TimeLog", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Collapsed, hiddenForWell);
    }

    [Fact]
    public async Task TimeLogService_UpdateData_UpdatesDataTableRowsMatchingDateTime()
    {
        var (wellId, wellboreId, logId, tableName) = await SeedTimelogAsync();
        var dataService = _session.GetDataService();

        // Create update DataTable
        var updateTable = new System.Data.DataTable();
        updateTable.Columns.Add("DATETIME", typeof(string));
        updateTable.Columns.Add("DEPT", typeof(double)); // WITSML mnemonic mapped to DEPTH
        updateTable.Columns.Add("HKLD", typeof(double)); // WITSML mnemonic mapped to HKLD

        var row = updateTable.NewRow();
        row["DATETIME"] = "01-Mar-2026 10:00:00";
        row["DEPT"] = 1650.75;
        row["HKLD"] = 520.25;
        updateTable.Rows.Add(row);

        string lastError = "";
        bool ok = DrillIntel.Data.Objects.DataObjects.Services.TimeLogService.updateData(
            dataService, wellId, wellboreId, logId, updateTable, "UTC", ref lastError);

        Assert.True(ok, $"updateData failed: {lastError}");

        // Verify updated values in SQLite table
        var conn = _session.GetConnection();
        var updatedDept = await conn.ExecuteScalarAsync<double>(
            $"SELECT DEPTH FROM [{tableName}] WHERE DATETIME = '2026-03-01 10:00:00' OR DATETIME = '01-Mar-2026 10:00:00';");
        var updatedHkld = await conn.ExecuteScalarAsync<double>(
            $"SELECT HKLD FROM [{tableName}] WHERE DATETIME = '2026-03-01 10:00:00' OR DATETIME = '01-Mar-2026 10:00:00';");

        Assert.Equal(1650.75, updatedDept);
        Assert.Equal(520.25, updatedHkld);
    }

    [Fact]
    public async Task TimeLog_UpdateData_Wrapper_SuccessfullyUpdatesData()
    {
        var (wellId, wellboreId, logId, tableName) = await SeedTimelogAsync();
        var dataService = _session.GetDataService();

        var updateTable = new System.Data.DataTable();
        updateTable.Columns.Add("DATETIME", typeof(string));
        updateTable.Columns.Add("HKLD", typeof(double));

        var row = updateTable.NewRow();
        row["DATETIME"] = "01-Mar-2026 10:00:00";
        row["HKLD"] = 999.0;
        updateTable.Rows.Add(row);

        string lastError = "";
        bool ok = DrillIntel.Data.Objects.DataObjects.Models.TimeLog.updateData(
            dataService, wellId, wellboreId, logId, updateTable, "UTC", ref lastError);

        Assert.True(ok, $"TimeLog.updateData failed: {lastError}");

        var conn = _session.GetConnection();
        var updatedHkld = await conn.ExecuteScalarAsync<double>(
            $"SELECT HKLD FROM [{tableName}] WHERE DATETIME = '2026-03-01 10:00:00' OR DATETIME = '01-Mar-2026 10:00:00';");
        Assert.Equal(999.0, updatedHkld);
    }

    [Fact]
    public async Task TimeLogService_RemoveTimeLog_RemovesMetadataAndDropsTable()
    {
        var (wellId, wellboreId, logId, tableName) = await SeedTimelogAsync();
        var dataService = _session.GetDataService();
        var conn = _session.GetConnection();

        // Verify table exists before deletion
        int countBefore = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM [{tableName}];");
        Assert.True(countBefore > 0);

        string lastError = "";
        bool ok = DrillIntel.Data.Objects.DataObjects.Services.TimeLogService.RemoveTimeLog(
            dataService, wellId, wellboreId, logId, dropDataTable: true, ref lastError);

        Assert.True(ok, $"RemoveTimeLog failed: {lastError}");

        // Verify VMX_TIME_LOG row deleted
        int logCount = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM VMX_TIME_LOG WHERE LOG_ID = '{logId}';");
        Assert.Equal(0, logCount);

        // Verify VMX_TIME_LOG_COLUMNS rows deleted
        int colCount = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM VMX_TIME_LOG_COLUMNS WHERE LOG_ID = '{logId}';");
        Assert.Equal(0, colCount);

        // Verify physical table was dropped
        int tableExists = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{tableName}';");
        Assert.Equal(0, tableExists);
    }

    [Fact]
    public async Task TimeLog_Remove_InstanceMethod_SuccessfullyDeletes()
    {
        var (wellId, wellboreId, logId, tableName) = await SeedTimelogAsync();
        var dataService = _session.GetDataService();
        var conn = _session.GetConnection();

        // Load TimeLog instance
        var timeLog = DrillIntel.Data.Objects.DataObjects.Models.TimeLog.loadTimeLog(dataService, wellId, wellboreId, logId);
        Assert.NotNull(timeLog);

        // Call instance Remove with dropDataTable: true
        bool ok = timeLog.Remove(dataService, dropDataTable: true);
        Assert.True(ok);

        // Verify metadata deleted
        int logCount = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM VMX_TIME_LOG WHERE LOG_ID = '{logId}';");
        Assert.Equal(0, logCount);

        // Verify physical table dropped
        int tableExists = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{tableName}';");
        Assert.Equal(0, tableExists);
    }

    [Fact]
    public async Task ChannelPropertiesViewModel_PopulatesUnitsFromProjectUnitMaster()
    {
        await SeedTimelogAsync();
        var dataService = _session.GetDataService();

        // Seed custom unit in VMX_UNIT_MASTER
        DrillIntel.Models.Unit.Add(dataService, new DrillIntel.Models.Unit("custom_uom_test", "CustomCategory", "Custom UOM"), tableName: DrillIntel.Models.Unit.ProjectTableName);

        var vm = new ChannelPropertiesViewModel(dataService);

        Assert.Contains("custom_uom_test", vm.AvailableUnits);
        Assert.Equal("Expression", vm.ValueType);
    }

    [Fact]
    public void ChannelPropertiesViewModel_DefaultsToExpressionAndGeneratesLogChannel()
    {
        var vm = new ChannelPropertiesViewModel(null, null, null, 1)
        {
            Mnemonic = "TEST_EXPR",
            Description = "Test Expression Channel",
            SelectedUnit = "m/hr",
            Expression = "DEPTH / 10.0",
            IsStoredProcedure = true,
            Parameters = "@Param1, @Param2"
        };

        Assert.Equal("Expression", vm.ValueType);

        var ch = vm.ToLogChannel();
        Assert.Equal("TEST_EXPR", ch.Mnemonic);
        Assert.Equal("Test Expression Channel", ch.Description);
        Assert.Equal("m/hr", ch.Unit);
        Assert.Equal(1, ch.valueType); // Expression type is 1
        Assert.Equal("1", ch.ValueType);
        Assert.Equal("DEPTH / 10.0", ch.Expression);
        Assert.True(ch.isStoredProc);
        Assert.Equal("@Param1, @Param2", ch.StoredProcParams);
        Assert.True(ch.Upload);
    }

    [Fact]
    public void ChannelPropertiesViewModel_ValidatesBlankAndDuplicateMnemonic()
    {
        var existing = new List<string> { "DEPTH", "ROP" };
        var vm = new ChannelPropertiesViewModel(null, existing)
        {
            Mnemonic = ""
        };

        vm.Ok();
        Assert.True(vm.HasError);
        Assert.Contains("blank", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        vm.Mnemonic = "depth"; // Duplicate case-insensitive
        vm.Ok();
        Assert.True(vm.HasError);
        Assert.Contains("already exists", vm.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        vm.Mnemonic = "NEW_MNEM";
        vm.Ok();
        Assert.False(vm.HasError);
    }

    [Fact]
    public async Task EditTimeLogViewModel_AddChannel_OpensDialogAndAddsExpressionChannel()
    {
        await SeedTimelogAsync();
        var vm = new EditTimeLogViewModel(_session, _repo, "TL-001");
        await vm.InitializeAsync();

        int initialCount = vm.Channels.Count;

        // Mock dialog handler to simulate user filling the Channel Properties dialog
        vm.OpenChannelPropertiesDialogHandler = dialogVm =>
        {
            dialogVm.Mnemonic = "CALC_ROP";
            dialogVm.Description = "Calculated ROP";
            dialogVm.SelectedUnit = "m/hr";
            dialogVm.ValueType = "Expression";
            dialogVm.Expression = "DEPTH_DIFF / TIME_DIFF";
            dialogVm.IsStoredProcedure = true;
            dialogVm.Parameters = "@WellID";
            dialogVm.Ok();
            return !dialogVm.HasError;
        };

        vm.AddChannelCommand.Execute(null);

        Assert.Equal(initialCount + 1, vm.Channels.Count);
        var added = vm.Channels.FirstOrDefault(c => c.Mnemonic == "CALC_ROP");
        Assert.NotNull(added);
        Assert.Equal("Calculated ROP", added.Description);
        Assert.Equal("m/hr", added.Unit);
        Assert.Equal(1, added.valueType);
        Assert.Equal("DEPTH_DIFF / TIME_DIFF", added.Expression);
        Assert.True(added.isStoredProc);
        Assert.Equal("@WellID", added.StoredProcParams);
        Assert.True(added.Upload);
    }

    [Fact]
    public async Task EditTimeLogViewModel_EditChannel_OpensDialogAndUpdatesProperties()
    {
        await SeedTimelogAsync();
        var vm = new EditTimeLogViewModel(_session, _repo, "TL-001");
        await vm.InitializeAsync();

        var hkldChannel = vm.Channels.First(c => c.Mnemonic == "HKLD");

        // Mock dialog handler to simulate user editing properties
        vm.OpenChannelPropertiesDialogHandler = dialogVm =>
        {
            Assert.Equal("HKLD", dialogVm.Mnemonic);
            dialogVm.Description = "Updated Hookload Channel";
            dialogVm.SelectedUnit = "klbf";
            dialogVm.Expression = "HKLD_RAW * 1.05";
            dialogVm.Ok();
            return !dialogVm.HasError;
        };

        vm.EditChannelCommand.Execute(hkldChannel);

        Assert.Equal("Updated Hookload Channel", hkldChannel.Description);
        Assert.Equal("klbf", hkldChannel.Unit);
        Assert.Equal("HKLD_RAW * 1.05", hkldChannel.Expression);
    }

    [Fact]
    public void ChannelPropertiesViewModel_WhenEditing_LocksMnemonicAsPrimaryKey()
    {
        var existing = new LogChannel
        {
            Mnemonic = "IMMUTABLE_PK",
            Description = "Immutable Primary Key Channel",
            Unit = "psi",
            valueType = 1,
            Expression = "P1 * 2"
        };

        var vm = new ChannelPropertiesViewModel(null, new[] { "IMMUTABLE_PK", "OTHER" }, existing);

        Assert.True(vm.IsEditMode);
        Assert.False(vm.CanEditMnemonic);
        Assert.Contains("primary key", vm.MnemonicToolTip, StringComparison.OrdinalIgnoreCase);

        // Attempting to change mnemonic during edit mode
        vm.Mnemonic = "ATTEMPTED_CHANGE";
        vm.Ok();

        // Mnemonic is locked to original PK
        Assert.Equal("IMMUTABLE_PK", vm.Mnemonic);
        Assert.False(vm.HasError);

        vm.ApplyTo(existing);
        Assert.Equal("IMMUTABLE_PK", existing.Mnemonic);
    }

    [Fact]
    public void ExpressionEditorViewModel_InitializesWithChannelsAndInsertsTokens()
    {
        var channels = new List<LogChannel>
        {
            new() { Mnemonic = "DEPTH", Description = "Hole Depth", Unit = "m" },
            new() { Mnemonic = "HKLD", Description = "Hook Load", Unit = "kN" },
            new() { Mnemonic = "ROP", Description = "Rate of Penetration", Unit = "m/hr" }
        };

        var vm = new ExpressionEditorViewModel("", channels);

        Assert.Equal(3, vm.AvailableChannels.Count);
        Assert.Contains("abs()", vm.AvailableFunctions);
        Assert.Contains("+", vm.AvailableOperators);

        vm.InsertChannel(channels[0]);
        vm.InsertOperator("+");
        vm.InsertFunction("abs()");

        Assert.Contains("DEPTH", vm.Expression);
        Assert.Contains("+", vm.Expression);
        Assert.Contains("abs()", vm.Expression);
    }

    [Fact]
    public void ExpressionEditorViewModel_VerifySyntax_ValidatesCorrectly()
    {
        var vm = new ExpressionEditorViewModel("");

        // Blank
        vm.Verify();
        Assert.True(vm.IsStatusError);
        Assert.Contains("empty", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);

        // Unbalanced parens
        vm.Expression = "(DEPTH + 10";
        vm.Verify();
        Assert.True(vm.IsStatusError);
        Assert.Contains("Unbalanced parentheses", vm.StatusMessage);

        // Consecutive operators
        vm.Expression = "DEPTH ++ 10";
        vm.Verify();
        Assert.True(vm.IsStatusError);
        Assert.Contains("Consecutive operators", vm.StatusMessage);

        // Trailing operator
        vm.Expression = "DEPTH +";
        vm.Verify();
        Assert.True(vm.IsStatusError);
        Assert.Contains("cannot end with an operator", vm.StatusMessage);

        // Valid expression
        vm.Expression = "DEPTH / 1000.0 + abs(HKLD)";
        vm.Verify();
        Assert.False(vm.IsStatusError);
        Assert.Contains("valid", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ChannelPropertiesViewModel_OpenEditor_OpensExpressionEditorAndAppliesFormula()
    {
        var channels = new List<LogChannel>
        {
            new() { Mnemonic = "DEPTH", Description = "Depth" }
        };

        var vm = new ChannelPropertiesViewModel(null, null, null, 1, channels)
        {
            Expression = "DEPTH * 1"
        };

        vm.OpenExpressionEditorHandler = exprVm =>
        {
            Assert.Equal("DEPTH * 1", exprVm.Expression);
            Assert.Single(exprVm.AvailableChannels);
            exprVm.Expression = "DEPTH * 2.5 + 10";
            exprVm.Ok();
            return true;
        };

        vm.OpenEditorCommand.Execute(null);

        Assert.Equal("DEPTH * 2.5 + 10", vm.Expression);
    }
}

