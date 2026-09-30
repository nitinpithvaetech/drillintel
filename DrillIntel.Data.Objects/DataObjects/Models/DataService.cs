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

        public static DateTime checkNull(object? value, DateTime defaultValue)
        {
            if (value == null || value == DBNull.Value) return defaultValue;
            if (value is DateTime dt) return dt;
            string str = value.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(str)) return defaultValue;

            // If string is only a time (e.g. "07:07:55" or "15:10:05"), it has no date component - return defaultValue
            if (!str.Contains("-") && !str.Contains("/") && !str.Contains(" ") && TimeSpan.TryParse(str, out _))
            {
                return defaultValue;
            }

            if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                return parsed;
            if (DateTime.TryParse(str, out parsed))
                return parsed;
            if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out double oaDate))
            {
                try { return DateTime.FromOADate(oaDate); } catch { }
            }
            return defaultValue;
        }
    }
}

