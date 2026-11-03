using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.DrillingStandProcessor;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Projects;
using DrillIntel.Services;
using DrillIntel.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;
using SystemSettings = DrillIntel.Data.Objects.DataObjects.Models.Common.SystemSettings;

namespace DrillIntel.Tests
{
    public class DrillingStandProcessorTests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly DataServiceDIntel _dataService;

        public DrillingStandProcessorTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"drlg_stand_test_{Guid.NewGuid():N}.sqlite");
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
        public void EnsureTables_CreatesVMX_AKPI_DRLG_STANDInSqlite()
        {
            // Act
            DrillingStandProcessor.EnsureTables(_dataService);

            // Assert
            Assert.True(_dataService.IsRecordExist("SELECT 1 FROM sqlite_master WHERE type='table' AND name='VMX_AKPI_DRLG_STAND';"));
        }

        [Fact]
        public void DetermineDayOrNight_IdentifiesDayAndNightCorrectly()
        {
            var processor = new DrillingStandProcessor
            {
                DayTimeHoursFrom = "12:00"
            };

            // 14:00 (2 PM) local -> DayTime
            var localDay = new DateTime(2026, 11, 2, 14, 0, 0, DateTimeKind.Local);
            var dayStatus = processor.determineDayOrNight(localDay.ToUniversalTime());
            Assert.Equal(StandPoint.cnDayNightTime.DayTime, dayStatus);

            // 02:00 (2 AM) local -> NightTime
            var localNight = new DateTime(2026, 11, 2, 2, 0, 0, DateTimeKind.Local);
            var nightStatus = processor.determineDayOrNight(localNight.ToUniversalTime());
            Assert.Equal(StandPoint.cnDayNightTime.NightTime, nightStatus);
        }

        [Fact]
        public void StandDetection_CalculatesStandsBetweenConsecutiveConnections()
        {
            string wellId = "WELL_STAND_001";
            DrillingStandProcessor.EnsureTables(_dataService);

            // Create connections table
            _dataService.ExecuteNonQuery(@"
CREATE TABLE IF NOT EXISTS VMX_AKPI_DRLG_CONNECTIONS (
    WELL_ID TEXT NOT NULL,
    ENTRY_ID INTEGER NOT NULL,
    DEPTH REAL,
    FROM_DATE TEXT,
    TO_DATE TEXT,
    PRIMARY KEY (WELL_ID, ENTRY_ID)
);");

            // Connection 1: Depth 1000, 08:00 to 08:10
            // Connection 2: Depth 1090, 09:00 to 09:10
            // Connection 3: Depth 1180, 10:00 to 10:10
            _dataService.ExecuteNonQuery(@"
INSERT INTO VMX_AKPI_DRLG_CONNECTIONS (WELL_ID, ENTRY_ID, DEPTH, FROM_DATE, TO_DATE) VALUES
('WELL_STAND_001', 1, 1000.0, '02-Nov-2026 08:00:00', '02-Nov-2026 08:10:00'),
('WELL_STAND_001', 2, 1090.0, '02-Nov-2026 09:00:00', '02-Nov-2026 09:10:00'),
('WELL_STAND_001', 3, 1180.0, '02-Nov-2026 10:00:00', '02-Nov-2026 10:10:00');");

            // Create timelog data table
            string tableName = "DATA_TL_STAND_TEST";
            _dataService.ExecuteNonQuery($@"
CREATE TABLE IF NOT EXISTS [{tableName}] (
    [DATA_INDEX] INTEGER PRIMARY KEY,
    [DATETIME] TEXT,
    [DEPTH] REAL,
    [HDTH] REAL,
    [NEXT_DEPTH] REAL,
    [FOOTAGE] REAL,
    [RIG_STATE] REAL,
    [TIME_DURATION] REAL
);");

            // Seed timelog rows for stand 1 (08:10 to 09:00) and stand 2 (09:10 to 10:00)
            _dataService.ExecuteNonQuery($@"
INSERT INTO [{tableName}] (DATA_INDEX, DATETIME, DEPTH, HDTH, NEXT_DEPTH, FOOTAGE, RIG_STATE, TIME_DURATION) VALUES
(1, '02-Nov-2026 08:15:00', 1045.0, 1045.0, 1000.0, 45.0, 0.0, 1500.0),
(2, '02-Nov-2026 08:45:00', 1090.0, 1090.0, 1045.0, 45.0, 1.0, 1500.0),
(3, '02-Nov-2026 09:15:00', 1135.0, 1135.0, 1090.0, 45.0, 0.0, 1500.0),
(4, '02-Nov-2026 09:45:00', 1180.0, 1180.0, 1135.0, 45.0, 19.0, 1500.0);");

            var timeLog = new TimeLog
            {
                WellID = wellId,
                ObjectID = "TL_01",
                __dataTableName = tableName
            };

            var processor = new DrillingStandProcessor
            {
                objLocalConn = _dataService,
                WellID = wellId,
                __localTimeLog = timeLog,
                DayTimeHoursFrom = "06:00",
                OverwriteExisting = true
            };

            // Act
            processor.ProcessPoints(
                new DateTime(2026, 11, 2, 7, 0, 0),
                new DateTime(2026, 11, 2, 11, 0, 0),
                out DateTime lastConnDate);

            // Assert
            Assert.Equal(2, processor.connectionPoints.Count);

            // Stand 1
            var stand1 = processor.connectionPoints[1];
            Assert.Equal(1000.0, stand1.Depth);
            Assert.Equal(new DateTime(2026, 11, 2, 8, 10, 0), stand1.FromDate);
            Assert.Equal(new DateTime(2026, 11, 2, 9, 0, 0), stand1.ToDate);
            Assert.True(stand1.ROP > 0);
            Assert.True(stand1.RotaryROP > 0);
            Assert.True(stand1.SlideROP > 0);

            // Stand 2
            var stand2 = processor.connectionPoints[2];
            Assert.Equal(1090.0, stand2.Depth);
            Assert.Equal(new DateTime(2026, 11, 2, 9, 10, 0), stand2.FromDate);
            Assert.Equal(new DateTime(2026, 11, 2, 10, 0, 0), stand2.ToDate);

            // Check database records in VMX_AKPI_DRLG_STAND
            int countInDb = KPIDrlgStand.GetStandsCount(_dataService, wellId);
            Assert.Equal(2, countInDb);
        }

        [Fact]
        public void StandDetection_RejectsStandsWithROPOver5000()
        {
            string wellId = "WELL_STAND_002";
            DrillingStandProcessor.EnsureTables(_dataService);

            _dataService.ExecuteNonQuery(@"
CREATE TABLE IF NOT EXISTS VMX_AKPI_DRLG_CONNECTIONS (
    WELL_ID TEXT NOT NULL,
    ENTRY_ID INTEGER NOT NULL,
    DEPTH REAL,
    FROM_DATE TEXT,
    TO_DATE TEXT,
    PRIMARY KEY (WELL_ID, ENTRY_ID)
);");

            _dataService.ExecuteNonQuery(@"
INSERT INTO VMX_AKPI_DRLG_CONNECTIONS (WELL_ID, ENTRY_ID, DEPTH, FROM_DATE, TO_DATE) VALUES
('WELL_STAND_002', 1, 1000.0, '02-Nov-2026 08:00:00', '02-Nov-2026 08:01:00'),
('WELL_STAND_002', 2, 11000.0, '02-Nov-2026 08:01:02', '02-Nov-2026 08:02:00');");

            string tableName = "DATA_TL_STAND_OUTLIER";
            _dataService.ExecuteNonQuery($@"
CREATE TABLE IF NOT EXISTS [{tableName}] (
    [DATA_INDEX] INTEGER PRIMARY KEY,
    [DATETIME] TEXT,
    [DEPTH] REAL,
    [HDTH] REAL,
    [NEXT_DEPTH] REAL,
    [RIG_STATE] REAL,
    [TIME_DURATION] REAL
);");

            _dataService.ExecuteNonQuery($@"
INSERT INTO [{tableName}] (DATA_INDEX, DATETIME, DEPTH, HDTH, NEXT_DEPTH, RIG_STATE, TIME_DURATION) VALUES
(1, '02-Nov-2026 08:01:01', 11000.0, 11000.0, 1000.0, 0.0, 1.0);");

            var timeLog = new TimeLog
            {
                WellID = wellId,
                ObjectID = "TL_02",
                __dataTableName = tableName
            };

            var processor = new DrillingStandProcessor
            {
                objLocalConn = _dataService,
                WellID = wellId,
                __localTimeLog = timeLog,
                OverwriteExisting = true
            };

            processor.ProcessPoints(
                new DateTime(2026, 11, 2, 7, 0, 0),
                new DateTime(2026, 11, 2, 9, 0, 0),
                out _);

            Assert.Empty(processor.connectionPoints);
            Assert.Equal(0, KPIDrlgStand.GetStandsCount(_dataService, wellId));
        }

        [Fact]
        public void KPIDrlgStand_ClearStandsFromDB_DeletesAllWellRecords()
        {
            DrillingStandProcessor.EnsureTables(_dataService);

            string wellId = "WELL_CLEAR_TEST";
            var stand = new KPIDrlgStand
            {
                WellID = wellId,
                Depth = 2500.0,
                FromDate = new DateTime(2026, 11, 2, 10, 0, 0),
                ToDate = new DateTime(2026, 11, 2, 10, 50, 0),
                ROP = 65.5,
                RotaryROP = 70.0,
                SlideROP = 0.0
            };

            long entryId = 0;
            string err = "";
            bool added = KPIDrlgStand.addToDatabase(_dataService, stand, ref entryId, ref err);
            Assert.True(added);
            Assert.Equal(1, KPIDrlgStand.GetStandsCount(_dataService, wellId));

            // Clear
            bool cleared = KPIDrlgStand.clearStandsFromDB(_dataService, wellId, ref err);
            Assert.True(cleared);
            Assert.Equal(0, KPIDrlgStand.GetStandsCount(_dataService, wellId));
        }

        [Fact]
        public void ViewModel_InitializesPropertiesCorrectly()
        {
            var session = new ProjectSession();
            var vm = new ProcessDrillingStandsViewModel(session);

            Assert.Equal("12:00", vm.DayTimeHoursFrom);
            Assert.False(vm.OverwriteExisting);
            Assert.False(vm.IsRunning);
            Assert.False(vm.IsCompleted);
            Assert.False(vm.HasError);
        }

        [Fact]
        public void ViewModel_Close_InvokesRequestClose()
        {
            var session = new ProjectSession();
            var vm = new ProcessDrillingStandsViewModel(session);

            bool closeInvoked = false;
            vm.RequestClose += (res) => closeInvoked = true;

            vm.CloseCommand.Execute(null);
            Assert.True(closeInvoked);
        }

        [Fact]
        public async Task ViewModel_ClearDbEntries_UsesConfirmationHandler()
        {
            var session = new ProjectSession();
            var mockService = new MockDrillingStandsService();
            var vm = new ProcessDrillingStandsViewModel(session, service: mockService)
            {
                WellId = "W1"
            };

            bool confirmationCalled = false;
            vm.ConfirmationHandler = (msg, title) =>
            {
                confirmationCalled = true;
                return true;
            };

            await vm.ClearDbEntriesCommand.ExecuteAsync(null);

            Assert.True(confirmationCalled);
            Assert.True(mockService.ClearStandsCalled);
        }

        [Fact]
        public async Task ViewModel_StartAsync_ExecutesAndSetsCompleted()
        {
            var session = new ProjectSession();
            var mockService = new MockDrillingStandsService();
            var vm = new ProcessDrillingStandsViewModel(session, service: mockService)
            {
                WellId = "W1"
            };

            await vm.StartCommand.ExecuteAsync(null);

            Assert.True(mockService.ProcessStandsCalled);
            Assert.True(vm.IsCompleted);
            Assert.Equal(10, vm.DetectedStandsCount);
            Assert.Equal(100.0, vm.ProgressPercent);
        }

        [Fact]
        public void DrillingStandPlot_GetData_PopulatesRigStateData()
        {
            string wellId = "WELL_PLOT_TEST_01";
            DrillingStandProcessor.EnsureTables(_dataService);

            // Add stands
            var s1 = new KPIDrlgStand { WellID = wellId, Depth = 1000.0, FromDate = new DateTime(2026, 11, 2, 8, 0, 0), ToDate = new DateTime(2026, 11, 2, 9, 0, 0), Time = "D" };
            var s2 = new KPIDrlgStand { WellID = wellId, Depth = 1090.0, FromDate = new DateTime(2026, 11, 2, 9, 0, 0), ToDate = new DateTime(2026, 11, 2, 10, 0, 0), Time = "N" };
            long id = 0; string err = "";
            KPIDrlgStand.addToDatabase(_dataService, s1, ref id, ref err);
            KPIDrlgStand.addToDatabase(_dataService, s2, ref id, ref err);

            // Create timelog table with dd-MMM-yyyy format
            string tbl = "DATA_TL_PLOT_TEST";
            _dataService.ExecuteNonQuery($"CREATE TABLE [{tbl}] (DATA_INDEX INTEGER PRIMARY KEY, DATETIME TEXT, RIG_STATE REAL, TIME_DURATION REAL);");
            _dataService.ExecuteNonQuery($"INSERT INTO [{tbl}] (DATA_INDEX, DATETIME, RIG_STATE, TIME_DURATION) VALUES " +
                "(1, '02-Nov-2026 08:30:00', 0.0, 1800.0)," +
                "(2, '02-Nov-2026 08:45:00', 1.0, 1800.0)," +
                "(3, '02-Nov-2026 09:30:00', 0.0, 3600.0);");

            var plot = new DrillingStandPlot(_dataService);
            var data = plot.GetData(wellId, tbl, new DateTime(2026, 11, 2, 0, 0, 0), new DateTime(2026, 11, 2, 23, 59, 59));

            Assert.Equal(2, data.standData.Rows.Count);
            Assert.Equal(2, data.rigStateData.Rows.Count);
            string payload0 = data.rigStateData.Rows[0]["RIG_STATES"]?.ToString() ?? "";
            Assert.Contains("0#30", payload0);
            Assert.Contains("1#30", payload0);
        }

        [Fact]
        public void DrillingStandPlot_GetData_WithIsoDates_PopulatesRigStateData()
        {
            string wellId = "WELL_PLOT_TEST_ISO";
            DrillingStandProcessor.EnsureTables(_dataService);

            var s1 = new KPIDrlgStand { WellID = wellId, Depth = 2000.0, FromDate = new DateTime(2026, 11, 2, 8, 0, 0), ToDate = new DateTime(2026, 11, 2, 9, 0, 0), Time = "D" };
            long id = 0; string err = "";
            KPIDrlgStand.addToDatabase(_dataService, s1, ref id, ref err);

            // Create timelog table with ISO format
            string tbl = "DATA_TL_PLOT_ISO";
            _dataService.ExecuteNonQuery($"CREATE TABLE [{tbl}] (DATA_INDEX INTEGER PRIMARY KEY, DATETIME TEXT, RIG_STATE REAL, TIME_DURATION REAL);");
            _dataService.ExecuteNonQuery($"INSERT INTO [{tbl}] (DATA_INDEX, DATETIME, RIG_STATE, TIME_DURATION) VALUES " +
                "(1, '2026-11-02 08:30:00', 0.0, 1800.0);");

            var plot = new DrillingStandPlot(_dataService);
            var data = plot.GetData(wellId, tbl, new DateTime(2026, 11, 2, 0, 0, 0), new DateTime(2026, 11, 2, 23, 59, 59));

            Assert.Equal(1, data.standData.Rows.Count);
            Assert.Equal(1, data.rigStateData.Rows.Count);
            string payload0 = data.rigStateData.Rows[0]["RIG_STATES"]?.ToString() ?? "";
            Assert.Contains("0#30", payload0);
        }

        [Fact]
        public void DrillingStandPlot_GetData_WithNullTimeLogTable_ResolvesPrimaryLogAndPopulatesRigStates()
        {
            string wellId = "WELL_PLOT_TEST_NULL_TABLE";
            DrillingStandProcessor.EnsureTables(_dataService);

            var s1 = new KPIDrlgStand { WellID = wellId, Depth = 4000.0, FromDate = new DateTime(2026, 11, 2, 8, 0, 0), ToDate = new DateTime(2026, 11, 2, 9, 0, 0), Time = "D" };
            long id = 0; string err = "";
            KPIDrlgStand.addToDatabase(_dataService, s1, ref id, ref err);

            string tbl = "DATA_TL_PLOT_NULL";
            _dataService.ExecuteNonQuery($"CREATE TABLE [{tbl}] (DATA_INDEX INTEGER PRIMARY KEY, DATETIME TEXT, RIG_STATE REAL, TIME_DURATION REAL);");
            _dataService.ExecuteNonQuery($"INSERT INTO [{tbl}] (DATA_INDEX, DATETIME, RIG_STATE, TIME_DURATION) VALUES (1, '02-Nov-2026 08:30:00', 0.0, 1800.0);");

            // Register in VMX_TIME_LOG
            _dataService.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS VMX_TIME_LOG (LOG_ID TEXT PRIMARY KEY, WELL_ID TEXT, DATA_TABLE_NAME TEXT, PRIMARY_LOG INTEGER, CREATED_DATE TEXT);");
            _dataService.ExecuteNonQuery($"INSERT INTO VMX_TIME_LOG (LOG_ID, WELL_ID, DATA_TABLE_NAME, PRIMARY_LOG, CREATED_DATE) VALUES ('TL_P', '{wellId}', '{tbl}', 1, '02-Nov-2026 00:00:00');");

            var plot = new DrillingStandPlot(_dataService);
            var data = plot.GetData(wellId, null, new DateTime(2026, 11, 2, 0, 0, 0), new DateTime(2026, 11, 2, 23, 59, 59));

            Assert.Equal(1, data.standData.Rows.Count);
            Assert.Equal(1, data.rigStateData.Rows.Count);
            string payload0 = data.rigStateData.Rows[0]["RIG_STATES"]?.ToString() ?? "";
            Assert.Contains("0#30", payload0);
        }

        [Fact]
        public void DrillingStandPlot_GetData_WithDateAndTimeColumns_PopulatesRigStateData()
        {
            string wellId = "WELL_PLOT_TEST_SPLIT";
            DrillingStandProcessor.EnsureTables(_dataService);

            var s1 = new KPIDrlgStand { WellID = wellId, Depth = 3000.0, FromDate = new DateTime(2026, 11, 2, 8, 0, 0), ToDate = new DateTime(2026, 11, 2, 9, 0, 0), Time = "D" };
            long id = 0; string err = "";
            KPIDrlgStand.addToDatabase(_dataService, s1, ref id, ref err);

            // Create timelog table with DATE and TIME columns instead of DATETIME
            string tbl = "DATA_TL_PLOT_SPLIT";
            _dataService.ExecuteNonQuery($"CREATE TABLE [{tbl}] (DATA_INDEX INTEGER PRIMARY KEY, [DATE] TEXT, [TIME] TEXT, RIG_STATE REAL, TIME_DURATION REAL);");
            _dataService.ExecuteNonQuery($"INSERT INTO [{tbl}] (DATA_INDEX, [DATE], [TIME], RIG_STATE, TIME_DURATION) VALUES " +
                "(1, '02-Nov-2026', '08:30:00', 0.0, 1800.0);");

            var plot = new DrillingStandPlot(_dataService);
            var data = plot.GetData(wellId, tbl, new DateTime(2026, 11, 2, 0, 0, 0), new DateTime(2026, 11, 2, 23, 59, 59));

            Assert.Equal(1, data.standData.Rows.Count);
            Assert.Equal(1, data.rigStateData.Rows.Count);
            string payload0 = data.rigStateData.Rows[0]["RIG_STATES"]?.ToString() ?? "";
            Assert.Contains("0#30", payload0);
        }

        [Fact]
        public void DrillingStandPlot_GetData_WithSpecialCharacterTableName_PopulatesRigStateData()
        {
            string wellId = "WELL_PLOT_TEST_SPECIAL_CHARS";
            DrillingStandProcessor.EnsureTables(_dataService);

            var s1 = new KPIDrlgStand { WellID = wellId, Depth = 5000.0, FromDate = new DateTime(2026, 11, 2, 8, 0, 0), ToDate = new DateTime(2026, 11, 2, 9, 0, 0), Time = "D" };
            long id = 0; string err = "";
            KPIDrlgStand.addToDatabase(_dataService, s1, ref id, ref err);

            // Table name with hash symbol (standard VuMax/eVuMax generated table name format)
            string tbl = "timeLog23798241#40580182";
            _dataService.ExecuteNonQuery($"CREATE TABLE [{tbl}] (DATA_INDEX INTEGER PRIMARY KEY, DATETIME TEXT, RIG_STATE REAL, TIME_DURATION REAL);");
            _dataService.ExecuteNonQuery($"INSERT INTO [{tbl}] (DATA_INDEX, DATETIME, RIG_STATE, TIME_DURATION) VALUES " +
                "(1, '02-Nov-2026 08:30:00', 0.0, 1800.0);");

            var plot = new DrillingStandPlot(_dataService);
            var data = plot.GetData(wellId, tbl, new DateTime(2026, 11, 2, 0, 0, 0), new DateTime(2026, 11, 2, 23, 59, 59));

            Assert.Equal(1, data.standData.Rows.Count);
            Assert.Equal(1, data.rigStateData.Rows.Count);
            string payload0 = data.rigStateData.Rows[0]["RIG_STATES"]?.ToString() ?? "";
            Assert.Contains("0#30", payload0);
            Assert.NotEmpty(data.rigStates.Rows);
        }

        private class MockDrillingStandsService : IProcessDrillingStandsService
        {
            public bool ProcessStandsCalled { get; set; }
            public bool ClearStandsCalled { get; set; }

            public Task<CalculationResult> ProcessStandsAsync(ProcessDrillingStandsOptions options, IProgress<CalculationProgress>? progress = null, CancellationToken ct = default)
            {
                ProcessStandsCalled = true;
                progress?.Report(new CalculationProgress(100, "Done", 1, 1));
                return Task.FromResult(new CalculationResult(true, "ProcessDrillingStands", 10, TimeSpan.FromSeconds(1), ""));
            }

            public Task<bool> ClearStandsAsync(string wellId, CancellationToken ct = default)
            {
                ClearStandsCalled = true;
                return Task.FromResult(true);
            }

            public int GetExistingStandsCount(string wellId) => 0;

            public SystemSettings LoadCommonSettings() => new SystemSettings();
        }
    }
}
