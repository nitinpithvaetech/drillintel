using System;
using System.Collections.Generic;
using DrillIntel.Models.TChart;
using Xunit;

namespace DrillIntel.Tests;

public class TChartModelTests
{
    [Fact]
    public void Point_Instantiation_And_GetCopy()
    {
        var pt = new Point(10.5, 20.3);
        var copy = pt.GetCopy();

        Assert.Equal(10.5, copy.X);
        Assert.Equal(20.3, copy.Y);

        var staticCopy = Point.GetCopy(pt);
        Assert.Equal(10.5, staticCopy.X);
    }

    [Fact]
    public void AnchorPoint_Instantiation_And_GetCopy()
    {
        var ap = new AnchorPoint(5, 15, 2.5);
        Assert.Equal(5, ap.x);
        Assert.Equal(15, ap.y);
        Assert.Equal(2.5, ap.size);

        var copy = ap.GetCopy();
        Assert.Equal(5, copy.X);
        Assert.Equal(15, copy.Y);
        Assert.Equal(2.5, copy.Size);
    }

    [Fact]
    public void RTRectangle_Instantiation_And_GetCopy()
    {
        var rect = new RTRectangle(10, 20, 100, 50);
        Assert.Equal(10, rect.Left);
        Assert.Equal(20, rect.Top);
        Assert.Equal(100, rect.Width);
        Assert.Equal(50, rect.Height);
        Assert.Equal(110, rect.Right);
        Assert.Equal(70, rect.Bottom);

        var copy = rect.GetCopy();
        Assert.Equal(10, copy.Left);
        Assert.Equal(100, copy.Width);
    }

    [Fact]
    public void VshalColors_DefaultValues_And_GetCopy()
    {
        var vshal = new VshalColors();
        Assert.Equal("#F7F6E5", vshal.Color1);
        Assert.Equal("#76D2DB", vshal.Color2);
        Assert.Equal("#DA4848", vshal.Color3);

        var copy = vshal.GetCopy();
        Assert.Equal("#F7F6E5", copy.Color1);
    }

    [Fact]
    public void VHColorDepthRange_GetCopy()
    {
        var range = new VHColorDepthRange
        {
            Id = "r1",
            FromDepth = 1000,
            ToDepth = 2000,
            Color1 = "blue",
            Color2 = "green",
            Color3 = "yellow"
        };

        var copy = range.GetCopy();
        Assert.Equal("r1", copy.Id);
        Assert.Equal(1000, copy.FromDepth);
        Assert.Equal(2000, copy.ToDepth);
        Assert.Equal("blue", copy.Color1);
    }

    [Fact]
    public void RTDataSource_GetCopy()
    {
        var ds = new RTDataSource
        {
            WellID = "w1",
            WellboreID = "wb1",
            TimeLogID = "tl1",
            DepthLogID = "dl1",
            DatasourceType = enumRTDataSourceType.TimeLog
        };

        var copy = ds.GetCopy();
        Assert.Equal("w1", copy.WellID);
        Assert.Equal(enumRTDataSourceType.TimeLog, copy.DatasourceType);
    }

    [Fact]
    public void RTYAxis_And_RTAxis_Properties_And_GetCopy()
    {
        var yAxis = new RTYAxis
        {
            ID = "y1",
            Title = "Measured Depth",
            Unit = "ft",
            Min = 0,
            Max = 10000,
            MajorLineStyle = enumRTLineStyle.Solid
        };

        var yCopy = yAxis.GetCopy();
        Assert.Equal("y1", yCopy.ID);
        Assert.Equal("Measured Depth", yCopy.Title);
        Assert.Equal(10000, yCopy.Max);

        var xAxis = new RTAxis
        {
            ID = "x1",
            Title = "ROP",
            Unit = "m/hr",
            Logarithmic = true,
            Location = enumRTAxisLocation.Top
        };

        var xCopy = xAxis.GetCopy();
        Assert.Equal("x1", xCopy.ID);
        Assert.True(xCopy.Logarighmic);
        Assert.True(xCopy.Logarithmic);
        Assert.Equal(enumRTAxisLocation.Top, xCopy.Location);
    }

    [Fact]
    public void VHTrackChannel_Properties_And_GetCopy()
    {
        var ch = new VHTrackChannel
        {
            ID = "c1",
            Mnemonic = "HKLD",
            Title = "Hookload",
            SeriesType = enumRTSeriesStyle.Line,
            LineColor = "red",
            LineWidth = 3
        };
        ch.xData.AddRange(new[] { 1.0, 2.0, 3.0 });
        ch.yData.AddRange(new[] { 10.0, 20.0, 30.0 });

        var copy = ch.GetCopy();
        Assert.Equal("c1", copy.ID);
        Assert.Equal("HKLD", copy.Mnemonic);
        Assert.Equal("Hookload", copy.Title);
        Assert.Equal("red", copy.LineColor);
        Assert.Equal(3, copy.LineWidth);
        Assert.Equal(3, copy.xData.Count);
        Assert.Equal(3, copy.yData.Count);
    }

    [Fact]
    public void VHTrack_GetSystemDefaultMultiWellTimeTracks_CreatesExpectedTracks()
    {
        var tracks = VHTrack.GetSystemDefaultMultiWellTimeTracks();
        Assert.Equal(2, tracks.Count);

        var indexTrack = tracks[0];
        Assert.Equal(enumRTTrackType.Index, indexTrack.TrackType);
        Assert.Equal("Index", indexTrack.Title);

        var depthTrack = tracks[1];
        Assert.Equal(enumRTTrackType.Regular, depthTrack.TrackType);
        Assert.Equal("Depth", depthTrack.Title);
        Assert.Single(depthTrack.Channels);
        Assert.Equal("DEPTH", depthTrack.Channels[0].Mnemonic);
    }

    [Fact]
    public void VHTrack_GetBlankCopy_And_GetCopy()
    {
        var track = new VHTrack
        {
            ID = "t1",
            Title = "Drilling Params",
            Width = 3,
            TrackType = enumRTTrackType.Regular
        };
        track.Channels.Add(new VHTrackChannel { ID = "c1", Mnemonic = "WOB" });

        var copy = track.GetCopy();
        Assert.Equal("t1", copy.ID);
        Assert.Single(copy.Channels);

        var rootDs = new RTDataSource { WellID = "MasterWell" };
        var blankCopy = VHTrack.GetBlankCopy(track, rootDs);
        Assert.Equal("MasterWell", blankCopy.DataSource.WellID);
    }

    [Fact]
    public void VHTrackConsole_Factory_And_UpdatePropertyByName()
    {
        var console = VHTrackConsole.CreateConsoleFromIndexType(enumIndexType.Deptlog);
        Assert.Equal(enumIndexType.Deptlog, console.IndexType);

        VHTrackConsole.UpdatePropertyByName("My Custom Console", "Name", "String", console);
        Assert.Equal("My Custom Console", console.Name);

        VHTrackConsole.UpdatePropertyByName("250", "displayResolution", "Number", console);
        Assert.Equal(250, console.displayResolution);

        VHTrackConsole.UpdatePropertyByName("true", "syncWithWidgets", "Boolean", console);
        Assert.True(console.syncWithWidgets);
    }

    [Fact]
    public void VHTrackConsole_CalculateDateDifference()
    {
        var start = new DateTime(2026, 1, 1);
        var end = new DateTime(2026, 1, 11);
        var diff = VHTrackConsole.CalculateDateDifference(start, end);
        Assert.Equal(10.0, diff);

        var invalid = VHTrackConsole.CalculateDateDifference(DateTime.MinValue, end);
        Assert.Equal(-999.25, invalid);
    }

    [Fact]
    public void VHTrackConsole_DeepCopy_Includes_Nested_Collections()
    {
        var console = new VHTrackConsole
        {
            ID = "console-1",
            Name = "Master Console",
            Tracks = new List<VHTrack>
            {
                new VHTrack { ID = "t1", Title = "Track 1" }
            }
        };
        console.WellList["well1"] = new MultiWellInfoEx { WellID = "well1", WellName = "Well 1" };
        console.RoadmapEntry["rm1"] = new RMEx { ID = "rm1", Name = "Roadmap 1", Value = 42 };

        var copy = console.GetCopy();
        Assert.Equal("console-1", copy.ID);
        Assert.Equal("Master Console", copy.Name);
        Assert.Single(copy.Tracks);
        Assert.Equal("Track 1", copy.Tracks[0].Title);
        Assert.True(copy.WellList.ContainsKey("well1"));
        Assert.Equal("Well 1", copy.WellList["well1"].WellName);
        Assert.True(copy.RoadmapEntry.ContainsKey("rm1"));
        Assert.Equal(42, copy.RoadmapEntry["rm1"].Value);

        // Ensure mutations on copy do not affect original
        copy.Tracks[0].Title = "Modified Track 1";
        Assert.Equal("Track 1", console.Tracks[0].Title);
    }
}

