using System;
using System.Globalization;

namespace DrillIntel.Data.Objects.DataObjects.Models.Util
{
    public static class utilFunctions
    {
        public static string quote(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Replace("'", "''");
        }

        public static string parseDate(string? dateVal)
        {
            if (string.IsNullOrWhiteSpace(dateVal)) return "";
            if (DateTime.TryParse(dateVal, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt) ||
                DateTime.TryParse(dateVal, out dt))
            {
                return dt.ToString("dd-MMM-yyyy HH:mm:ss");
            }
            return dateVal.Trim();
        }

        public static string parseDateForDB(string? dateVal)
        {
            string parsed = parseDate(dateVal);
            if (string.IsNullOrWhiteSpace(parsed)) return "NULL";
            return "'" + parsed.Replace("'", "''") + "'";
        }

        // --- [NEW LOGIC (parseDateToUTC: converts date string/object to UTC DateTime)] ---
        public static DateTime parseDateToUTC(object? dateVal)
        {
            if (dateVal == null || dateVal == DBNull.Value) return DateTime.MinValue;
            if (dateVal is DateTime dt) return dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            string s = Convert.ToString(dateVal) ?? "";
            if (string.IsNullOrWhiteSpace(s)) return DateTime.MinValue;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed))
                return parsed;
            if (DateTime.TryParse(s, out parsed))
                return parsed.ToUniversalTime();
            return DateTime.MinValue;
        }

        // --- [NEW LOGIC (convertDate: converts date string/object to DateTime with optional timeZone)] ---
        public static DateTime convertDate(object? dateVal, string? timeZone = null)
        {
            if (dateVal == null || dateVal == DBNull.Value) return DateTime.MinValue;
            if (dateVal is DateTime dt) return dt;
            string s = Convert.ToString(dateVal) ?? "";
            if (string.IsNullOrWhiteSpace(s)) return DateTime.MinValue;
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsed))
                return parsed;
            if (DateTime.TryParse(s, out parsed))
                return parsed;
            return DateTime.MinValue;
        }
    }
}

