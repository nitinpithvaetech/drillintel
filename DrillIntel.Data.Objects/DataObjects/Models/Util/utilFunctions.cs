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
    }
}

