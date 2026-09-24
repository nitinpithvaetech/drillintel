using DrillIntel.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace DrillIntel.Services;

/// <summary>
/// Options for parsing and formatting DateTime values during Timelog import.
/// Directly mirrors legacy VuMax Main (DataImporter.vb / frmMain.vb).
/// </summary>
public class TimeLogDateTimeOptions
{
    public bool IsDatetimeInSeperatorColumn { get; set; }
    public string DatetimeSeparator { get; set; } = string.Empty;
    public int DateColNo { get; set; } = 0;
    public int TimeColNo { get; set; } = 1;
    public DateFormatType DateFormat { get; set; } = DateFormatType.ISOFormat;
    public int SingleDateTimeColIdx { get; set; } = -1;
    public string? DateColHeader { get; set; }
    public string? TimeColHeader { get; set; }
    public bool CarryForwardMissingDates { get; set; } = true;
}

/// <summary>
/// Result of validating a sequence of TimeLog records or DateTime values.
/// </summary>
public class TimeLogValidationResult
{
    public bool IsValid { get; set; } = true;
    public bool IsSequential { get; set; } = true;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int NonSequentialCount { get; set; }
    public int DuplicateTimestampCount { get; set; }
    public int DuplicateDateVaryingTimeCount { get; set; }
    public DateTime? MinDateTime { get; set; }
    public DateTime? MaxDateTime { get; set; }
    public List<string> ValidationMessages { get; set; } = new();
}

/// <summary>
/// Replicates and enhances the DateTime parsing, merge, and conversion rules from legacy VuMax Main (DataImporter.vb).
/// Supports ISO, British (dd-MM-yyyy), and American (MM-dd-yyyy) formats,
/// combined single-column or separate date/time column inputs, custom separators,
/// and comprehensive sequentiality validation.
/// </summary>
public static class TimeLogDateTimeParser
{
    private static readonly string[] IsoFormats =
    {
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm:ss.ffffff",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ss.ffffff",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "yyyy/MM/dd HH:mm:ss",
        "yyyy/MM/dd HH:mm:ss.fff",
        "yyyy/MM/dd HH:mm",
        "yyyy-MM-dd hh:mm:ss tt",
        "yyyy-MM-dd h:mm:ss tt",
        "yyyy-MM-dd hh:mm tt",
        "yyyy/MM/dd hh:mm:ss tt",
        "yyyy-M-d HH:mm:ss",
        "yyyy-M-d H:m:s",
        "yyyy-MM-dd",
        "yyyy/MM/dd",
        "o",
        "s"
    };

    private static readonly string[] DdMmYyyyFormats =
    {
        "dd-MM-yyyy HH:mm:ss",
        "dd-MM-yyyy HH:mm:ss.fff",
        "dd-MM-yyyy HH:mm:ss.ffffff",
        "dd-MM-yyyy HH:mm",
        "dd/MM/yyyy HH:mm:ss",
        "dd/MM/yyyy HH:mm:ss.fff",
        "dd/MM/yyyy HH:mm:ss.ffffff",
        "dd/MM/yyyy HH:mm",
        "dd.MM.yyyy HH:mm:ss",
        "dd.MM.yyyy HH:mm:ss.fff",
        "dd.MM.yyyy HH:mm",
        "dd-MMM-yyyy HH:mm:ss",
        "dd-MMM-yyyy HH:mm:ss.fff",
        "dd-MMM-yyyy HH:mm",
        "dd-MM-yyyy hh:mm:ss tt",
        "dd/MM/yyyy hh:mm:ss tt",
        "d/M/yyyy HH:mm:ss",
        "d/M/yyyy H:m:s",
        "d-M-yyyy HH:mm:ss",
        "d-M-yyyy H:m:s",
        "d.M.yyyy HH:mm:ss",
        "d/M/yyyy hh:mm:ss tt",
        "d-M-yyyy hh:mm:ss tt",
        "d/M/yyyy h:mm:ss tt",
        "d-M-yyyy h:mm:ss tt",
        "dd-MM-yyyy",
        "dd/MM/yyyy",
        "dd.MM.yyyy",
        "dd-MMM-yyyy",
        "d/M/yyyy",
        "d-M-yyyy",
        "d.M.yyyy"
    };

    private static readonly string[] MmDdYyyyFormats =
    {
        "MM-dd-yyyy HH:mm:ss",
        "MM-dd-yyyy HH:mm:ss.fff",
        "MM-dd-yyyy HH:mm:ss.ffffff",
        "MM-dd-yyyy HH:mm",
        "MM/dd/yyyy HH:mm:ss",
        "MM/dd/yyyy HH:mm:ss.fff",
        "MM/dd/yyyy HH:mm:ss.ffffff",
        "MM/dd/yyyy HH:mm",
        "MM.dd.yyyy HH:mm:ss",
        "MM.dd.yyyy HH:mm:ss.fff",
        "MM.dd.yyyy HH:mm",
        "MM-dd-yyyy hh:mm:ss tt",
        "MM/dd/yyyy hh:mm:ss tt",
        "M/d/yyyy HH:mm:ss",
        "M/d/yyyy H:m:s",
        "M-d-yyyy HH:mm:ss",
        "M-d-yyyy H:m:s",
        "M/d/yyyy hh:mm:ss tt",
        "M-d-yyyy hh:mm:ss tt",
        "M/d/yyyy h:mm:ss tt",
        "M-d-yyyy h:mm:ss tt",
        "MM-dd-yyyy",
        "MM/dd/yyyy",
        "MM.dd.yyyy",
        "M/d/yyyy",
        "M-d-yyyy"
    };

    private static readonly string[] TimeFormats =
    {
        "HH:mm:ss",
        "HH:mm:ss.fff",
        "HH:mm:ss.ffffff",
        "HH:mm",
        "H:mm:ss",
        "H:mm:ss.fff",
        "H:mm",
        "hh:mm:ss tt",
        "h:mm:ss tt",
        "hh:mm tt",
        "h:mm tt"
    };

    public static bool TryParse(
        string[] tokens,
        TimeLogDateTimeOptions options,
        out string formattedDate)
    {
        return TryParse(tokens, options, out _, out formattedDate);
    }

    public static bool TryParse(
        string[] tokens,
        TimeLogDateTimeOptions options,
        out DateTime parsedDate,
        out string formattedDate)
    {
        parsedDate = default;
        formattedDate = string.Empty;

        if (tokens == null || tokens.Length == 0 || options == null)
            return false;

        string dateTimeRaw;

        // 1. Separate Date and Time columns (IsDatetimeInSeperatorColumn)
        if (options.IsDatetimeInSeperatorColumn)
        {
            int dateIdx = options.DateColNo;
            int timeIdx = options.TimeColNo;

            // 1. Try 0-based indexing first
            if (dateIdx >= 0 && dateIdx < tokens.Length && timeIdx >= 0 && timeIdx < tokens.Length)
            {
                if (TryMergeRowTokens(tokens[dateIdx], tokens[timeIdx], options.DateFormat, out parsedDate))
                {
                    formattedDate = FormatDateTime(parsedDate);
                    return true;
                }
            }

            // 2. If 0-based did not match, try 1-based indexing fallback (dateIdx - 1, timeIdx - 1)
            if (dateIdx - 1 >= 0 && dateIdx - 1 < tokens.Length && timeIdx - 1 >= 0 && timeIdx - 1 < tokens.Length)
            {
                if (TryMergeRowTokens(tokens[dateIdx - 1], tokens[timeIdx - 1], options.DateFormat, out parsedDate))
                {
                    formattedDate = FormatDateTime(parsedDate);
                    return true;
                }
            }

            // 3. Candidate fallback: try raw string combination $"{d} {t}"
            if (dateIdx >= 0 && dateIdx < tokens.Length && timeIdx >= 0 && timeIdx < tokens.Length)
            {
                var d = tokens[dateIdx]?.Trim().Trim('"', '\'') ?? "";
                var t = tokens[timeIdx]?.Trim().Trim('"', '\'') ?? "";
                if (!string.IsNullOrWhiteSpace(d) && !string.IsNullOrWhiteSpace(t))
                {
                    string cand = $"{d} {t}";
                    if (TryParseDateTimeInternal(cand, options.DateFormat, out parsedDate))
                    {
                        formattedDate = FormatDateTime(parsedDate);
                        return true;
                    }
                }
            }

            return false;
        }
        else
        {
            // Single column containing date and time
            int colIdx = options.SingleDateTimeColIdx >= 0 && options.SingleDateTimeColIdx < tokens.Length
                ? options.SingleDateTimeColIdx
                : (options.DateColNo >= 0 && options.DateColNo < tokens.Length ? options.DateColNo : -1);

            if (colIdx < 0)
                return false;

            dateTimeRaw = tokens[colIdx]?.Trim().Trim('"', '\'') ?? string.Empty;

            // Handle custom DateTimeSeparator within a single column
            if (!string.IsNullOrWhiteSpace(options.DatetimeSeparator) && dateTimeRaw.Contains(options.DatetimeSeparator))
            {
                var parts = dateTimeRaw.Split(new[] { options.DatetimeSeparator }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    string datePart;
                    string timePart;
                    if (parts[0].Contains(":"))
                    {
                        timePart = parts[0].Trim();
                        datePart = parts[1].Trim();
                    }
                    else
                    {
                        datePart = parts[0].Trim();
                        timePart = parts[1].Trim();
                    }
                    dateTimeRaw = $"{datePart} {timePart}";
                }
            }
        }

        if (string.IsNullOrWhiteSpace(dateTimeRaw) || dateTimeRaw == "-999.25" || dateTimeRaw == "-9999")
            return false;

        // 2. Parse raw string according to configured DateFormat
        if (TryParseDateTimeInternal(dateTimeRaw, options.DateFormat, out parsedDate))
        {
            formattedDate = FormatDateTime(parsedDate);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Accurately merges a row's Date token and Time token into a single DateTime.
    /// Preserves the row's original Date and corresponding Time.
    /// Handles duplicate Dates with varying Times cleanly without cross-pollution.
    /// Handles Excel exported dates with embedded 00:00:00 or time strings with embedded 1899 date prefixes.
    /// </summary>
    public static bool TryMergeRowTokens(string? dateToken, string? timeToken, DateFormatType format, out DateTime mergedDate)
    {
        mergedDate = default;
        if (string.IsNullOrWhiteSpace(dateToken) || string.IsNullOrWhiteSpace(timeToken))
            return false;

        string d = dateToken.Trim().Trim('"', '\'');
        string t = timeToken.Trim().Trim('"', '\'');

        if (d == "-999.25" || d == "-9999" || t == "-999.25" || t == "-9999")
            return false;

        // 1. Robust component extraction: Date part from Date column, Time part from Time column
        if (TryExtractDate(d, format, out var datePart) && TryExtractTime(t, out var timePart))
        {
            mergedDate = datePart.Date.Add(timePart);
            return true;
        }

        // 2. Fallback: direct string combination
        string cand = $"{d} {t}";
        if (TryParseDateTimeInternal(cand, format, out mergedDate))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Extracts the calendar Date portion from a Date column string.
    /// Robust against embedded time parts (e.g. "2024-05-01 00:00:00" from Excel or databases).
    /// </summary>
    public static bool TryExtractDate(string raw, DateFormatType format, out DateTime datePart)
    {
        datePart = default;
        if (string.IsNullOrWhiteSpace(raw) || raw == "-999.25" || raw == "-9999")
            return false;

        raw = raw.Trim().Trim('"', '\'');

        // Try direct parsing with specified format
        if (TryParseDateTimeInternal(raw, format, out var fullDt))
        {
            datePart = fullDt.Date;
            return true;
        }

        // If string contains time like "2024-05-01 00:00:00", take the first part before whitespace
        int spaceIdx = raw.IndexOf(' ');
        if (spaceIdx > 0)
        {
            string dateOnlyStr = raw.Substring(0, spaceIdx).Trim();
            if (TryParseDateTimeInternal(dateOnlyStr, format, out var dtOnly))
            {
                datePart = dtOnly.Date;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Extracts the TimeSpan from a Time column string.
    /// Robust against 24-hr, 12-hr AM/PM, and dummy date prefixes (e.g. "1899-12-30 14:30:00" from Excel OADate).
    /// </summary>
    public static bool TryExtractTime(string raw, out TimeSpan timePart)
    {
        timePart = default;
        if (string.IsNullOrWhiteSpace(raw) || raw == "-999.25" || raw == "-9999")
            return false;

        raw = raw.Trim().Trim('"', '\'');

        // Direct TimeSpan parse (e.g. "14:30:00" or "14:30:00.123")
        if (TimeSpan.TryParse(raw, CultureInfo.InvariantCulture, out timePart))
        {
            return true;
        }

        // If time includes AM/PM or date prefix (e.g. "02:30:00 PM" or "1899-12-30 14:30:00")
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtFull))
        {
            timePart = dtFull.TimeOfDay;
            return true;
        }

        // Try exact time formats
        if (DateTime.TryParseExact(raw, TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dtExact))
        {
            timePart = dtExact.TimeOfDay;
            return true;
        }

        if (DateTime.TryParse(raw, out var dtAny))
        {
            timePart = dtAny.TimeOfDay;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Formats a DateTime using VuMax standard notation ("dd-MMM-yyyy HH:mm:ss" or with ".fff" if milliseconds present).
    /// </summary>
    public static string FormatDateTime(DateTime dt)
    {
        return dt.Millisecond > 0
            ? dt.ToString("dd-MMM-yyyy HH:mm:ss.fff", CultureInfo.InvariantCulture)
            : dt.ToString("dd-MMM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Auto-detects if source headers contain separate Date and Time columns.
    /// </summary>
    public static bool TryDetectSeparateDateTimeColumns(
        IList<string> headers,
        out int dateColIdx,
        out int timeColIdx)
    {
        dateColIdx = -1;
        timeColIdx = -1;
        if (headers == null || headers.Count == 0)
            return false;

        // Check if there is already a primary combined DATETIME / TIMESTAMP column
        bool hasCombined = headers.Any(h =>
            h.Equals("DATETIME", StringComparison.OrdinalIgnoreCase) ||
            h.Equals("DATE_TIME", StringComparison.OrdinalIgnoreCase) ||
            h.Equals("DATE TIME", StringComparison.OrdinalIgnoreCase) ||
            h.Equals("TIMESTAMP", StringComparison.OrdinalIgnoreCase));

        if (hasCombined)
            return false;

        for (int i = 0; i < headers.Count; i++)
        {
            var h = headers[i]?.Trim() ?? "";
            if (string.IsNullOrEmpty(h)) continue;

            // Date match
            if (dateColIdx < 0)
            {
                if (h.Equals("DATE", StringComparison.OrdinalIgnoreCase) ||
                    h.Equals("LOGDATE", StringComparison.OrdinalIgnoreCase) ||
                    h.Equals("LOG_DATE", StringComparison.OrdinalIgnoreCase) ||
                    h.Equals("DATE_UTC", StringComparison.OrdinalIgnoreCase) ||
                    h.Equals("RECORD_DATE", StringComparison.OrdinalIgnoreCase) ||
                    (h.Contains("DATE", StringComparison.OrdinalIgnoreCase) && !h.Contains("TIME", StringComparison.OrdinalIgnoreCase)))
                {
                    dateColIdx = i;
                    continue;
                }
            }

            // Time match
            if (timeColIdx < 0)
            {
                if (h.Equals("TIME", StringComparison.OrdinalIgnoreCase) ||
                    h.Equals("LOGTIME", StringComparison.OrdinalIgnoreCase) ||
                    h.Equals("LOG_TIME", StringComparison.OrdinalIgnoreCase) ||
                    h.Equals("TIME_UTC", StringComparison.OrdinalIgnoreCase) ||
                    h.Equals("RECORD_TIME", StringComparison.OrdinalIgnoreCase) ||
                    (h.Contains("TIME", StringComparison.OrdinalIgnoreCase) && !h.Contains("DATE", StringComparison.OrdinalIgnoreCase)))
                {
                    timeColIdx = i;
                    continue;
                }
            }
        }

        return dateColIdx >= 0 && timeColIdx >= 0 && dateColIdx != timeColIdx;
    }

    /// <summary>
    /// Inspects sample date values from a file to auto-detect the DateFormatType.
    /// </summary>
    public static DateFormatType DetectDateFormat(IEnumerable<string> sampleDateStrings)
    {
        if (sampleDateStrings == null) return DateFormatType.ISOFormat;

        foreach (var sample in sampleDateStrings)
        {
            if (string.IsNullOrWhiteSpace(sample)) continue;
            var clean = sample.Trim().Trim('"', '\'');
            var spaceIdx = clean.IndexOf(' ');
            if (spaceIdx > 0) clean = clean.Substring(0, spaceIdx);

            if (clean.Length >= 4 && char.IsDigit(clean[0]) && char.IsDigit(clean[1]) && char.IsDigit(clean[2]) && char.IsDigit(clean[3]))
            {
                return DateFormatType.ISOFormat;
            }

            var parts = clean.Split(new[] { '/', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3)
            {
                if (int.TryParse(parts[0], out int p0) && int.TryParse(parts[1], out int p1))
                {
                    if (p0 > 12 && p1 <= 12)
                    {
                        return DateFormatType.DDMMYYYYFormat;
                    }
                    if (p1 > 12 && p0 <= 12)
                    {
                        return DateFormatType.MMDDYYYYFormat;
                    }
                }
            }
        }

        return DateFormatType.ISOFormat;
    }

    /// <summary>
    /// Validates that a sequence of parsed DateTime values is sequential (monotonically non-decreasing)
    /// and consistent with expected time log rules.
    /// </summary>
    public static TimeLogValidationResult ValidateDateTimeSequence(IEnumerable<DateTime> dateTimes)
    {
        var result = new TimeLogValidationResult();
        if (dateTimes == null) return result;

        DateTime? prev = null;
        var distinctDateTimes = new HashSet<DateTime>();
        var dateToTimes = new Dictionary<DateTime, List<TimeSpan>>();

        foreach (var dt in dateTimes)
        {
            result.TotalRows++;
            result.ValidRows++;

            if (!result.MinDateTime.HasValue || dt < result.MinDateTime.Value)
                result.MinDateTime = dt;
            if (!result.MaxDateTime.HasValue || dt > result.MaxDateTime.Value)
                result.MaxDateTime = dt;

            // Check duplicate timestamps
            if (!distinctDateTimes.Add(dt))
            {
                result.DuplicateTimestampCount++;
            }

            // Track duplicate date with varying times
            var d = dt.Date;
            if (!dateToTimes.TryGetValue(d, out var timesList))
            {
                timesList = new List<TimeSpan>();
                dateToTimes[d] = timesList;
            }
            timesList.Add(dt.TimeOfDay);

            // Check sequentiality
            if (prev.HasValue)
            {
                if (dt < prev.Value)
                {
                    result.IsSequential = false;
                    result.NonSequentialCount++;
                    if (result.ValidationMessages.Count < 20)
                    {
                        result.ValidationMessages.Add(
                            $"Row {result.TotalRows}: Timestamp {dt:dd-MMM-yyyy HH:mm:ss} is earlier than previous timestamp {prev.Value:dd-MMM-yyyy HH:mm:ss}.");
                    }
                }
            }

            prev = dt;
        }

        foreach (var kvp in dateToTimes)
        {
            if (kvp.Value.Count > 1)
            {
                result.DuplicateDateVaryingTimeCount += kvp.Value.Count;
            }
        }

        result.IsValid = result.NonSequentialCount == 0 && result.ValidRows > 0;
        return result;
    }

    /// <summary>
    /// Validates raw data rows against TimeLogDateTimeOptions.
    /// Verifies row preservation, sequential monotonicity, and date change consistency.
    /// </summary>
    public static TimeLogValidationResult ValidateRecords(
        IEnumerable<string[]> rows,
        TimeLogDateTimeOptions options)
    {
        var result = new TimeLogValidationResult();
        if (rows == null || options == null) return result;

        int rowIdx = 0;
        DateTime? prevDt = null;
        string? prevDateStr = null;
        var distinctDateTimes = new HashSet<DateTime>();

        foreach (var tokens in rows)
        {
            rowIdx++;
            result.TotalRows++;

            if (TryParse(tokens, options, out var parsedDt, out _))
            {
                result.ValidRows++;

                if (!result.MinDateTime.HasValue || parsedDt < result.MinDateTime.Value)
                    result.MinDateTime = parsedDt;
                if (!result.MaxDateTime.HasValue || parsedDt > result.MaxDateTime.Value)
                    result.MaxDateTime = parsedDt;

                if (!distinctDateTimes.Add(parsedDt))
                {
                    result.DuplicateTimestampCount++;
                }

                // Validate monotonicity
                if (prevDt.HasValue && parsedDt < prevDt.Value)
                {
                    result.IsSequential = false;
                    result.NonSequentialCount++;
                    if (result.ValidationMessages.Count < 20)
                    {
                        result.ValidationMessages.Add(
                            $"Row {rowIdx}: Timestamp {parsedDt:dd-MMM-yyyy HH:mm:ss} is earlier than previous timestamp {prevDt.Value:dd-MMM-yyyy HH:mm:ss}.");
                    }
                }

                // Check if date changed in tokens but was improperly repeated
                if (options.IsDatetimeInSeperatorColumn && options.DateColNo >= 0 && options.DateColNo < tokens.Length)
                {
                    var currentDateStr = tokens[options.DateColNo]?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(prevDateStr) && !string.IsNullOrEmpty(currentDateStr))
                    {
                        if (currentDateStr != prevDateStr)
                        {
                            if (parsedDt.Date == prevDt?.Date)
                            {
                                result.ValidationMessages.Add(
                                    $"Row {rowIdx}: Source Date changed to '{currentDateStr}', but merged DateTime repeated previous Date '{prevDateStr}'.");
                            }
                        }
                    }
                    prevDateStr = currentDateStr;
                }

                prevDt = parsedDt;
            }
            else
            {
                if (result.ValidationMessages.Count < 20)
                {
                    result.ValidationMessages.Add($"Row {rowIdx}: Failed to parse DateTime from tokens.");
                }
            }
        }

        result.IsValid = result.NonSequentialCount == 0 && result.ValidRows == result.TotalRows && result.TotalRows > 0;
        return result;
    }

    private static bool TryParseDateTimeInternal(string dateTimeRaw, DateFormatType dateFormat, out DateTime parsedDate)
    {
        parsedDate = default;
        if (string.IsNullOrWhiteSpace(dateTimeRaw) || dateTimeRaw == "-999.25" || dateTimeRaw == "-9999")
            return false;

        dateTimeRaw = dateTimeRaw.Trim().Trim('"', '\'');

        bool success = false;
        switch (dateFormat)
        {
            case DateFormatType.ISOFormat:
                if (DateTime.TryParseExact(dateTimeRaw, IsoFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                {
                    success = true;
                }
                else if (DateTime.TryParse(dateTimeRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate))
                {
                    success = true;
                }
                break;

            case DateFormatType.DDMMYYYYFormat:
                if (DateTime.TryParseExact(dateTimeRaw, DdMmYyyyFormats, new CultureInfo("en-GB"), DateTimeStyles.None, out parsedDate))
                {
                    success = true;
                }
                else if (DateTime.TryParse(dateTimeRaw, new CultureInfo("en-GB"), DateTimeStyles.None, out parsedDate))
                {
                    success = true;
                }
                break;

            case DateFormatType.MMDDYYYYFormat:
                if (DateTime.TryParseExact(dateTimeRaw, MmDdYyyyFormats, new CultureInfo("en-US"), DateTimeStyles.None, out parsedDate))
                {
                    success = true;
                }
                else if (DateTime.TryParse(dateTimeRaw, new CultureInfo("en-US"), DateTimeStyles.None, out parsedDate))
                {
                    success = true;
                }
                break;
        }

        if (!success)
        {
            if (DateTime.TryParse(dateTimeRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsedDate) ||
                DateTime.TryParse(dateTimeRaw, out parsedDate))
            {
                success = true;
            }
        }

        return success;
    }
}

