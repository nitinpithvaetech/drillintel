using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DrillIntel.Data;
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
        Assert.Contains("DEPTH", vm.TrackBarOverviewChannelName);
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
}

