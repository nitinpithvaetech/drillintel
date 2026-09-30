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
using DrillIntel.Services;
using DrillIntel.ViewModels;
using System.Windows;
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

    [Fact]
    public async Task ContextMenu_AppearsOnWellNodeAndLogNodes()
    {
        await _repo.EnsureWellAsync("Etech4");

        // Import one depth log
        string depthCsv = Path.Combine(Path.GetTempPath(), $"cm_depth_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(depthCsv, "DEPTH,GR\n100.0,45.0\n");
            var options = new DepthLogImportOptions
            {
                LogName = "TestDepthLog",
                WellName = "Etech4",
                ColumnHeadingRow = 1,
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string> { ["DEPTH"] = "DEPTH" }
            };
            await _depthLogService.ImportFromFileAsync(depthCsv, options);

            var dashboard = new DashboardViewModel(_session, _repo);
            await dashboard.LoadDataAsync();

            var wellNode = dashboard.WellTree[0];
            var timeFolder = wellNode.Children.First(c => c.Name == "Timelogs");
            var depthFolder = wellNode.Children.First(c => c.Name == "Depthlogs");
            var depthChild = depthFolder.Children[0];

            // Validation: Well node MUST have context menu (for Edit Well) and is marked as Well node
            Assert.True(wellNode.HasContextMenu);
            Assert.True(wellNode.IsWellNode);
            Assert.False(wellNode.IsDepthLogNode);
            Assert.False(wellNode.IsTimeLogNode);
            Assert.False(wellNode.IsLogNode);

            // Validation: Timelogs parent node MUST NOT have context menu, and is not a log node
            Assert.False(timeFolder.HasContextMenu);
            Assert.False(timeFolder.IsTimeLogNode);
            Assert.False(timeFolder.IsLogNode);
            Assert.False(timeFolder.IsWellNode);
            Assert.False(timeFolder.IsDepthLogNode);

            // Validation: Depthlogs node MUST have context menu, is a log node, but not a well node
            Assert.True(depthFolder.HasContextMenu);
            Assert.True(depthFolder.IsDepthLogNode);
            Assert.True(depthFolder.IsLogNode);
            Assert.False(depthFolder.IsWellNode);
            Assert.False(depthFolder.IsTimeLogNode);

            // Validation: DepthLog item child node MUST have context menu, is a log node, but not a well node
            Assert.True(depthChild.HasContextMenu);
            Assert.True(depthChild.IsDepthLogNode);
            Assert.True(depthChild.IsLogNode);
            Assert.False(depthChild.IsWellNode);
        }
        finally
        {
            if (File.Exists(depthCsv)) File.Delete(depthCsv);
        }
    }

    [Fact]
    public async Task ContextMenu_EditWell_VisibilityRules_DisplaysOnlyOnRootWellNode_AndHiddenOnChildNodes()
    {
        await _repo.EnsureWellAsync("TestWell", "TestField");

        // Add a TimeLog and DepthLog
        var timeLog = new TimeLog
        {
            ObjectID = Guid.NewGuid().ToString(),
            nameLog = "Run1_Time",
            nameWell = "TestWell",
            __WellName = "TestWell"
        };
        await _repo.LogTimeLogAsync(timeLog);

        string depthCsv = Path.Combine(Path.GetTempPath(), $"cm_rules_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(depthCsv, "DEPTH,GR\n100.0,50.0\n");
            var options = new DepthLogImportOptions
            {
                LogName = "Run1_Depth",
                WellName = "TestWell",
                ColumnHeadingRow = 1,
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string> { ["DEPTH"] = "DEPTH" }
            };
            await _depthLogService.ImportFromFileAsync(depthCsv, options);

            var dashboard = new DashboardViewModel(_session, _repo);
            await dashboard.LoadDataAsync();

            var wellNode = dashboard.WellTree[0];
            var timeFolder = wellNode.Children.First(c => c.Name == "Timelogs");
            var depthFolder = wellNode.Children.First(c => c.Name == "Depthlogs");
            var timeChild = timeFolder.Children[0];
            var depthChild = depthFolder.Children[0];

            var enumConverter = new EnumToVisibilityConverter();
            var boolConverter = new BoolToVisibilityConverter();

            // 1. "Edit Well" menu item visibility: ConverterParameter="Well"
            // ROOT WELL NODE -> MUST BE Visible
            Assert.Equal(Visibility.Visible, enumConverter.Convert(wellNode.Type, typeof(Visibility), "Well", System.Globalization.CultureInfo.InvariantCulture));

            // CHILD NODES (Folders & Items) -> MUST BE Collapsed
            Assert.Equal(Visibility.Collapsed, enumConverter.Convert(timeFolder.Type, typeof(Visibility), "Well", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(Visibility.Collapsed, enumConverter.Convert(depthFolder.Type, typeof(Visibility), "Well", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(Visibility.Collapsed, enumConverter.Convert(timeChild.Type, typeof(Visibility), "Well", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(Visibility.Collapsed, enumConverter.Convert(depthChild.Type, typeof(Visibility), "Well", System.Globalization.CultureInfo.InvariantCulture));

            // 2. "View Data" menu item visibility: Bound to IsLogNode
            // ROOT WELL NODE -> MUST BE Collapsed
            Assert.Equal(Visibility.Collapsed, boolConverter.Convert(wellNode.IsLogNode, typeof(Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));

            // Timelogs parent node -> MUST BE Collapsed
            Assert.Equal(Visibility.Collapsed, boolConverter.Convert(timeFolder.IsLogNode, typeof(Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));
            // Depthlogs folder and child log items -> MUST BE Visible
            Assert.Equal(Visibility.Visible, boolConverter.Convert(depthFolder.IsLogNode, typeof(Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(Visibility.Visible, boolConverter.Convert(timeChild.IsLogNode, typeof(Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(Visibility.Visible, boolConverter.Convert(depthChild.IsLogNode, typeof(Visibility), null!, System.Globalization.CultureInfo.InvariantCulture));

            // 3. "Recalculate Rig State" menu item visibility: ConverterParameter="TimeLog"
            // ONLY TimeLog item child node -> Visible
            Assert.Equal(Visibility.Visible, enumConverter.Convert(timeChild.Type, typeof(Visibility), "TimeLog", System.Globalization.CultureInfo.InvariantCulture));
            // All others -> Collapsed
            Assert.Equal(Visibility.Collapsed, enumConverter.Convert(wellNode.Type, typeof(Visibility), "TimeLog", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(Visibility.Collapsed, enumConverter.Convert(timeFolder.Type, typeof(Visibility), "TimeLog", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(Visibility.Collapsed, enumConverter.Convert(depthFolder.Type, typeof(Visibility), "TimeLog", System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(Visibility.Collapsed, enumConverter.Convert(depthChild.Type, typeof(Visibility), "TimeLog", System.Globalization.CultureInfo.InvariantCulture));
        }
        finally
        {
            if (File.Exists(depthCsv)) File.Delete(depthCsv);
        }
    }

    [Fact]
    public async Task EditWellCommand_OnWellNode_OpensDialogAndSavesWell()
    {
        await _repo.EnsureWellAsync("WellAlpha", "FieldAlpha");

        var dashboard = new DashboardViewModel(_session, _repo);
        await dashboard.LoadDataAsync();

        var wellNode = dashboard.WellTree[0];

        // Simulate user clicking "Edit Well" on root well node
        bool dialogOpened = false;
        dashboard.OpenEditWellDialogHandler = (vm) =>
        {
            dialogOpened = true;
            Assert.Equal("WellAlpha", vm.WellName);
            Assert.Equal("FieldAlpha", vm.FieldName);

            vm.OperatorName = "Alpha Petroleum";
            vm.Country = "USA";
            vm.Status = "Producing";
            vm.Comments = "Edited via Edit Well context menu";
            return true; // Click Save
        };

        await dashboard.EditWellCommand.ExecuteAsync(wellNode);

        Assert.True(dialogOpened);

        // Verify changes were persisted in the database
        var savedWell = await _repo.GetProjectWellAsync();
        Assert.NotNull(savedWell);
        Assert.Equal("Alpha Petroleum", savedWell.operatorName);
        Assert.Equal("USA", savedWell.country);
        Assert.Equal("Producing", savedWell.statusWell);
        Assert.Equal("Edited via Edit Well context menu", savedWell.Comments);
    }

    [Fact]
    public async Task EditObject_OpensVmxWellEditDialog_AndPersistsWellFields()
    {
        await _repo.EnsureWellAsync("OriginalWell", "FieldA");

        var dashboard = new DashboardViewModel(_session, _repo);
        await dashboard.LoadDataAsync();

        var wellNode = dashboard.WellTree[0];
        var depthFolder = wellNode.Children.First(c => c.Name == "Depthlogs");

        // Simulate user clicking "Edit Object" via context menu
        dashboard.OpenEditWellDialogHandler = (vm) =>
        {
            Assert.Equal("OriginalWell", vm.WellName);
            Assert.Equal("FieldA", vm.FieldName);

            // User edits well-level information (vmx_well fields)
            vm.OperatorName = "PetroTech Corp";
            vm.Status = "Drilling";
            vm.RigName = "Rig-77";
            vm.Comments = "Updated from Well Tree Context Menu";
            return true; // Click "SAVE WELL"
        };

        await dashboard.EditObjectAsync(depthFolder);

        // Verify changes are saved to VMX_WELL in database
        var savedWell = await _repo.GetProjectWellAsync();
        Assert.NotNull(savedWell);
        Assert.Equal("PetroTech Corp", savedWell.operatorName);
        Assert.Equal("Drilling", savedWell.statusWell);
        Assert.Equal("Rig-77", savedWell.RigName);
        Assert.Equal("Updated from Well Tree Context Menu", savedWell.Comments);
    }

    [Fact]
    public async Task ViewData_OnDepthlogsNode_LoadsDepthLogData()
    {
        await _repo.EnsureWellAsync("Etech4");

        string depthCsv = Path.Combine(Path.GetTempPath(), $"view_depth_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(depthCsv, "DEPTH,GR,ROP\n1000.0,55.0,15.2\n1001.0,57.5,16.0\n");
            var options = new DepthLogImportOptions
            {
                LogName = "DepthLog_ForViewData",
                WellName = "Etech4",
                ColumnHeadingRow = 1,
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string>
                {
                    ["DEPTH"] = "DEPTH",
                    ["GR"] = "GR",
                    ["ROP"] = "ROP"
                }
            };
            await _depthLogService.ImportFromFileAsync(depthCsv, options);

            var dashboard = new DashboardViewModel(_session, _repo);
            await dashboard.LoadDataAsync();

            var wellNode = dashboard.WellTree[0];
            var depthFolder = wellNode.Children.First(c => c.Name == "Depthlogs");

            bool dialogOpened = false;
            dashboard.OpenViewDataDialogHandler = (vm) =>
            {
                dialogOpened = true;
                Assert.Equal("DepthLog", vm.LogType);
                Assert.Equal("DepthLog_ForViewData", vm.LogName);
                Assert.Equal(2, vm.RecordCount);
                Assert.True(vm.ColumnCount >= 3);
                return true;
            };

            await dashboard.ViewDataAsync(depthFolder);

            Assert.True(dialogOpened);
            Assert.NotNull(dashboard.LastLoadedLogData);
            Assert.Equal("DepthLog", dashboard.LastLoadedLogType);
            Assert.Equal(2, dashboard.LastLoadedLogData.Rows.Count);
            Assert.True(dashboard.LastLoadedLogData.Columns.Contains("DEPTH"));
            Assert.True(dashboard.LastLoadedLogData.Columns.Contains("GR"));
            Assert.True(dashboard.LastLoadedLogData.Columns.Contains("ROP"));
        }
        finally
        {
            if (File.Exists(depthCsv)) File.Delete(depthCsv);
        }
    }

    [Fact]
    public async Task ViewData_OnTimelogsNode_LoadsTimeLogData()
    {
        await _repo.EnsureWellAsync("Etech4");

        // Create and register a TimeLog in repository
        var timeLog = new TimeLog
        {
            ObjectID = Guid.NewGuid().ToString(),
            nameLog = "TimeLog_ForViewData",
            nameWell = "Etech4",
            __WellName = "Etech4",
            __dataTableName = $"timeLog_data_{Guid.NewGuid():N}"
        };

        // Create the underlying sqlite data table and insert records
        var conn = _session.GetConnection();
        await conn.ExecuteAsync($"CREATE TABLE [{timeLog.__dataTableName}] (DATETIME TEXT, DEPTH REAL, HKLD REAL);");
        await conn.ExecuteAsync($"INSERT INTO [{timeLog.__dataTableName}] VALUES ('2024-05-01 10:00:00', 1000.0, 50.0);");
        await conn.ExecuteAsync($"INSERT INTO [{timeLog.__dataTableName}] VALUES ('2024-05-01 10:00:10', 1000.5, 52.0);");

        await _repo.LogTimeLogAsync(timeLog);

        var dashboard = new DashboardViewModel(_session, _repo);
        await dashboard.LoadDataAsync();

        var wellNode = dashboard.WellTree[0];
        var timeFolder = wellNode.Children.First(c => c.Name == "Timelogs");
        var timeChild = timeFolder.Children[0];

        bool dialogOpened = false;
        dashboard.OpenViewDataDialogHandler = (vm) =>
        {
            dialogOpened = true;
            Assert.Equal("TimeLog", vm.LogType);
            Assert.Equal("TimeLog_ForViewData", vm.LogName);
            Assert.Equal(2, vm.RecordCount);
            Assert.True(vm.ColumnCount >= 3);
            return true;
        };

        // Parent node: does NOT open View Data dialog
        await dashboard.ViewDataAsync(timeFolder);
        Assert.False(dialogOpened);

        // Child item node: opens View Data dialog
        await dashboard.ViewDataAsync(timeChild);

        Assert.True(dialogOpened);
        Assert.NotNull(dashboard.LastLoadedLogData);
        Assert.Equal("TimeLog", dashboard.LastLoadedLogType);
        Assert.Equal(2, dashboard.LastLoadedLogData.Rows.Count);
        Assert.True(dashboard.LastLoadedLogData.Columns.Contains("DATETIME"));
        Assert.True(dashboard.LastLoadedLogData.Columns.Contains("DEPTH"));
        Assert.True(dashboard.LastLoadedLogData.Columns.Contains("HKLD"));
    }
}
