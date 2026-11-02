using System;
using System.Collections.Generic;

namespace DrillIntel.Data.Objects.DataObjects.DrillingStandProcessor
{
    public class StandPoint : IComparable
    {
        public enum cnDayNightTime
        {
            DayTime = 0,
            NightTime = 1
        }

        public double Depth { get; set; } = 0.0;
        public double ROP { get; set; } = 0.0;
        public double RotaryROP { get; set; } = 0.0;
        public double SlideROP { get; set; } = 0.0;
        public cnDayNightTime DayNightStatus { get; set; } = cnDayNightTime.DayTime;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public Dictionary<double, double> RigStates { get; set; } = new Dictionary<double, double>();
        public double OffsetTime { get; set; } = 0.0;
        public cnDayNightTime OffsetDayNightStatus { get; set; } = cnDayNightTime.DayTime;
        public double OffsetROP { get; set; } = 0.0;
        public double OffsetRotaryROP { get; set; } = 0.0;
        public double OffsetSlideROP { get; set; } = 0.0;

        public StandPoint getCopy()
        {
            try
            {
                var objNew = new StandPoint
                {
                    Depth = Depth,
                    ROP = ROP,
                    RotaryROP = RotaryROP,
                    SlideROP = SlideROP,
                    FromDate = FromDate,
                    ToDate = ToDate,
                    DayNightStatus = DayNightStatus,
                    OffsetTime = OffsetTime,
                    OffsetDayNightStatus = OffsetDayNightStatus,
                    OffsetROP = OffsetROP,
                    OffsetRotaryROP = OffsetRotaryROP,
                    OffsetSlideROP = OffsetSlideROP
                };

                foreach (var kvp in RigStates)
                {
                    objNew.RigStates[kvp.Key] = kvp.Value;
                }

                return objNew;
            }
            catch
            {
                return new StandPoint();
            }
        }

        public int CompareTo(object? obj)
        {
            try
            {
                if (obj is StandPoint other)
                {
                    return Depth.CompareTo(other.Depth);
                }
            }
            catch { }
            return 0;
        }
    }
}

