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
        vm.Channels.Add(new TimelogChannelItem
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
}
