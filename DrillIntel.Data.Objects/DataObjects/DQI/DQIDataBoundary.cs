using System;

namespace DrillIntel.Data.Objects.DataObjects.DQI
{
    public class DQIDataBoundary
    {
        public string Mnemonic { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Unit { get; set; } = string.Empty;
        public double Min { get; set; } = 0.0;
        public double Max { get; set; } = 0.0;

        public DQIDataBoundary() { }

        public DQIDataBoundary(string mnemonic, string name, string unit, double min, double max)
        {
            Mnemonic = mnemonic;
            Name = name;
            Unit = unit;
            Min = min;
            Max = max;
        }

        public DQIDataBoundary Clone() => new DQIDataBoundary
        {
            Mnemonic = Mnemonic,
            Name = Name,
            Unit = Unit,
            Min = Min,
            Max = Max
        };
    }
}

