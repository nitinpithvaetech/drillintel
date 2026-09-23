using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Models;
using DrillIntel.Projects;
using DrillIntel.Services;
using DrillIntel.ViewModels;
using Xunit;

namespace DrillIntel.Tests;

public class WellTreeImportDisplayRulesTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ProjectSession _session;
    private readonly WellDataRepository _repo;
    private readonly ImportDepthLogService _depthLogService;

    public WellTreeImportDisplayRulesTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"drillintel_tree_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(_tempDbPath);

        _session = new ProjectSession();
        _session.Load(_tempDbPath);

        _repo = new WellDataRepository(_session);
        _depthLogService = new ImportDepthLogService(_repo, _session);
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

    [Fact]
    public async Task DepthlogImport_DisplaysOnlyUnderDepthlogs_AndNotUnderTimelogs()
    {
        string csvPath = Path.Combine(Path.GetTempPath(), $"depth_tree_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath,
                "DEPTH,GR,ROP\n" +
                "100.0,45.2,12.3\n" +
                "101.0,46.1,13.1\n");

            var options = new DepthLogImportOptions
            {
                LogName = "DepthLog_Alpha",
                WellName = "Etech4",
                ColumnHeadingRow = 1,
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string>
                {
                    ["DEPTH"] = "DEPTH",
                    ["GR"] = "GR"
                }
            };

            var imported = await _depthLogService.ImportFromFileAsync(csvPath, options);
            Assert.Single(imported);

            // 1. Repository checks
            var depthLogs = await _repo.GetDepthLogsAsync();
            var timeLogs = await _repo.GetTimeLogsAsync();

            Assert.Single(depthLogs);
            Assert.Equal("DepthLog_Alpha", depthLogs[0].nameLog);
            Assert.Empty(timeLogs);

            // 2. TreeView in DashboardViewModel checks
            var dashboard = new DashboardViewModel(_session, _repo);
            await dashboard.LoadDataAsync();

            Assert.True(dashboard.HasData);
            Assert.Single(dashboard.WellTree);

            var wellNode = dashboard.WellTree[0];
            Assert.Equal("Etech4", wellNode.Name);

            var timeFolder = wellNode.Children.First(c => c.Name == "Timelogs");
            var depthFolder = wellNode.Children.First(c => c.Name == "Depthlogs");

            Assert.Equal("(0)", timeFolder.Badge);
            Assert.Empty(timeFolder.Children);

            Assert.Equal("(1)", depthFolder.Badge);
            Assert.Single(depthFolder.Children);
            Assert.Equal("DepthLog_Alpha", depthFolder.Children[0].Name);
            Assert.Equal(WellTreeNodeType.DepthLog, depthFolder.Children[0].Type);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task TimelogImport_DisplaysOnlyUnderTimelogs_AndNotUnderDepthlogs()
    {
        await _repo.EnsureWellAsync("Etech4");

        var timeLog = new TimeLog
        {
            ObjectID = Guid.NewGuid().ToString(),
            nameLog = "TimeLog_Alpha",
            nameWell = "Etech4",
            __WellName = "Etech4",
            __dataTableName = "timeLog12345678#87654321",
            comments = "Success",
            description = $"QC: 99.0% • {DateTime.Now:dd-MM-yyyy hh:mm tt}",
            creationDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss")
        };

        await _repo.LogTimeLogAsync(timeLog);

        // 1. Repository checks
        var timeLogs = await _repo.GetTimeLogsAsync();
        var depthLogs = await _repo.GetDepthLogsAsync();

        Assert.Single(timeLogs);
        Assert.Equal("TimeLog_Alpha", timeLogs[0].nameLog);
        Assert.Empty(depthLogs);

        // 2. TreeView in DashboardViewModel checks
        var dashboard = new DashboardViewModel(_session, _repo);
        await dashboard.LoadDataAsync();

        Assert.True(dashboard.HasData);
        var wellNode = dashboard.WellTree[0];

        var timeFolder = wellNode.Children.First(c => c.Name == "Timelogs");
        var depthFolder = wellNode.Children.First(c => c.Name == "Depthlogs");

        Assert.Equal("(1)", timeFolder.Badge);
        Assert.Single(timeFolder.Children);
        Assert.Equal("TimeLog_Alpha", timeFolder.Children[0].Name);
        Assert.Equal(WellTreeNodeType.TimeLog, timeFolder.Children[0].Type);

        Assert.Equal("(0)", depthFolder.Badge);
        Assert.Empty(depthFolder.Children);
    }

    [Fact]
    public async Task BothDepthlogAndTimelogImported_EachTreeNodesListsOnlyRespectiveLogs_NoCrossListing()
    {
        string csvPath = Path.Combine(Path.GetTempPath(), $"dual_tree_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath, "DEPTH,GR\n100.0,45.2\n101.0,46.1\n");

            // Import DepthLog
            var options = new DepthLogImportOptions
            {
                LogName = "DepthLog_A",
                WellName = "Etech4",
                ColumnHeadingRow = 1,
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string> { ["DEPTH"] = "DEPTH" }
            };
            await _depthLogService.ImportFromFileAsync(csvPath, options);

            // Import TimeLog
            var timeLog = new TimeLog
            {
                ObjectID = Guid.NewGuid().ToString(),
                nameLog = "TimeLog_B",
                nameWell = "Etech4",
                __WellName = "Etech4",
                __dataTableName = "timeLog99999999#11111111",
                comments = "Success",
                description = $"QC: 95.0% • {DateTime.Now:dd-MM-yyyy hh:mm tt}",
                creationDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss")
            };
            await _repo.LogTimeLogAsync(timeLog);

            // Verify in DashboardViewModel
            var dashboard = new DashboardViewModel(_session, _repo);
            await dashboard.LoadDataAsync();

            var wellNode = dashboard.WellTree[0];
            var timeFolder = wellNode.Children.First(c => c.Name == "Timelogs");
            var depthFolder = wellNode.Children.First(c => c.Name == "Depthlogs");

            // Timelogs node must contain only TimeLog_B
            Assert.Equal("(1)", timeFolder.Badge);
            Assert.Single(timeFolder.Children);
            Assert.Equal("TimeLog_B", timeFolder.Children[0].Name);
            Assert.DoesNotContain(timeFolder.Children, c => c.Name == "DepthLog_A");

            // Depthlogs node must contain only DepthLog_A
            Assert.Equal("(1)", depthFolder.Badge);
            Assert.Single(depthFolder.Children);
            Assert.Equal("DepthLog_A", depthFolder.Children[0].Name);
            Assert.DoesNotContain(depthFolder.Children, c => c.Name == "TimeLog_B");

            // Total logs metric
            Assert.Contains("2 logs in project", dashboard.RecentImports);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task LegacyCrossContaminatedDatabase_AutomaticallySanitizesAndMaintainsSeparation()
    {
        string csvPath = Path.Combine(Path.GetTempPath(), $"legacy_tree_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath, "DEPTH,GR\n100.0,45.2\n");

            var options = new DepthLogImportOptions
            {
                LogName = "ContaminatedDepthLog",
                WellName = "Etech4",
                ColumnHeadingRow = 1,
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string> { ["DEPTH"] = "DEPTH" }
            };
            var imported = await _depthLogService.ImportFromFileAsync(csvPath, options);
            var depthLog = imported[0];

            // Manually inject cross-contamination to simulate previous buggy behavior
            var dataService = _session.GetDataService();
            dataService.ExecuteNonQuery(
                $"INSERT INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, DATA_TABLE_NAME, COMMENTS) " +
                $"VALUES ('{depthLog.WellID}', 'wb_test', '{depthLog.ObjectID}', '{depthLog.nameLog}', '{depthLog.__dataTableName}', 'Success');");

            dataService.ExecuteNonQuery(
                $"CREATE TABLE IF NOT EXISTS VMX_TIME_LOG_SUMMARY (Id INTEGER PRIMARY KEY AUTOINCREMENT, LogId TEXT, LogName TEXT, DataTableName TEXT, ImportStatus TEXT);");
            dataService.ExecuteNonQuery(
                $"INSERT INTO VMX_TIME_LOG_SUMMARY (LogId, LogName, DataTableName, ImportStatus) " +
                $"VALUES ('{depthLog.ObjectID}', '{depthLog.nameLog}', '{depthLog.__dataTableName}', 'Success');");

            // Verify cross-contamination is present in raw SQLite before sanitization
            var rawTimeCheck = dataService.GetTable($"SELECT * FROM VMX_TIME_LOG WHERE LOG_ID = '{depthLog.ObjectID}';");
            Assert.Equal(1, rawTimeCheck.Rows.Count);

            // Now call repository methods
            var timeLogs = await _repo.GetTimeLogsAsync();
            var depthLogs = await _repo.GetDepthLogsAsync();

            // Depthlog must NOT be returned in timeLogs
            Assert.Empty(timeLogs);
            Assert.Single(depthLogs);
            Assert.Equal("ContaminatedDepthLog", depthLogs[0].nameLog);

            // Verify DashboardViewModel displays strictly separated
            var dashboard = new DashboardViewModel(_session, _repo);
            await dashboard.LoadDataAsync();

            var wellNode = dashboard.WellTree[0];
            var timeFolder = wellNode.Children.First(c => c.Name == "Timelogs");
            var depthFolder = wellNode.Children.First(c => c.Name == "Depthlogs");

            Assert.Equal("(0)", timeFolder.Badge);
            Assert.Empty(timeFolder.Children);

            Assert.Equal("(1)", depthFolder.Badge);
            Assert.Single(depthFolder.Children);
            Assert.Equal("ContaminatedDepthLog", depthFolder.Children[0].Name);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task DataChanged_AutomaticallyRefreshesWellTree()
    {
        await _repo.EnsureWellAsync("Etech4");

        var dashboard = new DashboardViewModel(_session, _repo);
        await dashboard.LoadDataAsync();

        var wellNode = dashboard.WellTree[0];
        var depthFolder = wellNode.Children.First(c => c.Name == "Depthlogs");
        Assert.Equal("(0)", depthFolder.Badge);

        // Import a DepthLog
        string csvPath = Path.Combine(Path.GetTempPath(), $"refresh_tree_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath, "DEPTH,GR\n500.0,75.0\n");

            var options = new DepthLogImportOptions
            {
                LogName = "AutoRefreshDepthLog",
                WellName = "Etech4",
                ColumnHeadingRow = 1,
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string> { ["DEPTH"] = "DEPTH" }
            };

            await _depthLogService.ImportFromFileAsync(csvPath, options);

            // Allow event to propagate and refresh
            await Task.Delay(100);

            // Verify tree refreshed
            wellNode = dashboard.WellTree[0];
            depthFolder = wellNode.Children.First(c => c.Name == "Depthlogs");
            var timeFolder = wellNode.Children.First(c => c.Name == "Timelogs");

            Assert.Equal("(1)", depthFolder.Badge);
            Assert.Single(depthFolder.Children);
            Assert.Equal("AutoRefreshDepthLog", depthFolder.Children[0].Name);

            Assert.Equal("(0)", timeFolder.Badge);
            Assert.Empty(timeFolder.Children);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }
}
