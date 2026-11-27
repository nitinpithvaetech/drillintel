using System;
using System.Collections.Generic;
using DrillIntel.Data.Objects.DataObjects.Models.DrillingConnectionsProcessor;

namespace DrillIntel.Data.Objects.DataObjects.TripConnectionProcessor
{
    /// <summary>Represents an off-bottom slips-to-slips trip connection.</summary>
    public class TripConnectionPoint
    {
        public double Depth { get; set; }
        public double SlipsToSlips { get; set; }
        public double SlipsToSlipsSeconds { get; set; }
        public double TotalSeconds { get; set; }
        public ConnectionLogPoint.cnDayNightTime DayNightStatus { get; set; } = ConnectionLogPoint.cnDayNightTime.DayTime;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public Dictionary<int, double> SlipsToSlipsRigStates { get; set; } = new();
    }
}
