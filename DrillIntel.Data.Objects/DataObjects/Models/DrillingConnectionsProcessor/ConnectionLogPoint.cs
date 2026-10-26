using System;
using System.Collections.Generic;

namespace DrillIntel.Data.Objects.DataObjects.Models.DrillingConnectionsProcessor
{
    public class ConnectionLogPoint
    {
        public enum cnDayNightTime
        {
            DayTime = 0,
            NightTime = 1
        }

        public enum cnNOVOSStatus
        {
            NotApplicable = 0,
            Automated = 1,
            Manual = 2,
            Hybrid = 3
        }

        public double Depth { get; set; } = 0.0;
        public double BottomToSlips { get; set; } = 0.0;
        public double SlipsToSlips { get; set; } = 0.0;
        public double SlipsToBottom { get; set; } = 0.0;

        public double BottomToSlipsSec { get; set; } = 0.0;
        public double SlipsToSlipsSec { get; set; } = 0.0;
        public double SlipsToBottomSec { get; set; } = 0.0;

        public cnDayNightTime DayNightStatus { get; set; } = cnDayNightTime.DayTime;
        public double TotalSeconds { get; set; } = 0.0;

        public Dictionary<double, double> BottomToSlipsRigStates { get; set; } = new Dictionary<double, double>();
        public Dictionary<double, double> SlipsToBottomRigStates { get; set; } = new Dictionary<double, double>();
        public Dictionary<double, double> SlipsToSlipsRigStates { get; set; } = new Dictionary<double, double>();

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public double BottomToSlipsSeconds { get; set; } = 0.0;
        public double SlipsToSlipsSeconds { get; set; } = 0.0;
        public double SlipsToBottomSeconds { get; set; } = 0.0;

        public double AvgECD { get; set; } = 0.0;
        public double AvgOnBottomROP { get; set; } = 0.0;
        public double AvgROP { get; set; } = 0.0;

        // Exact Dates
        public DateTime BottomEndDate { get; set; }
        public DateTime SlipStartDate { get; set; }
        public DateTime SlipEndDate { get; set; }
        public DateTime BottomStartDate { get; set; }

        public cnNOVOSStatus NOVStatus { get; set; } = cnNOVOSStatus.NotApplicable;

        public void updateSeconds()
        {
            try
            {
                BottomToSlipsSeconds = Math.Abs((BottomEndDate - SlipStartDate).TotalSeconds);
                SlipsToSlipsSeconds = Math.Abs((SlipStartDate - SlipEndDate).TotalSeconds);
                SlipsToBottomSeconds = Math.Abs((SlipEndDate - BottomStartDate).TotalSeconds);

                BottomToSlips = BottomToSlipsSeconds;
                SlipsToSlips = SlipsToSlipsSeconds;
                SlipsToBottom = SlipsToBottomSeconds;
            }
            catch
            {
            }
        }

        public void updateDates()
        {
            try
            {
                BottomEndDate = FromDate;
                SlipStartDate = BottomEndDate.AddSeconds(BottomToSlipsSeconds);
                SlipEndDate = SlipStartDate.AddSeconds(SlipsToSlipsSeconds);
                BottomStartDate = SlipEndDate.AddSeconds(SlipsToBottomSeconds);
            }
            catch
            {
            }
        }

        public ConnectionLogPoint getCopy()
        {
            try
            {
                var objNew = new ConnectionLogPoint
                {
                    Depth = Depth,
                    BottomToSlips = BottomToSlips,
                    SlipsToSlips = SlipsToSlips,
                    SlipsToBottom = SlipsToBottom,
                    DayNightStatus = DayNightStatus,
                    TotalSeconds = TotalSeconds,
                    FromDate = FromDate,
                    ToDate = ToDate,
                    BottomToSlipsSeconds = BottomToSlipsSeconds,
                    SlipsToSlipsSeconds = SlipsToSlipsSeconds,
                    SlipsToBottomSeconds = SlipsToBottomSeconds,
                    AvgECD = AvgECD,
                    AvgOnBottomROP = AvgOnBottomROP,
                    AvgROP = AvgROP,
                    BottomEndDate = BottomEndDate,
                    SlipStartDate = SlipStartDate,
                    SlipEndDate = SlipEndDate,
                    BottomStartDate = BottomStartDate,
                    NOVStatus = NOVStatus
                };

                foreach (var kvp in BottomToSlipsRigStates)
                {
                    objNew.BottomToSlipsRigStates.Add(kvp.Key, kvp.Value);
                }

                foreach (var kvp in SlipsToBottomRigStates)
                {
                    objNew.SlipsToBottomRigStates.Add(kvp.Key, kvp.Value);
                }

                foreach (var kvp in SlipsToSlipsRigStates)
                {
                    objNew.SlipsToSlipsRigStates.Add(kvp.Key, kvp.Value);
                }

                return objNew;
            }
            catch
            {
                return new ConnectionLogPoint();
            }
        }
    }
}

