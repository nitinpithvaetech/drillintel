using System;

namespace DrillIntel.Data.Objects.DataObjects.DQI
{
    public class WellDQI
    {
        public string WellId { get; set; } = string.Empty;
        public string Mnemonic { get; set; } = string.Empty;
        public double ConsIdx { get; set; } = 0.0;
        public double FreqIdx { get; set; } = 0.0;
        public double AccrIdx { get; set; } = 0.0;
        public double DQI { get; set; } = 0.0;

        public WellDQI() { }

        public WellDQI(string wellId, string mnemonic, double consIdx, double freqIdx, double accrIdx, double dqi)
        {
            WellId = wellId;
            Mnemonic = mnemonic;
            ConsIdx = consIdx;
            FreqIdx = freqIdx;
            AccrIdx = accrIdx;
            DQI = dqi;
        }
    }
}

