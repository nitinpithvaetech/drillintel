using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor;
using DrillIntel.Models.Broomstick;
using DrillIntel.Projects;
using DrillIntel.ViewModels.Broomstick;
using Xunit;

namespace DrillIntel.Tests;

public class BroomstickDocumentViewModelTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ProjectSession _session;
    private readonly BroomstickTemplateRepository _repo;

    public BroomstickDocumentViewModelTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"drillintel_bs_doc_vm_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(_tempDbPath);
        _session = new ProjectSession();
        _session.Load(_tempDbPath);
        _repo = new BroomstickTemplateRepository(_session);
    }

    public void Dispose()
    {
        _session.Close();
        if (File.Exists(_tempDbPath))
        {
            try { File.Delete(_tempDbPath); } catch { }
        }
    }

    [Fact]
    public void Construction_And_PropertySync_WorksAsExpected()
    {
        var doc = new BroomstickDocument
        {
            TemplateId = "tpl-init",
            DocumentName = "8.5\" Hookload Drilling",
            WellId = "W-1",
            PlanId = "P-1",
            PlotSettings = new BroomstickPlotSettings
            {
                InvertDepthAxis = true,
                HighlightFormationTop = false,
                UnitSystem = "klbs"
            }
        };

        var vm = new BroomstickDocumentViewModel(_session, _repo, null, doc);

        Assert.Equal("8.5\" Hookload Drilling", vm.DocumentName);
        Assert.Equal("tpl-init", vm.CurrentTemplateId);
        Assert.Equal("W-1", vm.WellId);
        Assert.True(vm.InvertDepthAxis);
        Assert.False(vm.HighlightFormationTop);
        Assert.False(vm.IsDirty);
        Assert.True(vm.IsTimeLog);
    }

    [Fact]
    public void TogglingPlotSettings_SetsDirtyAndFiresRepaint()
    {
        var vm = new BroomstickDocumentViewModel(_session, _repo);
        bool repainted = false;
        vm.ChartNeedsRepaint += () => repainted = true;

        vm.HighlightFormationTop = true;
        Assert.True(vm.IsDirty);
        Assert.True(vm.Document.PlotSettings.HighlightFormationTop);
        Assert.True(repainted);

        repainted = false;
        vm.ViewCasing = true;
        Assert.True(vm.Document.PlotSettings.ViewCasing);
        Assert.True(repainted);
    }

    [Fact]
    public void RangeTrackBar_AppliesFilteringOnPoints_DepthMode()
    {
        var doc = new BroomstickDocument();
        doc.BroomstickPointData = new List<PUSOPoint>
        {
            new PUSOPoint { Depth = 500, DynamicValue = 100, UserVisible = true },
            new PUSOPoint { Depth = 1500, DynamicValue = 150, UserVisible = true },
            new PUSOPoint { Depth = 2500, DynamicValue = 200, UserVisible = true },
            new PUSOPoint { Depth = 3500, DynamicValue = 250, UserVisible = true }
        };

        var vm = new BroomstickDocumentViewModel(_session, _repo, null, doc);
        vm.IsTimeLog = false;
        vm.TrackBarSelectionStart = 1000;
        vm.TrackBarSelectionEnd = 3000;

        vm.RangeTrackBarChangedCommand.Execute(null);

        Assert.Equal(2, vm.FilteredPoints.Count);
        Assert.Equal(1500, vm.FilteredPoints[0].Depth);
        Assert.Equal(2500, vm.FilteredPoints[1].Depth);
        Assert.Equal(2, vm.LoadedPointsCount);
    }

    [Fact]
    public void RangeTrackBar_AppliesFilteringOnPoints_DateTimeMode()
    {
        var baseDate = new DateTime(2026, 10, 20, 8, 0, 0);
        var doc = new BroomstickDocument();
        doc.BroomstickPointData = new List<PUSOPoint>
        {
            new PUSOPoint { Depth = 500, StartDate = baseDate.AddHours(1), DynamicValue = 100, UserVisible = true },
            new PUSOPoint { Depth = 1500, StartDate = baseDate.AddHours(5), DynamicValue = 150, UserVisible = true },
            new PUSOPoint { Depth = 2500, StartDate = baseDate.AddHours(10), DynamicValue = 200, UserVisible = true },
            new PUSOPoint { Depth = 3500, StartDate = baseDate.AddHours(20), DynamicValue = 250, UserVisible = true }
        };

        var vm = new BroomstickDocumentViewModel(_session, _repo, null, doc);
        vm.IsTimeLog = true;
        vm.TrackBarSelectionStart = baseDate.AddHours(4).ToOADate();
        vm.TrackBarSelectionEnd = baseDate.AddHours(12).ToOADate();

        vm.RangeTrackBarChangedCommand.Execute(null);

        Assert.Equal(2, vm.FilteredPoints.Count);
        Assert.Equal(1500, vm.FilteredPoints[0].Depth);
        Assert.Equal(2500, vm.FilteredPoints[1].Depth);
    }

    [Fact]
    public async Task LoadTrackBarOverviewAsync_CreatesDepthVsDateTimeGraph()
    {
        var baseDate = new DateTime(2026, 10, 20, 0, 0, 0);
        var doc = new BroomstickDocument();
        doc.BroomstickPointData = new List<PUSOPoint>
        {
            new PUSOPoint { Depth = 1000, StartDate = baseDate.AddHours(2), UserVisible = true },
            new PUSOPoint { Depth = 2000, StartDate = baseDate.AddHours(6), UserVisible = true },
            new PUSOPoint { Depth = 3000, StartDate = baseDate.AddHours(10), UserVisible = true }
        };

        var vm = new BroomstickDocumentViewModel(_session, _repo, null, doc);
        await vm.LoadTrackBarOverviewAsync();

        Assert.True(vm.IsTimeLog);
        Assert.NotNull(vm.TrackBarOverviewPoints);
        Assert.True(vm.TrackBarOverviewPoints.Count >= 2);
        Assert.Contains("Depth", vm.TrackBarOverviewChannelName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SaveDocumentAsync_PersistsToBroomstickTemplateTable()
    {
        var doc = new BroomstickDocument
        {
            TemplateId = "tpl-save-test",
            DocumentName = "Saved Test Document",
            WellId = "W-TEST",
            PlanId = "P-TEST"
        };

        var vm = new BroomstickDocumentViewModel(_session, _repo, null, doc);
        vm.HighlightFormationTop = true;
        vm.InvertDepthAxis = true;

        await vm.SaveDocumentAsync();

        Assert.False(vm.IsDirty);

        var loaded = await _repo.GetTemplateByIdAsync("tpl-save-test");
        Assert.NotNull(loaded);
        Assert.Equal("Saved Test Document", loaded.DocumentName);
        Assert.Equal("W-TEST", loaded.WellId);
        Assert.Equal("P-TEST", loaded.PlanId);

        var restoredDoc = BroomstickDocument.DeserializeFromBlob(loaded.TemplateData);
        Assert.NotNull(restoredDoc);
        Assert.True(restoredDoc.PlotSettings.HighlightFormationTop);
    }

    [Fact]
    public async Task Workspace_DeleteDocumentAsync_RemovesDocument_ClosesTab_AndPromotesDefaultWithoutHanging()
    {
        // Seed two templates
        var tpl1 = new BroomstickTemplateItem
        {
            TemplateId = "doc-1",
            DocumentName = "Doc 1",
            WellId = "W1",
            PlanId = "P1",
            IsDefault = true
        };
        var tpl2 = new BroomstickTemplateItem
        {
            TemplateId = "doc-2",
            DocumentName = "Doc 2",
            WellId = "W1",
            PlanId = "P1",
            IsDefault = false
        };
        await _repo.SaveTemplateAsync(tpl1);
        await _repo.SaveTemplateAsync(tpl2);

        var wsVm = new BroomstickWorkspaceViewModel(_session, _repo, null)
        {
            ConfirmDeleteHandler = (title, msg) => true // Auto-confirm
        };
        await wsVm.InitializeAsync();

        Assert.Equal(2, wsVm.AllDocumentNodes.Count);

        // Find doc-1 (which is default and currently open)
        var node1 = wsVm.AllDocumentNodes.First(n => n.TemplateId == "doc-1");
        wsVm.SelectedTreeNode = node1;

        // Execute delete
        await wsVm.DeleteDocumentAsync(node1);

        // Assert doc-1 is removed
        Assert.Single(wsVm.AllDocumentNodes);
        Assert.Equal("doc-2", wsVm.AllDocumentNodes[0].TemplateId);
        Assert.Null(await _repo.GetTemplateByIdAsync("doc-1"));

        // Assert doc-2 is promoted to default
        Assert.True(wsVm.AllDocumentNodes[0].IsDefault);
        var repoDoc2 = await _repo.GetTemplateByIdAsync("doc-2");
        Assert.NotNull(repoDoc2);
        Assert.True(repoDoc2.IsDefault);
    }

    [Fact]
    public void DocumentViewModel_LegendTitlesAndSeriesVisibility_SyncProperly()
    {
        var doc = new BroomstickDocument { DocumentName = "8.5\" Hookload Drilling" };
        var vm = new BroomstickDocumentViewModel(_session, _repo, null, doc);

        // Verify initial legend titles
        vm.UpdateLegendTitles();
        Assert.Contains("0.2 OHFF", vm.Plan02LegendText);
        Assert.Contains("0.3 OHFF", vm.Plan03LegendText);
        Assert.Contains("0.4 OHFF", vm.Plan04LegendText);

        // Verify series visibility toggling
        Assert.True(vm.IsPlan03Visible);
        vm.IsPlan03Visible = false;
        Assert.False(vm.IsPlan03Visible);
        Assert.False(doc.PlotSettings.SeriesVisibility["plan03"]);

        Assert.True(vm.IsActualPuVisible);
        vm.IsActualPuVisible = false;
        Assert.False(vm.IsActualPuVisible);
        Assert.False(doc.PlotSettings.SeriesVisibility["actualPu"]);
    }

    [Fact]
    public void DrillingConnectionsProcessor_WithSplitDateColumns_ProcessesConnectionsSuccessfully()
    {
        var ds = _session.GetDataService();
        string tableName = "TestTL_" + Guid.NewGuid().ToString("N");
        ds.ExecuteNonQuery($"CREATE TABLE [{tableName}] (DATA_INDEX INTEGER PRIMARY KEY, DATE TEXT, TIME TEXT, DEPTH REAL, HDTH REAL, HKLD REAL, RIG_STATE INTEGER, TIME_DURATION REAL);");

        var baseDate = new DateTime(2013, 3, 21, 15, 10, 0);
        // Insert mock connection pattern: drilling (0) -> in slips (2) -> drilling (0)
        for (int i = 0; i < 20; i++)
        {
            var dt = baseDate.AddMinutes(i);
            int rigState = (i >= 5 && i <= 8) ? 2 : 0;
            double hkld = (i >= 5 && i <= 8) ? 20.0 : 120.0;
            ds.ExecuteNonQuery($"INSERT INTO [{tableName}] VALUES ({i + 1}, '{dt:dd-MMM-yyyy}', '{dt:HH:mm:ss}', 1000.0, 1000.0, {hkld}, {rigState}, 60.0);");
        }

        var logCapture = new List<string>();
        var testLogger = new DrillIntel.Data.Objects.DataObjects.Models.DrillingConnectionsProcessor.DefaultDrillingConnectionsLogger(msg => logCapture.Add(msg));

        var tl = new DrillIntel.Data.Objects.DataObjects.Models.TimeLog
        {
            WellID = "W-TEST-DC",
            __dataTableName = tableName
        };

        var proc = new DrillIntel.Data.Objects.DataObjects.Models.DrillingConnectionsProcessor.DrillingConnectionsProcessor
        {
            objLocalConn = ds,
            WellID = "W-TEST-DC",
            logger = testLogger
        };

        proc.ProcessPoints(tl, baseDate, baseDate.AddHours(1));

        Assert.True(logCapture.Any(m => m.Contains("Processing drilling connections")));
        Assert.True(proc.connectionPoints.Count > 0);
    }

    [Fact]
    public async Task BroomstickPoints_WhenSavedInDb_AreLoadedAndPlottedAcrossAllPointTypes()
    {
        // 1. Arrange SQLite In-Memory Database with VMX_BROOMSTICK_POINTS
        var ds = _session.GetDataService();
        ds.ExecuteNonQuery(@"
            CREATE TABLE IF NOT EXISTS VMX_BROOMSTICK_POINTS (
                WELL_ID TEXT NOT NULL,
                DEPTH REAL NOT NULL,
                TYPE INTEGER NOT NULL,
                START_DATE TEXT,
                END_DATE TEXT,
                DYNAMIC_VALUE REAL,
                AVG REAL,
                MIN REAL,
                MAX REAL,
                USER_VISIBLE INTEGER DEFAULT 1
            );
        ");

        ds.ExecuteNonQuery("DELETE FROM VMX_BROOMSTICK_POINTS;");

        // Insert Type 0 (PU), Type 1 (SO), Type 2 (Rotate) with well name mismatch
        ds.ExecuteNonQuery(@"
            INSERT INTO VMX_BROOMSTICK_POINTS (WELL_ID, DEPTH, TYPE, START_DATE, END_DATE, DYNAMIC_VALUE, AVG, MIN, MAX, USER_VISIBLE)
            VALUES ('Etech420-2', 5000.0, 0, '21-Mar-2013 15:10:00', '21-Mar-2013 15:12:00', 180.5, 178.0, 160.0, 195.0, 1);
            INSERT INTO VMX_BROOMSTICK_POINTS (WELL_ID, DEPTH, TYPE, START_DATE, END_DATE, DYNAMIC_VALUE, AVG, MIN, MAX, USER_VISIBLE)
            VALUES ('Etech420-2', 5050.0, 1, '21-Mar-2013 15:15:00', '21-Mar-2013 15:17:00', 110.2, 108.0, 95.0, 120.0, 1);
            INSERT INTO VMX_BROOMSTICK_POINTS (WELL_ID, DEPTH, TYPE, START_DATE, END_DATE, DYNAMIC_VALUE, AVG, MIN, MAX, USER_VISIBLE)
            VALUES ('Etech420-2', 5100.0, 2, '21-Mar-2013 15:20:00', '21-Mar-2013 15:22:00', 145.0, 144.0, 130.0, 150.0, 1);
        ");

        var doc = new BroomstickDocument
        {
            WellId = "14fa9c8e-0f60-46b3-9cfa-945c98ec3a38", // GUID differs from 'Etech420-2'
            DocumentName = "Test Combined Broomstick"
        };

        var vm = new BroomstickDocumentViewModel(_session, _repo, null, doc);
        vm.WellName = "Etech420-2";

        // 2. Act - Load points from DB (exercises well name fallback & VMX_BROOMSTICK_POINTS query)
        await vm.LoadBroomstickPointsFromDbAsync(ds);

        // 3. Assert - All 3 points loaded
        Assert.Equal(3, doc.BroomstickPointData.Count);

        // 4. Test Entire Log filtering
        vm.SelectedPreset = "Entire Log";
        vm.ApplyRangeFilter();
        Assert.Equal(3, vm.FilteredPoints.Count);

        // 5. Test Chart Renderer Value Resolution across point types
        var puPt = vm.FilteredPoints.First(p => p.Type == 0);
        var soPt = vm.FilteredPoints.First(p => p.Type == 1);
        var rotPt = vm.FilteredPoints.First(p => p.Type == 2);

        Assert.Equal(180.5, DrillIntel.Services.Charting.BroomstickChartRenderer.ResolvePointPlotValue(puPt));
        Assert.Equal(110.2, DrillIntel.Services.Charting.BroomstickChartRenderer.ResolvePointPlotValue(soPt));
        Assert.Equal(145.0, DrillIntel.Services.Charting.BroomstickChartRenderer.ResolvePointPlotValue(rotPt));
    }

    [Fact]
    public void ExtractMainPlanKey_GroupsFrictionFactorRunsUnderMainPlan()
    {
        // 1. Friction factor suffixes in plan name
        Assert.Equal("8.5\" Hookload Drilling", AdnlHookloadPlan.ExtractMainPlanKey("8.5\" Hookload Drilling 0.3 OHFF", ""));
        Assert.Equal("8.5\" Hookload Drilling", AdnlHookloadPlan.ExtractMainPlanKey("8.5\" Hookload Drilling 0.20 FF", ""));
        Assert.Equal("8.5\" Hookload Drilling", AdnlHookloadPlan.ExtractMainPlanKey("8.5\" Hookload Drilling", "0.4 OHFF"));
        Assert.Equal("WELLS Broomstick Plan Latest - OFFBottom", AdnlHookloadPlan.ExtractMainPlanKey("WELLS Broomstick Plan Latest - OFFBottom 0.2 OHFF", ""));

        // 2. Friction factor extraction
        Assert.Equal(0.3, AdnlHookloadPlan.ExtractFrictionFactor("8.5\" Hookload Drilling 0.3 OHFF"));
        Assert.Equal(0.25, AdnlHookloadPlan.ExtractFrictionFactor("0.25 OHFF"));

        // 3. Torque point value resolution
        var torquePt = new DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor.PUSOPoint
        {
            Type = 3,
            DynamicTorqueValue = 2850.5
        };
        Assert.Equal(2850.5, DrillIntel.Services.Charting.BroomstickChartRenderer.ResolvePointPlotValue(torquePt));
    }
}
