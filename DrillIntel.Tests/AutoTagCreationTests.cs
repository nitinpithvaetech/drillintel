using System;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services.AutoTagDetection;
using DrillIntel.Projects;
using DrillIntel.Services;
using DrillIntel.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DrillIntel.Tests;

public class AutoTagCreationTests : IDisposable
{
    private class DummyProjectService : IProjectService
    {
        public bool CreateNewProject() => true;
        public Task<bool> CreateNewProjectAsync() => Task.FromResult(true);
        public bool OpenProject() => true;
        public bool OpenProject(string filePath) => true;
    }

    private readonly string _tempDbPath;
    private readonly string _connString;
    private readonly DataServiceDIntel _dataService;
    private readonly ProjectSession _session;

    public AutoTagCreationTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"autotag_test_{Guid.NewGuid():N}.sqlite");
        _connString = $"Data Source={_tempDbPath}";
        SchemaInitializer.CreateDatabase(_tempDbPath);
        _dataService = new DataServiceDIntel(_tempDbPath);
        _session = new ProjectSession();
    }

    public void Dispose()
    {
        try { _session.Close(); } catch { }
        try { _dataService.CloseConnection(); } catch { }
        try { _dataService.Dispose(); } catch { }
        try { if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath); } catch { }
    }

    private async Task SeedWellAndTelemetryAsync(string wellId = "WELL_TEST_01", string tableName = "timelog_data_test")
    {
        using var conn = new SqliteConnection(_connString);
        await conn.OpenAsync();

        // 1. Populate well metadata
        await conn.ExecuteAsync(
            "INSERT OR REPLACE INTO VMX_WELL (WELL_ID, WELL_NAME, FIELD) VALUES (@wellId, 'Offshore Bravo-1', 'Gulf Block 42');",
            new { wellId });

        await conn.ExecuteAsync(
            "INSERT OR REPLACE INTO VMX_WELLBORE (WELLBORE_ID, WELL_ID, WELLBORE_NAME) VALUES ('WB_01', @wellId, 'Wellbore 1');",
            new { wellId });

        DateTime baseTime = new DateTime(2026, 3, 15, 8, 0, 0);
        string minDate = baseTime.ToString("dd-MMM-yyyy HH:mm:ss");
        string maxDate = baseTime.AddHours(4).ToString("dd-MMM-yyyy HH:mm:ss");

        await conn.ExecuteAsync(@"
            INSERT OR REPLACE INTO VMX_TIME_LOG (LOG_ID, WELL_ID, WELLBORE_ID, LOG_NAME, DATA_TABLE_NAME, PRIMARY_LOG, MIN_DATE, MAX_DATE)
            VALUES ('TL_01', @wellId, 'WB_01', 'Primary 1-Sec Telemetry', @tableName, 1, @minDate, @maxDate);",
            new { wellId, tableName, minDate, maxDate });

        // 2. Create raw telemetry table with DATETIME, DEPTH, HDTH, RIG_STATE, TIME_DURATION
        await conn.ExecuteAsync($@"
            CREATE TABLE IF NOT EXISTS [{tableName}] (
                DATETIME TEXT PRIMARY KEY,
                DEPTH REAL,
                HDTH REAL,
                RIG_STATE REAL,
                TIME_DURATION REAL
            );");

        // 3. Insert simulated operational sequence:
        //    a) Rig Up (Rig State = 12 / In slips / Connection) for 30 min
        //    b) Drilling continuous > 15 min (60 minutes)
        //    c) Hole cleaning / CUC (30 minutes)
        DateTime cur = baseTime;
        double depth = 1000.0;
        double holeDepth = 1000.0;

        // a) Rig Up (30 minutes)
        for (int i = 0; i < 30; i++)
        {
            await conn.ExecuteAsync(
                $"INSERT INTO [{tableName}] (DATETIME, DEPTH, HDTH, RIG_STATE, TIME_DURATION) VALUES (@dt, @d, @hd, 12, 60);",
                new { dt = cur.ToString("dd-MMM-yyyy HH:mm:ss"), d = depth, hd = holeDepth });
            cur = cur.AddMinutes(1);
        }

        // b) Drilling continuous > 15 min (60 minutes)
        for (int i = 0; i < 60; i++)
        {
            depth += 2.0;
            holeDepth = depth;
            await conn.ExecuteAsync(
                $"INSERT INTO [{tableName}] (DATETIME, DEPTH, HDTH, RIG_STATE, TIME_DURATION) VALUES (@dt, @d, @hd, 0, 60);",
                new { dt = cur.ToString("dd-MMM-yyyy HH:mm:ss"), d = depth, hd = holeDepth });
            cur = cur.AddMinutes(1);
        }

        // c) Hole Cleaning / CUC (30 minutes)
        for (int i = 0; i < 30; i++)
        {
            await conn.ExecuteAsync(
                $"INSERT INTO [{tableName}] (DATETIME, DEPTH, HDTH, RIG_STATE, TIME_DURATION) VALUES (@dt, @d, @hd, 2, 60);",
                new { dt = cur.ToString("dd-MMM-yyyy HH:mm:ss"), d = depth - 10, hd = holeDepth });
            cur = cur.AddMinutes(1);
        }
    }

    [Fact]
    public async Task AutoTagCreationProcess_ExecutesSuccessfully_CreatesPhaseListAndActivityColumn()
    {
        string wellId = "WELL_AUTOTAG_01";
        string tableName = "timelog_telemetry_01";
        await SeedWellAndTelemetryAsync(wellId, tableName);

        var process = new AutoTagCreationProcess
        {
            objDataService = _dataService,
            WellID = wellId
        };

        double reportedPercent = 0;
        string reportedStatus = "";
        var progressPercent = new Progress<double>(p => reportedPercent = p);
        var progressStatus = new Progress<string>(s => reportedStatus = s);

        bool success = await process.ExecuteAsync(progressPercent, progressStatus);

        Assert.True(success);
        Assert.True(process.ProcessCompleted);
        Assert.True(process.DetectedTagsCount > 0, $"Expected at least one operational tag to be detected. LastError: {process.LastError}");
        Assert.True(reportedPercent >= 100.0);

        // Verify VMX_PHASE_LIST table exists and contains records matching schema
        using var conn = new SqliteConnection(_connString);
        await conn.OpenAsync();

        var tagCount = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM VMX_PHASE_LIST WHERE WELL_ID=@wellId;", new { wellId });
        Assert.True(tagCount > 0, "VMX_PHASE_LIST should contain detected phase records.");

        var firstTag = await conn.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM VMX_PHASE_LIST WHERE WELL_ID=@wellId ORDER BY START_DATE ASC LIMIT 1;", new { wellId });
        Assert.NotNull(firstTag);
        Assert.Equal(wellId, (string)firstTag.WELL_ID);
        Assert.False(string.IsNullOrWhiteSpace((string)firstTag.PHASE_ID));
        Assert.NotNull(firstTag.START_DATE);
        Assert.NotNull(firstTag.END_DATE);

        // Verify that all created tags have strictly valid, non-inverted, positive durations
        var allTags = (await conn.QueryAsync<dynamic>(
            "SELECT * FROM VMX_PHASE_LIST WHERE WELL_ID=@wellId;", new { wellId })).ToList();
        foreach (var tag in allTags)
        {
            DateTime s = DateTime.ParseExact((string)tag.START_DATE, "dd-MMM-yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            DateTime e = DateTime.ParseExact((string)tag.END_DATE, "dd-MMM-yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
            Assert.True(s < e, $"Tag START_DATE ({s}) must be strictly earlier than END_DATE ({e})");
        }

        // Verify ACTIVITY column was added to the timelog data table
        var hasActivityCol = await conn.ExecuteScalarAsync<int>(
            $"SELECT COUNT(*) FROM pragma_table_info('{tableName}') WHERE name='ACTIVITY';");
        Assert.Equal(1, hasActivityCol);

        // Verify web-application task tracking tables (eVuMaxWellTaskTracking & eVuMaxTasks) do NOT exist
        var hasTaskTrackingTable = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='eVuMaxWellTaskTracking';");
        Assert.Equal(0, hasTaskTrackingTable);

        var hasTasksTable = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='eVuMaxTasks';");
        Assert.Equal(0, hasTasksTable);
    }

    [Fact]
    public async Task ProcessTagCreationViewModel_InitializesAndStartsDetection()
    {
        string wellId = "WELL_VM_01";
        string tableName = "timelog_vm_01";
        await SeedWellAndTelemetryAsync(wellId, tableName);

        _session.Load(_tempDbPath);

        var vm = new ProcessTagCreationViewModel(_session);

        Assert.Equal(wellId, vm.WellId);
        Assert.Equal("Offshore Bravo-1", vm.WellName);
        Assert.Equal("Primary 1-Sec Telemetry", vm.LogName);
        Assert.Equal(tableName, vm.DataTableName);
        Assert.True(vm.CanStart);
        Assert.Equal("Cancel", vm.CancelButtonText);

        await vm.StartCommand.ExecuteAsync(null);

        Assert.True(vm.IsCompleted);
        Assert.False(vm.HasError);
        Assert.False(vm.IsRunning);
        Assert.True(vm.DetectedTagsCount > 0);
        Assert.True(vm.ExistingTagsCount > 0);
        Assert.Equal("Close", vm.CancelButtonText);
    }

    [Fact]
    public async Task MainViewModel_ProcessTagCreationCommand_HonorsCanExecute_AndTriggersDialogHandler()
    {
        string wellId = "WELL_MAIN_VM_01";
        string tableName = "timelog_main_01";
        await SeedWellAndTelemetryAsync(wellId, tableName);

        var dummyProjectService = new DummyProjectService();
        var mainVm = new MainViewModel(_session, dummyProjectService);

        // Initially no project is open
        Assert.False(mainVm.IsProjectOpen);
        Assert.False(mainVm.ProcessTagCreationCommand.CanExecute(null));

        // Open project session
        _session.Load(_tempDbPath);

        Assert.True(mainVm.IsProjectOpen);
        Assert.True(mainVm.ProcessTagCreationCommand.CanExecute(null));

        bool dialogHandlerInvoked = false;
        mainVm.OpenProcessTagCreationDialogHandler = vm =>
        {
            dialogHandlerInvoked = true;
            Assert.Equal(wellId, vm.WellId);
            Assert.Equal(tableName, vm.DataTableName);
            return true;
        };

        await mainVm.ProcessTagCreationCommand.ExecuteAsync(null);

        Assert.True(dialogHandlerInvoked, "Expected OpenProcessTagCreationDialogHandler to be invoked.");
    }

    [Fact]
    public async Task AutoTagCreationProcess_EmptyTagDetectionSettings_SeedsDefaultValues()
    {
        string wellId = "WELL_SETTINGS_01";
        string tableName = "timelog_settings_01";
        await SeedWellAndTelemetryAsync(wellId, tableName);

        using (var conn = new SqliteConnection(_connString))
        {
            await conn.OpenAsync();
            // Explicitly create empty eVuMaxTagDetectionSettings table
            await conn.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS eVuMaxTagDetectionSettings (
                    StandHeight REAL DEFAULT 120,
                    DrillingTimeLimit REAL DEFAULT 900,
                    DepthThreshold REAL DEFAULT 300
                );
                DELETE FROM eVuMaxTagDetectionSettings;");
        }

        var process = new AutoTagCreationProcess
        {
            objDataService = _dataService,
            WellID = wellId
        };

        bool success = await process.ExecuteAsync();
        Assert.True(success);

        // Verify that default values were inserted into eVuMaxTagDetectionSettings
        using (var conn = new SqliteConnection(_connString))
        {
            await conn.OpenAsync();
            var row = await conn.QueryFirstOrDefaultAsync<dynamic>("SELECT * FROM eVuMaxTagDetectionSettings LIMIT 1;");
            Assert.NotNull(row);
            Assert.Equal(120.0, (double)row.StandHeight);
            Assert.Equal(900.0, (double)row.DrillingTimeLimit);
            Assert.Equal(300.0, (double)row.DepthThreshold);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenClearExistingTagsIsTrue_RemovesPriorTagsBeforeCreatingFreshTags()
    {
        string wellId = "WELL_CLEAR_TEST";
        string tableName = "timelog_clear_test";
        await SeedWellAndTelemetryAsync(wellId, tableName);

        // Pre-insert an obsolete orphan tag
        using (var conn = new SqliteConnection(_connString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync(@"
                INSERT INTO VMX_PHASE_LIST (PHASE_INDEX, WELL_ID, PHASE_ID, STEP_ID, EMPH_ID, START_DATE, END_DATE)
                VALUES (999999, @wellId, 'OBSOLETE', 'OLD', '', '2023-01-01 00:00:00', '2023-01-02 00:00:00');",
                new { wellId });
        }

        var process = new AutoTagCreationProcess
        {
            objDataService = _dataService,
            WellID = wellId,
            ClearExistingTags = true
        };

        bool success = await process.ExecuteAsync();
        Assert.True(success);

        using (var conn = new SqliteConnection(_connString))
        {
            await conn.OpenAsync();
            var tags = (await conn.QueryAsync<dynamic>("SELECT * FROM VMX_PHASE_LIST WHERE WELL_ID = @wellId;", new { wellId })).ToList();
            
            // Obsolete tag was removed
            Assert.DoesNotContain(tags, t => (long)t.PHASE_INDEX == 999999);
            // New tags were cleanly inserted
            Assert.True(tags.Count > 0);
        }
    }
}

