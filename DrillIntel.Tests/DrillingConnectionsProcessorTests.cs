using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Models.Common;
using DrillIntel.Data.Objects.DataObjects.Models.DrillingConnectionsProcessor;
using DrillIntel.Projects;
using DrillIntel.Services;
using DrillIntel.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;
using SystemSettings = DrillIntel.Data.Objects.DataObjects.Models.Common.SystemSettings;

namespace DrillIntel.Tests
{
    public class DrillingConnectionsProcessorTests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly DataServiceDIntel _dataService;

        public DrillingConnectionsProcessorTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"drlg_conn_test_{Guid.NewGuid():N}.sqlite");
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
        public void EnsureTables_CreatesAllRequiredTablesInSqlite()
        {
            // Act
            DrillingConnectionsProcessor.EnsureTables(_dataService);

            // Assert
            Assert.True(_dataService.IsRecordExist("SELECT 1 FROM sqlite_master WHERE type='table' AND name='VMX_AKPI_DRLG_CONNECTIONS';"));
            Assert.True(_dataService.IsRecordExist("SELECT 1 FROM sqlite_master WHERE type='table' AND name='VMX_CON_ANNOTATIONS';"));
        }

        [Fact]
        public void DetermineDayOrNight_IdentifiesDayAndNightCorrectly()
        {
            var processor = new DrillingConnectionsProcessor
            {
                DayTimeHoursFrom = "06:00:00"
            };

            // 10:00 AM local -> DayTime
            var localDay = new DateTime(2026, 10, 26, 10, 0, 0, DateTimeKind.Local);
            var dayStatus = processor.determineDayOrNight(localDay.ToUniversalTime());
            Assert.Equal(ConnectionLogPoint.cnDayNightTime.DayTime, dayStatus);

            // 11:00 PM local -> NightTime
            var localNight = new DateTime(2026, 10, 26, 23, 0, 0, DateTimeKind.Local);
            var nightStatus = processor.determineDayOrNight(localNight.ToUniversalTime());
            Assert.Equal(ConnectionLogPoint.cnDayNightTime.NightTime, nightStatus);
        }

        [Fact]
        public void ConnectionLabel_SaveAndRetrieveComment_WorksAsExpected()
        {
            DrillingConnectionsProcessor.EnsureTables(_dataService);

            string wellId = "WELL_TEST_001";
            double depth = 4520.8;
            string comment = "Bit change connection";

            var label = new DrlgConnectionLabel
            {
                Depth = depth,
                Comment = comment
            };

            string lastError = "";
            DrlgConnectionLabel.saveConnectionLabel(_dataService, label, wellId, ref lastError);
            Assert.Empty(lastError);

            string retrieved = DrlgConnectionLabel.getComment(_dataService, depth, wellId);
            Assert.Equal(comment, retrieved);
        }

        [Fact]
        public void KPIDrillingConnection_AddAndClear_PersistsAndDeletesSuccessfully()
        {
            DrillingConnectionsProcessor.EnsureTables(_dataService);

            string wellId = "WELL_TEST_002";
            var fromDate = new DateTime(2026, 10, 26, 8, 0, 0);
            var toDate = new DateTime(2026, 10, 26, 8, 5, 0);

            var conn = new KPIDrillingConnection
            {
                WellID = wellId,
                Depth = 6125.5,
                FromDate = fromDate,
                ToDate = toDate,
                BottomToSlips = 12,
                SlipsToSlips = 180,
                SlipsToBottom = 15,
                Time = "D",
                UserComment = "Routine connection"
            };

            string err = "";
            bool added = KPIDrillingConnection.addToDatabase(_dataService, conn, ref err);
            Assert.True(added, $"Failed to add connection: {err}");

            // Verify exists
            var checkPoint = new ConnectionLogPoint
            {
                Depth = conn.Depth,
                FromDate = conn.FromDate,
                ToDate = conn.ToDate
            };
            Assert.True(KPIDrillingConnection.isDBEntryExist(_dataService, wellId, checkPoint));

            // Verify Clear
            string clearErr = "";
            bool cleared = KPIDrillingConnection.clearConnectionsFromDB(_dataService, wellId, ref clearErr);
            Assert.True(cleared);
            Assert.False(KPIDrillingConnection.isDBEntryExist(_dataService, wellId, checkPoint));
        }

        [Fact]
        public void DrillingConnectionsProcessor_ProcessPoints_DetectsAndSavesValidConnection()
        {
            // Setup tables
            DrillingConnectionsProcessor.EnsureTables(_dataService);
            SystemSettings.EnsureTableExists(_dataService);

            string wellId = "WELL_CALC_001";
            string logTable = "TL_TEST_DATA";

            // Create timelog data table
            _dataService.ExecuteNonQuery($@"
CREATE TABLE [{logTable}] (
    DATETIME TEXT NOT NULL,
    DEPTH REAL,
    HDTH REAL,
    HKLD REAL,
    RIG_STATE REAL,
    TIME_DURATION REAL
);");

            // Populate synthetic data
            // Time 0..120: Continuous drilling (RIG_STATE = 0, HKLD = 150)
            // Time 121..130: Bottom to slips (RIG_STATE = 11, HKLD = 100) -> 10s
            // Time 131..200: In Slips (RIG_STATE = 2, HKLD = 20) -> 70s
            // Time 201..215: Slips to bottom (RIG_STATE = 12, HKLD = 120) -> 15s
            // Time 216..336: Continuous drilling (RIG_STATE = 0, HKLD = 150) -> 120s
            var baseTime = new DateTime(2026, 10, 26, 9, 0, 0);

            // Phase 1: Drilling 120 seconds
            for (int s = 0; s < 120; s += 5)
            {
                var dt = baseTime.AddSeconds(s);
                _dataService.ExecuteNonQuery(
                    $"INSERT INTO [{logTable}] VALUES ('{dt:dd-MMM-yyyy HH:mm:ss}', 5000, 5000, 150, 0, 5);");
            }

            // Phase 2: Bottom to Slips (10 seconds)
            for (int s = 120; s < 130; s += 5)
            {
                var dt = baseTime.AddSeconds(s);
                _dataService.ExecuteNonQuery(
                    $"INSERT INTO [{logTable}] VALUES ('{dt:dd-MMM-yyyy HH:mm:ss}', 5000, 5000, 100, 11, 5);");
            }

            // Phase 3: In Slips (80 seconds, HKLD < cutoff)
            for (int s = 130; s < 210; s += 5)
            {
                var dt = baseTime.AddSeconds(s);
                _dataService.ExecuteNonQuery(
                    $"INSERT INTO [{logTable}] VALUES ('{dt:dd-MMM-yyyy HH:mm:ss}', 5000, 5000, 20, 2, 5);");
            }

            // Phase 4: Slips to bottom (20 seconds)
            for (int s = 210; s < 230; s += 5)
            {
                var dt = baseTime.AddSeconds(s);
                _dataService.ExecuteNonQuery(
                    $"INSERT INTO [{logTable}] VALUES ('{dt:dd-MMM-yyyy HH:mm:ss}', 5000, 5000, 130, 12, 5);");
            }

            // Phase 5: Continuous drilling after connection (120 seconds)
            for (int s = 230; s < 350; s += 5)
            {
                var dt = baseTime.AddSeconds(s);
                _dataService.ExecuteNonQuery(
                    $"INSERT INTO [{logTable}] VALUES ('{dt:dd-MMM-yyyy HH:mm:ss}', 5005, 5005, 150, 0, 5);");
            }

            // Setup TimeLog
            var timeLog = new TimeLog
            {
                ObjectID = "TL_01",
                WellID = wellId,
                __dataTableName = logTable
            };

            var processor = new DrillingConnectionsProcessor
            {
                objLocalConn = _dataService,
                WellID = wellId,
                __localTimeLog = timeLog,
                SkipConnections = false,
                RUN_DRLG_CHECK = false,
                RUN_HKLD_CHECK = true
            };

            DateTime fromDate = baseTime;
            DateTime toDate = baseTime.AddSeconds(360);

            // Act
            processor.ProcessPoints(timeLog, fromDate, toDate);

            // Assert
            Assert.NotEmpty(processor.connectionPoints);
            Assert.True(_dataService.IsRecordExist($"SELECT 1 FROM VMX_AKPI_DRLG_CONNECTIONS WHERE WELL_ID='{wellId}';"));

            DataTable dtResult = _dataService.GetTable($"SELECT * FROM VMX_AKPI_DRLG_CONNECTIONS WHERE WELL_ID='{wellId}';");
            Assert.NotNull(dtResult);
            Assert.True(dtResult.Rows.Count > 0);

            var row = dtResult.Rows[0];
            Assert.Equal(wellId, row["WELL_ID"].ToString());
            Assert.Equal(5000.0, Convert.ToDouble(row["DEPTH"]));
            Assert.True(Convert.ToDouble(row["BOTTOM_TO_SLIPS"]) >= 5.0);
            Assert.True(Convert.ToDouble(row["SLIPS_TO_SLIPS"]) >= 60.0);
        }

        [Fact]
        public void DrillingConnectionsProcessor_SkipsConnection_WhenExceedsMaxMinutes()
        {
            DrillingConnectionsProcessor.EnsureTables(_dataService);
            SystemSettings.EnsureTableExists(_dataService);

            string wellId = "WELL_SKIP_001";
            var processor = new DrillingConnectionsProcessor
            {
                objLocalConn = _dataService,
                WellID = wellId,
                SkipConnections = true,
                MaxConnectionTimeMinutes = 5.0 // Max 5 minutes allowed
            };

            // Add a connection point that lasts 10 minutes (600 seconds)
            var longConn = new ConnectionLogPoint
            {
                Depth = 8000,
                FromDate = DateTime.Now.AddMinutes(-30),
                ToDate = DateTime.Now.AddMinutes(-20),
                BottomToSlipsSeconds = 60,
                SlipsToSlipsSeconds = 480,
                SlipsToBottomSeconds = 60
            };
            processor.connectionPoints.Add(1, longConn);

            int saved = 0;
            int skipped = 0;
            processor.saveConnections(ref saved, ref skipped);

            Assert.Equal(0, saved);
            Assert.Equal(1, skipped);
            Assert.False(_dataService.IsRecordExist($"SELECT 1 FROM VMX_AKPI_DRLG_CONNECTIONS WHERE WELL_ID='{wellId}';"));
        }

        [Fact]
        public void ViewModel_InitializesPropertiesAndLoadsSettings()
        {
            var session = new ProjectSession();
            var vm = new ProcessDrillingConnectionsViewModel(session);

            Assert.True(vm.SkipConnectionsMoreThan);
            Assert.Equal(30.0, vm.SkipMinutes);
            Assert.False(vm.OverwriteExisting);
            Assert.False(vm.IsRunning);
        }

        [Fact]
        public void ViewModel_Close_InvokesRequestClose()
        {
            var session = new ProjectSession();
            var vm = new ProcessDrillingConnectionsViewModel(session);

            bool closeInvoked = false;
            vm.RequestClose += (res) => closeInvoked = true;

            vm.CloseCommand.Execute(null);
            Assert.True(closeInvoked);
        }
    }
}
