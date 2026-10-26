using System;
using System.IO;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models.Common;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DrillIntel.Tests
{
    public class SystemSettingsTests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly DataServiceDIntel _dataService;
        private readonly string _testLogFolder;

        public SystemSettingsTests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"settings_test_{Guid.NewGuid():N}.sqlite");
            _dataService = new DataServiceDIntel(_tempDbPath);
            _dataService.UserName = "TestOperator";
            _testLogFolder = Path.Combine(Path.GetTempPath(), $"settings_log_{Guid.NewGuid():N}");
        }

        public void Dispose()
        {
            try { _dataService.CloseConnection(); } catch { }
            try { _dataService.Dispose(); } catch { }
            SqliteConnection.ClearAllPools();
            try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
            try { if (Directory.Exists(_testLogFolder)) Directory.Delete(_testLogFolder, true); } catch { }
        }

        [Fact]
        public void LoadSettings_WhenTableDoesNotExist_CreatesTableAndSeedsDefaults()
        {
            var systemSettings = new SystemSettings();
            bool result = systemSettings.LoadSettings(_dataService);

            Assert.True(result);
            Assert.True(systemSettings.settings.Count >= 41);
            Assert.Equal("2", systemSettings.getSetting(SystemSettings.DigitSignificance));
            Assert.Equal("0", systemSettings.getSetting(SystemSettings.DepthOffset));
            Assert.Equal("False", systemSettings.getSetting(SystemSettings.IgnoreNegative));
            Assert.Equal(@"C:\ProgramData\DrillIntel\Logs", systemSettings.getSetting(SystemSettings.LogFolder));
            Assert.True(_dataService.TableExists("VMX_SETTINGS"));
        }

        [Fact]
        public void EnsureDefaultSettings_WhenZeroRecords_SeedsAllDefaultValues()
        {
            // Ensure table exists first, then empty it
            SystemSettings.EnsureTableExists(_dataService);
            _dataService.ExecuteNonQuery("DELETE FROM VMX_SETTINGS;");
            Assert.Equal(0, Convert.ToInt64(_dataService.GetTable("SELECT COUNT(*) FROM VMX_SETTINGS;").Rows[0][0]));

            SystemSettings.EnsureDefaultSettings(_dataService);

            long count = Convert.ToInt64(_dataService.GetTable("SELECT COUNT(*) FROM VMX_SETTINGS;").Rows[0][0]);
            Assert.Equal(41, count);

            Assert.Equal("2", SystemSettings.getASetting(_dataService, "DigitSignificance"));
            Assert.Equal("5000", SystemSettings.getASetting(_dataService, "DataPageSize"));
            Assert.Equal("127.0.0.1", SystemSettings.getASetting(_dataService, "DServerIP"));
            Assert.Equal("A4", SystemSettings.getASetting(_dataService, "PageSize"));
            Assert.Equal("Landscape", SystemSettings.getASetting(_dataService, "Orientation"));
            Assert.Equal("False", SystemSettings.getASetting(_dataService, "ShareBroomstickDouments"));

            // Calling again must be a no-op
            SystemSettings.EnsureDefaultSettings(_dataService);
            long countAgain = Convert.ToInt64(_dataService.GetTable("SELECT COUNT(*) FROM VMX_SETTINGS;").Rows[0][0]);
            Assert.Equal(41, countAgain);
        }

        [Fact]
        public void SaveSettings_PersistsSettingsAndLoadsThemBack()
        {
            var systemSettings = new SystemSettings();
            systemSettings.setSetting("TestKey1", "Value1");
            systemSettings.setSetting(SystemSettings.DepthOffset, "125.75");
            systemSettings.setSetting(SystemSettings.WellCheckEnabled, "True");

            bool saveResult = systemSettings.SaveSettings(_dataService);
            Assert.True(saveResult);

            var loaded = new SystemSettings();
            bool loadResult = loaded.LoadSettings(_dataService);
            Assert.True(loadResult);

            Assert.Equal("Value1", loaded.getSetting("TestKey1"));
            Assert.Equal(125.75, loaded.getNumericSetting(SystemSettings.DepthOffset));
            Assert.True(loaded.getBooleanSetting(SystemSettings.WellCheckEnabled));
        }

        [Fact]
        public void saveASetting_And_getASetting_DirectlyQueryAndPersistRow()
        {
            SystemSettings.saveASetting(_dataService, "DirectKey", "DirectValue42");

            string retrieved = SystemSettings.getASetting(_dataService, "DirectKey");
            Assert.Equal("DirectValue42", retrieved);

            // Test legacy spelling alias getAStting
            string legacyRetrieved = SystemSettings.getAStting(_dataService, "DirectKey");
            Assert.Equal("DirectValue42", legacyRetrieved);

            // Overwrite existing direct setting
            SystemSettings.saveASetting(_dataService, "DirectKey", "UpdatedValue");
            Assert.Equal("UpdatedValue", SystemSettings.getASetting(_dataService, "DirectKey"));
        }

        [Fact]
        public void BinarySettings_PersistsBlobAndLoadsFileBackToDisk()
        {
            // Create a temporary sample binary file (e.g. logo or license blob)
            Directory.CreateDirectory(_testLogFolder);
            string sourceFile = Path.Combine(_testLogFolder, "sample_logo.png");
            byte[] expectedBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03, 0xFF };
            File.WriteAllBytes(sourceFile, expectedBytes);

            var systemSettings = new SystemSettings();
            systemSettings.setSetting(SystemSettings.LogFolder, _testLogFolder);
            systemSettings.setBinarySetting(SystemSettings.CompanyLogo, sourceFile);

            bool saveResult = systemSettings.SaveSettings(_dataService);
            Assert.True(saveResult);

            // Load settings in a new instance and read binary value
            var reloaded = new SystemSettings();
            reloaded.LoadSettings(_dataService);

            string extractedPath = reloaded.loadBinaryValue(_dataService, SystemSettings.CompanyLogo);
            Assert.NotEmpty(extractedPath);
            Assert.True(File.Exists(extractedPath));

            byte[] actualBytes = File.ReadAllBytes(extractedPath);
            Assert.Equal(expectedBytes, actualBytes);
        }

        [Fact]
        public void GetNumericSetting_ParsesVariousFormats_AndDefaultsToZero()
        {
            var systemSettings = new SystemSettings();
            systemSettings.setSetting("NormalNumber", "3500.5");
            systemSettings.setSetting("NegativeNumber", "-45.2");
            systemSettings.setSetting("InvalidNumber", "abc");

            Assert.Equal(3500.5, systemSettings.getNumericSetting("NormalNumber"));
            Assert.Equal(-45.2, systemSettings.getNumericSetting("NegativeNumber"));
            Assert.Equal(0.0, systemSettings.getNumericSetting("InvalidNumber"));
            Assert.Equal(0.0, systemSettings.getNumericSetting("NonExistentKey"));
        }

        [Fact]
        public void GetBooleanSetting_ParsesVariousFormats_AndDefaultsToFalse()
        {
            var systemSettings = new SystemSettings();
            systemSettings.setSetting("B1", "True");
            systemSettings.setSetting("B2", "true");
            systemSettings.setSetting("B3", "1");
            systemSettings.setSetting("B4", "yes");
            systemSettings.setSetting("B5", "False");
            systemSettings.setSetting("B6", "0");
            systemSettings.setSetting("B7", "no");

            Assert.True(systemSettings.getBooleanSetting("B1"));
            Assert.True(systemSettings.getBooleanSetting("B2"));
            Assert.True(systemSettings.getBooleanSetting("B3"));
            Assert.True(systemSettings.getBooleanSetting("B4"));
            Assert.False(systemSettings.getBooleanSetting("B5"));
            Assert.False(systemSettings.getBooleanSetting("B6"));
            Assert.False(systemSettings.getBooleanSetting("B7"));
            Assert.False(systemSettings.getBooleanSetting("NonExistentKey"));
        }

        [Fact]
        public void GetCopy_CreatesIndependentDeepClone()
        {
            var original = new SystemSettings();
            original.setSetting("K1", "V1");
            original.LastError = "SampleError";

            var copy = original.GetCopy();
            Assert.Equal("V1", copy.getSetting("K1"));
            Assert.Equal("SampleError", copy.LastError);

            // Mutating copy does not affect original
            copy.setSetting("K1", "MutatedV1");
            Assert.Equal("V1", original.getSetting("K1"));
            Assert.Equal("MutatedV1", copy.getSetting("K1"));
        }
    }
}

