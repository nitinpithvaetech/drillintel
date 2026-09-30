using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Projects;
using DrillIntel.ViewModels;
using System.Threading.Tasks;
using Xunit;

namespace DrillIntel.Tests
{
    public class RigStateServiceTests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly DataServiceDIntel _dataService;

        public RigStateServiceTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"rigstate_test_{Guid.NewGuid():N}.dintel");
            SchemaInitializer.CreateDatabase(_tempDbPath);

            _dataService = new DataServiceDIntel(_tempDbPath);
            _dataService.UserName = "TestOperator";
        }

        public void Dispose()
        {
            _dataService.Dispose();
            try
            {
                if (File.Exists(_tempDbPath))
                    File.Delete(_tempDbPath);
            }
            catch { }
        }

        [Fact]
        public void IsColumnAvailable_ReturnsTrueForExistingColumn_AndFalseForNonExisting()
        {
            bool hasModifiedBy = RigStateService.IsColumnAvailable(_dataService, "VMX_COMMON_RIGSTATE_SETUP", "MODIFIED_BY");
            bool hasNonExistent = RigStateService.IsColumnAvailable(_dataService, "VMX_COMMON_RIGSTATE_SETUP", "NON_EXISTENT_COLUMN_XYZ");

            Assert.True(hasModifiedBy);
            Assert.False(hasNonExistent);
        }

        [Fact]
        public void SaveCommonRigStateSetup_NullChecks_ReturnFalseWithError()
        {
            bool result1 = RigStateService.SaveCommonRigStateSetup(null!, new rigState());
            Assert.False(result1);
            Assert.NotEmpty(RigStateService.LastError);
            Assert.Equal(RigStateService.LastError, rigState.LastError);

            bool result2 = rigState.SaveCommonRigStateSetup(_dataService, null!);
            Assert.False(result2);
            Assert.NotEmpty(rigState.LastError);
            Assert.Equal("RigState object is null", rigState.LastError);
        }

        [Fact]
        public void SaveCommonRigStateSetup_EmptyTable_InsertsAndUpdatesSuccessfully()
        {
            // Ensure table is empty initially
            _dataService.ExecuteNonQuery("DELETE FROM VMX_COMMON_RIGSTATE_SETUP;");
            _dataService.ExecuteNonQuery("DELETE FROM VMX_COMMON_RIGSTATE_ITEMS;");

            var rigStateObj = new rigState
            {
                UnknownName = "CustomUnknown",
                UnknownNumber = 99,
                UnknownColor = 123456,
                HookloadCutOff = 120.5,
                RPMCutOff = 45.0,
                CIRCCutOff = 10.2,
                Sensitivity = 1.5,
                PumpPressureCutOff = 2500.0,
                DepthComparisonSens = 3.2,
                DetectAutoSlideDrilling = true,
                SelectedSet = 2,
                TorqueMin = 100.0,
                TorqueMax = 500.0,
                CalibrationRows = 5,
                MinTorqueDifference = 15.0,
                MinRPM = 30.0,
                MaxRPM = 120.0,
                TorqueMin2 = 200.0,
                TorqueMax2 = 600.0,
                CalibrationRows2 = 6,
                MinTorqueDifference2 = 25.0,
                MinRPM2 = 40.0,
                MaxRPM2 = 130.0,
                TorqueMin3 = 300.0,
                TorqueMax3 = 700.0,
                CalibrationRows3 = 7,
                MinTorqueDifference3 = 35.0,
                MinRPM3 = 50.0,
                MaxRPM3 = 140.0,
                DetectAirDrilling = true,
                AirPressure = 350.0,
                TorqueCutOff = 80.0,
                MistFlowCutOff = 15.5,
                TorqueCycles = 4,
                CalibrationTime = 3,
                PercentWindow = 25.0,
                DetectPipeMovement = true,
                PipeMovementThreshold = 18.5
            };

            rigStateObj.rigStates[1] = new rigStateItem { Number = 1, Name = "Rotary Drilling", Color = 255 };
            rigStateObj.rigStates[2] = new rigStateItem { Number = 2, Name = "Slide Drilling", Color = 65280 };

            bool saved = RigStateService.SaveCommonRigStateSetup(_dataService, rigStateObj);
            Assert.True(saved, $"SaveCommonRigStateSetup failed: {RigStateService.LastError}");

            // Verify with LoadCommonRigStateSetup
            var loaded = RigStateService.LoadCommonRigStateSetup(_dataService);
            Assert.NotNull(loaded);
            Assert.Equal("CustomUnknown", loaded.UnknownName);
            Assert.Equal(99, loaded.UnknownNumber);
            Assert.Equal(123456, loaded.UnknownColor);
            Assert.Equal(120.5, loaded.HookloadCutOff);
            Assert.Equal(45.0, loaded.RPMCutOff);
            Assert.Equal(10.2, loaded.CIRCCutOff);
            Assert.Equal(1.5, loaded.Sensitivity);
            Assert.Equal(2500.0, loaded.PumpPressureCutOff);
            Assert.Equal(3.2, loaded.DepthComparisonSens);
            Assert.True(loaded.DetectAutoSlideDrilling);
            Assert.Equal(100.0, loaded.TorqueMin);
            Assert.Equal(500.0, loaded.TorqueMax);
            Assert.Equal(5, loaded.CalibrationRows);
            Assert.Equal(15.0, loaded.MinTorqueDifference);
            Assert.Equal(30.0, loaded.MinRPM);
            Assert.Equal(120.0, loaded.MaxRPM);
            Assert.Equal(200.0, loaded.TorqueMin2);
            Assert.Equal(600.0, loaded.TorqueMax2);
            Assert.Equal(6, loaded.CalibrationRows2);
            Assert.Equal(25.0, loaded.MinTorqueDifference2);
            Assert.Equal(40.0, loaded.MinRPM2);
            Assert.Equal(130.0, loaded.MaxRPM2);
            Assert.Equal(300.0, loaded.TorqueMin3);
            Assert.Equal(700.0, loaded.TorqueMax3);
            Assert.Equal(7, loaded.CalibrationRows3);
            Assert.Equal(35.0, loaded.MinTorqueDifference3);
            Assert.Equal(50.0, loaded.MinRPM3);
            Assert.Equal(140.0, loaded.MaxRPM3);
            // When MIST_CUTOFF is not present in schema, LoadCommonRigStateSetup resets DetectAirDrilling to false
            Assert.False(loaded.DetectAirDrilling);
            Assert.Equal(350.0, loaded.AirPressure);
            Assert.Equal(80.0, loaded.TorqueCutOff);
            Assert.Equal(4, loaded.TorqueCycles);
            Assert.Equal(3, loaded.CalibrationTime);
            Assert.Equal(25.0, loaded.PercentWindow);
            Assert.True(loaded.DetectPipeMovement);
            Assert.Equal(18.5, loaded.PipeMovementThreshold);

            // Verify items
            Assert.Equal(2, loaded.rigStates.Count);
            Assert.True(loaded.rigStates.ContainsKey(1));
            Assert.Equal("Rotary Drilling", loaded.rigStates[1].Name);
            Assert.Equal(255, loaded.rigStates[1].Color);
            Assert.True(loaded.rigStates.ContainsKey(2));
            Assert.Equal("Slide Drilling", loaded.rigStates[2].Name);
            Assert.Equal(65280, loaded.rigStates[2].Color);
        }

        [Fact]
        public void SaveCommonRigStateSetup_WithMistCutoffColumn_PersistsMistCutoff()
        {
            // Add MIST_CUTOFF column if not already present
            if (!RigStateService.IsColumnAvailable(_dataService, "VMX_COMMON_RIGSTATE_SETUP", "MIST_CUTOFF"))
            {
                _dataService.ExecuteNonQuery("ALTER TABLE VMX_COMMON_RIGSTATE_SETUP ADD COLUMN MIST_CUTOFF REAL;");
            }

            var rigStateObj = new rigState
            {
                UnknownName = "MistTest",
                DetectAirDrilling = true,
                MistFlowCutOff = 42.5
            };

            bool saved = rigState.SaveCommonRigStateSetup(_dataService, rigStateObj);
            Assert.True(saved, $"SaveCommonRigStateSetup failed: {rigState.LastError}");

            var loaded = rigState.LoadCommonRigStateSetup(_dataService);
            Assert.NotNull(loaded);
            Assert.Equal("MistTest", loaded.UnknownName);
            Assert.True(loaded.DetectAirDrilling);
            Assert.Equal(42.5, loaded.MistFlowCutOff);
        }

        [Fact]
        public void SaveCommonRigStateSetup_UpdatesExistingRecord_AndReplacesItems()
        {
            // 1. Initial save
            var initial = new rigState
            {
                UnknownName = "InitialSetup",
                HookloadCutOff = 50.0
            };
            initial.rigStates[1] = new rigStateItem { Number = 1, Name = "State1", Color = 100 };
            initial.rigStates[2] = new rigStateItem { Number = 2, Name = "State2", Color = 200 };

            Assert.True(rigState.SaveCommonRigStateSetup(_dataService, initial));

            // 2. Second save modifying values and items
            var updated = new rigState
            {
                UnknownName = "UpdatedSetup",
                HookloadCutOff = 75.0
            };
            updated.rigStates[3] = new rigStateItem { Number = 3, Name = "State3", Color = 300 };

            Assert.True(rigState.SaveCommonRigStateSetup(_dataService, updated));

            // 3. Verify updated record
            var loaded = rigState.LoadCommonRigStateSetup(_dataService);
            Assert.NotNull(loaded);
            Assert.Equal("UpdatedSetup", loaded.UnknownName);
            Assert.Equal(75.0, loaded.HookloadCutOff);

            // Previous items 1 and 2 should be deleted, item 3 present
            Assert.Single(loaded.rigStates);
            Assert.True(loaded.rigStates.ContainsKey(3));
            Assert.Equal("State3", loaded.rigStates[3].Name);
            Assert.Equal(300, loaded.rigStates[3].Color);
        }

        [Fact]
        public void RigState_DefaultValues_MatchExcelTemplate()
        {
            // Test 1: Property initializers on fresh new instance
            var defaultInstance = new rigState();
            Assert.Equal(-2818048, defaultInstance.UnknownColor);
            Assert.Equal("#D50000", defaultInstance.UnknownColorHex);
            Assert.Equal(90, defaultInstance.HookloadCutOff);
            Assert.Equal(1, defaultInstance.RPMCutOff);
            Assert.Equal(1, defaultInstance.CIRCCutOff);
            Assert.Equal(1, defaultInstance.Sensitivity);
            Assert.Equal(0.3, defaultInstance.DepthComparisonSens);
            Assert.Equal(1, defaultInstance.TorqueMin);
            Assert.Equal(12000, defaultInstance.TorqueMax);
            Assert.Equal(20, defaultInstance.CalibrationRows);
            Assert.Equal(500, defaultInstance.MinTorqueDifference);
            Assert.Equal(50, defaultInstance.MaxRPM);
            Assert.Equal(15000, defaultInstance.TorqueMax2);
            Assert.Equal(50, defaultInstance.CalibrationRows2);
            Assert.Equal(1, defaultInstance.MinTorqueDifference2);
            Assert.Equal(44, defaultInstance.MaxRPM2);
            Assert.Equal(0, defaultInstance.CalibrationRows3);

            // Test 2: SetDefaultValues resets modified instance
            defaultInstance.HookloadCutOff = 999;
            defaultInstance.SetDefaultValues();
            Assert.Equal(90, defaultInstance.HookloadCutOff);

            // Test 3: CreateDefault static factory method
            var factoryCreated = rigState.CreateDefault();
            Assert.Equal(90, factoryCreated.HookloadCutOff);
            Assert.Equal(-2818048, factoryCreated.UnknownColor);
            Assert.Equal(28, factoryCreated.rigStates.Count);
            Assert.Equal("Rotary Drill", factoryCreated.rigStates[0].Name);
        }

        [Fact]
        public void LoadCommonRigStateSetup_EmptyDatabase_AutoSeedsDefaultsAndAll28Items()
        {
            // Empty both tables
            _dataService.ExecuteNonQuery("DELETE FROM VMX_COMMON_RIGSTATE_SETUP;");
            _dataService.ExecuteNonQuery("DELETE FROM VMX_COMMON_RIGSTATE_ITEMS;");

            // Verify they are initially empty
            Assert.Equal(0, _dataService.GetTable("SELECT * FROM VMX_COMMON_RIGSTATE_SETUP;").Rows.Count);
            Assert.Equal(0, _dataService.GetTable("SELECT * FROM VMX_COMMON_RIGSTATE_ITEMS;").Rows.Count);

            // Call LoadCommonRigStateSetup
            var loaded = RigStateService.LoadCommonRigStateSetup(_dataService);

            Assert.NotNull(loaded);
            Assert.Equal("Unknown", loaded.UnknownName);
            Assert.Equal(15, loaded.UnknownNumber);
            Assert.Equal(-2818048, loaded.UnknownColor);
            Assert.Equal("#D50000", loaded.UnknownColorHex);
            Assert.Equal(90, loaded.HookloadCutOff);
            Assert.Equal(1, loaded.RPMCutOff);
            Assert.Equal(1, loaded.CIRCCutOff);
            Assert.Equal(1, loaded.Sensitivity);
            Assert.Equal(1, loaded.TorqueMin);
            Assert.Equal(12000, loaded.TorqueMax);
            Assert.Equal(20, loaded.CalibrationRows);
            Assert.Equal(500, loaded.MinTorqueDifference);
            Assert.Equal(50, loaded.MaxRPM);
            Assert.Equal(15000, loaded.TorqueMax2);
            Assert.Equal(50, loaded.CalibrationRows2);
            Assert.Equal(1, loaded.MinTorqueDifference2);
            Assert.Equal(44, loaded.MaxRPM2);

            // Verify all 28 items in loaded object
            Assert.Equal(28, loaded.rigStates.Count);
            Assert.Equal("Rotary Drill", loaded.rigStates[0].Name);
            Assert.Equal(-16711936, loaded.rigStates[0].Color);
            Assert.Equal("Slide Drill", loaded.rigStates[1].Name);
            Assert.Equal(-16732672, loaded.rigStates[1].Color);
            Assert.Equal("Stationary", loaded.rigStates[14].Name);
            Assert.Equal("No Data", loaded.rigStates[16].Name);
            Assert.Equal("Pipe Move In", loaded.rigStates[27].Name);
            Assert.Equal("Pipe Move Out", loaded.rigStates[28].Name);
            Assert.Equal(-8388353, loaded.rigStates[28].Color);

            // Verify database records were persisted into SQLite
            DataTable dtSetup = _dataService.GetTable("SELECT * FROM VMX_COMMON_RIGSTATE_SETUP;");
            Assert.Equal(1, dtSetup.Rows.Count);

            DataTable dtItems = _dataService.GetTable("SELECT * FROM VMX_COMMON_RIGSTATE_ITEMS;");
            Assert.Equal(28, dtItems.Rows.Count);
        }

        [Fact]
        public void LoadCommonRigStateSetup_SetupExistsWithoutItems_AutoSeedsDefaultItems()
        {
            // Setup exists with custom values, but items table is empty
            _dataService.ExecuteNonQuery("DELETE FROM VMX_COMMON_RIGSTATE_ITEMS;");
            _dataService.ExecuteNonQuery("DELETE FROM VMX_COMMON_RIGSTATE_SETUP;");
            _dataService.ExecuteNonQuery("INSERT INTO VMX_COMMON_RIGSTATE_SETUP (UNKNOWN_NAME, HOOKLOAD_CUTOFF) VALUES ('CustomOperator', 123.4);");

            Assert.Equal(1, _dataService.GetTable("SELECT * FROM VMX_COMMON_RIGSTATE_SETUP;").Rows.Count);
            Assert.Equal(0, _dataService.GetTable("SELECT * FROM VMX_COMMON_RIGSTATE_ITEMS;").Rows.Count);

            var loaded = RigStateService.LoadCommonRigStateSetup(_dataService);

            Assert.NotNull(loaded);
            Assert.Equal("CustomOperator", loaded.UnknownName);
            Assert.Equal(123.4, loaded.HookloadCutOff);

            // 28 items should be auto-seeded
            Assert.Equal(28, loaded.rigStates.Count);
            Assert.Equal("Rotary Drill", loaded.rigStates[0].Name);
            Assert.Equal("Pipe Move Out", loaded.rigStates[28].Name);

            // Database items should now contain 28 rows
            DataTable dtItems = _dataService.GetTable("SELECT * FROM VMX_COMMON_RIGSTATE_ITEMS;");
            Assert.Equal(28, dtItems.Rows.Count);
        }

        [Fact]
        public void RigStateViewModel_LoadsAndSavesCommonRigStateSetup()
        {
            var vm = new DrillIntel.ViewModels.RigStateViewModel(_dataService);

            // Verify loaded defaults
            Assert.Equal(28, vm.RigStateItems.Count);
            Assert.Equal("Unknown", vm.UnknownName);
            Assert.Equal(90, vm.HookloadCutOff);

            // Modify values
            vm.UnknownName = "CustomUnknownState";
            vm.HookloadCutOff = 125.5;
            vm.RPMCutOff = 5.0;
            vm.RigStateItems[0].Name = "Modified Rotary";

            // Test RequestClose on Save
            bool? closeResult = null;
            vm.RequestClose += (success) => closeResult = success;

            // Save via SaveCommand
            vm.SaveCommand.Execute(null);

            Assert.False(vm.IsStatusError);
            Assert.Contains("saved successfully", vm.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.True(closeResult.HasValue && closeResult.Value, "RequestClose should be invoked with true after save");

            // Test RequestClose on Cancel
            closeResult = null;
            vm.CancelCommand.Execute(null);
            Assert.True(closeResult.HasValue && !closeResult.Value, "RequestClose should be invoked with false on cancel");

            // Verify loaded directly from DB
            var loaded = RigStateService.LoadCommonRigStateSetup(_dataService);
            Assert.NotNull(loaded);
            Assert.Equal("CustomUnknownState", loaded.UnknownName);
            Assert.Equal(125.5, loaded.HookloadCutOff);
            Assert.Equal(5.0, loaded.RPMCutOff);
            Assert.Equal("Modified Rotary", loaded.rigStates[0].Name);
        }

        [Fact]
        public void BulkCommandExecutor_BatchesAndFlushesSuccessfully()
        {
            string tableName = "TEST_BULK_EXEC";
            _dataService.ExecuteNonQuery($"CREATE TABLE [{tableName}] (ID INTEGER, VAL TEXT);");

            var executor = new BulkCommandExecutor(_dataService, 5);

            for (int i = 1; i <= 7; i++)
            {
                executor.ExecuteCommand($"INSERT INTO [{tableName}] (ID, VAL) VALUES ({i}, 'val_{i}');");
            }

            // At 5 items, auto-flush occurred; 2 items remain in buffer
            var countMid = Convert.ToInt32(_dataService.GetValueFromDatabase($"SELECT COUNT(*) FROM [{tableName}];") ?? 0);
            Assert.Equal(5, countMid);

            // Flush remainder
            bool flushed = executor.FlushBuffer();
            Assert.True(flushed);

            var countFinal = Convert.ToInt32(_dataService.GetValueFromDatabase($"SELECT COUNT(*) FROM [{tableName}];") ?? 0);
            Assert.Equal(7, countFinal);
        }

        [Fact]
        public async Task RecalculateRigStateAsync_NullChecks_ReturnFalse()
        {
            var res1 = await RigStateService.RecalculateRigStateAsync(null!, new TimeLog());
            Assert.False(res1);

            var res2 = await RigStateService.RecalculateRigStateAsync(_dataService, null!);
            Assert.False(res2);

            var emptyLog = new TimeLog { __dataTableName = "" };
            var res3 = await RigStateService.RecalculateRigStateAsync(_dataService, emptyLog);
            Assert.False(res3);
            Assert.Equal("TimeLog data table name is missing.", RigStateService.LastError);
        }

        [Fact]
        public async Task RecalculateRigStateAsync_RecalculatesStates_AndReportsProgress()
        {
            string tableName = "timeLog_test_recalc";
            _dataService.ExecuteNonQuery($"DROP TABLE IF EXISTS [{tableName}];");
            _dataService.ExecuteNonQuery($"CREATE TABLE [{tableName}] (" +
                "DATETIME TEXT PRIMARY KEY, " +
                "RPM REAL, STOR REAL, CIRC REAL, DEPTH REAL, HDTH REAL, HKLD REAL, SPPA REAL, " +
                "RIG_STATE INTEGER, RIG_STATE_COLOR INTEGER);");

            // Populate rows
            // Row 1: Baseline
            _dataService.ExecuteNonQuery($"INSERT INTO [{tableName}] (DATETIME, RPM, STOR, CIRC, DEPTH, HDTH, HKLD, SPPA, RIG_STATE, RIG_STATE_COLOR) " +
                $"VALUES ('2026-01-01 10:00:00', 0, 0, 0, 1000.0, 1000.0, 50.0, 0, -999, 0);");

            // Row 2: In Slips (RPM=0, CIRC=0, Stall/No movement, HKLD=40 <= 90 cutoff, not on bottom)
            _dataService.ExecuteNonQuery($"INSERT INTO [{tableName}] (DATETIME, RPM, STOR, CIRC, DEPTH, HDTH, HKLD, SPPA, RIG_STATE, RIG_STATE_COLOR) " +
                $"VALUES ('2026-01-01 10:00:10', 0, 0, 0, 1000.0, 1005.0, 40.0, 0, -999, 0);");

            // Row 3: Rotary Drilling (RPM=60 > 1, CIRC=500 > 1, BitOnBottom: DEPTH=1005.0, HDTH=1005.0, Going Down)
            _dataService.ExecuteNonQuery($"INSERT INTO [{tableName}] (DATETIME, RPM, STOR, CIRC, DEPTH, HDTH, HKLD, SPPA, RIG_STATE, RIG_STATE_COLOR) " +
                $"VALUES ('2026-01-01 10:00:20', 60, 2000, 500, 1005.0, 1005.0, 150.0, 2000, -999, 0);");

            // Row 4: Rotary Drilling continued (DEPTH=1006.0, HDTH=1006.0)
            _dataService.ExecuteNonQuery($"INSERT INTO [{tableName}] (DATETIME, RPM, STOR, CIRC, DEPTH, HDTH, HKLD, SPPA, RIG_STATE, RIG_STATE_COLOR) " +
                $"VALUES ('2026-01-01 10:00:30', 60, 2000, 500, 1006.0, 1006.0, 150.0, 2000, -999, 0);");

            // Rows 5-8: Trailing rows to satisfy the lookahead buffer (rowCount - 3)
            for (int i = 5; i <= 8; i++)
            {
                _dataService.ExecuteNonQuery($"INSERT INTO [{tableName}] (DATETIME, RPM, STOR, CIRC, DEPTH, HDTH, HKLD, SPPA, RIG_STATE, RIG_STATE_COLOR) " +
                    $"VALUES ('2026-01-01 10:0{i / 6}:{(i % 6) * 10:D2}', 60, 2000, 500, {1006.0 + i}, {1006.0 + i}, 150.0, 2000, -999, 0);");
            }

            var timeLog = new TimeLog
            {
                ObjectID = "TL_RECALC_01",
                nameLog = "Recalc Test Log",
                __dataTableName = tableName
            };

            double lastReportedProgress = 0;
            var progress = new Progress<double>(p => lastReportedProgress = p);

            bool success = await RigStateService.RecalculateRigStateAsync(_dataService, timeLog, progress: progress);
            Assert.True(success, $"Recalculate failed with error: {RigStateService.LastError}");
            Assert.True(lastReportedProgress > 0, "Progress should be reported");

            // Verify recalculated states in DB
            DataTable dt = _dataService.GetTable($"SELECT DATETIME, RIG_STATE, RIG_STATE_COLOR FROM [{tableName}] ORDER BY DATETIME;");
            Assert.NotNull(dt);
            Assert.Equal(8, dt.Rows.Count);

            // Row 3 (index 2) should be Rotary Drill (0)
            int row3State = Convert.ToInt32(dt.Rows[2]["RIG_STATE"]);
            Assert.Equal(0, row3State);

            // Clean up
            _dataService.ExecuteNonQuery($"DROP TABLE IF EXISTS [{tableName}];");
        }

        [Fact]
        public async Task RecalculateRigState_EntireWell_UsesFirstAndLastIndexOptimized()
        {
            string wellId = "W_INDEX_01";
            string wellboreId = "WB_INDEX_01";
            string logId = "TL_INDEX_01";
            string tableName = "timeLog_IndexTest";

            DateTime minDate = new DateTime(2026, 3, 1, 10, 0, 0);
            DateTime maxDate = new DateTime(2026, 3, 1, 10, 5, 0);

            // Populate VMX_TIME_LOG with MIN_DATE and MAX_DATE
            string insertTimeLogSql = $"INSERT INTO VMX_TIME_LOG (WELL_ID, WELLBORE_ID, LOG_ID, DATA_TABLE_NAME, MIN_DATE, MAX_DATE) " +
                                      $"VALUES ('{wellId}', '{wellboreId}', '{logId}', '{tableName}', '{minDate:yyyy-MM-dd HH:mm:ss}', '{maxDate:yyyy-MM-dd HH:mm:ss}');";
            _dataService.ExecuteNonQuery(insertTimeLogSql);

            // Create and populate data table
            _dataService.ExecuteNonQuery($"CREATE TABLE IF NOT EXISTS [{tableName}] (" +
                "DATETIME TEXT NOT NULL, " +
                "RPM REAL, " +
                "TORQUE REAL, " +
                "PUMP_PRESS REAL, " +
                "DEPTH REAL, " +
                "HOLE_DEPTH REAL, " +
                "HOOK_LOAD REAL, " +
                "WOB REAL, " +
                "RIG_STATE INTEGER DEFAULT -999, " +
                "RIG_STATE_COLOR INTEGER DEFAULT 0);");

            _dataService.ExecuteNonQuery($"INSERT INTO [{tableName}] (DATETIME, RPM, TORQUE, PUMP_PRESS, DEPTH, HOLE_DEPTH, HOOK_LOAD, WOB) " +
                $"VALUES ('{minDate:yyyy-MM-dd HH:mm:ss}', 60, 2000, 500, 1000.0, 1000.0, 150.0, 2000);");
            _dataService.ExecuteNonQuery($"INSERT INTO [{tableName}] (DATETIME, RPM, TORQUE, PUMP_PRESS, DEPTH, HOLE_DEPTH, HOOK_LOAD, WOB) " +
                $"VALUES ('{maxDate:yyyy-MM-dd HH:mm:ss}', 60, 2000, 500, 1001.0, 1001.0, 150.0, 2000);");

            var timeLog = new TimeLog
            {
                WellID = wellId,
                WellboreID = wellboreId,
                ObjectID = logId,
                __dataTableName = tableName
            };

            // Call RecalculateRigStateAsync with startDate = null and endDate = null (Entire Well)
            bool success = await RigStateService.RecalculateRigStateAsync(_dataService, timeLog, startDate: null, endDate: null);
            Assert.True(success, $"Recalculate failed: {RigStateService.LastError}");

            DataTable dt = _dataService.GetTable($"SELECT DATETIME, RIG_STATE FROM [{tableName}] ORDER BY DATETIME;");
            Assert.NotNull(dt);
            Assert.Equal(2, dt.Rows.Count);

            // Clean up
            _dataService.ExecuteNonQuery($"DROP TABLE IF EXISTS [{tableName}];");
        }

        [Fact]
        public void RecalculateRigStateViewModel_DateTimeHandling_CombinesDateAndTimeCorrectly()
        {
            using var session = new ProjectSession();
            session.Load(_tempDbPath);

            var timeLog = new TimeLog
            {
                WellID = "W_VM_01",
                WellboreID = "WB_VM_01",
                ObjectID = "TL_VM_01",
                nameLog = "ViewModel Test Log"
            };

            var vm = new RecalculateRigStateViewModel(session, timeLog);

            // Test default scope is Entire Well
            Assert.True(vm.IsEntireLog);
            Assert.False(vm.IsCustomRange);
            Assert.False(vm.CanSelectDates);

            // Toggle to Custom Range
            vm.IsCustomRange = true;
            Assert.False(vm.IsEntireLog);
            Assert.True(vm.CanSelectDates);

            // Set specific Date and Time
            DateTime testFromDate = new DateTime(2025, 6, 15);
            DateTime testFromTime = new DateTime(2000, 1, 1, 14, 30, 45);

            DateTime testToDate = new DateTime(2025, 6, 20);
            DateTime testToTime = new DateTime(2000, 1, 1, 18, 45, 10);

            vm.FromDate = testFromDate;
            vm.FromTime = testFromTime;

            vm.ToDate = testToDate;
            vm.ToTime = testToTime;

            // Verify FromDateTime and ToDateTime properly combine date & time
            Assert.Equal(new DateTime(2025, 6, 15, 14, 30, 45), vm.FromDateTime);
            Assert.Equal(new DateTime(2025, 6, 20, 18, 45, 10), vm.ToDateTime);
        }

        [Fact]
        public void HexToBrushConverter_ConvertsHexStringsCorrectly()
        {
            var converter = new DrillIntel.Converters.HexToBrushConverter();

            // Valid hex with hash
            var brush1 = converter.Convert("#D50000", typeof(System.Windows.Media.Brush), null!, System.Globalization.CultureInfo.InvariantCulture) as System.Windows.Media.SolidColorBrush;
            Assert.NotNull(brush1);
            Assert.Equal(System.Windows.Media.Color.FromRgb(0xD5, 0x00, 0x00), brush1.Color);

            // Valid hex without hash
            var brush2 = converter.Convert("00FF00", typeof(System.Windows.Media.Brush), null!, System.Globalization.CultureInfo.InvariantCulture) as System.Windows.Media.SolidColorBrush;
            Assert.NotNull(brush2);
            Assert.Equal(System.Windows.Media.Color.FromRgb(0x00, 0xFF, 0x00), brush2.Color);

            // 8-digit ARGB hex
            var brush3 = converter.Convert("#80123456", typeof(System.Windows.Media.Brush), null!, System.Globalization.CultureInfo.InvariantCulture) as System.Windows.Media.SolidColorBrush;
            Assert.NotNull(brush3);
            Assert.Equal(System.Windows.Media.Color.FromArgb(0x80, 0x12, 0x34, 0x56), brush3.Color);

            // Invalid or empty hex returns Transparent
            var brushNull = converter.Convert(null, typeof(System.Windows.Media.Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(System.Windows.Media.Brushes.Transparent, brushNull);

            var brushEmpty = converter.Convert("   ", typeof(System.Windows.Media.Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(System.Windows.Media.Brushes.Transparent, brushEmpty);

            var brushInvalid = converter.Convert("invalid_hex", typeof(System.Windows.Media.Brush), null!, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal(System.Windows.Media.Brushes.Transparent, brushInvalid);
        }

        [Fact]
        public void RigStateViewModel_NullDataService_LoadsDefault28StatesAndHandlesCommands()
        {
            var vm = new DrillIntel.ViewModels.RigStateViewModel(null);

            // Should load 28 default rig states in memory
            Assert.Equal(28, vm.RigStateItems.Count);
            Assert.Equal("Unknown", vm.UnknownName);
            Assert.Equal("#D50000", vm.UnknownColorHex);
            Assert.True(vm.IsDefaultSetSelected);

            // Test SetUnknownColorHexCommand
            vm.SetUnknownColorHexCommand.Execute("#000000");
            Assert.Equal("#000000", vm.UnknownColorHex);

            // Test SetColorHexCommand on an individual item
            var firstItem = vm.RigStateItems[0];
            firstItem.SetColorHexCommand.Execute("#00C853");
            Assert.Equal("#00C853", firstItem.ColorHex);

            // Test SelectedSet radio button switching
            vm.IsSet2Selected = true;
            Assert.Equal(2, vm.SelectedSet);
            Assert.False(vm.IsDefaultSetSelected);
            Assert.True(vm.IsSet2Selected);
            Assert.False(vm.IsSet3Selected);

            vm.IsSet3Selected = true;
            Assert.Equal(3, vm.SelectedSet);
            Assert.False(vm.IsDefaultSetSelected);
            Assert.False(vm.IsSet2Selected);
            Assert.True(vm.IsSet3Selected);

            vm.IsDefaultSetSelected = true;
            Assert.True(vm.IsDefaultSetSelected);
            Assert.False(vm.IsSet2Selected);
            Assert.False(vm.IsSet3Selected);

            // Test Add and Remove AutoSlide rows
            int initialRowCount = vm.AutoSlideRows.Count;
            vm.AddAutoSlideRowCommand.Execute(null);
            Assert.Equal(initialRowCount + 1, vm.AutoSlideRows.Count);

            vm.SelectedAutoSlideRow = vm.AutoSlideRows[^1];
            vm.RemoveAutoSlideRowCommand.Execute(null);
            Assert.Equal(initialRowCount, vm.AutoSlideRows.Count);
        }

        [Fact]
        public void RigStateView_InitializesWithoutExceptionOnSTA()
        {
            Exception? ex = null;
            var thread = new System.Threading.Thread(() =>
            {
                try
                {
                    if (System.Windows.Application.Current == null)
                    {
                        new System.Windows.Application();
                    }
                    var view = new DrillIntel.Views.RigStateView();
                    Assert.NotNull(view);
                    if (view.DataContext == null)
                    {
                        view.DataContext = new DrillIntel.ViewModels.RigStateViewModel();
                    }
                    var vm = view.DataContext as DrillIntel.ViewModels.RigStateViewModel;
                    Assert.NotNull(vm);
                    Assert.Equal(28, vm.RigStateItems.Count);
                }
                catch (Exception e)
                {
                    ex = e;
                }
            });
            thread.SetApartmentState(System.Threading.ApartmentState.STA);
            thread.Start();
            thread.Join();

            Assert.Null(ex);
        }
    }
}
