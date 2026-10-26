using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DrillIntel.Data;
using DrillIntel.Models.TagVisualizer;
using DrillIntel.ViewModels;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DrillIntel.Tests;

public class TagVisualizerTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly string _connString;

    public TagVisualizerTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"tag_visualizer_test_{Guid.NewGuid():N}.sqlite");
        _connString = $"Data Source={_tempDbPath}";
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_tempDbPath))
                File.Delete(_tempDbPath);
        }
        catch { }
    }

    [Fact]
    public void Initialize_WhenNoDatabase_LoadsRealisticMockDataset()
    {
        // Parameterless constructor initializes realistic 24-hr multi-phase mock telemetry
        var vm = new TagVisualizerViewModel();

        Assert.NotNull(vm.Tags);
        Assert.Equal(7, vm.Tags.Count);
        Assert.NotEmpty(vm.DepthPoints);
        Assert.True(vm.DepthPoints.Count > 100);

        // Verify TagDisplayName formatting is Phase + Step + Emph
        var firstTag = vm.Tags[0];
        Assert.Equal("RIG_UP + NIPPLE_UP + BOP", firstTag.TagDisplayName);
        Assert.NotNull(firstTag.FillBrush);
        Assert.NotNull(firstTag.BorderBrush);

        var drillingTag = vm.Tags.FirstOrDefault(t => t.PhaseId == "DRILLING");
        Assert.NotNull(drillingTag);
        Assert.Contains("DRILLING + DRILLING + MAIN", drillingTag.TagDisplayName);

        // Verify min/max ranges
        Assert.True(vm.DataMaxDate > vm.DataMinDate);
        Assert.True(vm.DataMaxDepth > vm.DataMinDepth);
        Assert.NotNull(vm.SelectedTag);
    }

    [Fact]
    public void NudgeAdjustments_UpdateTagBoundariesAndInterpolateDepth()
    {
        var vm = new TagVisualizerViewModel();

        // Select the drilling tag
        var tag = vm.Tags.First(t => t.PhaseId == "DRILLING");
        vm.SelectedTag = tag;
        var originalStart = tag.StartDate;
        var originalEnd = tag.EndDate;
        var originalStartDepth = tag.StartDepth;

        // Nudge start forward by 15 minutes
        vm.NudgeStartTimeCommand.Execute(15);

        Assert.Equal(originalStart.AddMinutes(15), tag.StartDate);
        Assert.True(vm.HasUnsavedChanges);
        // Start depth should be updated based on telemetry curve
        Assert.True(tag.StartDepth >= originalStartDepth);

        // Nudge end back by 15 minutes
        vm.NudgeEndTimeCommand.Execute(-15);
        Assert.Equal(originalEnd.AddMinutes(-15), tag.EndDate);
    }

    [Fact]
    public void TagDisplayName_UpdatesDynamically_WhenMetadataChanges()
    {
        var vm = new TagVisualizerViewModel();

        var tag = vm.Tags[0];
        vm.SelectedTag = tag;

        tag.PhaseId = "CASING";
        tag.StepId = "RUN_CASING";
        tag.EmphId = "PRODUCTION";

        Assert.Equal("CASING + RUN_CASING + PRODUCTION", tag.TagDisplayName);
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void SplitTag_DividesTagIntoTwoEqualHalves()
    {
        var vm = new TagVisualizerViewModel();

        int initialCount = vm.Tags.Count;
        var tagToSplit = vm.Tags[2];
        var originalMinutes = tagToSplit.Duration.TotalMinutes;
        vm.SelectedTag = tagToSplit;

        vm.SplitSelectedTagCommand.Execute(null);

        Assert.Equal(initialCount + 1, vm.Tags.Count);
        Assert.NotNull(vm.SelectedTag);
        Assert.True(vm.HasUnsavedChanges);

        // Half 1 should end where Half 2 starts
        int splitIndex = vm.Tags.IndexOf(tagToSplit);
        var firstHalf = vm.Tags[splitIndex];
        var secondHalf = vm.Tags[splitIndex + 1];

        Assert.Equal(firstHalf.EndDate, secondHalf.StartDate);
        Assert.True(Math.Abs(firstHalf.Duration.TotalMinutes - (originalMinutes / 2.0)) <= 1.0);
    }

    [Fact]
    public void MergeWithNext_CombinesAdjacentTags()
    {
        var vm = new TagVisualizerViewModel();

        int initialCount = vm.Tags.Count;
        var tag1 = vm.Tags[0];
        var tag2 = vm.Tags[1];
        var targetEnd = tag2.EndDate;

        vm.SelectedTag = tag1;
        vm.MergeWithNextTagCommand.Execute(null);

        Assert.Equal(initialCount - 1, vm.Tags.Count);
        Assert.Equal(targetEnd, tag1.EndDate);
        Assert.DoesNotContain(tag2, vm.Tags);
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public void DeleteSelectedTag_RemovesSelectedTag()
    {
        var vm = new TagVisualizerViewModel();

        int initialCount = vm.Tags.Count;
        var tagToDelete = vm.Tags[1];
        vm.SelectedTag = tagToDelete;

        vm.DeleteSelectedTagCommand.Execute(null);

        Assert.Equal(initialCount - 1, vm.Tags.Count);
        Assert.DoesNotContain(tagToDelete, vm.Tags);
        Assert.True(vm.HasUnsavedChanges);
    }

    [Fact]
    public async Task SaveToDatabaseAsync_PersistsAdjustmentsToVmxPhaseList()
    {
        // 1. Prepare SQLite schema with VMX_PHASE_LIST and VMX_WELL
        using (var conn = new SqliteConnection(_connString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync(@"
                CREATE TABLE IF NOT EXISTS VMX_WELL (
                    WELL_ID TEXT PRIMARY KEY,
                    WELL_NAME TEXT
                );
                INSERT INTO VMX_WELL (WELL_ID, WELL_NAME) VALUES ('WELL_PERSIST_TEST', 'Test Offshore Well 01');

                CREATE TABLE IF NOT EXISTS VMX_PHASE_LIST (
                    PHASE_INDEX REAL NOT NULL,
                    WELL_ID TEXT NOT NULL,
                    START_DATE TEXT NOT NULL,
                    END_DATE TEXT NOT NULL,
                    PHASE_ID TEXT NOT NULL,
                    STEP_ID TEXT NOT NULL,
                    EMPH_ID TEXT NOT NULL,
                    NOTES TEXT,
                    CREATED_BY TEXT,
                    CREATED_DATE TEXT,
                    MODIFIED_BY TEXT,
                    MODIFIED_DATE TEXT,
                    IS_OPEN REAL,
                    PRIMARY KEY (PHASE_INDEX, WELL_ID)
                );
                INSERT INTO VMX_PHASE_LIST (PHASE_INDEX, WELL_ID, START_DATE, END_DATE, PHASE_ID, STEP_ID, EMPH_ID, NOTES) VALUES
                (101, 'WELL_PERSIST_TEST', '15-Mar-2026 08:00:00', '15-Mar-2026 12:00:00', 'DRILLING', 'DRILLING', 'MAIN', 'Original auto tag');
            ");
        }

        string wellId = "WELL_PERSIST_TEST";
        using var ds = new DataServiceDIntel(_tempDbPath);

        // 2. Initialize ViewModel with data service
        var vm = new TagVisualizerViewModel(ds, wellId);

        // Loads real tag from VMX_PHASE_LIST
        Assert.Single(vm.Tags);

        // Make an adjustment
        var first = vm.Tags[0];
        first.PhaseId = "RIG_UP";
        first.StepId = "RIG_MOB";
        first.EmphId = "PRIMARY";
        first.Notes = "Adjusted manually by engineer";

        // 3. Save
        await vm.SaveToDatabaseAsync();
        Assert.False(vm.HasUnsavedChanges);

        // 4. Verify directly in SQLite
        using (var conn = new SqliteConnection(_connString))
        {
            await conn.OpenAsync();
            var rows = (await conn.QueryAsync(@"
                SELECT PHASE_INDEX, WELL_ID, PHASE_ID, STEP_ID, EMPH_ID, NOTES 
                FROM VMX_PHASE_LIST 
                WHERE WELL_ID = @wellId 
                ORDER BY PHASE_INDEX ASC",
                new { wellId })).ToList();

            Assert.Single(rows);
            Assert.Equal("RIG_UP", (string)rows[0].PHASE_ID);
            Assert.Equal("RIG_MOB", (string)rows[0].STEP_ID);
            Assert.Equal("PRIMARY", (string)rows[0].EMPH_ID);
            Assert.Equal("Adjusted manually by engineer", (string)rows[0].NOTES);
        }

        // 5. Reload into a new ViewModel instance to confirm roundtrip loading
        var reloadVm = new TagVisualizerViewModel(ds, wellId);

        Assert.Single(reloadVm.Tags);
        Assert.Equal("RIG_UP", reloadVm.Tags[0].PhaseId);
        Assert.Equal("RIG_MOB", reloadVm.Tags[0].StepId);
        Assert.Equal("PRIMARY", reloadVm.Tags[0].EmphId);
        Assert.Equal("Adjusted manually by engineer", reloadVm.Tags[0].Notes);
    }

    [Fact]
    public void LoadFromDatabase_NonIsoDates_SortsChronologicallyAndDoesNotZigzag()
    {
        using (var conn = new SqliteConnection(_connString))
        {
            conn.Open();
            conn.Execute(@"
                CREATE TABLE IF NOT EXISTS VMX_WELL (
                    WELL_ID TEXT PRIMARY KEY,
                    WELL_NAME TEXT
                );
                INSERT OR REPLACE INTO VMX_WELL (WELL_ID, WELL_NAME) VALUES ('WELL_CHRONO', 'Etech420');

                CREATE TABLE IF NOT EXISTS VMX_TIME_LOG (
                    LOG_ID TEXT PRIMARY KEY,
                    WELL_ID TEXT,
                    WELLBORE_ID TEXT,
                    LOG_NAME TEXT,
                    DATA_TABLE_NAME TEXT,
                    PRIMARY_LOG REAL
                );
                INSERT OR REPLACE INTO VMX_TIME_LOG (LOG_ID, WELL_ID, WELLBORE_ID, LOG_NAME, DATA_TABLE_NAME, PRIMARY_LOG)
                VALUES ('TL_CHRONO', 'WELL_CHRONO', 'WB_01', 'TimeLog5Sec', 'telemetry_chrono', 1);

                CREATE TABLE IF NOT EXISTS [telemetry_chrono] (
                    DATETIME TEXT,
                    DEPTH REAL,
                    HDTH REAL
                );

                CREATE TABLE IF NOT EXISTS VMX_PHASE_LIST (
                    PHASE_INDEX REAL NOT NULL,
                    WELL_ID TEXT NOT NULL,
                    START_DATE TEXT NOT NULL,
                    END_DATE TEXT NOT NULL,
                    PHASE_ID TEXT NOT NULL,
                    STEP_ID TEXT NOT NULL,
                    EMPH_ID TEXT NOT NULL,
                    NOTES TEXT,
                    PRIMARY KEY (PHASE_INDEX, WELL_ID)
                );
            ");

            // Insert non-ISO formatted telemetry in chronological order:
            // 26-Mar-2013, 01-Apr-2013, 13-May-2013, 12-Sep-2013
            // Note: Lexicographically, '01-Apr' < '12-Sep' < '13-May' < '26-Mar'
            conn.Execute("INSERT INTO [telemetry_chrono] VALUES ('26-Mar-2013 15:57:00', 100.0, 100.0);");
            conn.Execute("INSERT INTO [telemetry_chrono] VALUES ('01-Apr-2013 10:00:00', 500.0, 500.0);");
            conn.Execute("INSERT INTO [telemetry_chrono] VALUES ('13-May-2013 12:00:00', 2500.0, 2500.0);");
            conn.Execute("INSERT INTO [telemetry_chrono] VALUES ('12-Sep-2013 13:14:00', 5000.0, 5000.0);");

            // Insert inverted or non-ISO tags
            conn.Execute("INSERT INTO VMX_PHASE_LIST (PHASE_INDEX, WELL_ID, START_DATE, END_DATE, PHASE_ID, STEP_ID, EMPH_ID) VALUES (1, 'WELL_CHRONO', '26-Mar-2013 15:57:00', '01-Apr-2013 10:00:00', 'DRILLING', 'DRILLING', 'MAIN');");
            conn.Execute("INSERT INTO VMX_PHASE_LIST (PHASE_INDEX, WELL_ID, START_DATE, END_DATE, PHASE_ID, STEP_ID, EMPH_ID) VALUES (2, 'WELL_CHRONO', '01-Apr-2013 10:00:00', '13-May-2013 12:00:00', 'TRIP_OUT', 'TRIP_OUT', '');");
        }

        using var ds = new DataServiceDIntel(_tempDbPath);
        var vm = new TagVisualizerViewModel(ds, "WELL_CHRONO");

        // Verify telemetry points are sorted strictly chronologically (not alphabetical)
        Assert.Equal(4, vm.DepthPoints.Count);
        for (int i = 0; i < vm.DepthPoints.Count - 1; i++)
        {
            Assert.True(vm.DepthPoints[i].DateTime <= vm.DepthPoints[i + 1].DateTime, 
                $"DepthPoint[{i}] ({vm.DepthPoints[i].DateTime}) must be <= DepthPoint[{i+1}] ({vm.DepthPoints[i+1].DateTime})");
        }

        // Verify DataMinDate is 26-Mar-2013 and DataMaxDate is 12-Sep-2013
        Assert.Equal(new DateTime(2013, 3, 26, 15, 57, 0), vm.DataMinDate);
        Assert.Equal(new DateTime(2013, 9, 12, 13, 14, 0), vm.DataMaxDate);

        // Verify Phase tags are loaded and positive duration
        Assert.Equal(2, vm.Tags.Count);
        Assert.True(vm.Tags[0].StartDate < vm.Tags[0].EndDate);
        Assert.True(vm.Tags[1].StartDate < vm.Tags[1].EndDate);
        Assert.True(vm.Tags[0].StartDate < vm.Tags[1].StartDate);
    }

    [Fact]
    public void DataSelector_TrackBarPropertiesAndCommands_SynchronizeViewTimeline()
    {
        var vm = new TagVisualizerViewModel();

        // 1. Verify TrackBar bounds are populated from data
        Assert.True(vm.TrackBarMax > vm.TrackBarMin);
        Assert.NotNull(vm.TrackBarOverviewPoints);
        Assert.NotEmpty(vm.TrackBarOverviewPoints);
        Assert.False(string.IsNullOrWhiteSpace(vm.FromDateTimeText));
        Assert.False(string.IsNullOrWhiteSpace(vm.ToDateTimeText));

        // 2. Changing SelectedPreset updates ViewMinDate and ViewMaxDate
        vm.SelectedPreset = "1 Hour";
        var spanHours = (vm.ViewMaxDate - vm.ViewMinDate).TotalHours;
        Assert.True(Math.Abs(spanHours - 1.0) < 0.1);

        // 3. ZoomToSelectedTag frames the active tag
        var tag = vm.Tags[1]; // Drilling
        vm.SelectedTag = tag;
        vm.ZoomToSelectedTagCommand.Execute(null);

        Assert.True(vm.ViewMinDate <= tag.StartDate);
        Assert.True(vm.ViewMaxDate >= tag.EndDate);

        // 4. Manual From/To text editing with Apply command
        DateTime targetStart = tag.StartDate.AddMinutes(-10);
        DateTime targetEnd = tag.EndDate.AddMinutes(10);
        vm.FromDateTimeText = targetStart.ToString("dd-MM-yyyy HH:mm");
        vm.ToDateTimeText = targetEnd.ToString("dd-MM-yyyy HH:mm");
        vm.ApplyDateTimeRangeCommand.Execute(null);

        Assert.Equal(targetStart.Year, vm.ViewMinDate.Year);
        Assert.Equal(targetStart.Month, vm.ViewMinDate.Month);
        Assert.Equal(targetStart.Day, vm.ViewMinDate.Day);
        Assert.Equal(targetStart.Hour, vm.ViewMinDate.Hour);
        Assert.Equal(targetStart.Minute, vm.ViewMinDate.Minute);

        // 5. ResetZoom restores full range
        vm.ResetZoomCommand.Execute(null);
        Assert.Equal(vm.DataMinDate, vm.ViewMinDate);
        Assert.Equal(vm.DataMaxDate, vm.ViewMaxDate);
        Assert.Equal("Full Well", vm.SelectedPreset);
    }

    [Fact]
    public void NudgeStartTime_SynchronizesPreviousTagEndDate()
    {
        var vm = new TagVisualizerViewModel();
        Assert.True(vm.Tags.Count >= 3);

        // Select Tag 1 (Drilling)
        var prevTag = vm.Tags[0];
        var currentTag = vm.Tags[1];
        vm.SelectedTag = currentTag;

        // Verify initial contiguous boundary
        Assert.Equal(prevTag.EndDate, currentTag.StartDate);

        // Nudge Start Time forward by 15 minutes
        vm.NudgeStartTimeCommand.Execute(15);

        // Current tag start moved forward
        Assert.Equal(prevTag.EndDate, currentTag.StartDate);
        Assert.Equal(prevTag.EndDepth, currentTag.StartDepth);
        Assert.True(prevTag.IsModified);
        Assert.True(currentTag.IsModified);
        Assert.True(vm.HasUnsavedChanges);

        // Nudge Start Time backward by 15 minutes
        vm.NudgeStartTimeCommand.Execute("-15");

        Assert.Equal(prevTag.EndDate, currentTag.StartDate);
        Assert.Equal(prevTag.EndDepth, currentTag.StartDepth);
    }

    [Fact]
    public void NudgeEndTime_SynchronizesNextTagStartDate()
    {
        var vm = new TagVisualizerViewModel();
        Assert.True(vm.Tags.Count >= 3);

        // Select Tag 1 (Drilling)
        var currentTag = vm.Tags[1];
        var nextTag = vm.Tags[2];
        vm.SelectedTag = currentTag;

        // Verify initial contiguous boundary
        Assert.Equal(currentTag.EndDate, nextTag.StartDate);

        // Nudge End Time backward by 15 minutes
        vm.NudgeEndTimeCommand.Execute(-15);

        // Next tag start moved backward to match
        Assert.Equal(currentTag.EndDate, nextTag.StartDate);
        Assert.Equal(currentTag.EndDepth, nextTag.StartDepth);
        Assert.True(currentTag.IsModified);
        Assert.True(nextTag.IsModified);
        Assert.True(vm.HasUnsavedChanges);

        // Nudge End Time forward by 15 minutes
        vm.NudgeEndTimeCommand.Execute("15");

        Assert.Equal(currentTag.EndDate, nextTag.StartDate);
        Assert.Equal(currentTag.EndDepth, nextTag.StartDepth);
    }

    [Fact]
    public void AdjustTagStartDate_ClampsToPreventShrinkingPreviousTagToZero()
    {
        var vm = new TagVisualizerViewModel();
        var prevTag = vm.Tags[0];
        var currentTag = vm.Tags[1];

        // Attempt to move start date earlier than previous tag's start date
        DateTime invalidEarlierDate = prevTag.StartDate.AddHours(-5);
        vm.AdjustTagStartDate(currentTag, invalidEarlierDate);

        // Clamped so previous tag maintains positive duration (at least 30 seconds)
        Assert.True(currentTag.StartDate >= prevTag.StartDate.AddSeconds(30));
        Assert.Equal(prevTag.EndDate, currentTag.StartDate);
        Assert.True(prevTag.Duration.TotalSeconds >= 30);
    }

    [Fact]
    public void AdjustTagEndDate_ClampsToPreventShrinkingNextTagToZero()
    {
        var vm = new TagVisualizerViewModel();
        var currentTag = vm.Tags[1];
        var nextTag = vm.Tags[2];

        // Attempt to move end date later than next tag's end date
        DateTime invalidLaterDate = nextTag.EndDate.AddHours(5);
        vm.AdjustTagEndDate(currentTag, invalidLaterDate);

        // Clamped so next tag maintains positive duration (at least 30 seconds)
        Assert.True(currentTag.EndDate <= nextTag.EndDate.AddSeconds(-30));
        Assert.Equal(nextTag.StartDate, currentTag.EndDate);
        Assert.True(nextTag.Duration.TotalSeconds >= 30);
    }

    [Fact]
    public void ApplySelectedTagStartAndEndTime_ParsesTextAndSynchronizesAdjacentTags()
    {
        var vm = new TagVisualizerViewModel();
        var prevTag = vm.Tags[0];
        var currentTag = vm.Tags[1];
        var nextTag = vm.Tags[2];
        vm.SelectedTag = currentTag;

        // Apply new Start Date via text box
        DateTime newStart = currentTag.StartDate.AddMinutes(10);
        vm.SelectedTagStartDateText = newStart.ToString("dd-MMM-yyyy HH:mm:ss");
        vm.ApplySelectedTagStartTimeCommand.Execute(null);

        Assert.Equal(newStart, currentTag.StartDate);
        Assert.Equal(newStart, prevTag.EndDate);

        // Apply new End Date via text box
        DateTime newEnd = currentTag.EndDate.AddMinutes(-10);
        vm.SelectedTagEndDateText = newEnd.ToString("dd-MMM-yyyy HH:mm:ss");
        vm.ApplySelectedTagEndTimeCommand.Execute(null);

        Assert.Equal(newEnd, currentTag.EndDate);
        Assert.Equal(newEnd, nextTag.StartDate);
    }

    [Fact]
    public void OverlappingTags_SortedWithWiderTagFirst_EnsuresNarrowTagDrawnOnTop()
    {
        using var conn = new SqliteConnection(_connString);
        conn.Open();

        conn.Execute(@"
            CREATE TABLE IF NOT EXISTS VMX_PHASE_LIST (
                PHASE_INDEX INTEGER PRIMARY KEY,
                WELL_ID TEXT,
                PHASE_ID TEXT,
                STEP_ID TEXT,
                EMPH_ID TEXT,
                START_DATE TEXT,
                END_DATE TEXT,
                START_DEPTH REAL,
                END_DEPTH REAL,
                NOTES TEXT,
                CREATED_BY TEXT,
                CREATED_DATE TEXT,
                MODIFIED_BY TEXT,
                MODIFIED_DATE TEXT,
                IS_OPEN INTEGER DEFAULT 0
            );");

        // Insert a 10-day DRILLING tag and a nested 1-day TRIP_OUT tag
        conn.Execute(@"
            INSERT INTO VMX_PHASE_LIST (PHASE_INDEX, WELL_ID, PHASE_ID, STEP_ID, EMPH_ID, START_DATE, END_DATE)
            VALUES 
                (1, 'WELL_TEST', 'DRILLING', 'DRILLING', 'MAIN', '2013-03-21 00:00:00', '2013-03-31 00:00:00'),
                (2, 'WELL_TEST', 'TRIP_OUT', 'TRIP_OUT', '', '2013-03-27 00:00:00', '2013-03-28 00:00:00');");

        var dataService = new DataServiceDIntel(_tempDbPath);
        var vm = new TagVisualizerViewModel(dataService, "WELL_TEST");

        Assert.Equal(2, vm.Tags.Count);
        // The 10-day background tag (DRILLING) is first; the narrow foreground tag (TRIP_OUT) is second (drawn on top)
        Assert.Equal("DRILLING", vm.Tags[0].PhaseId);
        Assert.Equal("TRIP_OUT", vm.Tags[1].PhaseId);
    }
}
