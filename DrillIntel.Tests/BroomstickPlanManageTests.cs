using System;
using System.IO;
using System.Linq;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Services;
using DrillIntel.ViewModels;
using ExcelDataReader;
using Xunit;

namespace DrillIntel.Tests;

public class BroomstickPlanManageTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _testDbPath;
    private readonly IDataServiceDIntel _dataService;

    public BroomstickPlanManageTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"drillintel_plan_tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _testDbPath = Path.Combine(_tempDir, "TestPlanDb.sqlite");
        _dataService = new DataServiceDIntel(_testDbPath);
        _dataService.OpenConnection(_testDbPath);

        // Ensure schema
        AdnlHookloadPlan.CreateTables(_dataService);
    }

    public void Dispose()
    {
        _dataService.CloseConnection();
        _dataService.Dispose();

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        try
        {
            if (Directory.Exists(_tempDir))
                Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    [Fact]
    public void ParseAndSavePlan_FromCsv_SavesSuccessfullyViaSavePlanEx()
    {
        // 1. Create sample CSV
        string csvPath = Path.Combine(_tempDir, "RotateOnBottom_0.2_OHFF.csv");
        string csvContent = @"Depth,Weight,Max Tension,Min Tension,Max Compress,Min Compress
0,23.0,0,0,0,0
100,28.2,0,0,0,0
200,30.1,0,0,0,0
300,31.9,0,0,0,0
400,33.8,0,0,0,0
500,35.6,0,0,0,0";
        File.WriteAllText(csvPath, csvContent);

        // 2. Parse via PlanImportService
        var service = new PlanImportService();
        var parseResult = service.ParsePlanFile(
            filePath: csvPath,
            planName: "RotateOnBottom 0.2 OHFF",
            wellId: "WELL-001",
            wellboreId: "WB-001",
            logId: "LOG-001",
            runNo: "1",
            planType: "HKLDP",
            targetCurve: "Pickup");

        Assert.True(parseResult.Success, parseResult.Message);
        Assert.NotNull(parseResult.Plan);
        Assert.Equal(6, parseResult.TotalPoints);
        Assert.Equal(6, parseResult.Plan.pickup.Count);

        // 3. Save via savePlanEx
        bool saved = service.SavePlan(
            _dataService,
            parseResult.Plan,
            importByRange: false,
            fromDepth: 0,
            toDepth: 0,
            errorMessage: out string errorMsg);

        Assert.True(saved, errorMsg);

        // 4. Verify SQLite tables
        var headerCount = Convert.ToInt32(_dataService.GetValue("SELECT COUNT(*) FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='WELL-001' AND PLAN_NAME='RotateOnBottom 0.2 OHFF'"));
        Assert.Equal(1, headerCount);

        var dataCount = Convert.ToInt32(_dataService.GetValue("SELECT COUNT(*) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='WELL-001' AND PLAN_TYPE='PKUP'"));
        Assert.Equal(6, dataCount);

        // 5. Verify loadObject
        var loaded = AdnlHookloadPlan.loadObject(_dataService, "WELL-001", "WB-001", "LOG-001", parseResult.Plan.PlanID);
        Assert.NotNull(loaded);
        Assert.Equal("RotateOnBottom 0.2 OHFF", loaded.Name);
        Assert.Equal(6, loaded.pickup.Count);
        Assert.Equal(23.0, loaded.pickup[1].Weight);
        Assert.Equal(35.6, loaded.pickup[6].Weight);
    }

    [Fact]
    public void SavePlanEx_ImportByRange_InsertsOnlySpecifiedDepthRange()
    {
        var plan = new AdnlHookloadPlan
        {
            WellID = "WELL-RANGE",
            WellboreID = "WB-RANGE",
            LogID = "LOG-RANGE",
            PlanID = "PLAN-RANGE-01",
            Name = "Range Test Plan",
            PlanType = "HKLDP",
            RunNo = "1"
        };

        for (int d = 0; d <= 1000; d += 100)
        {
            plan.pickup.Add(plan.pickup.Count + 1, new HookloadPlanData
            {
                Depth = d,
                Weight = 20.0 + (d * 0.05)
            });
        }

        string error = string.Empty;
        // Import only depth 200 to 600
        bool saved = AdnlHookloadPlan.savePlanEx(_dataService, plan, ref error, importByRange: true, fromDepth: 200, toDepth: 600);
        Assert.True(saved, error);

        var loaded = AdnlHookloadPlan.loadObject(_dataService, "WELL-RANGE", "WB-RANGE", "LOG-RANGE", "PLAN-RANGE-01");
        Assert.NotNull(loaded);
        Assert.Equal(5, loaded.pickup.Count); // 200, 300, 400, 500, 600
        Assert.Equal(200.0, loaded.pickup.Values.Min(x => x.Depth));
        Assert.Equal(600.0, loaded.pickup.Values.Max(x => x.Depth));
    }

    [Fact]
    public void BroomstickPlanManageViewModel_LoadsPlansAndSwitchesTabs()
    {
        var vm = new BroomstickPlanManageViewModel(_dataService, "WELL-VM", "WB-VM", "LOG-VM");
        vm.LoadSampleDemoPlansCommand.Execute(null);

        Assert.NotEmpty(vm.PlanGroups);
        var activePlan = vm.ActivePlanItem;
        Assert.NotNull(activePlan);

        // Test tab switching
        vm.SelectedTabIndex = 0; // Pickup
        Assert.Equal("Pickup", vm.SelectedTabTitle);
        Assert.NotEmpty(vm.CurrentTableRows);

        vm.SelectedTabIndex = 1; // SlackOff
        Assert.Equal("SlackOff", vm.SelectedTabTitle);

        // Add a row
        int countBefore = vm.CurrentTableRows.Count;
        vm.AddRowCommand.Execute(null);
        Assert.Equal(countBefore + 1, vm.CurrentTableRows.Count);

        // Delete row
        var lastRow = vm.CurrentTableRows.Last();
        vm.DeleteRowCommand.Execute(lastRow);
        Assert.Equal(countBefore, vm.CurrentTableRows.Count);
    }

    [Fact]
    public void DeleteAllPlans_WhenReopened_PlansRemainDeletedAndDoNotReseed()
    {
        // 1. Initialize ViewModel and seed sample plans
        var vm = new BroomstickPlanManageViewModel(_dataService, "WELL-DEL", "WB-DEL", "LOG-DEL");
        vm.ConfirmDeleteHandler = _ => true; // Bypass UI MessageBox
        vm.LoadSampleDemoPlansCommand.Execute(null);

        Assert.NotEmpty(vm.PlanGroups);
        int initialCount = Convert.ToInt32(_dataService.GetValue("SELECT COUNT(*) FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='WELL-DEL'"));
        Assert.True(initialCount > 0, "Initial plans should be seeded");

        // 2. Select all plans
        vm.SelectAllCommand.Execute(null);
        Assert.All(vm.PlanGroups.SelectMany(g => g.Plans), p => Assert.True(p.IsSelected));

        // 3. Delete all selected plans
        vm.DeleteSelectedPlansCommand.Execute(null);

        // 4. Verify in-memory state
        Assert.Empty(vm.PlanGroups);
        Assert.Null(vm.CurrentPlan);
        Assert.Null(vm.ActivePlanItem);
        Assert.Empty(vm.CurrentTableRows);
        Assert.Equal("Total Rows: 0", vm.TotalRowsText);

        // 5. Verify SQLite database has 0 plans for this well
        int dbPlanCount = Convert.ToInt32(_dataService.GetValue("SELECT COUNT(*) FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='WELL-DEL'"));
        Assert.Equal(0, dbPlanCount);

        int dbPointCount = Convert.ToInt32(_dataService.GetValue("SELECT COUNT(*) FROM VMX_ADNL_HKLD_PLAN_DATA WHERE WELL_ID='WELL-DEL'"));
        Assert.Equal(0, dbPointCount);

        // 6. User clicks Save Plan after deletion: verify it does not error or recreate plans
        vm.SavePlanCommand.Execute(null);
        Assert.Contains("committed to the database", vm.StatusMessage);

        // 7. Simulate REOPENING the manager dialog (create new ViewModel against same DB)
        var reopenedVm = new BroomstickPlanManageViewModel(_dataService, "WELL-DEL", "WB-DEL", "LOG-DEL");

        // Verify plans remain deleted: NO auto-seeding occurred
        Assert.Empty(reopenedVm.PlanGroups);
        Assert.Null(reopenedVm.CurrentPlan);
        Assert.Null(reopenedVm.ActivePlanItem);
        Assert.Empty(reopenedVm.CurrentTableRows);
        Assert.Equal("Total Rows: 0", reopenedVm.TotalRowsText);

        // Verify SQLite database STILL has 0 plans
        int reopenedDbCount = Convert.ToInt32(_dataService.GetValue("SELECT COUNT(*) FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='WELL-DEL'"));
        Assert.Equal(0, reopenedDbCount);
    }

    [Fact]
    public void TabSwitching_SyncsEdits_AndSavePlanPersistsAllTabsToDatabase()
    {
        var vm = new BroomstickPlanManageViewModel(_dataService, "WELL-TAB", "WB-TAB", "LOG-TAB");
        vm.CreateNewPlanCommand.Execute(null);

        Assert.NotNull(vm.CurrentPlan);
        string planId = vm.CurrentPlan.PlanID;

        // On Tab 0 (Pickup), add a row
        vm.SelectedTabIndex = 0;
        vm.AddRowCommand.Execute(null);
        vm.CurrentTableRows[0].Depth = 555.0;
        vm.CurrentTableRows[0].Weight = 77.7;

        // Switch to Tab 1 (SlackOff)
        vm.SelectedTabIndex = 1;
        Assert.Empty(vm.CurrentTableRows); // SlackOff has 0 rows initially

        // On Tab 1, add a row
        vm.AddRowCommand.Execute(null);
        vm.CurrentTableRows[0].Depth = 888.0;
        vm.CurrentTableRows[0].Weight = 44.4;

        // Switch back to Tab 0 (Pickup)
        vm.SelectedTabIndex = 0;
        // Verify Tab 0 edit was preserved via SyncRowsToPlanTab!
        Assert.Single(vm.CurrentTableRows);
        Assert.Equal(555.0, vm.CurrentTableRows[0].Depth);
        Assert.Equal(77.7, vm.CurrentTableRows[0].Weight);

        // Save plan to SQLite
        vm.SavePlanCommand.Execute(null);

        // Verify loaded object from SQLite has BOTH Pickup and SlackOff points
        var reloaded = AdnlHookloadPlan.loadObject(_dataService, "WELL-TAB", "WB-TAB", "LOG-TAB", planId);
        Assert.NotNull(reloaded);
        Assert.Single(reloaded.pickup);
        Assert.Equal(555.0, reloaded.pickup[1].Depth);
        Assert.Equal(77.7, reloaded.pickup[1].Weight);

        Assert.Single(reloaded.slackoff);
        Assert.Equal(888.0, reloaded.slackoff[1].Depth);
        Assert.Equal(44.4, reloaded.slackoff[1].Weight);
    }

    [Fact]
    public void InspectAllRealExcelFiles()
    {
        string dir = @"C:\Users\etech\OneDrive\Desktop\Broomstick";
        if (!Directory.Exists(dir)) return;

        var files = Directory.GetFiles(dir, "*.xlsx", SearchOption.AllDirectories);
        var sb = new System.Text.StringBuilder();

        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

        foreach (var filePath in files)
        {
            sb.AppendLine($"==================================================");
            sb.AppendLine($"FILE: {Path.GetFileName(filePath)}");
            sb.AppendLine($"PATH: {filePath}");

            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream);
            var dataSet = reader.AsDataSet(new ExcelDataReader.ExcelDataSetConfiguration
            {
                ConfigureDataTable = _ => new ExcelDataReader.ExcelDataTableConfiguration
                {
                    UseHeaderRow = false
                }
            });

            sb.AppendLine($"Sheets count: {dataSet.Tables.Count}");
            foreach (System.Data.DataTable table in dataSet.Tables)
            {
                if (table.Rows.Count < 2) continue;
                sb.AppendLine($"  Sheet: '{table.TableName}' (Rows: {table.Rows.Count}, Cols: {table.Columns.Count})");
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    string h0 = table.Rows[0][c]?.ToString() ?? "";
                    string h1 = table.Rows.Count > 1 ? table.Rows[1][c]?.ToString() ?? "" : "";
                    string h2 = table.Rows.Count > 2 ? table.Rows[2][c]?.ToString() ?? "" : "";
                    sb.AppendLine($"    Col {c}: '{h0}' | Unit: '{h1}' | Sample: '{h2}'");
                }
            }
        }

        File.WriteAllText(@"C:\Users\etech\OneDrive\Desktop\Broomstick\col_inspect.txt", sb.ToString());

        string btmFile = @"C:\Users\etech\OneDrive\Desktop\Broomstick\Broomstick plan\08.50 in On Btm Torque Plan Data.xlsx";
        if (File.Exists(btmFile))
        {
            using var stream = File.Open(btmFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream);
            var ds = reader.AsDataSet();
            var btmSb = new System.Text.StringBuilder();
            var t = ds.Tables["0.2 OHFF"];
            if (t != null)
            {
                for (int r = 0; r < Math.Min(10, t.Rows.Count); r++)
                {
                    btmSb.AppendLine($"Row {r}: " + string.Join(" | ", t.Rows[r].ItemArray));
                }
            }
            File.WriteAllText(@"C:\Users\etech\OneDrive\Desktop\Broomstick\btm_inspect.txt", btmSb.ToString());
        }
    }

    [Fact]
    public void ParseRealExcelFile_12_25_HookloadDrilling_Succeeds()
    {
        string filePath = @"C:\Users\etech\OneDrive\Desktop\Broomstick\Broomstick plan\HookLoad Plan\HookLoad Plan\12.5 In\12.25 In Hookload Drilling.xlsx";
        if (!File.Exists(filePath)) return;

        var service = new PlanImportService();
        var sheets = service.GetSheetNames(filePath);
        Assert.NotEmpty(sheets);
        Assert.Contains("0.2 OHFF", sheets);

        // Parse with default (should pick 0.2 OHFF and detect curves)
        var result = service.ParsePlanFile(filePath, "12.25 In Hookload Drilling", "WELL-12", "WB-12", "LOG-12");

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Plan);
        Assert.Contains("0.2 OHFF", result.SelectedSheetName);
        Assert.Equal("0.2 OHFF", result.Plan.RunNo);
        Assert.NotEmpty(result.PreviewHeaders);
        Assert.NotEmpty(result.PreviewRows);

        // Verify Pickup, SlackOff, OnTorque were parsed
        Assert.True(result.Plan.pickup.Count > 0, "Pickup count should be > 0");
        Assert.True(result.Plan.slackoff.Count > 0, "SlackOff count should be > 0");
        Assert.True(result.Plan.onTorque.Count > 0, "OnBottomTorque count should be > 0");

        // Verify savePlanEx works
        string error = string.Empty;
        bool saved = service.SavePlan(_dataService, result.Plan, false, 0, 0, out error);
        Assert.True(saved, error);

        // Verify loaded object from SQLite
        var loaded = AdnlHookloadPlan.loadObject(_dataService, "WELL-12", "WB-12", "LOG-12", result.Plan.PlanID);
        Assert.NotNull(loaded);
        Assert.Equal(result.Plan.pickup.Count, loaded.pickup.Count);
    }

    [Fact]
    public void ParseAllRealExcelFiles_VerifyEachParsesSuccessfully()
    {
        string dir = @"C:\Users\etech\OneDrive\Desktop\Broomstick";
        if (!Directory.Exists(dir)) return;

        var files = Directory.GetFiles(dir, "*.xlsx", SearchOption.AllDirectories);
        var service = new PlanImportService();

        foreach (var file in files)
        {
            var res = service.ParsePlanFile(file, Path.GetFileNameWithoutExtension(file));
            Assert.True(res.Success, $"Failed on {Path.GetFileName(file)}: {res.Message}");
            Assert.True(res.TotalPoints > 0, $"0 points parsed on {Path.GetFileName(file)}");
        }
    }

    [Fact]
    public void ImportPlanViewModel_Parses12_25_HookloadDrilling_AndSaves()
    {
        string filePath = @"C:\Users\etech\OneDrive\Desktop\Broomstick\Broomstick plan\HookLoad Plan\HookLoad Plan\12.5 In\12.25 In Hookload Drilling.xlsx";
        if (!File.Exists(filePath)) return;

        var vm = new ImportPlanViewModel(_dataService, "WELL-VM12", "WB-VM12", "LOG-VM12", "12.25 In Hookload Drilling", "All");
        vm.FilePath = filePath;
        // Trigger sheet load & parse
        var sheets = new PlanImportService().GetSheetNames(filePath);
        foreach (var s in sheets) vm.AvailableSheets.Add(s);
        vm.HasMultipleSheets = vm.AvailableSheets.Count > 1;
        vm.SelectedSheet = "0.2 OHFF";
        vm.ParseFile();

        Assert.True(vm.IsFileParsed);
        Assert.False(vm.IsStatusError);
        Assert.True(vm.TotalPointsParsed > 0);
        Assert.NotNull(vm.ImportedPlan);
        Assert.NotEmpty(vm.PreviewTable.Columns);
        Assert.NotEmpty(vm.PreviewTable.Rows);

        // Execute import
        bool closeCalled = false;
        vm.RequestClose += success => closeCalled = success;
        vm.ExecuteImportCommand.Execute(null);

        Assert.True(closeCalled);
        var loaded = AdnlHookloadPlan.loadObject(_dataService, "WELL-VM12", "WB-VM12", "LOG-VM12", vm.ImportedPlan.PlanID);
        Assert.NotNull(loaded);
        Assert.True(loaded.pickup.Count > 0);
    }

    [Fact]
    public void MultiTimelog_WithPrimary_SelectsPrimaryAndDisplaysContext()
    {
        _dataService.ExecuteNonQuery(@"
CREATE TABLE IF NOT EXISTS VMX_WELL (WELL_ID TEXT PRIMARY KEY, WELL_NAME TEXT);
CREATE TABLE IF NOT EXISTS VMX_WELLBORE (WELL_ID TEXT, WELLBORE_ID TEXT, WELLBORE_NAME TEXT, PRIMARY KEY (WELLBORE_ID, WELL_ID));
CREATE TABLE IF NOT EXISTS VMX_TIME_LOG (WELL_ID TEXT, WELLBORE_ID TEXT, LOG_ID TEXT, LOG_NAME TEXT, PRIMARY_LOG INTEGER, PRIMARY KEY (LOG_ID, WELLBORE_ID, WELL_ID));
");

        _dataService.ExecuteNonQuery("INSERT OR REPLACE INTO VMX_WELL (WELL_ID, WELL_NAME) VALUES ('W-100', 'Alpha Discovery #1');");
        _dataService.ExecuteNonQuery("INSERT OR REPLACE INTO VMX_WELLBORE (WELL_ID, WELLBORE_ID, WELLBORE_NAME) VALUES ('W-100', 'WB-100', '12.25in Production Section');");
        _dataService.ExecuteNonQuery("INSERT OR REPLACE INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, PRIMARY_LOG) VALUES ('W-100', 'WB-100', 'TL-01', '1-Sec Drilling Log', 1);");
        _dataService.ExecuteNonQuery("INSERT OR REPLACE INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, PRIMARY_LOG) VALUES ('W-100', 'WB-100', 'TL-02', '5-Sec Reaming Log', 0);");

        var vm = new BroomstickPlanManageViewModel(_dataService, "W-100", "WB-100", "");

        Assert.True(vm.HasMultipleTimelogs);
        Assert.False(vm.HasNoPrimaryTimelog);
        Assert.True(vm.IsPrimaryTimelog);
        Assert.Equal("Alpha Discovery #1", vm.CurrentWellName);
        Assert.Equal("12.25in Production Section", vm.CurrentWellboreName);
        Assert.Equal("1-Sec Drilling Log", vm.CurrentLogName);
        Assert.Equal("TL-01", vm.LogID);
        Assert.Contains("Alpha Discovery #1", vm.WindowTitle);
        Assert.Contains("1-Sec Drilling Log", vm.WindowTitle);
    }

    [Fact]
    public void MultiTimelog_NoPrimaryMarked_AutoSelectsTimelogWithPlansAndShowsWarning()
    {
        _dataService.ExecuteNonQuery(@"
CREATE TABLE IF NOT EXISTS VMX_WELL (WELL_ID TEXT PRIMARY KEY, WELL_NAME TEXT);
CREATE TABLE IF NOT EXISTS VMX_WELLBORE (WELL_ID TEXT, WELLBORE_ID TEXT, WELLBORE_NAME TEXT, PRIMARY KEY (WELLBORE_ID, WELL_ID));
CREATE TABLE IF NOT EXISTS VMX_TIME_LOG (WELL_ID TEXT, WELLBORE_ID TEXT, LOG_ID TEXT, LOG_NAME TEXT, PRIMARY_LOG INTEGER, PRIMARY KEY (LOG_ID, WELLBORE_ID, WELL_ID));
");

        _dataService.ExecuteNonQuery("INSERT OR REPLACE INTO VMX_WELL (WELL_ID, WELL_NAME) VALUES ('W-200', 'Beta Offshore #4');");
        _dataService.ExecuteNonQuery("INSERT OR REPLACE INTO VMX_WELLBORE (WELL_ID, WELLBORE_ID, WELLBORE_NAME) VALUES ('W-200', 'WB-200', 'Main Bore');");
        _dataService.ExecuteNonQuery("INSERT OR REPLACE INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, PRIMARY_LOG) VALUES ('W-200', 'WB-200', 'TL-A', 'Rig Time Log A', 0);");
        _dataService.ExecuteNonQuery("INSERT OR REPLACE INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, PRIMARY_LOG) VALUES ('W-200', 'WB-200', 'TL-B', 'Rig Time Log B', 0);");

        // Insert a plan associated with TL-B specifically
        var planB = new AdnlHookloadPlan
        {
            WellID = "W-200",
            WellboreID = "WB-200",
            LogID = "TL-B",
            PlanID = "PLAN-B-1",
            Name = "Beta 0.2 Plan",
            PlanType = "HKLDP",
            RunNo = "1"
        };
        planB.pickup.Add(1, new HookloadPlanData { Depth = 500, Weight = 45.0 });
        string err = "";
        AdnlHookloadPlan.savePlanEx(_dataService, planB, ref err, false, 0, 0);

        // Open ViewModel without explicit logId
        var vm = new BroomstickPlanManageViewModel(_dataService, "W-200", "WB-200", "");

        // Verify multi-timelog detected, and NO primary marked flag is TRUE
        Assert.True(vm.HasMultipleTimelogs);
        Assert.True(vm.HasNoPrimaryTimelog, "Should flag HasNoPrimaryTimelog as true because neither log has PRIMARY_LOG=1");
        Assert.False(vm.IsPrimaryTimelog, "Active log is not marked primary");

        // Verify auto-fallback selected TL-B because TL-B has saved plans
        Assert.Equal("TL-B", vm.LogID);
        Assert.Equal("Rig Time Log B", vm.CurrentLogName);
        Assert.Equal("Beta Offshore #4", vm.CurrentWellName);
        Assert.Equal("Main Bore", vm.CurrentWellboreName);

        // Verify switching to TL-A
        var itemA = vm.AvailableTimelogContexts.FirstOrDefault(x => x.LogId == "TL-A");
        Assert.NotNull(itemA);
        vm.SelectedTimelogContext = itemA;
        Assert.Equal("TL-A", vm.LogID);
        Assert.Equal("Rig Time Log A", vm.CurrentLogName);

        // Verify switching to "All Plans"
        var itemAll = vm.AvailableTimelogContexts.FirstOrDefault(x => x.IsAllPlansOption);
        Assert.NotNull(itemAll);
        vm.SelectedTimelogContext = itemAll;
        Assert.Equal("", vm.LogID);
        Assert.Equal("All Timelogs", vm.CurrentLogName);
        Assert.Equal("All Wells", vm.CurrentWellName);
        Assert.Contains("All Wells & Timelogs", vm.WindowTitle);
    }

    [Fact]
    public void ImportPlanDialog_InheritsContextFromViewModel()
    {
        var importVm = new ImportPlanViewModel(
            _dataService,
            wellId: "W-300",
            wellboreId: "WB-300",
            logId: "TL-300",
            defaultPlanName: "Test Import Plan",
            activeTabName: "Pickup",
            wellName: "Deepwater Gamma",
            wellboreName: "Lateral 1",
            logName: "1-Sec MWD Log",
            isPrimaryLog: true);

        Assert.Equal("Deepwater Gamma", importVm.WellName);
        Assert.Equal("Lateral 1", importVm.WellboreName);
        Assert.Equal("1-Sec MWD Log", importVm.LogName);
        Assert.True(importVm.IsPrimaryLog);
        Assert.Equal("W-300", importVm.WellID);
        Assert.Equal("WB-300", importVm.WellboreID);
        Assert.Equal("TL-300", importVm.LogID);
    }

    [Fact]
    public void VerifyAllViewPackIconKindsAreValid()
    {
        var iconsToTest = new[]
        {
            "Factory",
            "SourceBranch",
            "ClockOutline",
            "CheckCircle",
            "AlertCircleOutline",
            "Hook",
            "FileUploadOutline",
            "Sync",
            "ContentSaveOutline",
            "DatabaseImport",
            "TableEye",
            "FolderOpenOutline",
            "Target",
            "FileDocumentMultipleOutline"
        };

        foreach (var icon in iconsToTest)
        {
            bool valid = Enum.TryParse<MaterialDesignThemes.Wpf.PackIconKind>(icon, out var parsed);
            Assert.True(valid, $"PackIconKind '{icon}' is not a valid enum value in MaterialDesignThemes.Wpf!");
        }
    }

    [Fact]
    public void GetSheetNames_ExcludesHowToDocumentationSheet_ByDefault()
    {
        string excelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "12.25 In Hookload Drilling.xlsx");
        if (!File.Exists(excelPath))
        {
            excelPath = @"C:\Users\etech\OneDrive\Desktop\Broomstick\Broomstick plan\HookLoad Plan\HookLoad Plan\12.5 In\12.25 In Hookload Drilling.xlsx";
        }
        Assert.True(File.Exists(excelPath), $"Test excel file not found at {excelPath}");

        var service = new PlanImportService();

        // When excludeDocumentation is false, "How To" should be in the list
        var allSheets = service.GetSheetNames(excelPath, excludeDocumentation: false);
        Assert.Contains(allSheets, s => PlanImportService.IsDocumentationSheet(s));

        // When excludeDocumentation is true (default), "How To" must be filtered out
        var filteredSheets = service.GetSheetNames(excelPath, excludeDocumentation: true);
        Assert.DoesNotContain(filteredSheets, s => PlanImportService.IsDocumentationSheet(s));
        Assert.True(filteredSheets.Count >= 2, "Expected at least 2 data sheets in the sample workbook");
        Assert.Contains("0.2 OHFF", filteredSheets);
    }

    [Fact]
    public void ImportPlanViewModel_ParsesAllRunsWithoutSheetOrCurveDropdowns_AndSavesAllRuns()
    {
        string excelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "12.25 In Hookload Drilling.xlsx");
        if (!File.Exists(excelPath))
        {
            excelPath = @"C:\Users\etech\OneDrive\Desktop\Broomstick\Broomstick plan\HookLoad Plan\HookLoad Plan\12.5 In\12.25 In Hookload Drilling.xlsx";
        }
        Assert.True(File.Exists(excelPath), $"Test excel file not found at {excelPath}");

        var vm = new ImportPlanViewModel(
            _dataService,
            wellId: "WELL-RUNS",
            wellboreId: "WB-RUNS",
            logId: "LOG-RUNS");

        vm.LoadSheetsAndParse(excelPath);

        // Verify multiple runs were parsed (excluding sheet 0 "How To")
        Assert.True(vm.ParsedPlans.Count >= 2, "Expected multiple runs parsed from data sheets");
        Assert.DoesNotContain(vm.ParsedPlans, p => p.RunNo.Equals("How to", StringComparison.OrdinalIgnoreCase) || p.RunNo.Equals("How To", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(vm.ParsedPlans, p => p.RunNo == "0.2 OHFF");
        Assert.True(vm.TotalPointsParsed > 0);
        Assert.Contains("Runs", vm.DetectedCurvesSummary);

        // Execute import
        vm.PlanName = "12.25 In Hookload Drilling";
        vm.ExecuteImportCommand.Execute(null);

        // Verify SQLite database has all runs saved with the same PLAN_NAME and their respective RUN_NO
        var count = Convert.ToInt32(_dataService.GetValue("SELECT COUNT(*) FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='WELL-RUNS' AND PLAN_NAME='12.25 In Hookload Drilling'"));
        Assert.Equal(vm.ParsedPlans.Count, count);
    }

    [Fact]
    public void ParseExcel_TreatsSheetsExceptSheet0AsRunNumbers_UnderPlanName_AndDisplaysInManager()
    {
        string excelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "12.25 In Hookload Drilling.xlsx");
        if (!File.Exists(excelPath))
        {
            excelPath = @"C:\Users\etech\OneDrive\Desktop\Broomstick\Broomstick plan\HookLoad Plan\HookLoad Plan\12.5 In\12.25 In Hookload Drilling.xlsx";
        }
        Assert.True(File.Exists(excelPath), $"Test excel file not found at {excelPath}");

        var service = new PlanImportService();
        var result = service.ParsePlanFile(
            filePath: excelPath,
            planName: "12.25 In Hookload Drilling",
            wellId: "WELL-TREE",
            wellboreId: "WB-TREE",
            logId: "LOG-TREE",
            runNo: "",
            planType: "HKLDP",
            targetCurve: "All",
            sheetName: "");

        Assert.True(result.Success, result.Message);
        Assert.True(result.ParsedPlans.Count >= 2, "Expected at least 2 runs parsed");
        Assert.True(result.TotalPoints > 0);

        // Under "Plan Name" -> SheetName as RunNumber -> under Each RunNumber -> All Target Curve Data
        foreach (var run in result.ParsedPlans)
        {
            Assert.Equal("12.25 In Hookload Drilling", run.Name);
            Assert.False(string.IsNullOrWhiteSpace(run.RunNo));
            Assert.False(PlanImportService.IsDocumentationSheet(run.RunNo), "Documentation sheet should not be treated as a run");
            Assert.True(run.pickup.Count > 0 || run.slackoff.Count > 0 || run.rotate.Count > 0, $"Run {run.RunNo} should have curve points");
        }

        // Save all runs via SavePlans
        bool saved = service.SavePlans(
            _dataService,
            result.ParsedPlans,
            importByRange: false,
            fromDepth: 0,
            toDepth: 0,
            errorMessage: out string errorMsg);

        Assert.True(saved, errorMsg);

        // Verify SQLite: VMX_ADNL_HKLD_PLAN has each run
        var runCount = Convert.ToInt32(_dataService.GetValue("SELECT COUNT(*) FROM VMX_ADNL_HKLD_PLAN WHERE WELL_ID='WELL-TREE' AND PLAN_NAME='12.25 In Hookload Drilling'"));
        Assert.Equal(result.ParsedPlans.Count, runCount);

        // Open BroomstickPlanManageViewModel and verify hierarchy:
        // Group: "12.25 In Hookload Drilling" -> Child Items: SheetName as RunNumber ("0.2 OHFF", etc.)
        var managerVm = new BroomstickPlanManageViewModel(_dataService, "WELL-TREE", "WB-TREE", "LOG-TREE");

        var group = managerVm.PlanGroups.FirstOrDefault(g => g.GroupName == "12.25 In Hookload Drilling");
        Assert.NotNull(group);
        Assert.Equal(result.ParsedPlans.Count, group.Plans.Count);

        // Verify DisplayName on child items matches RunNo
        var run02 = group.Plans.FirstOrDefault(p => p.RunNo == "0.2 OHFF");
        Assert.NotNull(run02);
        Assert.Equal("0.2 OHFF", run02.DisplayName);

        // Verify selecting a run loads all curve data for that specific run
        managerVm.SelectPlan(run02);
        Assert.NotNull(managerVm.CurrentPlan);
        Assert.Equal("0.2 OHFF", managerVm.CurrentPlan.RunNo);
        Assert.True(managerVm.CurrentTableRows.Count > 0, "Selected run should populate rows in the active tab table");
    }

    [Fact]
    public void Parse_8_5_In_HookloadDrilling_ImportsAllRuns()
    {
        string excelPath = @"C:\Users\etech\OneDrive\Desktop\Broomstick\Broomstick plan\HookLoad Plan\HookLoad Plan\8.5 In\Drilling\8.5 In Hookload Drilling.xlsx";
        Assert.True(File.Exists(excelPath), $"Test excel file not found at {excelPath}");

        var service = new PlanImportService();
        var result = service.ParsePlanFile(
            filePath: excelPath,
            planName: "8.5 In Hookload Drilling",
            wellId: "WELL-85",
            wellboreId: "WB-85",
            logId: "LOG-85",
            runNo: "",
            planType: "HKLDP",
            targetCurve: "All",
            sheetName: "");

        Assert.True(result.Success, result.Message);
        // How To sheet must be excluded; 0.2, 0.25, 0.3, 0.35, 0.4 should all be parsed
        Assert.Equal(5, result.ParsedPlans.Count);
        Assert.Contains(result.ParsedPlans, p => p.RunNo == "0.2 OHFF");
        Assert.Contains(result.ParsedPlans, p => p.RunNo == "0.25 OHFF");
        Assert.Contains(result.ParsedPlans, p => p.RunNo == "0.3 OHFF");
        Assert.Contains(result.ParsedPlans, p => p.RunNo == "0.35 OHFF");
        Assert.Contains(result.ParsedPlans, p => p.RunNo == "0.4 OHFF");
    }

    [Fact]
    public void ParseAndImport_8_5_In_HookloadDrilling_ShowsAll5RunsUnderPlanGroupInManager()
    {
        string excelPath = @"C:\Users\etech\OneDrive\Desktop\Broomstick\Broomstick plan\HookLoad Plan\HookLoad Plan\8.5 In\Drilling\8.5 In Hookload Drilling.xlsx";
        Assert.True(File.Exists(excelPath), $"Test excel file not found at {excelPath}");

        // 1. User opens manager ViewModel
        var managerVm = new BroomstickPlanManageViewModel(_dataService, "WELL-85-MGR", "WB-85-MGR", "LOG-85-MGR");

        // 2. User opens ImportPlanViewModel and imports "8.5 In Hookload Drilling.xlsx"
        var importVm = new ImportPlanViewModel(_dataService, "WELL-85-MGR", "WB-85-MGR", "LOG-85-MGR");
        importVm.LoadSheetsAndParse(excelPath);

        Assert.Equal(5, importVm.ParsedPlans.Count);
        Assert.Equal("8.5 In Hookload Drilling", importVm.PlanName);

        // Execute import command (saves all runs via SavePlans)
        importVm.ExecuteImportCommand.Execute(null);

        // 3. Manager updates (ReloadPlans)
        managerVm.ReloadPlans();

        // 4. Verify PlanGroups in Manager
        var group = managerVm.PlanGroups.FirstOrDefault(g => g.GroupName == "8.5 In Hookload Drilling");
        Assert.NotNull(group);
        Assert.Equal(5, group.Plans.Count);

        var runNames = group.Plans.Select(p => p.DisplayName).ToList();
        Assert.Contains("0.2 OHFF", runNames);
        Assert.Contains("0.25 OHFF", runNames);
        Assert.Contains("0.3 OHFF", runNames);
        Assert.Contains("0.35 OHFF", runNames);
        Assert.Contains("0.4 OHFF", runNames);

        managerVm.SelectPlan(group.Plans.First(p => p.RunNo == "0.2 OHFF"));
        var rows02 = managerVm.CurrentTableRows.Select(r => (r.Depth, r.Weight)).ToList();

        managerVm.SelectPlan(group.Plans.First(p => p.RunNo == "0.25 OHFF"));
        var rows025 = managerVm.CurrentTableRows.Select(r => (r.Depth, r.Weight)).ToList();

        int firstDiff = -1;
        for (int i = 0; i < Math.Min(rows02.Count, rows025.Count); i++)
        {
            if (Math.Abs(rows02[i].Weight - rows025[i].Weight) > 0.001)
            {
                firstDiff = i;
                break;
            }
        }

        Assert.Equal(56, rows02.Count);
        Assert.Equal(56, rows025.Count);
        // Shallow depths (0 to ~3000 ft) are mathematically similar across friction factors,
        // but deeper depths diverge significantly (depth 12450: 416 klbf vs 440 klbf)
        Assert.True(firstDiff >= 0, "0.2 OHFF and 0.25 OHFF should diverge as depth increases");
        Assert.Equal(11, firstDiff); // At index 11 (depth 3320 ft), weight starts diverging (129 vs 130)
        Assert.NotEqual(rows02.Last().Weight, rows025.Last().Weight); // At bottom, 416 vs 440
    }
}

