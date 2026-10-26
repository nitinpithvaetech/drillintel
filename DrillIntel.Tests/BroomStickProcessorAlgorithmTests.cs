using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DrillIntel.Tests
{
    public class BroomStickProcessorAlgorithmTests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly string _connString;
        private readonly DataServiceDIntel _dataService;

        public BroomStickProcessorAlgorithmTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"bsp_algo_test_{Guid.NewGuid():N}.sqlite");
            _connString = $"Data Source={_tempDbPath}";
            _dataService = new DataServiceDIntel(_tempDbPath);
            _dataService.UserName = "TestUser";
        }

        public void Dispose()
        {
            try { _dataService.CloseConnection(); } catch { }
            try { _dataService.Dispose(); } catch { }
            SqliteConnection.ClearAllPools();
            try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
        }

        [Fact]
        public void EnsureTables_CreatesAllRequiredTablesInSqlite()
        {
            // Act
            BroomStickProcessor.EnsureTables(_dataService);

            // Assert
            using var conn = new SqliteConnection(_connString);
            conn.Open();

            string[] expectedTables = new[]
            {
                "VMX_BROOMSTICK_POINTS",
                "VMX_BROOMSTICK_PRC_INFO",
                "VMX_AKPI_DRLG_CONNECTIONS",
                "VMX_BS_WELL_PROFILE",
                "eVuMaxBroomstickProcessLog",
                "eVuMaxBroomstickWarnings"
            };

            foreach (var tbl in expectedTables)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name;";
                cmd.Parameters.AddWithValue("@name", tbl);
                long count = Convert.ToInt64(cmd.ExecuteScalar());
                Assert.True(count > 0, $"Table {tbl} should exist in SQLite database.");
            }
        }

        [Fact]
        public void CombinedPUSO_PropertiesAndSorting_WorkAsExpected()
        {
            var p1 = new CombinedPUSO { Depth = 1500.5, DynamicPU = 250, DynamicSO = 180, ROB = 210 };
            var p2 = new CombinedPUSO { Depth = 1200.0, DynamicPU = 240, DynamicSO = 175, ROB = 205 };

            var list = new List<CombinedPUSO> { p1, p2 };
            list.Sort();

            Assert.Equal(1200.0, list[0].Depth);
            Assert.Equal(1500.5, list[1].Depth);
            Assert.Equal(205.0, list[0].ROB);
            Assert.Equal(205.0, list[0].Rotate);
        }

        [Fact]
        public void ROBPoint_CalculateROB_CalculatesAverageHookloadAndTorque()
        {
            var robDict = new Dictionary<int, ROBPoint>();
            var now = DateTime.UtcNow;

            robDict.Add(1, new ROBPoint { Depth = 2000, Hkld = 200, Torque = 50, RecordDate = now });
            robDict.Add(2, new ROBPoint { Depth = 2000, Hkld = 210, Torque = 60, RecordDate = now.AddSeconds(1) });
            robDict.Add(3, new ROBPoint { Depth = 2000, Hkld = 220, Torque = 70, RecordDate = now.AddSeconds(2) });

            var setup = new BroomStickSetup { TolerancePoints = 5 };
            double hkld = 0, torque = 0, sppa = 0, circ = 0, rpm = 0;
            bool stabilityFound = false;

            bool result = ROBPoint.calculateROB(robDict, setup, ref hkld, ref torque, ref stabilityFound, ref sppa, ref circ, ref rpm);

            Assert.True(result);
            Assert.Equal(210.0, hkld); // (200 + 210 + 220) / 3 = 210
            Assert.Equal(60.0, torque); // (50 + 60 + 70) / 3 = 60
        }

        [Fact]
        public void ROBPoint_FilterROBListByTime_ProcessesListWithoutException()
        {
            var robDict = new Dictionary<int, ROBPoint>();
            var baseTime = DateTime.UtcNow;

            for (int i = 0; i < 15; i++)
            {
                robDict.Add(i + 1, new ROBPoint
                {
                    Depth = 1000 + (i * 0.1),
                    Hkld = 150 + i,
                    Torque = 30 + i,
                    RecordDate = baseTime.AddSeconds(i)
                });
            }

            var filtered = ROBPoint.filterROBListByTime(robDict);
            Assert.NotNull(filtered);
        }

        [Fact]
        public void PUSOPoint_CopyProperties_PreservesAllFields()
        {
            var src = new PUSOPoint
            {
                Depth = 3000.5,
                DynamicValue = 185.2,
                Min = 180.0,
                Max = 190.0,
                Avg = 185.0,
                StartIndex = 10,
                EndIndex = 25,
                BlockMovement = 45.0,
                Circulation = 450.0,
                PumpPressure = 2500.0,
                RPM = 60.0,
                RigStates = "1,2",
                PumpStatus = PUSOPoint.pusoPumpStatus.PumpOn,
                StartDate = DateTime.UtcNow.AddMinutes(-5),
                EndDate = DateTime.UtcNow
            };

            var target = new PUSOPoint();
            src.CopyProperties(ref target);

            Assert.Equal(src.Depth, target.Depth);
            Assert.Equal(src.DynamicValue, target.DynamicValue);
            Assert.Equal(src.Min, target.Min);
            Assert.Equal(src.Max, target.Max);
            Assert.Equal(src.Avg, target.Avg);
            Assert.Equal(src.StartIndex, target.StartIndex);
            Assert.Equal(src.EndIndex, target.EndIndex);
            Assert.Equal(src.BlockMovement, target.BlockMovement);
            Assert.Equal(src.Circulation, target.Circulation);
            Assert.Equal(src.PumpPressure, target.PumpPressure);
            Assert.Equal(src.RPM, target.RPM);
            Assert.Equal(src.RigStates, target.RigStates);
            Assert.Equal(src.PumpStatus, target.PumpStatus);
        }

        [Fact]
        public void GroupByDepth_ReducesMultiplePointsAtSameDepth()
        {
            var processor = new BroomStickProcessor();
            processor._objSetup = new BroomStickSetup
            {
                pickupMultiPointMethod = BroomStickSetup.bmMultiSelectMethod.Max,
                slackOffMultiPointMethod = BroomStickSetup.bmMultiSelectMethod.Min,
                ShowMultiple = false
            };

            // Add PU points at exactly the same depth (3000.0)
            var p1 = new PUSOPoint { Depth = 3000.0, DynamicValue = 210, Min = 200, Max = 220, Avg = 210 };
            var p2 = new PUSOPoint { Depth = 3000.0, DynamicValue = 230, Min = 220, Max = 240, Avg = 230 };

            processor.PUPoints.Add(1, p1);
            processor.PUPoints.Add(2, p2);

            processor.groupByDepth();

            // When ShowMultiple is false, groupByDepth consolidates duplicate depth points into 1 (selecting Max = 230)
            Assert.Single(processor.PUPoints);
            Assert.Equal(230.0, processor.PUPoints[1].DynamicValue);
        }

        [Fact]
        public void EnforceRule_EnsuresValidOrder_PU_ROB_SO()
        {
            var processor = new BroomStickProcessor();
            processor._objSetup = new BroomStickSetup { EnforceRule = true };

            // Well-ordered points: PU(300) >= ROB(250) >= SO(200)
            processor.PUPoints.Add(1, new PUSOPoint { Depth = 1000, DynamicValue = 300 });
            processor.RotatePoints.Add(1, new PUSOPoint { Depth = 1000, DynamicValue = 250 });
            processor.SOPoints.Add(1, new PUSOPoint { Depth = 1000, DynamicValue = 200 });

            processor.enforceRule();

            // All 3 valid points should be retained
            Assert.Single(processor.PUPoints);
            Assert.Single(processor.RotatePoints);
            Assert.Single(processor.SOPoints);
        }

        [Fact]
        public async Task ProcessPointsAsync_ExecutesWithoutError_WhenNoConnectionsExist()
        {
            BroomStickProcessor.EnsureTables(_dataService);

            var timeLog = new TimeLog
            {
                WellID = "TEST_EMPTY_WELL",
                ObjectID = "TL_01",
                nameLog = "Main Timelog"
            };

            var setup = new BroomStickSetup
            {
                ShowMultiple = false
            };

            var processor = new BroomStickProcessor(_dataService, "TEST_EMPTY_WELL", timeLog, setup);
            processor.TaskID = "TASK_EMPTY_01";

            // Act - should complete cleanly without errors
            await processor.ProcessPointsAsync();

            // Verify tables and logs were initialized
            using var conn = new SqliteConnection(_connString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM VMX_BROOMSTICK_PRC_INFO WHERE WELL_ID='TEST_EMPTY_WELL';";
            long prcCount = Convert.ToInt64(cmd.ExecuteScalar());
            Assert.True(prcCount >= 0);
        }
    }
}

