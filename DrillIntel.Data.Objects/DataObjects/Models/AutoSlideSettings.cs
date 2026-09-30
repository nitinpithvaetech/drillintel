using System;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    public class AutoSlideSettings
    {
        public double FromDepth { get; set; }
        public double ToDepth { get; set; }
        public double MinTorque { get; set; }
        public double MaxTorque { get; set; }
        public int CalibrationRows { get; set; }
        public double MinTorqueDiff { get; set; }
        public double MinRPM { get; set; }
        public double MaxRPM { get; set; }

        public AutoSlideSettings GetCopy() => new AutoSlideSettings
        {
            FromDepth = this.FromDepth,
            ToDepth = this.ToDepth,
            MinTorque = this.MinTorque,
            MaxTorque = this.MaxTorque,
            CalibrationRows = this.CalibrationRows,
            MinTorqueDiff = this.MinTorqueDiff,
            MinRPM = this.MinRPM,
            MaxRPM = this.MaxRPM
        };
    }
}

