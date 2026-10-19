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
}
