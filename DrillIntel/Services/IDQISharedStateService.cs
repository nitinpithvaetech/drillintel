using System;
using DrillIntel.Data.Objects.DataObjects.DQI;

namespace DrillIntel.Services
{
    public enum DQIChartType
    {
        Line,
        Area
    }

    public interface IDQISharedStateService
    {
        string? SelectedWellId { get; set; }
        string? SelectedWellName { get; set; }
        DQIChannel? SelectedChannel { get; set; }
        DQIChartType ChartType { get; set; }
        bool IsConfigOpen { get; set; }
        WellAverageIndices? WellAvgIndices { get; set; }
        DQIData? CurrentDQIData { get; set; }

        event EventHandler? WellChanged;
        event EventHandler? ChannelChanged;
        event EventHandler? ChartTypeChanged;
        event EventHandler? ConfigOpenChanged;
        event EventHandler? DataRefreshed;
        event EventHandler? ConfigurationSaved;

        void NotifyDataRefreshed();
        void NotifyConfigurationSaved();
    }
}

