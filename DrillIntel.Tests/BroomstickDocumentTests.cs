using System;
using System.Collections.Generic;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Models.Broomstick;
using DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor;
using Xunit;

namespace DrillIntel.Tests;

public class BroomstickDocumentTests
{
    [Fact]
    public void Serialization_RoundTrip_PreservesAllPropertiesAndProfile()
    {
        var doc = new BroomstickDocument
        {
            TemplateId = "tpl-test-123",
            DocumentName = "8.5\" Hookload Drilling",
            WellId = "W-420",
            WellboreId = "WB-1",
            LogId = "L-5SEC",
            PlanId = "PLAN-8.5",
            ProfileId = "PROF-HOOKLOAD",
            Description = "Custom Broomstick Setup",
            IsDefault = true,
            Profile = new BroomstickProfile
            {
                ID = "PROF-HOOKLOAD",
                Name = "Hookload Test Profile",
                FilterByRange = true,
                MinHooklaod = 10.0,
                MaxHookload = 280.0,
                DataPoints = 50,
                DownSampleOn = 0 // Depth
            },
            PlotSettings = new BroomstickPlotSettings
            {
                InvertDepthAxis = true,
                HighlightFormationTop = true,
                ViewCasing = true,
                UnitSystem = "klbs"
            }
        };

        doc.PlotSettings.SeriesVisibility["plan02"] = false;

        byte[] blob = doc.SerializeToBlob();
        Assert.NotNull(blob);
        Assert.True(blob.Length > 0);

        var restored = BroomstickDocument.DeserializeFromBlob(blob);
        Assert.NotNull(restored);
        Assert.Equal("tpl-test-123", restored.TemplateId);
        Assert.Equal("8.5\" Hookload Drilling", restored.DocumentName);
        Assert.Equal("W-420", restored.WellId);
        Assert.Equal("PLAN-8.5", restored.PlanId);
        Assert.Equal("PROF-HOOKLOAD", restored.ProfileId);
        Assert.True(restored.IsDefault);
        Assert.True(restored.Profile.FilterByRange);
        Assert.Equal(10.0, restored.Profile.MinHooklaod);
        Assert.Equal(280.0, restored.Profile.MaxHookload);
        Assert.True(restored.PlotSettings.HighlightFormationTop);
        Assert.False(restored.PlotSettings.SeriesVisibility["plan02"]);
        Assert.True(restored.PlotSettings.SeriesVisibility["plan03"]);
    }

    [Fact]
    public void GetFilteredPoints_AppliesDepthAndHookloadRangeFilters()
    {
        var doc = new BroomstickDocument
        {
            Profile = new BroomstickProfile
            {
                FilterByRange = true,
                MinHooklaod = 50.0,
                MaxHookload = 200.0,
                DataPoints = 0 // Disable downsampling for this test
            }
        };

        doc.BroomstickPointData = new List<PUSOPoint>
        {
            new PUSOPoint { Depth = 1000, DynamicValue = 40.0, UserVisible = true },  // Below min hookload
            new PUSOPoint { Depth = 2000, DynamicValue = 120.0, UserVisible = true }, // OK
            new PUSOPoint { Depth = 3000, DynamicValue = 180.0, UserVisible = true }, // OK
            new PUSOPoint { Depth = 4000, DynamicValue = 250.0, UserVisible = true }, // Above max hookload
            new PUSOPoint { Depth = 5000, DynamicValue = 150.0, UserVisible = false } // Hidden by user
        };

        var filtered = doc.GetFilteredPoints(minDepth: 1500, maxDepth: 4500);

        Assert.Equal(2, filtered.Count);
        Assert.Equal(2000, filtered[0].Depth);
        Assert.Equal(3000, filtered[1].Depth);
    }
}
