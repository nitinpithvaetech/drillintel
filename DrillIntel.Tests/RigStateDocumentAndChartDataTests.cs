using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Models.TChart;
using DrillIntel.Services.Charting;
using DrillIntel.ViewModels;
using Xunit;

namespace DrillIntel.Tests;

public class RigStateDocumentAndChartDataTests
{
    [Fact]
    public void ChartDataService_TryParseDateTime_HandlesVariousFormats()
    {
        Assert.True(ChartDataService.TryParseDateTime("24-Aug-2025 15:00:00", out var dt1));
        Assert.Equal(new DateTime(2025, 8, 24, 15, 0, 0), dt1);

        Assert.True(ChartDataService.TryParseDateTime("2025-08-24 15:00:00", out var dt2));
        Assert.Equal(new DateTime(2025, 8, 24, 15, 0, 0), dt2);

        Assert.True(ChartDataService.TryParseDateTime(new DateTime(2025, 9, 9, 16, 25, 0), out var dt3));
        Assert.Equal(new DateTime(2025, 9, 9, 16, 25, 0), dt3);

        double oa = new DateTime(2025, 8, 24, 15, 0, 0).ToOADate();
        Assert.True(ChartDataService.TryParseDateTime(oa.ToString(System.Globalization.CultureInfo.InvariantCulture), out var dt4));
        Assert.Equal(new DateTime(2025, 8, 24, 15, 0, 0), dt4);
    }

    [Fact]
    public async Task ChartDataService_RealDatabase_DateRangeAndFiltering_ReturnsValidSamples()
    {
        var appDb = @"C:\Users\etech\OneDrive\Desktop\New Project.dintel";
        if (!System.IO.File.Exists(appDb))
        {
            return; // Skip on machines without this specific test project file
        }

        var ds = new DrillIntel.Data.DataServiceDIntel(appDb);
        Assert.True(ds.IsConnectionOpen(), ds.LastError);

        var service = new ChartDataService(ds);
        var logId = "timeLog98400152#82397684";

        // 1. Test GetTimeLogDateRangeAsync
        var range = await service.GetTimeLogDateRangeAsync(logId);
        Assert.NotNull(range);
        Assert.Equal(new DateTime(2025, 8, 24, 15, 0, 0), range.Value.MinDate);
        Assert.Equal(new DateTime(2025, 9, 9, 16, 25, 35), range.Value.MaxDate);

        // 2. Test GetTimelogDataAsync with specific date range (formerly returned 0 samples due to string comparison)
        var fromDate = new DateTime(2025, 8, 24, 15, 0, 0);
        var toDate = new DateTime(2025, 9, 9, 16, 25, 0);
        var result = await service.GetTimelogDataAsync(
            "well1", "wellbore1", logId,
            new[] { "BIT_DEPTH", "HOLE_DEPTH", "HOOK_LOAD", "RPM" },
            fromDate: fromDate,
            toDate: toDate);

        Assert.NotNull(result);
        Assert.True(result.TotalPointCount > 0, "Points count must be greater than 0 when querying log with date range");
        Assert.True(result.ChannelValues.ContainsKey("HOOK_LOAD"));
        Assert.NotEmpty(result.ChannelValues["HOOK_LOAD"]);

        // 3. Test removing a channel from track and re-querying
        var resultWithoutHkld = await service.GetTimelogDataAsync(
            "well1", "wellbore1", logId,
            new[] { "BIT_DEPTH", "HOLE_DEPTH", "RPM" },
            fromDate: fromDate,
            toDate: toDate);

        Assert.NotNull(resultWithoutHkld);
        Assert.True(resultWithoutHkld.TotalPointCount > 0);
        Assert.False(resultWithoutHkld.ChannelValues.ContainsKey("HOOK_LOAD"));
        Assert.True(resultWithoutHkld.ChannelValues.ContainsKey("RPM"));
        Assert.NotEmpty(resultWithoutHkld.ChannelValues["RPM"]);
    }

    [Fact]
    public void TChartTrackConsoleRenderer_ConvertLineStyle_MapsCorrectly()
    {
        Assert.Equal(Steema.TeeChart.Drawing.DashStyle.Solid, TChartTrackConsoleRenderer.ConvertLineStyle(enumRTLineStyle.Solid));
        Assert.Equal(Steema.TeeChart.Drawing.DashStyle.Dash, TChartTrackConsoleRenderer.ConvertLineStyle(enumRTLineStyle.Dash));
        Assert.Equal(Steema.TeeChart.Drawing.DashStyle.Dot, TChartTrackConsoleRenderer.ConvertLineStyle(enumRTLineStyle.Dot));
        Assert.Equal(Steema.TeeChart.Drawing.DashStyle.DashDot, TChartTrackConsoleRenderer.ConvertLineStyle(enumRTLineStyle.DashDot));
    }

    [Fact]
    public void TChartTrackConsoleRenderer_ConvertPointStyle_MapsCorrectly()
    {
        Assert.Equal(Steema.TeeChart.Styles.PointerStyles.Circle, TChartTrackConsoleRenderer.ConvertPointStyle(enumRTPointStyle.Circle));
        Assert.Equal(Steema.TeeChart.Styles.PointerStyles.Triangle, TChartTrackConsoleRenderer.ConvertPointStyle(enumRTPointStyle.Triangle));
        Assert.Equal(Steema.TeeChart.Styles.PointerStyles.Diamond, TChartTrackConsoleRenderer.ConvertPointStyle(enumRTPointStyle.Diamond));
        Assert.Equal(Steema.TeeChart.Styles.PointerStyles.Rectangle, TChartTrackConsoleRenderer.ConvertPointStyle(enumRTPointStyle.Square));
    }

    [Fact]
    public void AddTrackChannelViewModel_AddsChannelToTrackWithCorrectProperties()
    {
        var console = new VHTrackConsole();
        var track = new VHTrack { ID = "track-1", Title = "Drilling Mechanics", Width = 2 };
        console.Tracks.Add(track);

        var available = new List<LogChannelMetadata>
        {
            new LogChannelMetadata { Mnemonic = "HOOK_LOAD", Name = "Hook Load", Unit = "klbf", MinValue = 10, MaxValue = 250, SampleCount = 1000 },
            new LogChannelMetadata { Mnemonic = "RPM", Name = "Rotary RPM", Unit = "rpm", MinValue = 0, MaxValue = 150, SampleCount = 1000 }
        };

        var vm = new AddTrackChannelViewModel(console, available);

        // Select HOOK_LOAD
        vm.SelectedChannel = available[0];
        Assert.Equal("HOOK_LOAD", vm.Mnemonic);
        Assert.Equal("klbf", vm.Unit);

        // Customize properties
        vm.SelectedSeriesType = enumRTSeriesStyle.Line;
        vm.LineWidth = 3;
        vm.LineColorHex = "#388E3C";
        vm.SelectedFont = "Segoe UI";
        vm.FontSize = 10;
        vm.FontColorHex = "#1B5E20";
        vm.AutoScale = false;
        vm.AxisMin = 0;
        vm.AxisMax = 300;
        vm.ColorCodeAsRigState = true;
        vm.SelectedTrack = "Drilling Mechanics";

        bool closedWithSuccess = false;
        vm.RequestClose += (result) => closedWithSuccess = result == true;

        vm.SaveCommand.Execute(null);

        Assert.True(closedWithSuccess);
        Assert.NotNull(vm.CreatedChannel);
        Assert.Single(track.Channels);

        var addedChannel = track.Channels[0];
        Assert.Equal("HOOK_LOAD", addedChannel.Mnemonic);
        Assert.Equal(3, addedChannel.LineWidth);
        Assert.Equal("#388E3C", addedChannel.LineColor);
        Assert.True(addedChannel.ColorCodeAsRigState);
        Assert.Equal("Segoe UI", addedChannel.TitleFontFontName);
        Assert.Equal(10, addedChannel.TitleFontFontSize);
        Assert.Equal(0, addedChannel.objXAxis.Min);
        Assert.Equal(300, addedChannel.objXAxis.Max);
        Assert.False(addedChannel.objXAxis.AutoScale);
    }

    [Fact]
    public void AddTrackChannelViewModel_CreatesNewTrack_WhenSelected()
    {
        var console = new VHTrackConsole();
        var available = new List<LogChannelMetadata>
        {
            new LogChannelMetadata { Mnemonic = "TORQUE", Name = "Surface Torque", Unit = "ft-lbf", MinValue = 0, MaxValue = 15000, SampleCount = 500 }
        };

        var vm = new AddTrackChannelViewModel(console, available);
        vm.SelectedChannel = available[0];
        vm.SelectedTrack = "+ Add to New Track";

        vm.SaveCommand.Execute(null);

        Assert.Single(console.Tracks);
        Assert.Equal("Surface Torque", console.Tracks[0].Title);
        Assert.Single(console.Tracks[0].Channels);
        Assert.Equal("TORQUE", console.Tracks[0].Channels[0].Mnemonic);
    }

    [Fact]
    public void TrackPropertiesViewModel_CanModifyTrackAndRemoveChannel()
    {
        var console = new VHTrackConsole();
        var track = new VHTrack { ID = "track-1", Title = "Original Title", Width = 1.5 };
        var ch1 = new VHTrackChannel { ID = "ch-1", Mnemonic = "BIT_DEPTH", Title = "Bit Depth" };
        var ch2 = new VHTrackChannel { ID = "ch-2", Mnemonic = "HOLE_DEPTH", Title = "Hole Depth" };
        track.Channels.Add(ch1);
        track.Channels.Add(ch2);
        console.Tracks.Add(track);

        var vm = new TrackPropertiesViewModel(console);
        Assert.Single(vm.Tracks);
        Assert.Equal(track, vm.SelectedTrack);

        // Edit track title
        vm.SelectedTrack!.Title = "Updated Depth Track";
        Assert.Equal("Updated Depth Track", track.Title);

        // Select and remove second channel
        vm.SelectedChannel = ch2;
        vm.DeleteChannelCommand.Execute(null);

        Assert.Single(track.Channels);
        Assert.Equal("BIT_DEPTH", track.Channels[0].Mnemonic);
    }

    [Fact]
    public async Task RigStateDocumentViewModel_ApplyPreset_CalculatesCorrectRanges()
    {
        // Session with mock/empty data
        var session = new DrillIntel.Projects.ProjectSession();
        var vm = new RigStateDocumentViewModel(session);

        await vm.ApplyPresetAsync("Last 24 Hours");

        Assert.NotNull(vm.FromDate);
        Assert.NotNull(vm.ToDate);
        var diff = (vm.ToDate!.Value - vm.FromDate!.Value).TotalHours;
        Assert.InRange(diff, 23.9, 24.1);

        await vm.ApplyPresetAsync("Last 48 Hours");
        var diff48 = (vm.ToDate!.Value - vm.FromDate!.Value).TotalHours;
        Assert.InRange(diff48, 47.9, 48.1);

        await vm.ApplyPresetAsync("Last 1 Hour");
        var diff1 = (vm.ToDate!.Value - vm.FromDate!.Value).TotalHours;
        Assert.InRange(diff1, 0.0, 24.0); // Within same or adjacent day
    }

    [Fact]
    public void TrackPropertiesViewModel_CurrentTrackChannels_SyncsAndUpdatesOnDelete()
    {
        var console = new VHTrackConsole();
        var track = new VHTrack { ID = "track-1", Title = "Main Track", Width = 2 };
        var ch1 = new VHTrackChannel { ID = "c1", Mnemonic = "ROP", Title = "Rate of Penetration" };
        var ch2 = new VHTrackChannel { ID = "c2", Mnemonic = "WOB", Title = "Weight on Bit" };
        track.Channels.Add(ch1);
        track.Channels.Add(ch2);
        console.Tracks.Add(track);

        var vm = new TrackPropertiesViewModel(console);

        // Verify CurrentTrackChannels is an ObservableCollection populated with track channels
        Assert.Equal(2, vm.CurrentTrackChannels.Count);
        Assert.Equal("ROP", vm.CurrentTrackChannels[0].Mnemonic);
        Assert.Equal("WOB", vm.CurrentTrackChannels[1].Mnemonic);
        Assert.Equal(ch1, vm.SelectedChannel);

        // Delete ch1
        vm.SelectedChannel = ch1;
        vm.DeleteChannelCommand.Execute(null);

        // Both domain list and ObservableCollection must be updated
        Assert.Single(track.Channels);
        Assert.Single(vm.CurrentTrackChannels);
        Assert.Equal("WOB", vm.CurrentTrackChannels[0].Mnemonic);
        Assert.Equal(ch2, vm.SelectedChannel);
    }

    [Fact]
    public void TrackPropertiesViewModel_CurrentTrackChannels_UpdatesWhenTrackChanges()
    {
        var console = new VHTrackConsole();
        var track1 = new VHTrack { ID = "t1", Title = "Track 1" };
        track1.Channels.Add(new VHTrackChannel { ID = "c1", Mnemonic = "GAMMA" });
        var track2 = new VHTrack { ID = "t2", Title = "Track 2" };
        track2.Channels.Add(new VHTrackChannel { ID = "c2", Mnemonic = "RES_DEEP" });
        track2.Channels.Add(new VHTrackChannel { ID = "c3", Mnemonic = "RES_SHALLOW" });
        console.Tracks.Add(track1);
        console.Tracks.Add(track2);

        var vm = new TrackPropertiesViewModel(console);
        Assert.Equal(track1, vm.SelectedTrack);
        Assert.Single(vm.CurrentTrackChannels);
        Assert.Equal("GAMMA", vm.CurrentTrackChannels[0].Mnemonic);

        // Switch to Track 2
        vm.SelectedTrack = track2;
        Assert.Equal(2, vm.CurrentTrackChannels.Count);
        Assert.Equal("RES_DEEP", vm.CurrentTrackChannels[0].Mnemonic);
        Assert.Equal("RES_SHALLOW", vm.CurrentTrackChannels[1].Mnemonic);
    }

    [Fact]
    public void NullToVisibilityConverter_ConvertsCorrectly()
    {
        var conv = new DrillIntel.Converters.NullToVisibilityConverter();

        Assert.Equal(System.Windows.Visibility.Visible, conv.Convert("hello", typeof(System.Windows.Visibility), string.Empty, System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(System.Windows.Visibility.Collapsed, conv.Convert(null, typeof(System.Windows.Visibility), string.Empty, System.Globalization.CultureInfo.InvariantCulture));

        // Inverted
        Assert.Equal(System.Windows.Visibility.Collapsed, conv.Convert("hello", typeof(System.Windows.Visibility), "Invert", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(System.Windows.Visibility.Visible, conv.Convert(null, typeof(System.Windows.Visibility), "Invert", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void RigStateDocumentViewModel_SyncDisplayTracks_ReflectsConsoleTracks()
    {
        var session = new DrillIntel.Projects.ProjectSession();
        var console = new VHTrackConsole();
        console.Tracks.Add(new VHTrack { ID = "t1", Title = "Track A" });
        console.Tracks.Add(new VHTrack { ID = "t2", Title = "Track B" });

        var vm = new RigStateDocumentViewModel(session, console: console);
        Assert.Equal(2, vm.DisplayTracks.Count);
        Assert.Equal("Track A", vm.DisplayTracks[0].Title);
        Assert.Equal("Track B", vm.DisplayTracks[1].Title);
    }

    [Fact]
    public void ChartDataService_FindMatchingColumn_BidirectionalAndAliases_MatchesCorrectly()
    {
        var dbCols = new List<string> { "DTIM", "DBIT", "DMEA", "HKLD", "TORQ", "RPM", "PUMP_PRESS", "RIG_STATE", "RIG_STATE_COLOR" };

        // Test alias matching from standard mnemonic to abbreviated column names
        Assert.Equal("DBIT", ChartDataService.FindMatchingColumn("BIT_DEPTH", dbCols));
        Assert.Equal("DMEA", ChartDataService.FindMatchingColumn("HOLE_DEPTH", dbCols));
        Assert.Equal("HKLD", ChartDataService.FindMatchingColumn("HOOK_LOAD", dbCols));
        Assert.Equal("TORQ", ChartDataService.FindMatchingColumn("TORQUE", dbCols));
        Assert.Equal("RPM", ChartDataService.FindMatchingColumn("RPM", dbCols));
        Assert.Equal("PUMP_PRESS", ChartDataService.FindMatchingColumn("SPP", dbCols));
        Assert.Equal("RIG_STATE", ChartDataService.FindMatchingColumn("RIG_STATE", dbCols));

        // Test reverse alias matching: requested is DBIT, column list has BIT_DEPTH
        var altCols = new List<string> { "BIT_DEPTH", "HOLE_DEPTH", "HOOKLOAD" };
        Assert.Equal("BIT_DEPTH", ChartDataService.FindMatchingColumn("DBIT", altCols));
        Assert.Equal("HOLE_DEPTH", ChartDataService.FindMatchingColumn("DMEA", altCols));
        Assert.Equal("HOOKLOAD", ChartDataService.FindMatchingColumn("HKLD", altCols));
    }

    [Fact]
    public async Task RigStateDocumentViewModel_ApplyPreset_AnchorsToLogMaxDate()
    {
        var session = new DrillIntel.Projects.ProjectSession();
        var vm = new RigStateDocumentViewModel(session);

        // Simulate historical log recorded on 2023-05-10
        var logStart = new DateTime(2023, 5, 1, 0, 0, 0);
        var logEnd = new DateTime(2023, 5, 10, 12, 0, 0);
        vm.LogMinDate = logStart;
        vm.LogMaxDate = logEnd;

        // Apply "Last 24 Hours" preset
        await vm.ApplyPresetCommand.ExecuteAsync("Last 24 Hours");

        // The From/To window should anchor to 2023-05-10, NOT DateTime.Now (2026)!
        Assert.NotNull(vm.ToDate);
        Assert.Equal(logEnd.Date, vm.ToDate.Value.Date);
        Assert.NotNull(vm.FromDate);
        Assert.Equal(logEnd.AddHours(-24).Date, vm.FromDate.Value.Date);
        Assert.Equal(logEnd.AddHours(-24), vm.ConsoleModel.currentMinDate);
        Assert.Equal(logEnd, vm.ConsoleModel.currentMaxDate);

        // Apply "Entire Log" preset
        await vm.ApplyPresetCommand.ExecuteAsync("Entire Log");
        Assert.Equal(logStart.Date, vm.FromDate.Value.Date);
        Assert.Equal(logEnd.Date, vm.ToDate.Value.Date);
        Assert.Equal(logStart, vm.ConsoleModel.currentMinDate);
        Assert.Equal(logEnd, vm.ConsoleModel.currentMaxDate);
    }

    [Fact]
    public void TeeChart_Diagnostic_CheckRenderConsoleExecution()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var chart = new Steema.TeeChart.WPF.TChart();
                var renderer = new TChartTrackConsoleRenderer(chart);

                var console = new VHTrackConsole { IndexType = enumIndexType.TimeLog, TrackOrientation = enumTrackOrientation.Vertical };
                var track = new VHTrack { ID = "t1", Title = "Track 1", Width = 2 };
                var ch = new VHTrackChannel
                {
                    ID = "c1",
                    Mnemonic = "RPM",
                    SeriesType = enumRTSeriesStyle.Line,
                    LineWidth = 2,
                    LineColor = "#FF0000"
                };
                track.Channels.Add(ch);
                console.Tracks.Add(track);

                var data = new ChartDataSeriesResult
                {
                    SourceType = enumRTDataSourceType.TimeLog,
                    IndexValues = new List<double> { 46298.0, 46298.1, 46298.2 },
                    ChannelValues = new Dictionary<string, List<double>>
                    {
                        ["RPM"] = new List<double> { 50.0, 60.0, 70.0 }
                    }
                };

                renderer.RenderConsole(console, data);

                Assert.Single(chart.Series);
                var s = chart.Series[0];
                Assert.Equal(3, s.Count);

                // Check axes
                Assert.NotNull(s.CustomHorizAxis);
                Assert.Equal(chart.Axes.Left, s.GetVertAxis);

                // Let TeeChart draw to in-memory bitmap or chart draw
                chart.Width = 800;
                chart.Height = 600;
                chart.Measure(new System.Windows.Size(800, 600));
                chart.Arrange(new System.Windows.Rect(0, 0, 800, 600));
                chart.UpdateLayout();

                var testConsole = new VHTrackConsole { IndexType = enumIndexType.TimeLog, TrackOrientation = enumTrackOrientation.Vertical };
                var testTrack = new VHTrack { ID = "t1", Title = "Track 1", Width = 2 };
                var testCh = new VHTrackChannel
                {
                    ID = "c1",
                    Mnemonic = "BIT_DEPTH",
                    Title = "Bit Depth",
                    Unit = "m",
                    SeriesType = enumRTSeriesStyle.Line,
                    LineWidth = 2,
                    LineColor = "#1976D2",
                    objXAxis = new RTAxis { Mnemonic = "BIT_DEPTH", Title = "Bit Depth", Unit = "m", Min = 0, Max = 4500, AutoScale = false }
                };
                testTrack.Channels.Add(testCh);
                testConsole.Tracks.Add(testTrack);

                var testCh2 = new VHTrackChannel
                {
                    ID = "c2",
                    Mnemonic = "HOLE_DEPTH",
                    Title = "Hole Depth",
                    Unit = "m",
                    SeriesType = enumRTSeriesStyle.Line,
                    LineWidth = 2,
                    LineColor = "#FF5722",
                    objXAxis = new RTAxis { Mnemonic = "HOLE_DEPTH", Title = "Hole Depth", Unit = "m", Min = 0, Max = 4500, AutoScale = false }
                };
                testTrack.Channels.Add(testCh2);

                var indexVals = new List<double> { 46297.0, 46297.1, 46297.2, 46297.3 };
                var channelVals = new List<double> { 500.0, 1500.0, 3000.0, 4400.0 };
                var channelVals2 = new List<double> { 600.0, 1600.0, 3100.0, 4500.0 };

                var testData = new ChartDataSeriesResult
                {
                    SourceType = enumRTDataSourceType.TimeLog,
                    IndexValues = indexVals,
                    ChannelValues = new Dictionary<string, List<double>>
                    {
                        ["BIT_DEPTH"] = channelVals,
                        ["HOLE_DEPTH"] = channelVals2
                    }
                };

                renderer.RenderConsole(testConsole, testData);

                // Verify vertical chart custom axes spacing and top alignment
                Assert.Equal(2, chart.Axes.Custom.Count);
                var vAx0 = chart.Axes.Custom[0];
                var vAx1 = chart.Axes.Custom[1];
                Assert.True(vAx0.OtherSide); // Top
                Assert.True(vAx1.OtherSide); // Top
                Assert.Equal(0, vAx0.RelativePosition);
                Assert.Equal(-48, vAx1.RelativePosition);
                Assert.True(vAx0.Labels.CustomSize >= 18);
                Assert.True(vAx0.Title.Distance >= 14);
                Assert.Equal(Steema.TeeChart.Drawing.StringAlignment.Center, vAx0.Title.Alignment);
                Assert.Equal(Steema.TeeChart.Drawing.StringAlignment.Center, vAx0.Title.TextAlign);
                Assert.Equal(Steema.TeeChart.Drawing.StringAlignment.Center, vAx0.Labels.TextAlign);
                Assert.True(chart.Panel.MarginTop >= 120);
                Assert.Equal(22, chart.Panel.MarginBottom);

                // Test D: Horizontal orientation with Depth on Left and Hook Load / RPM on Right
                var horizConsole = new VHTrackConsole { IndexType = enumIndexType.TimeLog, TrackOrientation = enumTrackOrientation.Horizontal };
                var trackDepth = new VHTrack { ID = "td", Title = "Depth Track", Width = 1.5 };
                trackDepth.Channels.Add(new VHTrackChannel
                {
                    ID = "chD",
                    Mnemonic = "BIT_DEPTH",
                    Title = "Bit Depth",
                    Unit = "m",
                    SeriesType = enumRTSeriesStyle.Line,
                    LineColor = "#1976D2",
                    objXAxis = new RTAxis { Mnemonic = "BIT_DEPTH", Title = "Bit Depth", Unit = "m", Min = 0, Max = 4500, AutoScale = false }
                });
                horizConsole.Tracks.Add(trackDepth);

                var trackMain = new VHTrack { ID = "tm", Title = "Main Track", Width = 2.0 };
                trackMain.Channels.Add(new VHTrackChannel
                {
                    ID = "chHk",
                    Mnemonic = "HOOK_LOAD",
                    Title = "Hook Load",
                    Unit = "klbf",
                    SeriesType = enumRTSeriesStyle.Line,
                    LineColor = "#388E3C",
                    objXAxis = new RTAxis { Mnemonic = "HOOK_LOAD", Title = "Hook Load", Unit = "klbf", Min = 0, Max = 500, AutoScale = false }
                });
                trackMain.Channels.Add(new VHTrackChannel
                {
                    ID = "chRpm",
                    Mnemonic = "RPM",
                    Title = "RPM",
                    Unit = "rpm",
                    SeriesType = enumRTSeriesStyle.Line,
                    LineColor = "#D32F2F",
                    objXAxis = new RTAxis { Mnemonic = "RPM", Title = "RPM", Unit = "rpm", Min = 0, Max = 200, AutoScale = false }
                });
                horizConsole.Tracks.Add(trackMain);

                var horizData = new ChartDataSeriesResult
                {
                    SourceType = enumRTDataSourceType.TimeLog,
                    IndexValues = indexVals,
                    ChannelValues = new Dictionary<string, List<double>>
                    {
                        ["BIT_DEPTH"] = channelVals,
                        ["HOOK_LOAD"] = new List<double> { 120.0, 180.0, 240.0, 310.0 },
                        ["RPM"] = new List<double> { 50.0, 80.0, 110.0, 90.0 }
                    }
                };

                var chartH = new Steema.TeeChart.WPF.TChart();
                var rendererH = new TChartTrackConsoleRenderer(chartH);
                rendererH.RenderConsole(horizConsole, horizData);

                // Verify horizontal chart: Depth on Left, Hook Load and RPM on Right
                Assert.Equal(3, chartH.Axes.Custom.Count);
                var hAxDepth = chartH.Axes.Custom[0];
                var hAxHook = chartH.Axes.Custom[1];
                var hAxRpm = chartH.Axes.Custom[2];

                Assert.False(hAxDepth.OtherSide); // Left
                Assert.True(hAxHook.OtherSide);   // Right
                Assert.True(hAxRpm.OtherSide);    // Right

                Assert.Equal(0, hAxHook.RelativePosition);
                Assert.Equal(-56, hAxRpm.RelativePosition); // Outward into right margin

                Assert.True(hAxDepth.Labels.CustomSize >= 28);
                Assert.True(hAxDepth.Title.Distance >= 10);
                Assert.True(hAxHook.Labels.CustomSize >= 28);
                Assert.True(hAxHook.Title.Distance >= 10);
                Assert.True(hAxRpm.Labels.CustomSize >= 28);
                Assert.True(hAxRpm.Title.Distance >= 10);

                Assert.True(chartH.Panel.MarginLeft >= 30);
                Assert.True(chartH.Panel.MarginRight >= 26);
                Assert.Equal(54, chartH.Panel.MarginBottom);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (threadEx != null) throw threadEx;
    }

    [Fact]
    public void TChartTrackConsoleRenderer_ColorCodeAsRigState_CreatesColorEachLineSeries()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var chart = new Steema.TeeChart.WPF.TChart();
                var renderer = new TChartTrackConsoleRenderer(chart);

                var console = new VHTrackConsole { IndexType = enumIndexType.TimeLog };
                var track = new VHTrack { ID = "track1", Title = "Depth", Width = 1.5 };
                var chDepth = new VHTrackChannel
                {
                    ID = "ch1",
                    Mnemonic = "BIT_DEPTH",
                    SeriesType = enumRTSeriesStyle.Line,
                    ColorCodeAsRigState = true,
                    Visible = true
                };
                track.Channels.Add(chDepth);
                console.Tracks.Add(track);

                var data = new ChartDataSeriesResult
                {
                    SourceType = enumRTDataSourceType.TimeLog,
                    IndexValues = new List<double> { 46000.1, 46000.2, 46000.3 },
                    ChannelValues = new Dictionary<string, List<double>> { ["BIT_DEPTH"] = new List<double> { 100, 105, 110 } },
                    RigStateNumbers = new List<int> { 0, 1, 2 },
                    RigStateColors = new List<int> { -16711936, -16732672, -8355712 }
                };

                renderer.RenderConsole(console, data);

                Assert.Single(chart.Series);
                var series = chart.Series[0];

                // Must be Line series (not FastLine) with ColorEach and ColorEachLine enabled for multi-colored segment rendering
                var lineSeries = Assert.IsType<Steema.TeeChart.Styles.Line>(series);
                Assert.True(lineSeries.ColorEach, "ColorEach must be enabled for rig state color coding");
                Assert.True(lineSeries.ColorEachLine, "ColorEachLine must be enabled for line segments to display rig state colors");
                Assert.Equal(3, lineSeries.Count);
                Assert.Equal(System.Drawing.Color.FromArgb(-16711936), lineSeries.Colors[0]);
                Assert.Equal(System.Drawing.Color.FromArgb(-16732672), lineSeries.Colors[1]);
                Assert.Equal(System.Drawing.Color.FromArgb(-8355712), lineSeries.Colors[2]);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (threadEx != null) throw threadEx;
    }

    [Fact]
    public void TChartTrackConsoleRenderer_BothOrientations_RenderSuccessfully()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var chart = new Steema.TeeChart.WPF.TChart();
                var renderer = new TChartTrackConsoleRenderer(chart);

                var console = new VHTrackConsole { IndexType = enumIndexType.TimeLog, TrackOrientation = enumTrackOrientation.Vertical };
                var track = new VHTrack { ID = "track1", Title = "Drilling", Width = 2.0 };
                var ch = new VHTrackChannel { ID = "c1", Mnemonic = "RPM", SeriesType = enumRTSeriesStyle.Line, Visible = true };
                track.Channels.Add(ch);
                console.Tracks.Add(track);

                var data = new ChartDataSeriesResult
                {
                    SourceType = enumRTDataSourceType.TimeLog,
                    IndexValues = new List<double> { 46000.1, 46000.2 },
                    ChannelValues = new Dictionary<string, List<double>> { ["RPM"] = new List<double> { 60, 65 } }
                };

                // 1. Render in Vertical orientation (Left axis = index, custom horizontal axes)
                renderer.RenderConsole(console, data);
                Assert.True(chart.Axes.Left.Visible);
                Assert.False(chart.Axes.Bottom.Visible);
                Assert.True(ch.__scale is Steema.TeeChart.Axis axisV && axisV.Horizontal);

                // 2. Render in Horizontal orientation (Bottom axis = index, custom vertical axes)
                console.TrackOrientation = enumTrackOrientation.Horizontal;
                renderer.RenderConsole(console, data);
                Assert.True(chart.Axes.Bottom.Visible);
                Assert.False(chart.Axes.Left.Visible);
                Assert.True(ch.__scale is Steema.TeeChart.Axis axisH && !axisH.Horizontal);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (threadEx != null) throw threadEx;
    }

    [Fact]
    public void TChartTrackConsoleRenderer_GetAllChannelsHoverInfo_ReturnsAllVisibleChannels()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var chart = new Steema.TeeChart.WPF.TChart();
                var renderer = new TChartTrackConsoleRenderer(chart);

                var console = new VHTrackConsole { IndexType = enumIndexType.TimeLog, TrackOrientation = enumTrackOrientation.Horizontal };
                var track = new VHTrack { ID = "track1", Title = "Main Track", Width = 2.0 };
                var ch1 = new VHTrackChannel { ID = "c1", Mnemonic = "FLOWIN", Title = "Circulation", Unit = "galUS/min", LineColor = "#3B82F6", SeriesType = enumRTSeriesStyle.Line, Visible = true };
                var ch2 = new VHTrackChannel { ID = "c2", Mnemonic = "HKLD", Title = "Hookload", Unit = "klb", LineColor = "#4338CA", SeriesType = enumRTSeriesStyle.Line, Visible = true };
                var ch3 = new VHTrackChannel { ID = "c3", Mnemonic = "DEPTH", Title = "Depth", Unit = "m", LineColor = "#10B981", SeriesType = enumRTSeriesStyle.Line, Visible = true };
                track.Channels.Add(ch1);
                track.Channels.Add(ch2);
                track.Channels.Add(ch3);
                console.Tracks.Add(track);

                // Date Time: 2013-03-22 02:02:05 in OADate
                var testDt = new DateTime(2013, 3, 22, 2, 2, 5);
                double oaDate = testDt.ToOADate();

                var data = new ChartDataSeriesResult
                {
                    SourceType = enumRTDataSourceType.TimeLog,
                    IndexValues = new List<double> { oaDate - 0.01, oaDate, oaDate + 0.01 },
                    RigStateNumbers = new List<int> { 1, 2, 1 },
                    RigStateColors = new List<int> { unchecked((int)0xFF2E7D32), unchecked((int)0xFFE65100), unchecked((int)0xFF2E7D32) },
                    ChannelValues = new Dictionary<string, List<double>>
                    {
                        ["FLOWIN"] = new List<double> { 400.0, 457.54, 460.0 },
                        ["HKLD"] = new List<double> { 90.0, 103.38, 100.0 },
                        ["DEPTH"] = new List<double> { 525.0, 531.71, 540.0 }
                    }
                };

                renderer.RenderConsole(console, data);

                chart.Width = 800;
                chart.Height = 600;
                chart.Measure(new System.Windows.Size(800, 600));
                chart.Arrange(new System.Windows.Rect(0, 0, 800, 600));
                chart.UpdateLayout();

                // Simulate mouse hover point along bottom axis corresponding to oaDate
                int xPos = chart.Axes.Bottom.CalcXPosValue(oaDate);
                var hoverInfo = renderer.GetAllChannelsHoverInfo(new System.Windows.Point(xPos, 150), "Etech 420");

                Assert.NotNull(hoverInfo);
                Assert.Equal("Etech 420", hoverInfo.WellName);
                Assert.Contains("Mar-22-2013 02:02:05", hoverInfo.FormattedIndex);
                Assert.Equal(3, hoverInfo.Channels.Count);

                var circ = hoverInfo.Channels.FirstOrDefault(c => c.ChannelMnemonic == "FLOWIN");
                Assert.NotNull(circ);
                Assert.Equal("Circulation", circ.ChannelTitle);
                Assert.Equal(457.54, circ.Value);
                Assert.Equal("galUS/min", circ.Unit);
                Assert.Equal("Circulation: 457.54 galUS/min", circ.DisplayText);

                var hkld = hoverInfo.Channels.FirstOrDefault(c => c.ChannelMnemonic == "HKLD");
                Assert.NotNull(hkld);
                Assert.Equal("Hookload", hkld.ChannelTitle);
                Assert.Equal(103.38, hkld.Value);
                Assert.Equal("klb", hkld.Unit);
                Assert.Equal("Hookload: 103.38 klb", hkld.DisplayText);

                var depth = hoverInfo.Channels.FirstOrDefault(c => c.ChannelMnemonic == "DEPTH");
                Assert.NotNull(depth);
                Assert.Equal("Depth", depth.ChannelTitle);
                Assert.Equal(531.71, depth.Value);
                Assert.Equal("m", depth.Unit);
                Assert.Equal("Depth: 531.71 m", depth.DisplayText);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (threadEx != null) throw threadEx;
    }

    [Fact]
    public void TeeChart_CoordinateCalculations_TestAxisAndSeries()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var chart = new Steema.TeeChart.WPF.TChart();
                var renderer = new TChartTrackConsoleRenderer(chart);

                var console = new VHTrackConsole { IndexType = enumIndexType.TimeLog, TrackOrientation = enumTrackOrientation.Vertical };
                var track = new VHTrack { ID = "track1", Title = "Drilling", Width = 2.0 };
                var ch = new VHTrackChannel { ID = "c1", Mnemonic = "RPM", SeriesType = enumRTSeriesStyle.Line, Visible = true };
                track.Channels.Add(ch);
                console.Tracks.Add(track);

                var data = new ChartDataSeriesResult
                {
                    SourceType = enumRTDataSourceType.TimeLog,
                    IndexValues = new List<double> { 46000.0, 46001.0 },
                    ChannelValues = new Dictionary<string, List<double>> { ["RPM"] = new List<double> { 60.0, 80.0 } }
                };

                renderer.RenderConsole(console, data);

                chart.Width = 800;
                chart.Height = 600;
                chart.Measure(new System.Windows.Size(800, 600));
                chart.Arrange(new System.Windows.Rect(0, 0, 800, 600));
                chart.UpdateLayout();

                var leftAxis = chart.Axes.Left;
                double valFromMid = leftAxis.CalcPosPoint(300);
                Assert.True(valFromMid >= 45999.0 && valFromMid <= 46002.0, $"Expected value around 46000-46001, got {valFromMid}");

                var series = chart.Series[0];
                int x0 = series.CalcXPos(0);
                int y0 = series.CalcYPos(0);
                Assert.True(x0 > 0, $"Expected x0 > 0, got {x0}");
                Assert.True(y0 > 0, $"Expected y0 > 0, got {y0}");
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (threadEx != null) throw threadEx;
    }

    [Fact]
    public void TChartTrackConsoleRenderer_FindClosestIndex_HandlesBothDirections()
    {
        var asc = new List<double> { 10.0, 20.0, 30.0, 40.0, 50.0 };
        Assert.Equal(0, TChartTrackConsoleRenderer.FindClosestIndex(asc, 5.0));
        Assert.Equal(2, TChartTrackConsoleRenderer.FindClosestIndex(asc, 28.0));
        Assert.Equal(2, TChartTrackConsoleRenderer.FindClosestIndex(asc, 31.0));
        Assert.Equal(4, TChartTrackConsoleRenderer.FindClosestIndex(asc, 100.0));

        var desc = new List<double> { 50.0, 40.0, 30.0, 20.0, 10.0 };
        Assert.Equal(0, TChartTrackConsoleRenderer.FindClosestIndex(desc, 60.0));
        Assert.Equal(2, TChartTrackConsoleRenderer.FindClosestIndex(desc, 32.0));
        Assert.Equal(4, TChartTrackConsoleRenderer.FindClosestIndex(desc, 5.0));
    }

    [Fact]
    public void TChartTrackConsoleRenderer_HitTestSeries_ReturnsHoverInfo()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var chart = new Steema.TeeChart.WPF.TChart();
                var renderer = new TChartTrackConsoleRenderer(chart);

                var console = new VHTrackConsole { IndexType = enumIndexType.TimeLog, TrackOrientation = enumTrackOrientation.Vertical };
                var track = new VHTrack { ID = "track1", Title = "Drilling Mechanics", Width = 2.0 };
                var ch = new VHTrackChannel
                {
                    ID = "c1",
                    Mnemonic = "HOOK_LOAD",
                    Title = "Hook Load",
                    Unit = "klbf",
                    LineColor = "#388E3C",
                    SeriesType = enumRTSeriesStyle.Line,
                    Visible = true
                };
                track.Channels.Add(ch);
                console.Tracks.Add(track);

                var dt1 = new DateTime(2025, 8, 24, 15, 0, 0);
                var dt2 = new DateTime(2025, 8, 24, 15, 1, 0);

                var data = new ChartDataSeriesResult
                {
                    SourceType = enumRTDataSourceType.TimeLog,
                    IndexValues = new List<double> { dt1.ToOADate(), dt2.ToOADate() },
                    ChannelValues = new Dictionary<string, List<double>> { ["HOOK_LOAD"] = new List<double> { 125.5, 140.0 } },
                    RigStateNumbers = new List<int> { 0, 1 },
                    RigStateColors = new List<int> { -16711936, -16732672 }
                };

                renderer.RenderConsole(console, data);

                chart.Width = 800;
                chart.Height = 600;
                chart.Measure(new System.Windows.Size(800, 600));
                chart.Arrange(new System.Windows.Rect(0, 0, 800, 600));
                chart.UpdateLayout();

                var series = chart.Series[0];
                int x0 = series.CalcXPos(0);
                int y0 = series.CalcYPos(0);

                // Hit test directly at the screen point of sample 0
                var hit = renderer.HitTestSeries(new System.Windows.Point(x0, y0), tolerancePixels: 20.0);
                Assert.NotNull(hit);
                Assert.Equal("Drilling Mechanics", hit.TrackTitle);
                Assert.Equal("HOOK_LOAD", hit.ChannelMnemonic);
                Assert.Equal("Hook Load", hit.ChannelTitle);
                Assert.Equal("klbf", hit.Unit);
                Assert.Equal("#388E3C", hit.LineColorHex);
                Assert.Equal(125.5, hit.Value);
                Assert.Equal("125.50", hit.FormattedValue);
                Assert.Equal(0, hit.PointIndex);
                Assert.Equal("2025-08-24 15:00:00", hit.FormattedIndex);
                Assert.Equal("Rotary Drill", hit.RigStateName);

                // Hit test far away from the series (e.g. at x = 10, y = 10)
                var miss = renderer.HitTestSeries(new System.Windows.Point(10, 10), tolerancePixels: 5.0);
                Assert.Null(miss);

                // Test cursor telemetry
                var telemetry = renderer.GetCursorTelemetry(new System.Windows.Point(x0, y0));
                Assert.NotNull(telemetry);
                Assert.Equal("2025-08-24 15:00:00", telemetry.Value.FormattedIndex);
                Assert.Equal("Rotary Drill", telemetry.Value.RigStateName);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (threadEx != null) throw threadEx;
    }

    [Fact]
    public void RigStateDocumentViewModel_CursorTelemetry_UpdatesAndResets()
    {
        var session = new DrillIntel.Projects.ProjectSession();
        var vm = new RigStateDocumentViewModel(session);

        Assert.Equal("--", vm.CursorIndexText);
        Assert.Equal("None", vm.CursorRigStateText);
        Assert.Equal("#9E9E9E", vm.CursorRigStateColorHex);

        vm.UpdateCursorTelemetry("2025-08-24 15:00:00", "Rotary Drill", "#00FF00");
        Assert.Equal("2025-08-24 15:00:00", vm.CursorIndexText);
        Assert.Equal("Rotary Drill", vm.CursorRigStateText);
        Assert.Equal("#00FF00", vm.CursorRigStateColorHex);

        vm.ResetCursorTelemetry();
        Assert.Equal("--", vm.CursorIndexText);
        Assert.Equal("None", vm.CursorRigStateText);
        Assert.Equal("#9E9E9E", vm.CursorRigStateColorHex);
    }

    [Fact]
    public void TeeChart_AxisLabelFormatting_DiagnosticTest()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var chart = new Steema.TeeChart.WPF.TChart();
                var renderer = new TChartTrackConsoleRenderer(chart);

                var console = new VHTrackConsole { IndexType = enumIndexType.TimeLog, TrackOrientation = enumTrackOrientation.Vertical };
                var track = new VHTrack { ID = "track1", Title = "Drilling Mechanics", Width = 2.0 };
                var ch1 = new VHTrackChannel { ID = "c1", Mnemonic = "HOOK_LOAD", LineColor = "#388E3C", Visible = true };
                var ch2 = new VHTrackChannel { ID = "c2", Mnemonic = "TORQUE", LineColor = "#F57C00", Visible = true };
                track.Channels.Add(ch1);
                track.Channels.Add(ch2);
                console.Tracks.Add(track);

                var data = new ChartDataSeriesResult
                {
                    SourceType = enumRTDataSourceType.TimeLog,
                    IndexValues = new List<double> { 46000.0, 46001.0 },
                    ChannelValues = new Dictionary<string, List<double>>
                    {
                        ["HOOK_LOAD"] = new List<double> { 120.0, 130.0 },
                        ["TORQUE"] = new List<double> { 5000.0, 6000.0 }
                    }
                };

                renderer.RenderConsole(console, data);

                // 1. Verify Left DateTime Axis has 40 separation and single-line dd-MMM HH:mm format
                Assert.Equal(40, chart.Axes.Left.Labels.Separation);
                Assert.Equal("dd-MMM HH:mm", chart.Axes.Left.Labels.DateTimeFormat);

                // 2. Verify channel 0 (Hookload) is on top (OtherSide == true) per vertical chart requirements
                var axis1 = Assert.IsType<Steema.TeeChart.Axis>(ch1.__scale);
                Assert.True(axis1.OtherSide, "Channel 0 should be at Top in vertical chart");
                Assert.Equal(40, axis1.Labels.Separation);
                Assert.Equal("#,##0.##", axis1.Labels.ValueFormat);

                // 3. Verify channel 1 (Torque) is on top (OtherSide == true)
                var axis2 = Assert.IsType<Steema.TeeChart.Axis>(ch2.__scale);
                Assert.True(axis2.OtherSide, "Channel 1 should be at Top in vertical chart");
                Assert.Equal(40, axis2.Labels.Separation);
                Assert.Equal("#,##0.##", axis2.Labels.ValueFormat);

                // 4. Verify channel EffectiveMin and EffectiveMax are populated from data
                Assert.Equal(120.0, ch1.EffectiveMin);
                Assert.Equal(130.0, ch1.EffectiveMax);
                Assert.Equal("120", ch1.FormattedMin);
                Assert.Equal("130", ch1.FormattedMax);
                Assert.Equal("120 - 130", ch1.FormattedRange);

                Assert.Equal(5000.0, ch2.EffectiveMin);
                Assert.Equal(6000.0, ch2.EffectiveMax);
                Assert.Equal("5,000", ch2.FormattedMin);
                Assert.Equal("6,000", ch2.FormattedMax);
                Assert.Equal("5,000 - 6,000", ch2.FormattedRange);

                // 5. Verify LeftAxisOffset returns layout offset
                Assert.True(renderer.LeftAxisOffset > 0, "LeftAxisOffset should be positive for vertical log");
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (threadEx != null) throw threadEx;
    }

    [Fact]
    public void VHTrackChannel_FormattingHelpers_FormatValuesAndRangesAccurately()
    {
        var channel = new VHTrackChannel
        {
            Mnemonic = "TEST_CH",
            EffectiveMin = 0.0,
            EffectiveMax = 30.0,
            LineColor = "#8E24AA",
            objXAxis = new RTAxis { Min = 0.0, Max = 30.0, AutoScale = false }
        };

        Assert.Equal("0", channel.FormattedMin);
        Assert.Equal("30", channel.FormattedMax);
        Assert.Equal("0 - 30", channel.FormattedRange);

        // Large number with commas
        channel.EffectiveMin = 1500;
        channel.EffectiveMax = 25000;
        Assert.Equal("1,500", channel.FormattedMin);
        Assert.Equal("25,000", channel.FormattedMax);
        Assert.Equal("1,500 - 25,000", channel.FormattedRange);

        // Decimal formatting
        channel.EffectiveMin = 1.25;
        channel.EffectiveMax = 9.87;
        Assert.Equal("1.25", channel.FormattedMin);
        Assert.Equal("9.87", channel.FormattedMax);

        // Auto range with zero/uninitialized
        var autoChannel = new VHTrackChannel
        {
            Mnemonic = "AUTO_CH",
            EffectiveMin = 0,
            EffectiveMax = 0,
            objXAxis = new RTAxis { AutoScale = true }
        };
        Assert.Equal("Auto", autoChannel.FormattedRange);
    }

    [Fact]
    public void TChartTrackConsoleRenderer_LegendAndAxisTitles_RenderCorrectly()
    {
        Exception? threadEx = null;
        var thread = new System.Threading.Thread(() =>
        {
            try
            {
                var chart = new Steema.TeeChart.WPF.TChart();
                var renderer = new TChartTrackConsoleRenderer(chart);

                var console = new VHTrackConsole
                {
                    Name = "4-track surface parameters RIGS",
                    IndexType = enumIndexType.TimeLog,
                    TrackOrientation = enumTrackOrientation.Horizontal,
                    ShowLegend = true,
                    ShowAxisTitles = true
                };

                var track1 = new VHTrack { ID = "track1", Title = "Drilling Mechanics", Width = 2.0 };
                var chTorque = new VHTrackChannel
                {
                    ID = "c1",
                    Mnemonic = "TORQUE",
                    Title = "Torque",
                    Unit = "ft lbf",
                    LineColor = "#E53935",
                    Visible = true,
                    objXAxis = new RTAxis { Mnemonic = "TORQUE", Title = "Torque", Unit = "ft lbf", Location = enumRTAxisLocation.Left }
                };
                var chRpm = new VHTrackChannel
                {
                    ID = "c2",
                    Mnemonic = "RPM",
                    Title = "RPM",
                    Unit = "rpm",
                    LineColor = "#1E88E5",
                    Visible = true,
                    objXAxis = new RTAxis { Mnemonic = "RPM", Title = "RPM", Unit = "rpm", Location = enumRTAxisLocation.Left }
                };
                var chDepth = new VHTrackChannel
                {
                    ID = "c0",
                    Mnemonic = "BIT_DEPTH",
                    Title = "Bit Depth",
                    Unit = "m",
                    LineColor = "#1976D2",
                    Visible = true,
                    objXAxis = new RTAxis { Mnemonic = "BIT_DEPTH", Title = "Bit Depth", Unit = "m" }
                };
                track1.Channels.Add(chDepth);
                track1.Channels.Add(chTorque);
                track1.Channels.Add(chRpm);
                console.Tracks.Add(track1);

                var data = new ChartDataSeriesResult
                {
                    SourceType = enumRTDataSourceType.TimeLog,
                    IndexValues = new List<double> { 46000.0, 46001.0 },
                    ChannelValues = new Dictionary<string, List<double>>
                    {
                        ["BIT_DEPTH"] = new List<double> { 1200.0, 1205.0 },
                        ["TORQUE"] = new List<double> { 500.0, 600.0 },
                        ["RPM"] = new List<double> { 60.0, 80.0 }
                    }
                };

                renderer.RenderConsole(console, data);

                // 1. Verify Legend Configuration & Panel Margins in Pixels
                Assert.Equal(Steema.TeeChart.PanelMarginUnits.Pixels, chart.Panel.MarginUnits);
                Assert.True(chart.Panel.MarginLeft < 120, "MarginLeft must be in pixels (< 120px) to prevent shrinking chart");
                Assert.True(chart.Legend.Visible);
                Assert.Equal(Steema.TeeChart.LegendAlignments.Right, chart.Legend.Alignment);
                Assert.Equal(Steema.TeeChart.LegendStyles.Series, chart.Legend.LegendStyle);
                Assert.Equal("4-track surface parameters RIGS", chart.Legend.Title.Text);

                // 2. Verify Index Axis Title
                Assert.True(chart.Axes.Bottom.Title.Visible);
                Assert.Equal("DateTime (DateTime)", chart.Axes.Bottom.Title.Text);
                Assert.Equal(0, chart.Axes.Bottom.Title.Angle);

                // 3. Horizontal orientation rule: Depth on Left (OtherSide=false, Angle=90)
                var axisDepth = Assert.IsType<Steema.TeeChart.Axis>(chDepth.__scale);
                Assert.True(axisDepth.Title.Visible);
                Assert.Equal("Bit Depth (m)", axisDepth.Title.Text);
                Assert.False(axisDepth.OtherSide, "Depth channel must be on the Left (OtherSide=false) in horizontal mode");
                Assert.Equal(90, axisDepth.Title.Angle);
                Assert.Equal(Steema.TeeChart.Drawing.StringAlignment.Center, axisDepth.Title.Alignment);
                Assert.Equal(Steema.TeeChart.Drawing.StringAlignment.Center, axisDepth.Labels.TextAlign);

                // 4. Horizontal orientation rule: Non-depth channels on Right (OtherSide=true, Angle=270)
                var axisTorque = Assert.IsType<Steema.TeeChart.Axis>(chTorque.__scale);
                Assert.True(axisTorque.Title.Visible);
                Assert.Equal("Torque (ft lbf)", axisTorque.Title.Text);
                Assert.True(axisTorque.OtherSide, "Torque (non-depth) must be on the Right (OtherSide=true) in horizontal mode");
                Assert.Equal(270, axisTorque.Title.Angle);
                Assert.Equal(Steema.TeeChart.Drawing.StringAlignment.Center, axisTorque.Title.Alignment);
                Assert.Equal(Steema.TeeChart.Drawing.StringAlignment.Center, axisTorque.Labels.TextAlign);
                Assert.Equal(ColorTranslator.FromHtml("#E53935"), axisTorque.Title.Font.Color);
                Assert.Equal(ColorTranslator.FromHtml("#E53935"), axisTorque.AxisPen.Color);

                var axisRpm = Assert.IsType<Steema.TeeChart.Axis>(chRpm.__scale);
                Assert.True(axisRpm.Title.Visible);
                Assert.Equal("RPM (rpm)", axisRpm.Title.Text);
                Assert.True(axisRpm.OtherSide, "RPM (non-depth) must be on the Right (OtherSide=true) in horizontal mode");
                Assert.Equal(270, axisRpm.Title.Angle);
                Assert.Equal(Steema.TeeChart.Drawing.StringAlignment.Center, axisRpm.Title.Alignment);
                Assert.Equal(Steema.TeeChart.Drawing.StringAlignment.Center, axisRpm.Labels.TextAlign);
                Assert.Equal(ColorTranslator.FromHtml("#1E88E5"), axisRpm.Title.Font.Color);
                Assert.Equal(Steema.TeeChart.PositionUnits.Pixels, axisRpm.PositionUnits);

                // 5. Vertical orientation rule: ALL channel axes on TOP (OtherSide=true, Angle=0)
                console.TrackOrientation = enumTrackOrientation.Vertical;
                renderer.RenderConsole(console, data);

                Assert.True(chart.Axes.Left.Title.Visible);
                Assert.Equal("Time", chart.Axes.Left.Title.Text);

                var axisDepthVert = Assert.IsType<Steema.TeeChart.Axis>(chDepth.__scale);
                Assert.True(axisDepthVert.OtherSide, "All channel axes must be on Top (OtherSide=true) in vertical mode");
                Assert.Equal(0, axisDepthVert.Title.Angle);
                Assert.Equal(Steema.TeeChart.Drawing.StringAlignment.Center, axisDepthVert.Title.Alignment);
                Assert.Equal(Steema.TeeChart.Drawing.StringAlignment.Center, axisDepthVert.Labels.TextAlign);

                var axisTorqueVert = Assert.IsType<Steema.TeeChart.Axis>(chTorque.__scale);
                Assert.True(axisTorqueVert.OtherSide, "All channel axes must be on Top (OtherSide=true) in vertical mode");
                Assert.Equal(0, axisTorqueVert.Title.Angle);
                Assert.Equal(Steema.TeeChart.Drawing.StringAlignment.Center, axisTorqueVert.Title.Alignment);
                Assert.Equal(Steema.TeeChart.Drawing.StringAlignment.Center, axisTorqueVert.Labels.TextAlign);

                var axisRpmVert = Assert.IsType<Steema.TeeChart.Axis>(chRpm.__scale);
                Assert.True(axisRpmVert.OtherSide, "All channel axes must be on Top (OtherSide=true) in vertical mode");
                Assert.Equal(0, axisRpmVert.Title.Angle);
                Assert.Equal(Steema.TeeChart.Drawing.StringAlignment.Center, axisRpmVert.Title.Alignment);
                Assert.Equal(Steema.TeeChart.Drawing.StringAlignment.Center, axisRpmVert.Labels.TextAlign);
            }
            catch (Exception ex)
            {
                threadEx = ex;
            }
        });
        thread.SetApartmentState(System.Threading.ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (threadEx != null) throw threadEx;
    }

    [Fact]
    public void TChartTrackConsoleRenderer_DepthChannelDetection_WorksAccurately()
    {
        // 1. Should identify Bit Depth and Hole Depth variations as Depth channels
        Assert.True(TChartTrackConsoleRenderer.IsDepthOrHoleDepthChannel(new VHTrackChannel { Mnemonic = "BIT_DEPTH" }));
        Assert.True(TChartTrackConsoleRenderer.IsDepthOrHoleDepthChannel(new VHTrackChannel { Mnemonic = "HOLE_DEPTH" }));
        Assert.True(TChartTrackConsoleRenderer.IsDepthOrHoleDepthChannel(new VHTrackChannel { Mnemonic = "DBIT" }));
        Assert.True(TChartTrackConsoleRenderer.IsDepthOrHoleDepthChannel(new VHTrackChannel { Mnemonic = "DMEA" }));
        Assert.True(TChartTrackConsoleRenderer.IsDepthOrHoleDepthChannel(new VHTrackChannel { Mnemonic = "MD" }));
        Assert.True(TChartTrackConsoleRenderer.IsDepthOrHoleDepthChannel(new VHTrackChannel { Mnemonic = "DEPTH" }));
        Assert.True(TChartTrackConsoleRenderer.IsDepthOrHoleDepthChannel(new VHTrackChannel { Title = "Bit Depth" }));
        Assert.True(TChartTrackConsoleRenderer.IsDepthOrHoleDepthChannel(new VHTrackChannel { Title = "Hole Depth" }));
        Assert.True(TChartTrackConsoleRenderer.IsDepthOrHoleDepthChannel(new VHTrackChannel { Title = "Measured Depth" }));

        // 2. Should NOT identify other channels as Depth channels
        Assert.False(TChartTrackConsoleRenderer.IsDepthOrHoleDepthChannel(new VHTrackChannel { Mnemonic = "RIG_STATE", Title = "Rig State" }));
        Assert.False(TChartTrackConsoleRenderer.IsDepthOrHoleDepthChannel(new VHTrackChannel { Mnemonic = "HOOK_LOAD", Title = "Hook Load" }));
        Assert.False(TChartTrackConsoleRenderer.IsDepthOrHoleDepthChannel(new VHTrackChannel { Mnemonic = "TORQUE", Title = "Torque" }));
        Assert.False(TChartTrackConsoleRenderer.IsDepthOrHoleDepthChannel(new VHTrackChannel { Mnemonic = "RPM", Title = "Rotary RPM" }));
        Assert.False(TChartTrackConsoleRenderer.IsDepthOrHoleDepthChannel(new VHTrackChannel { Mnemonic = "SPP", Title = "Standpipe Pressure" }));
    }

    [Fact]
    public void DataSelectorViewModel_InitializesAndAppliesFieldMappingRules()
    {
        var console = new VHTrackConsole { Name = "TestConsole" };
        var track = new VHTrack { Title = "Track 1" };
        var channel = new VHTrackChannel
        {
            Name = "ROPA",
            Title = "Rate of Penetration",
            Mnemonic = "ROPA",
            Unit = "m/hr"
        };
        track.Channels.Add(channel);
        console.Tracks.Add(track);

        var vm = new DataSelectorViewModel(console);

        // Verify Target Series contains the channel title
        Assert.Contains("Rate of Penetration", vm.AvailableSeries);
        Assert.Equal("Rate of Penetration", vm.TargetSeries);

        // Verify Field Mapping Rules defaults
        Assert.Contains("Timestamp", vm.AvailableLabelFields);
        Assert.Contains("DayIndex", vm.AvailableXCoordinates);
        Assert.Contains("ROPA", vm.AvailableYCoordinates);

        // Verify ApplyChanges
        vm.YCoordinate = "ROPA";
        bool closedWithResult = false;
        vm.RequestClose += (result) => closedWithResult = result;

        vm.ApplyChangesCommand.Execute(null);

        Assert.True(closedWithResult);
        Assert.Equal("ROPA", channel.Mnemonic);

        // Verify Cancel
        bool cancelResult = true;
        vm.RequestClose += (result) => cancelResult = result;
        vm.CancelCommand.Execute(null);
        Assert.False(cancelResult);
    }

    [Fact]
    public void DataSelectorViewModel_ChangingTargetSeries_UpdatesYCoordinateAndSelectedChannelMnemonic()
    {
        var console = new VHTrackConsole { Name = "TestConsole" };
        var track = new VHTrack { Title = "Track 1" };
        var channelRop = new VHTrackChannel
        {
            Name = "ROPA",
            Title = "Rate of Penetration",
            Mnemonic = "ROPA",
            Unit = "m/hr",
            LineColor = "green"
        };
        var channelWob = new VHTrackChannel
        {
            Name = "WOB",
            Title = "Weight on Bit",
            Mnemonic = "WOB",
            Unit = "klbf",
            LineColor = "blue"
        };
        track.Channels.Add(channelRop);
        track.Channels.Add(channelWob);
        console.Tracks.Add(track);

        var vm = new DataSelectorViewModel(console);
        vm.TargetSeries = "Weight on Bit";

        Assert.Equal("WOB", vm.YCoordinate);

        vm.ApplyChangesCommand.Execute(null);
        Assert.Equal("WOB", vm.SelectedChannelMnemonic);
    }

    [Fact]
    public async Task RigStateDocumentViewModel_TrackBarOverview_UpdatesWhenChannelSelected()
    {
        var session = new DrillIntel.Projects.ProjectSession();
        var console = new VHTrackConsole { Name = "TestConsole" };
        var track = new VHTrack { Title = "Track 1" };
        var channel = new VHTrackChannel
        {
            Name = "ROPA",
            Title = "Rate of Penetration",
            Mnemonic = "ROPA",
            Unit = "m/hr",
            LineColor = "#388E3C"
        };
        track.Channels.Add(channel);
        console.Tracks.Add(track);

        var vm = new RigStateDocumentViewModel(session, console: console);
        vm.SelectedOverviewChannelMnemonic = "ROPA";

        await vm.UpdateTrackBarOverviewAsync();

        Assert.Equal("Rate of Penetration (m/hr)", vm.TrackBarOverviewChannelName);
        Assert.NotNull(vm.TrackBarOverviewBrush);
    }
}


