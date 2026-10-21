using System;
using System.IO;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DrillIntel.Tests
{
    public class WellPUSOSettingsTests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly string _connString;
        private readonly DataServiceDIntel _dataService;

        public WellPUSOSettingsTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"puso_test_{Guid.NewGuid():N}.sqlite");
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
        public void DefaultValues_MatchOriginalVbSpecification()
        {
            var settings = new WellPUSOSettings();

            Assert.Equal(string.Empty, settings.WellID);

            // PU Defaults
            Assert.Equal("SPPA", settings.PUChannel);
            Assert.Equal(99.0, settings.PUPumpCutOff);
            Assert.Equal(12.0, settings.PURPMCutOff);
            Assert.Equal(70.0, settings.PUMaxMovement);
            Assert.Equal(5.0, settings.PUMinMovement);
            Assert.Equal(0, settings.PUDynMethod);
            Assert.False(settings.PUFilterMaxLocal);

            // SO Defaults
            Assert.Equal("SPPA", settings.SOChannel);
            Assert.Equal(99.0, settings.SOPumpCutOff);
            Assert.Equal(12.0, settings.SORPMCutOff);
            Assert.Equal(70.0, settings.SOMaxMovement);
            Assert.Equal(5.0, settings.SOMinMovement);
            Assert.Equal(0, settings.SODynMethod);
            Assert.False(settings.SOFilterMaxLocal);

            // ROT Defaults
            Assert.Equal("SPPA", settings.ROTChannel);
            Assert.Equal(99.0, settings.ROTPumpCutOff);
            Assert.Equal(12.0, settings.ROTMinRPM);
            Assert.Equal(30.0, settings.ROTMaxRPM);
            Assert.Equal(1.0, settings.ROTTolerance);
            Assert.Equal(1, settings.ROTPoints);
            Assert.Equal(1, settings.DepthMovementTime);
            Assert.Equal(string.Empty, settings.RunNo);
        }

        [Fact]
        public void EnsureTableExists_CreatesTableAndIndexInSqlite()
        {
            WellPUSOSettings.EnsureTableExists(_dataService);

            using var conn = new SqliteConnection(_connString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='VMX_WELL_PUSO_SETTINGS';";
            long count = (long)cmd.ExecuteScalar()!;
            Assert.Equal(1, count);
        }

        [Fact]
        public void SaveSettings_And_LoadSettings_RoundTrip_ViaIDataService()
        {
            var original = new WellPUSOSettings("WELL_001")
            {
                PUChannel = "HKLD_PU",
                PUPumpCutOff = 85.5,
                PURPMCutOff = 15.0,
                PUMaxMovement = 65.0,
                PUMinMovement = 8.0,
                PUDynMethod = 1,
                PUFilterMaxLocal = true,

                SOChannel = "HKLD_SO",
                SOPumpCutOff = 92.0,
                SORPMCutOff = 10.0,
                SOMaxMovement = 55.0,
                SOMinMovement = 6.0,
                SODynMethod = 2,
                SOFilterMaxLocal = true,

                ROTChannel = "TORQ_ROT",
                ROTPumpCutOff = 75.0,
                ROTMinRPM = 20.0,
                ROTMaxRPM = 45.0,
                ROTTolerance = 2.5,
                ROTPoints = 3,
                DepthMovementTime = 4,
                RunNo = "RUN_02"
            };

            // Test legacy method name
            WellPUSOSettings.saveSettings(_dataService, original);

            // Test legacy load method name
            var loaded = WellPUSOSettings.loadSettings(_dataService, "WELL_001");

            Assert.NotNull(loaded);
            Assert.Equal("WELL_001", loaded.WellID);

            Assert.Equal("HKLD_PU", loaded.PUChannel);
            Assert.Equal(85.5, loaded.PUPumpCutOff);
            Assert.Equal(15.0, loaded.PURPMCutOff);
            Assert.Equal(65.0, loaded.PUMaxMovement);
            Assert.Equal(8.0, loaded.PUMinMovement);
            Assert.Equal(1, loaded.PUDynMethod);
            Assert.True(loaded.PUFilterMaxLocal);

            Assert.Equal("HKLD_SO", loaded.SOChannel);
            Assert.Equal(92.0, loaded.SOPumpCutOff);
            Assert.Equal(10.0, loaded.SORPMCutOff);
            Assert.Equal(55.0, loaded.SOMaxMovement);
            Assert.Equal(6.0, loaded.SOMinMovement);
            Assert.Equal(2, loaded.SODynMethod);
            Assert.True(loaded.SOFilterMaxLocal);

            Assert.Equal("TORQ_ROT", loaded.ROTChannel);
            Assert.Equal(75.0, loaded.ROTPumpCutOff);
            Assert.Equal(20.0, loaded.ROTMinRPM);
            Assert.Equal(45.0, loaded.ROTMaxRPM);
            Assert.Equal(2.5, loaded.ROTTolerance);
            Assert.Equal(3, loaded.ROTPoints);
            Assert.Equal(4, loaded.DepthMovementTime);
            Assert.Equal("RUN_02", loaded.RunNo);
            Assert.Equal("TestUser", loaded.ModifiedBy);
        }

        [Fact]
        public void SaveSettings_And_LoadSettings_RoundTrip_ViaSqliteConnection()
        {
            using var conn = new SqliteConnection(_connString);
            conn.Open();

            var original = new WellPUSOSettings("WELL_CONN_001")
            {
                PUChannel = "PRESS",
                PUPumpCutOff = 50.0,
                ROTPoints = 5,
                RunNo = "RUN_CONN"
            };

            bool saved = WellPUSOSettings.SaveSettings(conn, original, "ConnUser");
            Assert.True(saved);

            var loaded = WellPUSOSettings.LoadSettings(conn, "WELL_CONN_001");
            Assert.NotNull(loaded);
            Assert.Equal("WELL_CONN_001", loaded.WellID);
            Assert.Equal("PRESS", loaded.PUChannel);
            Assert.Equal(50.0, loaded.PUPumpCutOff);
            Assert.Equal(5, loaded.ROTPoints);
            Assert.Equal("RUN_CONN", loaded.RunNo);
            Assert.Equal("ConnUser", loaded.ModifiedBy);
        }

        [Fact]
        public void SaveSettings_OverwritesPreviousSettingsForSameWell()
        {
            var first = new WellPUSOSettings("WELL_REPLACE")
            {
                PUChannel = "OLD_CHANNEL",
                PUPumpCutOff = 10.0
            };
            WellPUSOSettings.SaveSettings(_dataService, first);

            var second = new WellPUSOSettings("WELL_REPLACE")
            {
                PUChannel = "NEW_CHANNEL",
                PUPumpCutOff = 90.0
            };
            WellPUSOSettings.SaveSettings(_dataService, second);

            var loaded = WellPUSOSettings.LoadSettings(_dataService, "WELL_REPLACE");
            Assert.Equal("NEW_CHANNEL", loaded.PUChannel);
            Assert.Equal(90.0, loaded.PUPumpCutOff);

            // Ensure only 1 record exists in table for this well
            using var conn = new SqliteConnection(_connString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM VMX_WELL_PUSO_SETTINGS WHERE WELL_ID='WELL_REPLACE';";
            long count = (long)cmd.ExecuteScalar()!;
            Assert.Equal(1, count);
        }

        [Fact]
        public void LoadSettings_NonExistentWell_ReturnsDefaultInstanceWithWellID()
        {
            var loaded = WellPUSOSettings.LoadSettings(_dataService, "NON_EXISTENT_WELL");
            Assert.NotNull(loaded);
            Assert.Equal("NON_EXISTENT_WELL", loaded.WellID);
            Assert.Equal("SPPA", loaded.PUChannel);
            Assert.Equal(99.0, loaded.PUPumpCutOff);
        }

        [Fact]
        public void SaveSettings_ProtectsAgainstSqlInjectionInStrings()
        {
            string maliciousWellId = "WELL'; DROP TABLE VMX_WELL_PUSO_SETTINGS; --";
            var settings = new WellPUSOSettings(maliciousWellId)
            {
                PUChannel = "SPPA' OR '1'='1",
                RunNo = "RUN'; DELETE FROM VMX_WELL_PUSO_SETTINGS; --"
            };

            bool saved = WellPUSOSettings.SaveSettings(_dataService, settings);
            Assert.True(saved);

            // Verify table still exists and record can be safely loaded
            var loaded = WellPUSOSettings.LoadSettings(_dataService, maliciousWellId);
            Assert.Equal(maliciousWellId, loaded.WellID);
            Assert.Equal("SPPA' OR '1'='1", loaded.PUChannel);
            Assert.Equal("RUN'; DELETE FROM VMX_WELL_PUSO_SETTINGS; --", loaded.RunNo);
        }

        [Fact]
        public void GetCopy_And_Clone_ProduceIndependentDeepCopy()
        {
            var original = new WellPUSOSettings("WELL_COPY")
            {
                PUChannel = "ORIGINAL_PU",
                PUPumpCutOff = 77.0
            };

            var copy = original.GetCopy();
            Assert.Equal(original.WellID, copy.WellID);
            Assert.Equal(original.PUChannel, copy.PUChannel);
            Assert.Equal(original.PUPumpCutOff, copy.PUPumpCutOff);

            copy.PUChannel = "CHANGED_PU";
            Assert.Equal("ORIGINAL_PU", original.PUChannel);
        }
    }
}

