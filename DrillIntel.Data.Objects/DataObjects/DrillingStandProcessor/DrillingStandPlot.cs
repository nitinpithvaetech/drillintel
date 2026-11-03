using System.Data;
using System.Drawing;
using System.Globalization;
using DrillIntel.Data;

namespace DrillIntel.Data.Objects.DataObjects.DrillingStandProcessor;

/// <summary>SQLite-backed equivalent of eVuMaxPro.DataBroker.Summary.DrlgStand.DrlgStandPlot.</summary>
public sealed class DrillingStandPlot
{
    private readonly IDataServiceDIntel _db;

    public DrillingStandPlot(IDataServiceDIntel db) => _db = db ?? throw new ArgumentNullException(nameof(db));

    /// <summary>
    /// Converts the legacy report's VMX_AKPI_DRLG_STAND aggregation to parameterized SQLite queries.
    /// The result retains the legacy standData columns and summary field names for easy UI binding.
    /// </summary>
    public DrillingStandPlotData GetData(string wellId, string? timeLogTable, DateTime fromDate, DateTime toDate)
    {
        if (string.IsNullOrWhiteSpace(wellId)) throw new ArgumentException("A well ID is required.", nameof(wellId));
        KPIDrlgStand.EnsureTableExists(_db);
        var result = new DrillingStandPlotData();
        var excluded = new HashSet<double>();
        try
        {
            var rows = _db.GetTable("SELECT ROUND(DEPTH,0) AS DEPTH FROM VMX_STAND_INFO WHERE WELL_ID=@WellID",
                new Dictionary<string, object?> { ["@WellID"] = wellId });
            foreach (DataRow row in rows.Rows) excluded.Add(ReadDouble(row["DEPTH"]));
        }
        catch { /* Legacy behavior: exclusion metadata is optional. */ }

        // Existing project SQLite files store dates as dd-MMM-yyyy text, which SQLite cannot
        // compare chronologically. Filter parsed DateTime values after the indexed well lookup.
        var stands = _db.GetTable(@"SELECT DEPTH, FROM_DATE, TO_DATE, TIME, ROP, ROTARY_ROP, SLIDE_ROP
FROM VMX_AKPI_DRLG_STAND WHERE WELL_ID=@WellID ORDER BY DEPTH",
            new Dictionary<string, object?> { ["@WellID"] = wellId });

        if (string.IsNullOrWhiteSpace(timeLogTable) || !IsValidTable(timeLogTable))
        {
            timeLogTable = ResolveTimeLogTable(wellId);
        }

        string? safeLogTable = IsValidTable(timeLogTable) ? timeLogTable : null;
        var allRigStateNumbers = new HashSet<double>();
        double total = 0, dayTotal = 0, nightTotal = 0;
        int count = 0, dayCount = 0, nightCount = 0;
        var offsetWell = GetFirstOffsetWell(wellId);
        List<RigStateLogRow>? allRigStateRows = null;
        if (!string.IsNullOrWhiteSpace(safeLogTable))
        {
            allRigStateRows = LoadAllRigStateRows(safeLogTable);
        }

        foreach (DataRow row in stands.Rows)
        {
            DateTime start = ReadDate(row["FROM_DATE"]), end = ReadDate(row["TO_DATE"]);
            if (start < fromDate || start > toDate) continue;
            double depth = ReadDouble(row["DEPTH"]);
            if (excluded.Contains(Math.Round(depth))) continue;
            double minutes = Math.Abs((end - start).TotalMinutes);
            string dayNight = Convert.ToString(row["TIME"]) == "N" ? "N" : "D";
            double offsetTime = 0;
            string offsetDayNight = "D";
            if (!string.IsNullOrWhiteSpace(offsetWell))
            {
                var offsetRows = _db.GetTable(@"SELECT FROM_DATE, TO_DATE, TIME FROM VMX_AKPI_DRLG_STAND
WHERE WELL_ID=@WellID AND DEPTH>=@Low AND DEPTH<=@High ORDER BY ABS(DEPTH-@Depth) LIMIT 1",
                    new Dictionary<string, object?> { ["@WellID"] = offsetWell, ["@Low"] = depth - 50, ["@High"] = depth + 50, ["@Depth"] = depth });
                if (offsetRows.Rows.Count > 0)
                {
                    var off = offsetRows.Rows[0];
                    offsetTime = Math.Abs((ReadDate(off["TO_DATE"]) - ReadDate(off["FROM_DATE"])).TotalMinutes);
                    offsetDayNight = Convert.ToString(off["TIME"]) == "N" ? "N" : "D";
                }
            }

            result.standData.Rows.Add(Math.Round(depth, 2), minutes, dayNight, offsetTime, offsetDayNight);
            total += minutes; count++;
            if (dayNight == "D") { dayTotal += minutes; dayCount++; } else { nightTotal += minutes; nightCount++; }

            // Query stand's time-log window and sum TIME_DURATION by RIG_STATE.
            var stateDurations = PopulateRigStatesFromCache(allRigStateRows, start, end);
            foreach (double state in stateDurations.Keys) allRigStateNumbers.Add(state);
            string statePayload = string.Join(",", stateDurations.Select(s => $"{s.Key.ToString(CultureInfo.InvariantCulture)}#{Math.Round(s.Value / 60, 3).ToString(CultureInfo.InvariantCulture)}"));
            result.rigStateData.Rows.Add(Math.Round(depth, 2), statePayload);
        }

        result.AvgTime = count == 0 ? 0 : Math.Round(total / count, 2);
        result.AvgDTime = dayCount == 0 ? 0 : Math.Round(dayTotal / dayCount, 2);
        result.AvgNTime = nightCount == 0 ? 0 : Math.Round(nightTotal / nightCount, 2);
        result.AvgDayTime = result.AvgDTime;
        result.AvgNightTime = result.AvgNTime;
        // The legacy chart's normal view shows Main and Offset; rig-state view uses state-coded bars.
        result.rigStates = LoadRigStateNames(wellId, safeLogTable, allRigStateNumbers);
        return result;
    }

    private static SortedDictionary<double, double> PopulateRigStatesFromCache(List<RigStateLogRow>? allRows, DateTime fromDate, DateTime toDate)
    {
        var rigStates = new SortedDictionary<double, double>();
        if (allRows == null || allRows.Count == 0) return rigStates;

        int startIndex = allRows.BinarySearch(new RigStateLogRow(fromDate, 0, 0), Comparer<RigStateLogRow>.Create((x, y) => x.DateTime.CompareTo(y.DateTime)));
        if (startIndex < 0) startIndex = ~startIndex;

        for (int i = startIndex; i < allRows.Count; i++)
        {
            var r = allRows[i];
            if (r.DateTime > toDate) break;
            double state = Math.Truncate(r.State);
            rigStates[state] = rigStates.GetValueOrDefault(state) + Math.Max(0, r.DurationSeconds);
        }

        return rigStates;
    }

    private List<RigStateLogRow> LoadAllRigStateRows(string timeLogTable)
    {
        var list = new List<RigStateLogRow>();
        if (!IsValidTable(timeLogTable)) return list;

        string? dtExpr = ResolveDateTimeExpression(timeLogTable);
        string? rsCol = ResolveRigStateColumn(timeLogTable);
        if (dtExpr == null || rsCol == null) return list;

        string durExpr = ResolveDurationExpression(timeLogTable);
        string sql = $"SELECT {dtExpr} AS [DATETIME], {rsCol} AS [RIG_STATE], {durExpr} AS [TIME_DURATION] FROM {EscapeTable(timeLogTable)} WHERE {rsCol} IS NOT NULL";

        try
        {
            using var dt = _db.GetTable(sql);
            if (dt != null)
            {
                foreach (DataRow row in dt.Rows)
                {
                    DateTime date = ReadDate(row["DATETIME"]);
                    if (date == DateTime.MinValue) continue;
                    double state = ReadDouble(row["RIG_STATE"]);
                    double dur = ReadDouble(row["TIME_DURATION"]);
                    if (dur <= 0) dur = 5.0;
                    list.Add(new RigStateLogRow(date, state, dur));
                }
            }
        }
        catch { }

        list.Sort((a, b) => a.DateTime.CompareTo(b.DateTime));
        return list;
    }

    private string? ResolveDateTimeExpression(string table)
    {
        if (ColumnExists(table, "DATETIME")) return "[DATETIME]";
        if (ColumnExists(table, "DATE") && ColumnExists(table, "TIME")) return "([DATE] || ' ' || [TIME])";
        if (ColumnExists(table, "DATE")) return "[DATE]";
        if (ColumnExists(table, "DATE_TIME")) return "[DATE_TIME]";
        return null;
    }

    private string? ResolveRigStateColumn(string table)
    {
        if (ColumnExists(table, "RIG_STATE")) return "[RIG_STATE]";
        if (ColumnExists(table, "RIGSTATE")) return "[RIGSTATE]";
        return null;
    }

    private string ResolveDurationExpression(string table)
    {
        if (ColumnExists(table, "TIME_DURATION")) return "COALESCE([TIME_DURATION],0)";
        if (ColumnExists(table, "DURATION")) return "COALESCE([DURATION],0)";
        return "5.0";
    }

    private string? ResolveTimeLogTable(string wellId)
    {
        try
        {
            var dt = _db.GetTable("SELECT DATA_TABLE_NAME FROM VMX_TIME_LOG WHERE WELL_ID=@WellID ORDER BY PRIMARY_LOG DESC, CREATED_DATE DESC LIMIT 1;",
                new Dictionary<string, object?> { ["@WellID"] = wellId });
            if (dt != null && dt.Rows.Count > 0)
            {
                string tbl = Convert.ToString(dt.Rows[0]["DATA_TABLE_NAME"]) ?? "";
                if (IsValidTable(tbl)) return tbl;
            }
        }
        catch { }

        try
        {
            var dtSummary = _db.GetTable("SELECT DataTableName FROM VMX_TIME_LOG_SUMMARY WHERE WellId=@WellID ORDER BY PrimaryLog DESC LIMIT 1;",
                new Dictionary<string, object?> { ["@WellID"] = wellId });
            if (dtSummary != null && dtSummary.Rows.Count > 0)
            {
                string tbl = Convert.ToString(dtSummary.Rows[0]["DataTableName"]) ?? "";
                if (IsValidTable(tbl)) return tbl;
            }
        }
        catch { }

        return null;
    }

    private DataTable LoadRigStateNames(string wellId, string? logTable, IReadOnlyCollection<double> observedStates)
    {
        var table = new DataTable();
        table.Columns.Add("RIG_STATE", typeof(double)); table.Columns.Add("RIG_STATE_COLOR", typeof(string)); table.Columns.Add("RIG_STATE_NAME", typeof(string));
        var catalog = new Dictionary<double, (string Name, string ColorHex)>();
        try
        {
            string itemsTable = _db.TableExists("APP_RIGSTATE_COMMON_ITEMS") ? "APP_RIGSTATE_COMMON_ITEMS" : "VMX_COMMON_RIGSTATE_ITEMS";
            if (_db.TableExists(itemsTable))
            {
                var rows = _db.GetTable($"SELECT RIG_STATE_NUMBER AS RIG_STATE, RIG_STATE_COLOR, RIG_STATE_NAME FROM [{itemsTable}] ORDER BY RIG_STATE_NUMBER");
                foreach (DataRow row in rows.Rows)
                {
                    double state = ReadDouble(row["RIG_STATE"]);
                    int colorValue = (int)ReadDouble(row["RIG_STATE_COLOR"]);
                    Color c = Color.FromArgb(colorValue);
                    if (c.A == 0 && colorValue != 0) c = Color.FromArgb(255, c.R, c.G, c.B);
                    string colorHex = colorValue != 0 ? ColorTranslator.ToHtml(c) : "#808080";
                    string name = Convert.ToString(row["RIG_STATE_NAME"]) ?? $"Rig State {state}";
                    catalog[state] = (name, colorHex);
                }
            }
        }
        catch { /* Older project databases may not have the shared state catalog populated. */ }

        var defaults = DrillIntel.Data.Objects.DataObjects.Services.RigStateService.DefaultRigStateItems
            .ToDictionary(s => (double)s.Number, s => s);

        var statesToInclude = observedStates.Count > 0
            ? observedStates.Distinct().OrderBy(s => s)
            : catalog.Keys.OrderBy(s => s);

        foreach (double state in statesToInclude)
        {
            if (catalog.TryGetValue(state, out var item))
            {
                table.Rows.Add(state, item.ColorHex, item.Name);
            }
            else if (defaults.TryGetValue(state, out var def))
            {
                Color c = Color.FromArgb(def.Color);
                if (c.A == 0) c = Color.FromArgb(255, c.R, c.G, c.B);
                table.Rows.Add(state, ColorTranslator.ToHtml(c), def.Name);
            }
            else
            {
                table.Rows.Add(state, "#607D8B", $"Rig State {state.ToString(CultureInfo.InvariantCulture)}");
            }
        }
        return table;
    }

    private string? GetFirstOffsetWell(string wellId)
    {
        try
        {
            var rows = _db.GetTable("SELECT OFFSET_WELL_ID FROM VMX_WELL_OFFSET WHERE WELL_ID=@WellID ORDER BY OFFSET_WELL_ID LIMIT 1", new Dictionary<string, object?> { ["@WellID"] = wellId });
            if (rows.Rows.Count > 0) return Convert.ToString(rows.Rows[0][0]);
        }
        catch { }
        return null;
    }

    private bool ColumnExists(string table, string column)
    {
        try
        {
            return _db.GetTable($"PRAGMA table_info({EscapeTable(table)})")
                .Rows.Cast<DataRow>()
                .Any(r => string.Equals(Convert.ToString(r["name"]), column, StringComparison.OrdinalIgnoreCase));
        }
        catch { return false; }
    }
    private sealed record RigStateLogRow(DateTime DateTime, double State, double DurationSeconds);
    private bool IsValidTable(string? value) => !string.IsNullOrWhiteSpace(value) && _db.TableExists(value);
    private static string EscapeTable(string table) => $"[{table.Replace("]", "]]")}]";
    // New SQLite conversion: this matches the date text written by the project's legacy stand/log tables.
    private static string FormatDate(DateTime value) => value.ToString("dd-MMM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
    private static DateTime ReadDate(object value)
    {
        if (value is DateTime dateValue) return dateValue;
        string? text = Convert.ToString(value, CultureInfo.InvariantCulture);
        string[] formats =
        {
            "dd-MMM-yyyy HH:mm:ss", "dd-MMM-yyyy HH:mm:ss.fff", "dd-MMM-yyyy H:mm:ss",
            "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd HH:mm:ss.fff", "yyyy-MM-dd'T'HH:mm:ss",
            "dd/MM/yyyy HH:mm:ss", "MM/dd/yyyy HH:mm:ss", "dd-MM-yyyy HH:mm:ss",
            "yyyy/MM/dd HH:mm:ss", "dd-MMM-yy HH:mm:ss", "dd-MMM-yyyy", "yyyy-MM-dd"
        };
        if (DateTime.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var date)) return date;
        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out date)) return date;
        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out date)) return date;
        // Time-log tables can store their DateTime index as an OLE Automation date.
        if (double.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out double oaDate))
        {
            try { return DateTime.FromOADate(oaDate); } catch { }
        }
        return DateTime.MinValue;
    }
    private static double ReadDouble(object value) => double.TryParse(Convert.ToString(value), NumberStyles.Any, CultureInfo.InvariantCulture, out var number) ? number : 0;
}

/// <summary>Legacy report payload expressed as typed .NET collections instead of a Broker JSON response.</summary>
public sealed class DrillingStandPlotData
{
    public DataTable standData { get; } = CreateStandTable();
    public DataTable rigStateData { get; } = CreateRigStateDataTable();
    public DataTable rigStates { get; set; } = new();
    public double AvgTime { get; set; }
    public double AvgDTime { get; set; }
    public double AvgNTime { get; set; }
    public double AvgDayTime { get; set; }
    public double AvgNightTime { get; set; }
    public string xTitle { get; set; } = "Depth";
    private static DataTable CreateStandTable()
    {
        var t = new DataTable(); t.Columns.Add("Depth", typeof(double)); t.Columns.Add("Time", typeof(double)); t.Columns.Add("DayNight", typeof(string)); t.Columns.Add("OffsetTime", typeof(double)); t.Columns.Add("OffsetDayNight", typeof(string)); return t;
    }
    private static DataTable CreateRigStateDataTable()
    {
        var t = new DataTable(); t.Columns.Add("DEPTH", typeof(double)); t.Columns.Add("RIG_STATES", typeof(string)); return t;
    }
}
