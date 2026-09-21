using System;
using System.Globalization;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    public class DataService
    {
        public static string checkNull(object? value, string defaultValue)
        {
            if (value == null || value == DBNull.Value) return defaultValue;
            return value.ToString() ?? defaultValue;
        }

        public static double checkNull(object? value, double defaultValue)
        {
            if (value == null || value == DBNull.Value) return defaultValue;
            if (double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                return d;
            return defaultValue;
        }

        public static int checkNull(object? value, int defaultValue)
        {
            if (value == null || value == DBNull.Value) return defaultValue;
            if (int.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out int i))
                return i;
            return defaultValue;
        }

        public static bool checkNull(object? value, bool defaultValue)
        {
            if (value == null || value == DBNull.Value) return defaultValue;
            if (bool.TryParse(value.ToString(), out bool b))
                return b;
            return defaultValue;
        }
    }
}

