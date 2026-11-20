using System;
using System.Collections.Generic;
using System.Linq;

namespace DrillIntel.Data.Objects.DataObjects.DQI
{
    public class DQINullValue
    {
        public string NullValues { get; set; } = "-999.25, -999.0";

        public DQINullValue() { }

        public DQINullValue(string nullValues)
        {
            NullValues = nullValues;
        }

        public HashSet<double> ParseValues()
        {
            var result = new HashSet<double>();
            if (string.IsNullOrWhiteSpace(NullValues)) return result;

            var cleaned = NullValues.Trim('"');
            foreach (var part in cleaned.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (double.TryParse(part.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double v))
                {
                    result.Add(v);
                }
            }
            return result;
        }
    }
}

