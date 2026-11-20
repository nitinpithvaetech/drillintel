using System;
using System.Collections.Generic;

namespace DrillIntel.Data.Objects.DataObjects.DQI
{
    public class DQIData
    {
        public string WellId { get; set; } = string.Empty;
        public string Mnemonic { get; set; } = string.Empty;
        public double OverallWellConsIdxAvg { get; set; } = 0.0;
        public double OverallWellFreqIdxAvg { get; set; } = 0.0;
        public double OverallWellAccrIdxAvg { get; set; } = 0.0;
        public ChannelDQIData ChannelDQI { get; set; } = new ChannelDQIData();
        public string ChartDataError { get; set; } = string.Empty;
        public string WellAvgDataError { get; set; } = string.Empty;

        public double OverallWellAvg => (OverallWellConsIdxAvg + OverallWellFreqIdxAvg + OverallWellAccrIdxAvg) / 3.0;
    }

    public class ChannelDQIData
    {
        public double ConsIdxAvg { get; set; } = 0.0;
        public double FreqIdxAvg { get; set; } = 0.0;
        public double AccrIdxAvg { get; set; } = 0.0;
        public double OverAllChannelDQI { get; set; } = 0.0;
        public List<ChannelDataPoint> Points { get; set; } = new List<ChannelDataPoint>();
    }

    public class ChannelDataPoint
    {
        public DateTime DateTime { get; set; } = DateTime.MinValue;
        public double ConsIdx { get; set; } = 0.0;
        public double FreqIdx { get; set; } = 0.0;
        public double AccrIdx { get; set; } = 0.0;

        public ChannelDataPoint() { }

        public ChannelDataPoint(DateTime dateTime, double consIdx, double freqIdx, double accrIdx)
        {
            DateTime = dateTime;
            ConsIdx = consIdx;
            FreqIdx = freqIdx;
            AccrIdx = accrIdx;
        }
    }

    public class WellAverageIndices
    {
        public double ConsistencyAvg { get; set; } = 0.0;
        public double FrequencyAvg { get; set; } = 0.0;
        public double AccuracyAvg { get; set; } = 0.0;
        public double OverallAvg => (ConsistencyAvg + FrequencyAvg + AccuracyAvg) / 3.0;
    }

    public class DQIWellLookupItem
    {
        public string WellId { get; set; } = string.Empty;
        public string WellName { get; set; } = string.Empty;

        public DQIWellLookupItem() { }

        public DQIWellLookupItem(string wellId, string wellName)
        {
            WellId = wellId;
            WellName = wellName;
        }

        public override string ToString() => WellName;
    }
}
