using System;

namespace DrillIntel.Data.Objects.DataObjects.DQI
{
    public class WellDQITracking
    {
        public string WellId { get; set; } = string.Empty;
        public DateTime? LastIndex { get; set; }

        public WellDQITracking() { }

        public WellDQITracking(string wellId, DateTime? lastIndex)
        {
            WellId = wellId;
            LastIndex = lastIndex;
        }
    }
}

