using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.DQI;
using DrillIntel.Projects;
using DrillIntel.Services;
using DrillIntel.ViewModels.DQI;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DrillIntel.Tests
{
    public class DQITests : IDisposable
    {
        private readonly string _tempDbPath;
        private readonly DataServiceDIntel _dataService;
        private readonly ProjectSession _session;

        public DQITests()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"dqi_test_{Guid.NewGuid():N}.sqlite");
            _dataService = new DataServiceDIntel(_tempDbPath);
            _dataService.UserName = "TestAnalyst";

            _session = new ProjectSession();
            _session.Load(_tempDbPath);
        }

        public void Dispose()
        {
            try { _session.Close(); } catch { }
            try { _session.Dispose(); } catch { }
            try { _dataService.CloseConnection(); } catch { }
            try { _dataService.Dispose(); } catch { }
            SqliteConnection.ClearAllPools();
            try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
        }

        [Fact]
        public void NullValues_Parsing_HandlesCommaAndSemicolonDelimiters()
        {
            var nullValueObj = new DQINullValue("-999.25, -999.0; -9999");
            var parsed = nullValueObj.ParseValues();

            Assert.Contains(-999.25, parsed);
            Assert.Contains(-999.0, parsed);
            Assert.Contains(-9999.0, parsed);
            Assert.Equal(3, parsed.Count);
        }

        [Fact]
        public void ConsistencyIndex_Formula_CalculatesCorrectPercentage()
        {
            // If total rows = 100 and null rows = 15, Consistency = 100 - (15 * 100 / 100) = 85.0%
            int totalRows = 100;
            int nullCount = 15;
            double consIdx = totalRows > 0 ? 100.0 - ((nullCount * 100.0) / totalRows) : 0.0;

            Assert.Equal(85.0, consIdx);
        }

        [Fact]
        public void FrequencyIndex_Formula_CalculatesCorrectPercentage()
        {
            // 300 second window, 1 second sampling frequency -> expectedRows = 300
            // If 270 rows received: Frequency = (270 * 100) / 300 = 90.0%
            int actualRows = 270;
            int expectedRows = 300;
            double freqIdx = expectedRows > 0 ? (actualRows * 100.0) / expectedRows : 0.0;

            Assert.Equal(90.0, freqIdx);
        }

        [Fact]
        public void AccuracyIndex_Formula_FlagsOutliersCorrectly()
        {
            // Boundary: Min = 0.0, Max = 1000.0
            var boundary = new DQIDataBoundary("HKLD", "Hookload", "klbs", 0.0, 1000.0);
            var readings = new List<double> { 100, 200, 300, 1200, 500, -50, 700, 800, 900, 450 }; // 10 readings, 2 outliers

            int outlierCount = readings.Count(v => v < boundary.Min || v > boundary.Max);
            double accrIdx = readings.Count > 0 ? 100.0 - ((outlierCount * 100.0) / readings.Count) : 0.0;

            Assert.Equal(2, outlierCount);
            Assert.Equal(80.0, accrIdx);
        }

        [Fact]
        public void OverallDQI_Formula_AveragesThreeIndices()
        {
            double cons = 100.0;
            double freq = 99.74;
            double accr = 0.03;

            double overall = (cons + freq + accr) / 3.0;

            Assert.Equal(66.59, Math.Round(overall, 2));
        }

        [Fact]
        public async Task DQIRepository_EnsureTablesExist_SeedsDefaults()
        {
            var repo = new DQIRepository(_session);
            await repo.EnsureTablesExistAsync();

            var channels = await repo.GetChannelsAsync();
            Assert.NotEmpty(channels);
            Assert.Contains(channels, c => c.Mnemonic == "BPOS");
            Assert.Contains(channels, c => c.Mnemonic == "CIRC");
            Assert.Contains(channels, c => c.Mnemonic == "HKLD");
            Assert.Contains(channels, c => c.Mnemonic == "SPPA");

            var boundaries = await repo.GetBoundariesAsync();
            Assert.NotEmpty(boundaries);
            Assert.Contains(boundaries, b => b.Mnemonic == "BPOS" && b.Max == 120.0);

            var nullValues = await repo.GetNullValuesAsync();
            Assert.Contains("-999.25", nullValues);
        }

        [Fact]
        public async Task DQIRepository_SaveChannels_UpdatesChannelList()
        {
            var repo = new DQIRepository(_session);
            await repo.EnsureTablesExistAsync();

            var newChannels = new List<DQIChannel>
            {
                new DQIChannel("TORQ", "Surface Torque"),
                new DQIChannel("RPM", "Rotary Speed")
            };

            bool saved = await repo.SaveChannelsAsync(newChannels);
            Assert.True(saved);

            var channels = await repo.GetChannelsAsync();
            Assert.Equal(2, channels.Count);
            Assert.Equal("RPM", channels[0].Mnemonic);
            Assert.Equal("TORQ", channels[1].Mnemonic);
        }

        [Fact]
        public async Task DQIRepository_SaveBoundaries_UpdatesBoundaryList()
        {
            var repo = new DQIRepository(_session);
            await repo.EnsureTablesExistAsync();

            var boundaries = new List<DQIDataBoundary>
            {
                new DQIDataBoundary("TORQ", "Surface Torque", "ft-lbf", 0.0, 30000.0)
            };

            bool saved = await repo.SaveBoundariesAsync(boundaries);
            Assert.True(saved);

            var loaded = await repo.GetBoundariesAsync();
            Assert.Single(loaded);
            Assert.Equal(30000.0, loaded[0].Max);
        }

        [Fact]
        public async Task DQIRepository_SaveNullValues_UpdatesNullValues()
        {
            var repo = new DQIRepository(_session);
            await repo.EnsureTablesExistAsync();

            bool saved = await repo.SaveNullValuesAsync("-9999.0, -999.25");
            Assert.True(saved);

            var loaded = await repo.GetNullValuesAsync();
            Assert.Equal("-9999.0, -999.25", loaded);
        }

        [Fact]
        public void SelectionRule_SingleChannelSelectionEnforcedInLeftPanel()
        {
            var repo = new DQIRepository(_session);
            var service = new DQIService(repo, _session);
            var sharedState = new DQISharedStateService();
            var vm = new DQILeftPanelViewModel(service, sharedState, _session);

            var ch1 = new DQIChannelItemViewModel(new DQIChannel("BPOS", "Block Position"));
            var ch2 = new DQIChannelItemViewModel(new DQIChannel("CIRC", "Circulation"));

            vm.Channels.Add(ch1);
            vm.Channels.Add(ch2);

            // Select ch1
            vm.SelectChannel(ch1);
            Assert.True(ch1.IsSelected);
            Assert.False(ch2.IsSelected);
            Assert.Equal("BPOS", sharedState.SelectedChannel?.Mnemonic);

            // Select ch2
            vm.SelectChannel(ch2);
            Assert.False(ch1.IsSelected);
            Assert.True(ch2.IsSelected);
            Assert.Equal("CIRC", sharedState.SelectedChannel?.Mnemonic);
        }
        [Fact]
        public async Task GetLiveWellsAsync_LoadsWellsFromVMX_WELL()
        {
            _dataService.ExecuteNonQuery("CREATE TABLE IF NOT EXISTS VMX_WELL (WELL_ID TEXT, WELL_NAME TEXT);");
            _dataService.ExecuteNonQuery("INSERT INTO VMX_WELL (WELL_ID, WELL_NAME) VALUES ('W1', 'Alpha Well'), ('W2', 'Beta Well');");

            var repo = new DQIRepository(_session);
            var wells = await repo.GetLiveWellsAsync();

            Assert.Equal(2, wells.Count);
            Assert.Contains(wells, w => w.WellId == "W1" && w.WellName == "Alpha Well");
            Assert.Contains(wells, w => w.WellId == "W2" && w.WellName == "Beta Well");
        }

        [Fact]
        public void CreateDatabase_CreatesDQITables_OnNewProject()
        {
            var testDb = Path.Combine(Path.GetTempPath(), $"new_proj_{Guid.NewGuid():N}.dintel");
            try
            {
                DrillIntel.Projects.SchemaInitializer.CreateDatabase(testDb);
                using var ds = new DrillIntel.Data.DataServiceDIntel(testDb);

                Assert.True(ds.TableExists("eVuMaxDQIChannels"));
                Assert.True(ds.TableExists("eVuMaxDQIDataBoundaries"));
                Assert.True(ds.TableExists("eVuMaxDQINullValues"));
                Assert.True(ds.TableExists("eVuMaxWellDQI"));
                Assert.True(ds.TableExists("eVuMaxWellDQITracking"));

                var chCount = Convert.ToInt64(ds.GetValue("SELECT COUNT(*) FROM eVuMaxDQIChannels;"));
                Assert.Equal(4, chCount);

                var boundsCount = Convert.ToInt64(ds.GetValue("SELECT COUNT(*) FROM eVuMaxDQIDataBoundaries;"));
                Assert.Equal(4, boundsCount);

                var nullVal = ds.GetValue("SELECT nullValues FROM eVuMaxDQINullValues;");
                Assert.Equal("-999.25, -999.0", nullVal?.ToString());
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(testDb)) File.Delete(testDb);
            }
        }
    }
}

