using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DrillIntel.Tests
{
    public class PUSOPointTests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly DataServiceDIntel _dataService;

        public PUSOPointTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"puso_point_test_{Guid.NewGuid():N}.sqlite");
            _dataService = new DataServiceDIntel(_tempDbPath);
            _dataService.UserName = "TestAnalyst";
        }

        public void Dispose()
        {
            try { _dataService.CloseConnection(); } catch { }
            try { _dataService.Dispose(); } catch { }
            SqliteConnection.ClearAllPools();
            try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
        }

        [Fact]
        public void DefaultValues_AreCorrectlyInitialized()
        {
            var pt = new PUSOPoint();
            Assert.Equal(0.0, pt.Depth);
            Assert.Equal(0, pt.StartIndex);
            Assert.Equal(0, pt.EndIndex);
            Assert.True(pt.UserVisible);
            Assert.Equal(PUSOPoint.pusoPumpStatus.PumpOn, pt.PumpStatus);
            Assert.Equal(0.0, pt.BlockMovement);
        }

        [Fact]
        public void CopyAndClone_ProduceExactIndependentCopies()
        {
            var original = new PUSOPoint(2500.5)
            {
                StartIndex = 100,
                EndIndex = 80,
                Max = 120.5,
                Min = 85.0,
                Avg = 102.75,
                DynamicValue = 118.0,
                PumpPressure = 2500.0,
                Circulation = 450.0,
                RigStates = "10",
                UserVisible = true,
                PumpStatus = PUSOPoint.pusoPumpStatus.PumpOn
            };

            var copy = original.getCopy();
            Assert.Equal(original.Depth, copy.Depth);
            Assert.Equal(original.Max, copy.Max);
            Assert.Equal(original.Avg, copy.Avg);
            Assert.Equal(original.DynamicValue, copy.DynamicValue);

            copy.Max = 999.0;
            Assert.Equal(120.5, original.Max);
        }

        [Fact]
        public void Validation_PickUpAndSlackOff_CheckBlockMovementRange()
        {
            var setup = new BroomStickSetup
            {
                pickupMinBlockMovement = 5.0,
                pickupMaxBlockMovement = 70.0,
                slackOffMinBlockMovement = 10.0,
                slackOffMaxBlockMovement = 60.0
            };

            var validPu = new PUSOPoint { StartDepth = 100.0, EndDepth = 150.0 }; // Movement = 50
            string log = "";
            Assert.True(PUSOPoint.isValidPickUp(validPu, setup, ref log));

            var invalidPu = new PUSOPoint { StartDepth = 100.0, EndDepth = 102.0 }; // Movement = 2 (< 5)
            log = "";
            Assert.False(PUSOPoint.isValidPickUp(invalidPu, setup, ref log));

            var validSo = new PUSOPoint { StartDepth = 200.0, EndDepth = 230.0 }; // Movement = 30
            log = "";
            Assert.True(PUSOPoint.isValidSlackOff(validSo, setup, ref log));

            var invalidSo = new PUSOPoint { StartDepth = 200.0, EndDepth = 280.0 }; // Movement = 80 (> 60)
            log = "";
            Assert.False(PUSOPoint.isValidSlackOff(invalidSo, setup, ref log));
        }

        [Fact]
        public void Validation_RigStates_AcceptsAllowedStatesOnly()
        {
            var dt = new DataTable();
            dt.Columns.Add("RIG_STATE", typeof(double));
            dt.Rows.Add(8.0);
            dt.Rows.Add(10.0);
            dt.Rows.Add(6.0);
            dt.Rows.Add(4.0);

            var pt = new PUSOPoint { StartIndex = 3, EndIndex = 0 };
            Assert.True(PUSOPoint.isValidPU(dt, pt));
            Assert.True(PUSOPoint.isValidSO(dt, pt));

            // Test VirtualTable wrapper
            var vt = new VirtualTable.VirtualTable(dt);
            Assert.True(PUSOPoint.isValidPU(vt, pt));

            // Introduce invalid rig state
            dt.Rows.Add(1.0); // Rotary Drilling (not tripping PU/SO)
            var ptInvalid = new PUSOPoint { StartIndex = 4, EndIndex = 0 };
            Assert.False(PUSOPoint.isValidPU(dt, ptInvalid));
            Assert.False(PUSOPoint.isValidPU(vt, ptInvalid));
        }

        [Fact]
        public void Calculation_StaticPUValues_FindsMaxMinAndAverage()
        {
            var dt = new DataTable();
            dt.Columns.Add("RIG_STATE", typeof(double));
            dt.Columns.Add("HKLD", typeof(double));
            dt.Columns.Add("SPPA", typeof(double));

            dt.Rows.Add(10.0, 100.0, 50.0); // index 0
            dt.Rows.Add(10.0, 150.0, 50.0); // index 1
            dt.Rows.Add(10.0, 80.0, 50.0);  // index 2
            dt.Rows.Add(10.0, 120.0, 50.0); // index 3

            var setup = new BroomStickSetup { pickupPumpMnemonic = "SPPA" };
            var pt = new PUSOPoint { StartIndex = 3, EndIndex = 0 };

            PUSOPoint.calculateStaticPUValues(dt, ref pt, setup);

            Assert.Equal(150.0, pt.Max);
            Assert.Equal(80.0, pt.Min);
            Assert.Equal(112.5, pt.Avg); // (100 + 150 + 80 + 120) / 4 = 112.5
            Assert.Equal(1, pt.MaxIndex);
        }

        [Fact]
        public void Calculation_BreakInIndex_And_StaticSOValues()
        {
            var dt = new DataTable();
            dt.Columns.Add("RIG_STATE", typeof(double));
            dt.Columns.Add("HKLD", typeof(double));
            dt.Columns.Add("SPPA", typeof(double));

            // Rows from EndIndex to StartIndex
            dt.Rows.Add(4.0, 70.0, 0.0); // row 0: EndIndex
            dt.Rows.Add(4.0, 80.0, 0.0); // row 1
            dt.Rows.Add(4.0, 75.0, 0.0); // row 2: break in (row 3 prev is 90 > 75)
            dt.Rows.Add(4.0, 90.0, 0.0); // row 3: StartIndex

            var pt = new PUSOPoint { StartIndex = 3, EndIndex = 0 };
            int breakIn = PUSOPoint.getBreakInIndex(dt, ref pt);
            Assert.True(breakIn >= 0);

            var setup = new BroomStickSetup { slackOffPumpMnemonic = "SPPA" };
            PUSOPoint.calculateStaticSOValues(dt, ref pt, setup);

            Assert.True(pt.Max > 0);
            Assert.True(pt.Min > 0);
        }

        [Fact]
        public void Calculation_DynamicPUValues_CalculatesBreakOverValue()
        {
            var dt = new DataTable();
            dt.Columns.Add("RIG_STATE", typeof(double));
            dt.Columns.Add("HKLD", typeof(double));
            dt.Columns.Add("SPPA", typeof(double));
            dt.Columns.Add("BPOS", typeof(double));
            dt.Columns.Add("RPM", typeof(double));

            dt.Rows.Add(10.0, 110.0, 200.0, 15.0, 0.0); // 0
            dt.Rows.Add(10.0, 130.0, 210.0, 20.0, 0.0); // 1
            dt.Rows.Add(10.0, 150.0, 220.0, 25.0, 0.0); // 2: Max
            dt.Rows.Add(10.0, 140.0, 230.0, 30.0, 0.0); // 3

            var setup = new BroomStickSetup
            {
                pickupPumpMnemonic = "SPPA",
                pickupDynamicMethod = BroomStickSetup.bmDynamicMethod.BreakOver
            };

            var pt = new PUSOPoint
            {
                StartIndex = 3,
                EndIndex = 0,
                Max = 150.0,
                MaxIndex = 2
            };

            PUSOPoint.calculateDynamicPUValues(dt, ref pt, setup);
            Assert.Equal(150.0, pt.DynamicValue);
            Assert.Equal(210.0, pt.PumpPressure);
        }

        [Fact]
        public void PumpStatus_Updates_BasedOnCutOffThreshold()
        {
            var setup = new BroomStickSetup
            {
                pickupPumpMnemonic = "SPPA",
                pickupCutOffValue = 100.0,
                slackOffPumpMnemonic = "SPPA",
                slackOffCutOffValue = 50.0
            };

            var ptOn = new PUSOPoint { PumpPressure = 150.0 };
            PUSOPoint.updatePumpStatusPU(ref ptOn, setup);
            Assert.Equal(PUSOPoint.pusoPumpStatus.PumpOn, ptOn.PumpStatus);

            var ptOff = new PUSOPoint { PumpPressure = 50.0 };
            PUSOPoint.updatePumpStatusPU(ref ptOff, setup);
            Assert.Equal(PUSOPoint.pusoPumpStatus.PumpOff, ptOff.PumpStatus);
        }

        [Fact]
        public void CompareTo_SortsPointsByDepthAscending()
        {
            var p1 = new PUSOPoint(3000.0);
            var p2 = new PUSOPoint(1000.0);
            var p3 = new PUSOPoint(2000.0);

            var list = new List<PUSOPoint> { p1, p2, p3 };
            list.Sort();

            Assert.Equal(1000.0, list[0].Depth);
            Assert.Equal(2000.0, list[1].Depth);
            Assert.Equal(3000.0, list[2].Depth);
        }

        [Fact]
        public void SqlitePersistence_SaveAndLoadPoints_RoundTrip()
        {
            var points = new List<PUSOPoint>
            {
                new PUSOPoint(1050.0)
                {
                    Type = PUSOPoint.PointTypePickUp,
                    Min = 80.0,
                    Max = 140.0,
                    Avg = 110.0,
                    DynamicValue = 135.0,
                    PumpPressure = 200.0,
                    PumpStatus = PUSOPoint.pusoPumpStatus.PumpOn
                },
                new PUSOPoint(1100.0)
                {
                    Type = PUSOPoint.PointTypePickUp,
                    Min = 85.0,
                    Max = 145.0,
                    Avg = 115.0,
                    DynamicValue = 140.0,
                    PumpPressure = 210.0,
                    PumpStatus = PUSOPoint.pusoPumpStatus.PumpOn
                }
            };

            bool saved = PUSOPoint.SavePoints(_dataService, "WELL_SQLITE_TEST", PUSOPoint.PointTypePickUp, points, "TestUser");
            Assert.True(saved);

            var loaded = PUSOPoint.LoadPoints(_dataService, "WELL_SQLITE_TEST", PUSOPoint.PointTypePickUp);
            Assert.Equal(2, loaded.Count);
            Assert.Equal(1050.0, loaded[0].Depth);
            Assert.Equal(140.0, loaded[0].Max);
            Assert.Equal(135.0, loaded[0].DynamicValue);
            Assert.Equal(1100.0, loaded[1].Depth);
            Assert.Equal(145.0, loaded[1].Max);

            // Test ClearPoints
            bool cleared = PUSOPoint.ClearPoints(_dataService, "WELL_SQLITE_TEST");
            Assert.True(cleared);

            var remaining = PUSOPoint.LoadPoints(_dataService, "WELL_SQLITE_TEST");
            Assert.Empty(remaining);
        }

        [Fact]
        public void SqlitePersistence_SaveAndLoadPoints_WithRpmAndRigStates_RoundTrip()
        {
            var points = new List<PUSOPoint>
            {
                new PUSOPoint(2450.0)
                {
                    Type = PUSOPoint.PointTypeRotate,
                    Min = 100.0,
                    Max = 180.0,
                    Avg = 145.0,
                    DynamicValue = 160.0,
                    RPM = 60.5,
                    RigStates = "10,12",
                    PumpPressure = 2200.0,
                    Circulation = 500.0,
                    PumpStatus = PUSOPoint.pusoPumpStatus.PumpOn
                }
            };

            bool saved = PUSOPoint.SavePoints(_dataService, "WELL_RPM_TEST", PUSOPoint.PointTypeRotate, points, "TestRPMUser");
            Assert.True(saved);

            var loaded = PUSOPoint.LoadPoints(_dataService, "WELL_RPM_TEST", PUSOPoint.PointTypeRotate);
            Assert.Single(loaded);
            Assert.Equal(2450.0, loaded[0].Depth);
            Assert.Equal(60.5, loaded[0].RPM);
            Assert.Equal("10,12", loaded[0].RigStates);
            Assert.Equal(180.0, loaded[0].Max);
        }

        [Fact]
        public void SqlitePersistence_PreExistingTableWithoutRpm_AutoMigratesAndSucceeds()
        {
            // Simulate legacy database where VMX_BROOMSTICK_POINTS was created without RPM and RIG_STATES
            string legacyDbPath = Path.Combine(Path.GetTempPath(), $"puso_legacy_{Guid.NewGuid():N}.sqlite");
            using (var ds = new DataServiceDIntel(legacyDbPath))
            {
                ds.ExecuteNonQuery(@"
                    CREATE TABLE VMX_BROOMSTICK_POINTS (
                        ID INTEGER PRIMARY KEY AUTOINCREMENT,
                        WELL_ID TEXT NOT NULL,
                        DEPTH REAL,
                        TYPE INTEGER,
                        START_DATE TEXT,
                        END_DATE TEXT,
                        START_INDEX REAL,
                        END_INDEX REAL,
                        MIN REAL,
                        MAX REAL,
                        AVG REAL,
                        AVG2 REAL,
                        START_DEPTH REAL,
                        END_DEPTH REAL,
                        BLOCK_MOVEMENT REAL,
                        MAX_INDEX REAL,
                        DYNAMIC_VALUE REAL,
                        DYNAMIC_TORQUE_VALUE REAL,
                        CIRCULATION REAL,
                        PUMP_PRESSURE REAL,
                        USER_DYNAMIC_VALUE REAL,
                        USER_STATIC_VALUE REAL,
                        USER_VISIBLE INTEGER DEFAULT 1,
                        PUMP_STATUS INTEGER DEFAULT 0,
                        CREATED_BY TEXT,
                        CREATED_DATE TEXT,
                        MODIFIED_BY TEXT,
                        MODIFIED_DATE TEXT
                    );");

                var pt = new PUSOPoint(3200.0)
                {
                    Type = PUSOPoint.PointTypePickUp,
                    RPM = 75.0,
                    RigStates = "1",
                    Max = 200.0
                };

                // SavePoint must auto-migrate the table by adding RPM and RIG_STATES columns without throwing
                bool saved = PUSOPoint.SavePoint(ds, "LEGACY_WELL", PUSOPoint.PointTypePickUp, pt);
                Assert.True(saved);

                var loaded = PUSOPoint.LoadPoints(ds, "LEGACY_WELL", PUSOPoint.PointTypePickUp);
                Assert.Single(loaded);
                Assert.Equal(3200.0, loaded[0].Depth);
                Assert.Equal(75.0, loaded[0].RPM);
                Assert.Equal("1", loaded[0].RigStates);
            }

            SqliteConnection.ClearAllPools();
            try { if (File.Exists(legacyDbPath)) File.Delete(legacyDbPath); } catch { }
        }
    }
}

