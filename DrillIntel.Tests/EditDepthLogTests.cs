using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Dapper;
using DrillIntel.Converters;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Models;
using DrillIntel.Projects;
using DrillIntel.ViewModels;
using Xunit;

namespace DrillIntel.Tests;

[Collection("AppDatabaseCollection")]
public class EditDepthLogTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ProjectSession _session;
    private readonly WellDataRepository _repo;

    public EditDepthLogTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"editdepthlog_test_{Guid.NewGuid():N}.dintel");
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

    private async Task<(string wellId, string wellboreId, string logId, string tableName)> SeedDepthlogAsync(
        string logName = "Depthlog1",
        string serviceCompany = "Baker Hughes",
        string edrProvider = "Pason",
        string runNo = "42",
        string description = "Test depthlog description",
        string comments = "Routine test run",
        bool primaryLog = true,
        double minDepth = 1000.0,
        double maxDepth = 3500.0,
        double stepIncrement = 0.2,
        string direction = "Top To Bottom",
        bool linkToParent = false)
    {
        var conn = _session.GetConnection();
        string wellId = "WELL-001";
        string wellboreId = "WB-001";
        string logId = "DL-001";
        string tableName = "depthLog_test_data";

        // Seed Well
        await conn.ExecuteAsync(
            "INSERT OR REPLACE INTO VMX_WELL (WELL_ID, WELL_NAME, FIELD) VALUES (@wId, @wName, 'Field A');",
            new { wId = wellId, wName = "Test Well Alpha" });

        // Seed Wellbore
        await conn.ExecuteAsync(
            "INSERT OR REPLACE INTO VMX_WELLBORE (WELLBORE_ID, WELL_ID, WELLBORE_NAME) VALUES (@wbId, @wId, 'Wellbore 1');",
            new { wbId = wellboreId, wId = wellId });

        // Seed VMX_DEPTH_LOG
        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO VMX_DEPTH_LOG (
                WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, RUN_NO, SERVICE_COMPANY,
                DATA_TABLE_NAME, DESCRIPTION, COMMENTS, EDR_PROVIDER, PRIMARY_LOG,
                MIN_DEPTH, MAX_DEPTH, LINK_TO_PARENT, DUPLICATE_ACTION
            ) VALUES (
                @wellId, @wellboreId, @logId, @logName, @runNo, @serviceCompany,
                @tableName, @description, @comments, @edrProvider, @primaryLog,
                @minDepth, @maxDepth, @linkToParent, 2
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
            comments,
            edrProvider,
            primaryLog = primaryLog ? 1 : 0,
            minDepth,
            maxDepth,
            linkToParent = linkToParent ? 1 : 0
        });

        // Seed VMX_DEPTH_LOG_COLUMNS
        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO VMX_DEPTH_LOG_COLUMNS (
                WELL_ID, WELLBORE_ID, LOG_ID, MNEMONIC, CHANNEL_NAME, DATA_TYPE,
                UNIT, VUMAX_UNIT_ID, VALUE_TYPE, VALUE_QUERY, WITSML_MNEMONIC,
                WRITE_BACK, COLUMN_ORDER
            ) VALUES 
            (@wellId, @wellboreId, @logId, 'DEPTH', 'Measured Depth', 'Double', 'm', 'm', 0, '', 'DEPT', 1, 1),
            (@wellId, @wellboreId, @logId, 'ROP', 'Rate of Penetration', 'Double', 'm/h', 'm/h', 0, '', 'ROP', 1, 2),
            (@wellId, @wellboreId, @logId, 'GR', 'Gamma Ray', 'Double', 'gAPI', 'gAPI', 0, '', 'GR', 1, 3);",
            new { wellId, wellboreId, logId });

        // Seed data table
        await conn.ExecuteAsync($@"
            CREATE TABLE IF NOT EXISTS [{tableName}] (
                DATA_INDEX DECIMAL(8),
                [DEPTH] REAL,
                [ROP] REAL,
                [GR] REAL
            );");

        await conn.ExecuteAsync($@"
            INSERT INTO [{tableName}] (DATA_INDEX, [DEPTH], [ROP], [GR])
            VALUES (1, 1000.0, 25.0, 45.0);");

        return (wellId, wellboreId, logId, tableName);
    }

    [Fact]
    public async Task DialogInitialization_PopulatesLogInformation_FromDepthLog()
    {
        await SeedDepthlogAsync(
            logName: "Depthlog1",
            serviceCompany: "Schlumberger (SLB)",
            edrProvider: "Pason",
            runNo: "12",
            description: "High accuracy depth log",
            comments: "Test comments",
            primaryLog: true,
            minDepth: 1200.5,
            maxDepth: 3400.0);

        var vm = new EditDepthLogViewModel(_session, _repo, "DL-001");
        await vm.InitializeAsync();

        Assert.Equal("Depthlog1", vm.LogName);
        Assert.Equal("Schlumberger (SLB)", vm.ServiceCompany);
        Assert.Equal("Pason", vm.EdrProvider);
        Assert.Equal("12", vm.RunNo);
        Assert.Equal("High accuracy depth log", vm.Description);
        Assert.Equal("Test comments", vm.Comments);
        Assert.True(vm.PrimaryLog);
        Assert.Equal("1200.5", vm.StartDepth);
        Assert.Equal("3400", vm.EndDepth);
        Assert.Equal("depthLog_test_data", vm.DataTableName);
    }

    [Fact]
    public async Task DialogInitialization_PopulatesChannelsGrid_FromDataTableAndColumns()
    {
        await SeedDepthlogAsync();

        var vm = new EditDepthLogViewModel(_session, _repo, "DL-001");
        await vm.InitializeAsync();

        Assert.NotEmpty(vm.Channels);

        // Verify required columns exist
        var depthChannel = vm.Channels.FirstOrDefault(c => c.Mnemonic == "DEPTH");
        Assert.NotNull(depthChannel);
        Assert.True(depthChannel.Upload);
        Assert.Equal("m", depthChannel.Unit);
        Assert.Equal("m", depthChannel.VuMaxUnitId);
        Assert.Equal("Measured Depth", depthChannel.Description);
        Assert.Equal("DEPT", depthChannel.UploadMnemonic);
        Assert.Equal("0", depthChannel.ValueType);

        var grChannel = vm.Channels.FirstOrDefault(c => c.Mnemonic == "GR");
        Assert.NotNull(grChannel);
        Assert.Equal("gAPI", grChannel.Unit);
    }

    [Fact]
    public async Task DialogInitialization_WhenDataTableStoresChannelRows_PopulatesDirectly()
    {
        var conn = _session.GetConnection();
        string logId = "DL-CHAN-TBL";
        string chanTable = "custom_depth_channel_data_tbl";

        // Insert into VMX_DEPTH_LOG
        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO VMX_DEPTH_LOG (
                WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, DATA_TABLE_NAME, SERVICE_COMPANY, EDR_PROVIDER, RUN_NO
            ) VALUES ('W1', 'WB1', @logId, 'DirectChannelDepthLog', @chanTable, 'Halliburton', 'NOV', '5');",
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
            (1, 'CALI', 'in', 'in', 'Caliper', 'CALI', '0', '', 0),
            (1, 'POROSITY', '%', '%', 'Effective Porosity', 'POR', '1', 'GR * 0.05', 0);");

        var vm = new EditDepthLogViewModel(_session, _repo, logId);
        await vm.InitializeAsync();

        Assert.Equal("DirectChannelDepthLog", vm.LogName);
        Assert.Equal("Halliburton", vm.ServiceCompany);
        Assert.Equal("NOV", vm.EdrProvider);
        Assert.Equal("5", vm.RunNo);
        Assert.Equal(2, vm.Channels.Count);

        var cali = vm.Channels.First(c => c.Mnemonic == "CALI");
        Assert.True(cali.Upload);
        Assert.Equal("in", cali.Unit);
        Assert.Equal("in", cali.VuMaxUnitId);
        Assert.Equal("Caliper", cali.Description);

        var por = vm.Channels.First(c => c.Mnemonic == "POROSITY");
        Assert.Equal("POR", por.UploadMnemonic);
        Assert.Equal("1", por.ValueType);
        Assert.Equal("GR * 0.05", por.Expression);
    }

    [Fact]
    public async Task ChannelsTab_AddChannel_AddsNewItemWithUniqueMnemonic()
    {
        await SeedDepthlogAsync();
        var vm = new EditDepthLogViewModel(_session, _repo, "DL-001");
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
        await SeedDepthlogAsync();
        var vm = new EditDepthLogViewModel(_session, _repo, "DL-001");
        await vm.InitializeAsync();

        var grChannel = vm.Channels.First(c => c.Mnemonic == "GR");
        vm.SelectedChannel = grChannel;

        vm.RemoveChannelCommand.Execute(grChannel);

        Assert.DoesNotContain(vm.Channels, c => c.Mnemonic == "GR");
    }

    [Fact]
    public async Task ChannelsTab_ApplyUnitConversionProfile_StandardizesUnits()
    {
        await SeedDepthlogAsync();
        var vm = new EditDepthLogViewModel(_session, _repo, "DL-001");
        await vm.InitializeAsync();

        // Apply Imperial profile
        vm.ApplyUnitConversionProfileCommand.Execute("Imperial");
        var depthCh = vm.Channels.First(c => c.Mnemonic == "DEPTH");
        var ropCh = vm.Channels.First(c => c.Mnemonic == "ROP");
        Assert.Equal("ft", depthCh.Unit);
        Assert.Equal("ft/h", ropCh.Unit);

        // Apply Metric profile
        vm.ApplyUnitConversionProfileCommand.Execute("Metric");
        Assert.Equal("m", depthCh.Unit);
        Assert.Equal("m/h", ropCh.Unit);
    }

    [Fact]
    public async Task LinkDepthLogTab_LoadsAvailableWells_Wellbores_AndLogs_ExcludingCurrent()
    {
        await SeedDepthlogAsync();
        var conn = _session.GetConnection();

        // Add a second depthlog to link to
        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO VMX_DEPTH_LOG (
                WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME
            ) VALUES ('WELL-001', 'WB-001', 'DL-002', 'ParentDepthlog');");

        var vm = new EditDepthLogViewModel(_session, _repo, "DL-001");
        await vm.InitializeAsync();

        Assert.NotEmpty(vm.AvailableWells);
        Assert.NotEmpty(vm.AvailableWellbores);
        Assert.NotEmpty(vm.AvailableDepthLogs);

        // Current log DL-001 should not be in the list of available parent logs
        Assert.DoesNotContain(vm.AvailableDepthLogs, dl => dl.LogId == "DL-001");
        Assert.Contains(vm.AvailableDepthLogs, dl => dl.LogId == "DL-002");
    }

    [Fact]
    public async Task Validation_EmptyLogName_SetsStatusErrorAndDoesNotSave()
    {
        await SeedDepthlogAsync();
        var vm = new EditDepthLogViewModel(_session, _repo, "DL-001");
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
        await SeedDepthlogAsync();
        var vm = new EditDepthLogViewModel(_session, _repo, "DL-001");
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
    public async Task Validation_InvalidStartDepth_SetsStatusErrorAndDoesNotSave()
    {
        await SeedDepthlogAsync();
        var vm = new EditDepthLogViewModel(_session, _repo, "DL-001");
        await vm.InitializeAsync();

        vm.StartDepth = "XYZ_INVALID";
        bool? closeResult = null;
        vm.RequestClose += res => closeResult = res;

        await vm.SaveAsync();

        Assert.Null(closeResult);
        Assert.True(vm.IsStatusError);
        Assert.Contains("Start Depth must be a valid number", vm.StatusMessage);
    }

    [Fact]
    public async Task Validation_InvalidEndDepth_SetsStatusErrorAndDoesNotSave()
    {
        await SeedDepthlogAsync();
        var vm = new EditDepthLogViewModel(_session, _repo, "DL-001");
        await vm.InitializeAsync();

        vm.EndDepth = "INVALID_END";
        bool? closeResult = null;
        vm.RequestClose += res => closeResult = res;

        await vm.SaveAsync();

        Assert.Null(closeResult);
        Assert.True(vm.IsStatusError);
        Assert.Contains("End Depth must be a valid number", vm.StatusMessage);
    }

    [Fact]
    public async Task Validation_InvalidStepIncrement_SetsStatusErrorAndDoesNotSave()
    {
        await SeedDepthlogAsync();
        var vm = new EditDepthLogViewModel(_session, _repo, "DL-001");
        await vm.InitializeAsync();

        vm.StepIncrement = "NEGATIVE_OR_NOT_NUMBER";
        bool? closeResult = null;
        vm.RequestClose += res => closeResult = res;

        await vm.SaveAsync();

        Assert.Null(closeResult);
        Assert.True(vm.IsStatusError);
        Assert.Contains("valid number", vm.StatusMessage);

        vm.StepIncrement = "-0.5";
        await vm.SaveAsync();
        Assert.True(vm.IsStatusError);
        Assert.Contains("greater than zero", vm.StatusMessage);
    }

    [Fact]
    public async Task Validation_DuplicateChannelMnemonic_SetsStatusErrorAndDoesNotSave()
    {
        await SeedDepthlogAsync();
        var vm = new EditDepthLogViewModel(_session, _repo, "DL-001");
        await vm.InitializeAsync();

        // Introduce duplicate mnemonic
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
    public async Task SaveAsync_PersistsAllChangesToDepthLogAndColumns()
    {
        await SeedDepthlogAsync();
        var vm = new EditDepthLogViewModel(_session, _repo, "DL-001");
        await vm.InitializeAsync();

        // Update fields
        vm.LogName = "UpdatedDepthlogName";
        vm.ServiceCompany = "Halliburton";
        vm.EdrProvider = "NOV";
        vm.RunNo = "99";
        vm.Description = "Updated depthlog description";
        vm.Comments = "Updated comments";
        vm.PrimaryLog = false;
        vm.StartDepth = "1500.5";
        vm.EndDepth = "4000.0";
        vm.StepIncrement = "0.25";
        vm.Direction = "Bottom To Top";
        vm.LinkToParent = true;
        vm.SelectedDuplicateAction = "Replace";

        // Modify a channel
        var gr = vm.Channels.First(c => c.Mnemonic == "GR");
        gr.Unit = "API";
        gr.Description = "Gamma Ray Modified";

        bool? closeResult = null;
        vm.RequestClose += res => closeResult = res;

        await vm.SaveAsync();

        Assert.True(closeResult);

        // Verify database updated
        var conn = _session.GetConnection();
        var row = await conn.QuerySingleAsync<dynamic>("SELECT * FROM VMX_DEPTH_LOG WHERE LOG_ID = 'DL-001';");
        Assert.Equal("UpdatedDepthlogName", (string)row.LOG_NAME);
        Assert.Equal("Halliburton", (string)row.SERVICE_COMPANY);
        Assert.Equal("NOV", (string)row.EDR_PROVIDER);
        Assert.Equal("99", (string)row.RUN_NO);
        Assert.Equal("Updated depthlog description", (string)row.DESCRIPTION);
        Assert.Equal("Updated comments", (string)row.COMMENTS);
        Assert.Equal(0, Convert.ToInt32(row.PRIMARY_LOG));
        Assert.Equal(1500.5, Convert.ToDouble(row.MIN_DEPTH));
        Assert.Equal(4000.0, Convert.ToDouble(row.MAX_DEPTH));
        Assert.Equal(1, Convert.ToInt32(row.LINK_TO_PARENT));
        Assert.Equal(0, Convert.ToInt32(row.DUPLICATE_ACTION)); // 0 = Replace

        // Verify VMX_DEPTH_LOG_COLUMNS
        var colRow = await conn.QuerySingleAsync<dynamic>(
            "SELECT * FROM VMX_DEPTH_LOG_COLUMNS WHERE LOG_ID = 'DL-001' AND MNEMONIC = 'GR';");
        Assert.Equal("API", (string)colRow.UNIT);
        Assert.Equal("Gamma Ray Modified", (string)colRow.CHANNEL_NAME);
    }

    [Fact]
    public async Task CancelCommand_InvokesRequestCloseWithFalse_AndDoesNotPersist()
    {
        await SeedDepthlogAsync(logName: "OriginalDepthlogName");
        var vm = new EditDepthLogViewModel(_session, _repo, "DL-001");
        await vm.InitializeAsync();

        vm.LogName = "DiscardedName";

        bool? closeResult = null;
        vm.RequestClose += res => closeResult = res;

        vm.CancelCommand.Execute(null);

        Assert.False(closeResult);

        // Verify database was NOT updated
        var conn = _session.GetConnection();
        var logName = await conn.ExecuteScalarAsync<string>("SELECT LOG_NAME FROM VMX_DEPTH_LOG WHERE LOG_ID = 'DL-001';");
        Assert.Equal("OriginalDepthlogName", logName);
    }

    [Fact]
    public async Task DashboardViewModel_EditDepthLogCommand_OpensDialogWithSelectedNode()
    {
        await SeedDepthlogAsync(logName: "DepthlogNodeTest");
        var dashboard = new DashboardViewModel(_session, _repo);
        await dashboard.LoadDataAsync();

        var wellNode = dashboard.WellTree[0];
        var depthFolder = wellNode.Children.First(c => c.Name == "Depthlogs");
        var depthNode = depthFolder.Children.First();

        EditDepthLogViewModel? openedVm = null;
        dashboard.OpenEditDepthLogDialogHandler = (vm) =>
        {
            openedVm = vm;
            return false;
        };

        await dashboard.EditDepthLogAsync(depthNode);

        Assert.NotNull(openedVm);
        Assert.Equal("DepthlogNodeTest", openedVm.LogName);
        Assert.Equal("DL-001", openedVm.LogId);
    }

    [Fact]
    public void ContextMenu_HasEditDepthLogOption_ForDepthLogType()
    {
        var converter = new EnumToVisibilityConverter();
        var visibleForDepthLog = converter.Convert(WellTreeNodeType.DepthLog, typeof(Visibility), "DepthLog", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Visible, visibleForDepthLog);

        var hiddenForTimeLog = converter.Convert(WellTreeNodeType.TimeLog, typeof(Visibility), "DepthLog", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Collapsed, hiddenForTimeLog);

        var hiddenForWell = converter.Convert(WellTreeNodeType.Well, typeof(Visibility), "DepthLog", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Collapsed, hiddenForWell);
    }

    [Fact]
    public async Task DepthLogService_LoadObject_LoadsMetadataAndCurves()
    {
        await SeedDepthlogAsync();
        var ds = _session.GetDataService();
        string err = "";

        var log = DepthLogService.LoadObject(ds, "DL-001", ref err);
        Assert.NotNull(log);
        Assert.Equal("DL-001", log.ObjectID);
        Assert.Equal("Depthlog1", log.nameLog);
        Assert.Equal("Baker Hughes", log.serviceCompany);
        Assert.Equal("Pason", log.EDRProvider);
        Assert.True(log.PrimaryLog);
        Assert.NotEmpty(log.LogCurves);
        Assert.True(log.LogCurves.ContainsKey("DEPTH"));
        Assert.True(log.LogCurves.ContainsKey("ROP"));
        Assert.True(log.LogCurves.ContainsKey("GR"));
    }

    [Fact]
    public async Task DepthLogService_SaveDepthLog_PersistsMetadataAndColumns()
    {
        await SeedDepthlogAsync();
        var ds = _session.GetDataService();
        string err = "";

        var log = DepthLogService.LoadObject(ds, "DL-001", ref err);
        Assert.NotNull(log);

        log.nameLog = "ServiceUpdatedDepthLog";
        log.serviceCompany = "Weatherford";
        log.startIndex = "1100.0";
        log.endIndex = "3600.0";

        var channels = log.LogCurves.Values.ToList();
        var ropCh = channels.First(c => c.Mnemonic == "ROP");
        ropCh.Unit = "ft/hr";
        ropCh.Description = "Speed of drilling";

        bool ok = DepthLogService.SaveDepthLog(ds, log, channels, ref err);
        Assert.True(ok, err);

        var reloaded = DepthLogService.LoadObject(ds, "DL-001", ref err);
        Assert.NotNull(reloaded);
        Assert.Equal("ServiceUpdatedDepthLog", reloaded.nameLog);
        Assert.Equal("Weatherford", reloaded.serviceCompany);
        Assert.Equal(1100.0, double.Parse(reloaded.startIndex));
        Assert.Equal(3600.0, double.Parse(reloaded.endIndex));
        Assert.Equal("ft/hr", reloaded.LogCurves["ROP"].Unit);
        Assert.Equal("Speed of drilling", reloaded.LogCurves["ROP"].Description);
    }

    [Fact]
    public async Task EditDepthLogViewModel_AddChannel_OpensDialogAndAddsExpressionChannel()
    {
        await SeedDepthlogAsync();
        var vm = new EditDepthLogViewModel(_session, _repo, "DL-001");
        await vm.InitializeAsync();

        int initialCount = vm.Channels.Count;

        // Mock dialog handler to simulate user filling the Channel Properties dialog
        vm.OpenChannelPropertiesDialogHandler = dialogVm =>
        {
            dialogVm.Mnemonic = "CALC_POROSITY";
            dialogVm.Description = "Calculated Porosity";
            dialogVm.SelectedUnit = "%";
            dialogVm.ValueType = "Expression";
            dialogVm.Expression = "(2.65 - RHO) / (2.65 - 1.0)";
            dialogVm.IsStoredProcedure = false;
            dialogVm.Ok();
            return !dialogVm.HasError;
        };

        vm.AddChannelCommand.Execute(null);

        Assert.Equal(initialCount + 1, vm.Channels.Count);
        var added = vm.Channels.FirstOrDefault(c => c.Mnemonic == "CALC_POROSITY");
        Assert.NotNull(added);
        Assert.Equal("Calculated Porosity", added.Description);
        Assert.Equal("%", added.Unit);
        Assert.Equal(1, added.valueType);
        Assert.Equal("(2.65 - RHO) / (2.65 - 1.0)", added.Expression);
        Assert.False(added.isStoredProc);
        Assert.True(added.Upload);
    }

    [Fact]
    public async Task EditDepthLogViewModel_EditChannel_OpensDialogAndUpdatesProperties()
    {
        await SeedDepthlogAsync();
        var vm = new EditDepthLogViewModel(_session, _repo, "DL-001");
        await vm.InitializeAsync();

        var grChannel = vm.Channels.First(c => c.Mnemonic == "GR");

        // Mock dialog handler to simulate user editing properties
        vm.OpenChannelPropertiesDialogHandler = dialogVm =>
        {
            Assert.Equal("GR", dialogVm.Mnemonic);
            dialogVm.Description = "Updated Gamma Ray Channel";
            dialogVm.SelectedUnit = "CPS";
            dialogVm.Expression = "GR_RAW * 1.2";
            dialogVm.Ok();
            return !dialogVm.HasError;
        };

        vm.EditChannelCommand.Execute(grChannel);

        Assert.Equal("Updated Gamma Ray Channel", grChannel.Description);
        Assert.Equal("CPS", grChannel.Unit);
        Assert.Equal("GR_RAW * 1.2", grChannel.Expression);
    }
}
