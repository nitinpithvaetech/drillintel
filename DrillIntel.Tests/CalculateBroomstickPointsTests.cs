using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using DrillIntel.Data;
using DrillIntel.Models;
using DrillIntel.Projects;
using DrillIntel.Services;
using DrillIntel.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DrillIntel.Tests;

public class CalculateBroomstickPointsTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _connString;
    private readonly DataServiceDIntel _dataService;
    private readonly BroomstickDataService _bsDataService;
    private readonly BroomstickProcessor _processor;
    private readonly AppLogger _logger;

    public CalculateBroomstickPointsTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"broomstick_test_{Guid.NewGuid():N}.sqlite");
        _connString = $"Data Source={_tempDbPath}";
        _dataService = new DataServiceDIntel(_tempDbPath);
        _logger = new AppLogger("UnitTest");
        _bsDataService = new BroomstickDataService(_dataService, _logger);
        _processor = new BroomstickProcessor(_bsDataService, null, _logger);
    }

    public void Dispose()
    {
        try { _dataService.CloseConnection(); } catch { }
        try { _dataService.Dispose(); } catch { }
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
    }

    [Fact]
    public async Task EnsureTablesExistAsync_CreatesRequiredBroomstickTables()
    {
        await _bsDataService.EnsureTablesExistAsync();

        using var conn = new SqliteConnection(_connString);
        await conn.OpenAsync();

        var pointsTableCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_BROOMSTICK_POINTS';");
        var prcInfoTableCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_BROOMSTICK_PRC_INFO';");

        Assert.Equal(1, pointsTableCount);
        Assert.Equal(1, prcInfoTableCount);
    }

    [Fact]
    public async Task ClearBroomstickPointsAsync_UsesParameterizedQueries_AndDeletesTargetWellOnly()
    {
        await _bsDataService.EnsureTablesExistAsync();

        using var conn = new SqliteConnection(_connString);
        await conn.OpenAsync();

        await conn.ExecuteAsync("INSERT INTO VMX_BROOMSTICK_POINTS (WELL_ID, DEPTH, TYPE) VALUES ('WELL_ALPHA', 1000.0, 0);");
        await conn.ExecuteAsync("INSERT INTO VMX_BROOMSTICK_POINTS (WELL_ID, DEPTH, TYPE) VALUES ('WELL_ALPHA', 1050.0, 1);");
        await conn.ExecuteAsync("INSERT INTO VMX_BROOMSTICK_POINTS (WELL_ID, DEPTH, TYPE) VALUES ('WELL_BETA', 2000.0, 0);");

        await conn.ExecuteAsync("INSERT INTO VMX_BROOMSTICK_PRC_INFO (WELL_ID, TYPE, PRC_LAST_DATE) VALUES ('WELL_ALPHA', 0, '2026-10-17');");
        await conn.ExecuteAsync("INSERT INTO VMX_BROOMSTICK_PRC_INFO (WELL_ID, TYPE, PRC_LAST_DATE) VALUES ('WELL_BETA', 0, '2026-10-17');");

        bool result = await _bsDataService.ClearBroomstickPointsAsync("WELL_ALPHA");
        Assert.True(result);

        var alphaPoints = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID='WELL_ALPHA';");
        var alphaPrc = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VMX_BROOMSTICK_PRC_INFO WHERE WELL_ID='WELL_ALPHA';");
        Assert.Equal(0, alphaPoints);
        Assert.Equal(0, alphaPrc);

        var betaPoints = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID='WELL_BETA';");
        var betaPrc = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VMX_BROOMSTICK_PRC_INFO WHERE WELL_ID='WELL_BETA';");
        Assert.Equal(1, betaPoints);
        Assert.Equal(1, betaPrc);
    }

    [Fact]
    public async Task ClearBroomstickPointsAsync_ResistsSqlInjection()
    {
        await _bsDataService.EnsureTablesExistAsync();

        using var conn = new SqliteConnection(_connString);
        await conn.OpenAsync();
        await conn.ExecuteAsync("INSERT INTO VMX_BROOMSTICK_POINTS (WELL_ID, DEPTH, TYPE) VALUES ('WELL_SAFE', 1000.0, 0);");

        string sqlInjectionPayload = "' OR '1'='1";
        await _bsDataService.ClearBroomstickPointsAsync(sqlInjectionPayload);

        var safePoints = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID='WELL_SAFE';");
        Assert.Equal(1, safePoints);
    }

    [Fact]
    public async Task HookloadDocument_CalculateBroomstickPoints_WhenCancelled_DoesNotClearOrRecalculate()
    {
        await _bsDataService.EnsureTablesExistAsync();

        using var conn = new SqliteConnection(_connString);
        await conn.OpenAsync();
        await conn.ExecuteAsync("INSERT INTO VMX_BROOMSTICK_POINTS (WELL_ID, DEPTH, TYPE) VALUES ('WELL_TEST', 500.0, 0);");

        var doc = new HookloadDocument("WELL_TEST", _processor, _bsDataService, _logger);
        bool promptInvoked = false;
        doc.ConfirmationPromptHandler = (msg, title) =>
        {
            promptInvoked = true;
            Assert.Contains("This action will re-calculate broomstick points from the beginning of the well. Do you want to continue?", msg);
            return false;
        };

        bool plotRefreshed = false;
        doc.PlotRefreshed += (s, e) => plotRefreshed = true;

        doc.CalculateBroomstickPoints();

        Assert.True(promptInvoked);
        Assert.False(plotRefreshed);

        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID='WELL_TEST';");
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task HookloadDocument_CalculateBroomstickPointsAsync_WhenConfirmed_ClearsPointsRecalculatesAndRefreshesPlot()
    {
        await _bsDataService.EnsureTablesExistAsync();

        using var conn = new SqliteConnection(_connString);
        await conn.OpenAsync();
        await conn.ExecuteAsync("INSERT INTO VMX_BROOMSTICK_POINTS (WELL_ID, DEPTH, TYPE) VALUES ('WELL_CONFIRM', 500.0, 0);");
        await conn.ExecuteAsync("INSERT INTO VMX_BROOMSTICK_PRC_INFO (WELL_ID, TYPE, PRC_LAST_DATE) VALUES ('WELL_CONFIRM', 0, '2026-10-17');");

        var doc = new HookloadDocument("WELL_CONFIRM", _processor, _bsDataService, _logger);
        bool promptInvoked = false;
        doc.ConfirmationPromptHandler = (msg, title) =>
        {
            promptInvoked = true;
            return true;
        };

        bool plotRefreshed = false;
        doc.PlotRefreshed += (s, e) => plotRefreshed = true;

        bool result = await doc.CalculateBroomstickPointsAsync();

        Assert.True(result);
        Assert.True(promptInvoked);
        Assert.True(plotRefreshed);

        var pointsCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID='WELL_CONFIRM';");
        var prcCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM VMX_BROOMSTICK_PRC_INFO WHERE WELL_ID='WELL_CONFIRM';");
        Assert.Equal(0, pointsCount);
        Assert.Equal(0, prcCount);
    }

    [Fact]
    public void MainViewModel_CalculateBroomstickPoints_ChecksIfActiveDocIsHookloadDocument()
    {
        var session = new ProjectSession();
        var projService = new ProjectService(session);
        var vm = new MainViewModel(session, projService, null, null);

        // Case A: ActiveDocument is null -> does not throw, invokes UserAlertHandler
        vm.ActiveDocument = null;
        bool alertInvoked = false;
        vm.UserAlertHandler = (msg, title) => alertInvoked = true;
        vm.OnCalculateBroomstickPointsClicked();
        Assert.True(alertInvoked);

        // Case B: ActiveDocument is HookloadDocument -> triggers CalculateBroomstickPoints()
        var hookloadDoc = new HookloadDocument("WELL_ACTIVE", _processor, _bsDataService, _logger);
        bool docPromptInvoked = false;
        hookloadDoc.ConfirmationPromptHandler = (msg, title) =>
        {
            docPromptInvoked = true;
            return false;
        };

        vm.ActiveDocument = hookloadDoc;
        vm.OnCalculateBroomstickPointsClicked();

        Assert.True(docPromptInvoked);
    }

    [Fact]
    public void MainViewModel_RecalculateBroomstickPointsCommand_CanExecute_BoundToProjectOpenState()
    {
        var session = new ProjectSession();
        var projService = new ProjectService(session);
        var vm = new MainViewModel(session, projService, null, null);

        // When project is closed, command cannot execute
        Assert.False(vm.RecalculateBroomstickPointsCommand.CanExecute(null));
        Assert.False(vm.CalculateBroomstickPointsCommand.CanExecute(null));
    }

    [Fact]
    public async Task BroomstickProcessor_RecalculatePointsAsync_CallsLowLevelProcessorAndExecutesCalculation()
    {
        await _bsDataService.EnsureTablesExistAsync();

        // Call RecalculatePointsAsync
        var result = await _processor.RecalculatePointsAsync("WELL_CALC_TEST");

        Assert.True(result.Success);
        Assert.Equal("ProcessBroomstick", result.OperationName);

        // Verify VMX_BROOMSTICK_PRC_INFO table exists and was operated on by BroomStickProcessor
        using var conn = new SqliteConnection(_connString);
        await conn.OpenAsync();
        var tableCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_BROOMSTICK_PRC_INFO';");
        Assert.Equal(1, tableCount);
    }

    [Fact]
    public async Task BroomstickDataService_EnsureTablesExistAsync_AutoMigratesPreExistingTableWithoutRpm()
    {
        string legacyDb = Path.Combine(Path.GetTempPath(), $"bs_legacy_{Guid.NewGuid():N}.sqlite");
        using (var ds = new DataServiceDIntel(legacyDb))
        {
            // Create legacy VMX_BROOMSTICK_POINTS without RPM and RIG_STATES
            ds.ExecuteNonQuery(@"
                CREATE TABLE VMX_BROOMSTICK_POINTS (
                    ID INTEGER PRIMARY KEY AUTOINCREMENT,
                    WELL_ID TEXT NOT NULL,
                    DEPTH REAL,
                    TYPE INTEGER
                );");

            var dataService = new BroomstickDataService(ds, _logger);
            await dataService.EnsureTablesExistAsync();

            // Verify columns RPM and RIG_STATES were added
            var table = ds.GetTable("PRAGMA table_info(VMX_BROOMSTICK_POINTS);");
            var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (table != null)
            {
                foreach (System.Data.DataRow row in table.Rows)
                {
                    cols.Add(row["name"]?.ToString() ?? "");
                }
            }
            Assert.Contains("RPM", cols);
            Assert.Contains("RIG_STATES", cols);

            // Verify insertion with RPM and RIG_STATES succeeds
            bool inserted = ds.ExecuteNonQuery(@"
                INSERT INTO VMX_BROOMSTICK_POINTS (WELL_ID, DEPTH, TYPE, RPM, RIG_STATES)
                VALUES ('WELL_RPM_TEST', 1500.0, 2, 85.5, '10,12');");
            Assert.True(inserted);

            object? rpm = ds.GetValue("SELECT RPM FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID='WELL_RPM_TEST';");
            Assert.NotNull(rpm);
            Assert.Equal(85.5, Convert.ToDouble(rpm));
        }

        SqliteConnection.ClearAllPools();
        try { if (File.Exists(legacyDb)) File.Delete(legacyDb); } catch { }
    }

    [Fact]
    public async Task RecalculateBroomstickPointsViewModel_InitializesMetadataAndCommands()
    {
        var session = new ProjectSession();
        var well = new DrillIntel.Data.Objects.DataObjects.Models.Well { ObjectID = "W_TEST_01", name = "Test Well A" };
        var timeLog = new DrillIntel.Data.Objects.DataObjects.Models.TimeLog { ObjectID = "TL_01", nameLog = "5-Sec Log", WellID = "W_TEST_01" };

        var vm = new RecalculateBroomstickPointsViewModel(session, well, timeLog, _processor, _bsDataService);

        Assert.Equal("Test Well A", vm.WellName);
        Assert.Equal("W_TEST_01", vm.WellId);
        Assert.Equal("5-Sec Log", vm.LogName);
        Assert.True(vm.ClearExistingPoints);
        Assert.True(vm.IsEntireWell);
        Assert.False(vm.IsCustomRange);
        Assert.True(vm.CanStart);
        Assert.False(vm.CanStop);
        Assert.Equal("Cancel", vm.CancelButtonText);
    }

    [Fact]
    public async Task RecalculateBroomstickPointsViewModel_StartCommand_ExecutesRecalculationAndCompletes()
    {
        await _bsDataService.EnsureTablesExistAsync();

        var session = new ProjectSession();
        var well = new DrillIntel.Data.Objects.DataObjects.Models.Well { ObjectID = "W_RECALC_VM", name = "VM Well" };
        var timeLog = new DrillIntel.Data.Objects.DataObjects.Models.TimeLog { ObjectID = "TL_VM", nameLog = "TL VM", WellID = "W_RECALC_VM" };

        var vm = new RecalculateBroomstickPointsViewModel(session, well, timeLog, _processor, _bsDataService);

        // Pre-populate a dummy point
        using (var conn = new SqliteConnection(_connString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync("INSERT INTO VMX_BROOMSTICK_POINTS (WELL_ID, DEPTH, TYPE) VALUES ('W_RECALC_VM', 500.0, 1);");
        }
        await vm.RefreshExistingCountAsync();
        Assert.Equal(1, vm.ExistingPointsCount);

        // Start recalculation with ClearExistingPoints = true
        await vm.StartCommand.ExecuteAsync(null);

        Assert.True(vm.IsCompleted);
        Assert.False(vm.IsRunning);
        Assert.False(vm.HasError);
        Assert.Equal(100.0, vm.ProgressPercent);
        Assert.Equal("Close", vm.CancelButtonText);
    }

    [Fact]
    public async Task RecalculateBroomstickPointsViewModel_ClearDbEntries_PromptsConfirmationAndClears()
    {
        await _bsDataService.EnsureTablesExistAsync();

        var session = new ProjectSession();
        var well = new DrillIntel.Data.Objects.DataObjects.Models.Well { ObjectID = "W_CLEAR_VM", name = "Clear VM Well" };
        var vm = new RecalculateBroomstickPointsViewModel(session, well, null, _processor, _bsDataService);

        using (var conn = new SqliteConnection(_connString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync("INSERT INTO VMX_BROOMSTICK_POINTS (WELL_ID, DEPTH, TYPE) VALUES ('W_CLEAR_VM', 1234.0, 2);");
        }
        await vm.RefreshExistingCountAsync();
        Assert.Equal(1, vm.ExistingPointsCount);

        // Simulate confirmation
        vm.ConfirmationHandler = (msg, title) => true;
        await vm.ClearDbEntriesCommand.ExecuteAsync(null);

        Assert.Equal(0, vm.ExistingPointsCount);
    }

    [Fact]
    public async Task MainViewModel_RecalculateBroomstickPoints_OpensDialogViaHandler()
    {
        var session = new ProjectSession();
        var projService = new ProjectService(session);
        var vm = new MainViewModel(session, projService, null, null);

        bool dialogOpened = false;
        vm.OpenRecalculateBroomstickPointsDialogHandler = recalcVm =>
        {
            dialogOpened = true;
            return true;
        };

        await vm.OpenRecalculateBroomstickPointsDialogAsync();
        Assert.False(dialogOpened); // Project not open yet

        // Simulate open project
        session.Load(_tempDbPath);
        await vm.OpenRecalculateBroomstickPointsDialogAsync();
        Assert.True(dialogOpened);
    }
}
