using System;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    /// <summary>
    /// Represents data point for Hookload and Torque plans.
    /// Converted from HookloadPlanData.vb.
    /// </summary>
    public class HookloadPlanData
    {
        public double Depth { get; set; } = 0;
        public double Weight { get; set; } = 0;
        public double MaxTension { get; set; } = 0;
        public double MinTension { get; set; } = 0;
        public double MaxCompress { get; set; } = 0;
        public double MinCompress { get; set; } = 0;

        public HookloadPlanData GetCopy()
        {
            return new HookloadPlanData
            {
                Depth = this.Depth,
                Weight = this.Weight,
                MaxTension = this.MaxTension,
                MinTension = this.MinTension,
                MaxCompress = this.MaxCompress,
                MinCompress = this.MinCompress
            };
        }
    }
}
