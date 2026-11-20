using System;
using System.Collections.Generic;
using System.Drawing;
using DrillIntel.Data.Objects.DataObjects.DQI;
using DrillIntel.Services;
using Steema.TeeChart.Styles;
using Steema.TeeChart.WPF;

namespace DrillIntel.Views.DQI
{
    public static class DQITeeChartRenderer
    {
        // Light Theme Colors matching DrillIntel Standard Dialogs
        public static readonly Color BackgroundColor = Color.FromArgb(255, 255, 255);       // #FFFFFF
        public static readonly Color WallColor = Color.FromArgb(248, 250, 252);             // #F8FAFC
        public static readonly Color GridColor = Color.FromArgb(226, 232, 240);             // #E2E8F0
        public static readonly Color AxisLineColor = Color.FromArgb(203, 213, 225);          // #CBD5E1
        public static readonly Color LabelColor = Color.FromArgb(71, 85, 105);              // #475569

        // Series Palette (High contrast vibrant colors for light theme)
        public static readonly Color ConsistencyColor = Color.FromArgb(2, 132, 199);        // #0284C7 (Rich Sky/Blue)
        public static readonly Color FrequencyColor = Color.FromArgb(22, 163, 74);          // #16A34A (Rich Green)
        public static readonly Color AccuracyColor = Color.FromArgb(217, 119, 6);           // #D97706 (Rich Amber)

        public static void SetupTrackChart(TChart chart)
        {
            if (chart == null) return;

            chart.Aspect.View3D = false;
            chart.Panel.Brush.Color = BackgroundColor;
            chart.Panel.Brush.Gradient.Visible = false;
            chart.Walls.Back.Brush.Color = WallColor;
            chart.Walls.Back.Brush.Gradient.Visible = false;
            chart.Walls.Back.Pen.Color = AxisLineColor;
            chart.Legend.Visible = false;

            // X Axis (DateTime)
            var bAxis = chart.Axes.Bottom;
            bAxis.Labels.DateTimeFormat = "MMM-dd-yyyy HH:mm:ss";
            bAxis.Labels.Font.Color = LabelColor;
            bAxis.Labels.Font.Size = 9;
            bAxis.Grid.Color = GridColor;
            bAxis.AxisPen.Color = AxisLineColor;
            bAxis.Title.Visible = false;

            // Y Axis (0 - 100 Index)
            var lAxis = chart.Axes.Left;
            lAxis.Automatic = false;
            lAxis.Minimum = 0.0;
            lAxis.Maximum = 100.0;
            lAxis.Increment = 20.0;
            lAxis.Labels.Font.Color = LabelColor;
            lAxis.Labels.Font.Size = 9;
            lAxis.Grid.Color = GridColor;
            lAxis.AxisPen.Color = AxisLineColor;
            lAxis.Title.Visible = false;

            // Chart margins
            chart.Panel.MarginTop = 5;
            chart.Panel.MarginBottom = 15;
            chart.Panel.MarginLeft = 20;
            chart.Panel.MarginRight = 20;
        }

        public static void RenderSeries(
            TChart chart,
            List<ChannelDataPoint> points,
            Func<ChannelDataPoint, double> valueSelector,
            Color seriesColor,
            DQIChartType chartType)
        {
            if (chart == null) return;

            chart.Series.Clear();
            if (points == null || points.Count == 0)
            {
                chart.Invalidate();
                return;
            }

            if (chartType == DQIChartType.Area)
            {
                var area = new Area(chart.Chart);
                area.XValues.DateTime = true;
                area.LinePen.Color = seriesColor;
                area.LinePen.Width = 2;
                area.AreaBrush.Color = Color.FromArgb(50, seriesColor.R, seriesColor.G, seriesColor.B);
                area.AreaBrush.Gradient.Visible = false;
                area.AreaLines.Visible = false;

                foreach (var pt in points)
                {
                    area.Add(pt.DateTime, valueSelector(pt));
                }
            }
            else
            {
                var line = new FastLine(chart.Chart);
                line.XValues.DateTime = true;
                line.LinePen.Color = seriesColor;
                line.LinePen.Width = 2;
                line.TreatNulls = TreatNullsStyle.Ignore;

                foreach (var pt in points)
                {
                    line.Add(pt.DateTime, valueSelector(pt));
                }
            }

            chart.Axes.Bottom.Automatic = true;
            chart.Axes.Left.Automatic = false;
            chart.Axes.Left.Minimum = 0.0;
            chart.Axes.Left.Maximum = 100.0;
            chart.Invalidate();
        }

        public static void SynchronizeAxes(TChart master, params TChart[] slaves)
        {
            if (master == null || slaves == null) return;

            master.Zoomed += (s, e) =>
            {
                foreach (var slave in slaves)
                {
                    if (slave != null)
                    {
                        slave.Axes.Bottom.SetMinMax(master.Axes.Bottom.Minimum, master.Axes.Bottom.Maximum);
                        slave.Invalidate();
                    }
                }
            };

            master.UndoneZoom += (s, e) =>
            {
                foreach (var slave in slaves)
                {
                    if (slave != null)
                    {
                        slave.Axes.Bottom.Automatic = true;
                        slave.Invalidate();
                    }
                }
            };

            master.Scroll += (s, e) =>
            {
                foreach (var slave in slaves)
                {
                    if (slave != null)
                    {
                        slave.Axes.Bottom.SetMinMax(master.Axes.Bottom.Minimum, master.Axes.Bottom.Maximum);
                        slave.Invalidate();
                    }
                }
            };
        }
    }
}
