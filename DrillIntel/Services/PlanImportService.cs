using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using ExcelDataReader;

namespace DrillIntel.Services;

/// <summary>
/// Service to parse CSV and Excel (.xlsx, .xls) files into AdnlHookloadPlan objects
/// and persist them using AdnlHookloadPlan.savePlanEx according to Oil &amp; Gas standards.
/// Handles multi-sheet workbooks, multi-row headers, unit rows, and industry curve types.
/// </summary>
public class PlanImportService
{
    static PlanImportService()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public class PlanImportResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public AdnlHookloadPlan? Plan { get; set; }
        public List<AdnlHookloadPlan> ParsedPlans { get; set; } = new();
        public int TotalPoints { get; set; }
        public string SelectedSheetName { get; set; } = string.Empty;
        public List<string> DetectedCurves { get; set; } = new();
        public List<string> PreviewHeaders { get; set; } = new();
        public List<List<string>> PreviewRows { get; set; } = new();
    }

    /// <summary>
    /// Returns the list of worksheet names from an Excel file (.xlsx, .xls), optionally excluding documentation/instruction sheets like 'How To'.
    /// </summary>
    public List<string> GetSheetNames(string filePath, bool excludeDocumentation = true)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return list;
        string ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls") return list;

        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var reader = ExcelReaderFactory.CreateReader(stream);
            do
            {
                if (!string.IsNullOrWhiteSpace(reader.Name))
                {
                    string name = reader.Name.Trim();
                    if (!excludeDocumentation || !IsDocumentationSheet(name))
                    {
                        list.Add(name);
                    }
                }
            } while (reader.NextResult());
        }
        catch { }
        return list;
    }

    /// <summary>
    /// Reads and parses a CSV or Excel file into an AdnlHookloadPlan object.
    /// Supports selecting a specific worksheet, detecting multi-row headers/units,
    /// and mapping all Broomstick curves or filtering to a target curve.
    /// </summary>
    public PlanImportResult ParsePlanFile(
        string filePath,
        string planName,
        string wellId = "",
        string wellboreId = "",
        string logId = "",
        string runNo = "",
        string planType = "HKLDP",
        string targetCurve = "All",
        string sheetName = "")
    {
        var result = new PlanImportResult();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            result.Success = false;
            result.Message = "Selected file does not exist.";
            return result;
        }

        try
        {
            var plan = new AdnlHookloadPlan
            {
                WellID = wellId ?? "",
                WellboreID = wellboreId ?? "",
                LogID = logId ?? "",
                PlanID = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                Name = string.IsNullOrWhiteSpace(planName) ? Path.GetFileNameWithoutExtension(filePath) : planName.Trim(),
                PlanType = string.IsNullOrWhiteSpace(planType) ? "HKLDP" : planType.Trim(),
                RunNo = runNo ?? "",
                Type = 0
            };

            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".csv" || ext == ".txt")
            {
                ParseCsvFile(filePath, plan, targetCurve, result);
                if (CountPlanPoints(plan) > 0)
                {
                    result.ParsedPlans.Add(plan);
                }
            }
            else if (ext == ".xlsx" || ext == ".xls")
            {
                ParseExcelFile(filePath, plan, targetCurve, sheetName, result);
            }
            else
            {
                result.Success = false;
                result.Message = $"Unsupported file format: {ext}. Please select a .csv, .xlsx, or .xls file.";
                return result;
            }

            int count = result.ParsedPlans.Count > 0
                ? result.ParsedPlans.Sum(p => CountPlanPoints(p))
                : CountPlanPoints(plan);

            result.TotalPoints = count;
            result.Plan = result.ParsedPlans.FirstOrDefault() ?? plan;

            if (count > 0)
            {
                result.Success = true;
                if (result.ParsedPlans.Count > 1)
                {
                    result.Message = $"Successfully parsed {result.ParsedPlans.Count} run(s) ({string.Join(", ", result.ParsedPlans.Select(p => p.RunNo))}) with {count} total data point(s).";
                }
                else
                {
                    string sheetInfo = !string.IsNullOrWhiteSpace(result.SelectedSheetName) ? $" from sheet '{result.SelectedSheetName}'" : "";
                    result.Message = $"Successfully parsed {count} data point(s){sheetInfo} across {result.DetectedCurves.Count} curve(s).";
                }
            }
            else
            {
                result.Success = false;
                result.Message = "No valid depth or curve points could be parsed from the file. Please verify that the sheet contains numerical data rows.";
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error parsing file: {ex.Message}";
            return result;
        }
    }

    public static int CountPlanPoints(AdnlHookloadPlan? plan)
    {
        if (plan == null) return 0;
        return (plan.pickup?.Count ?? 0)
             + (plan.slackoff?.Count ?? 0)
             + (plan.rotate?.Count ?? 0)
             + (plan.torque?.Count ?? 0)
             + (plan.onTorque?.Count ?? 0)
             + (plan.mkTorque?.Count ?? 0)
             + (plan.tqLimit?.Count ?? 0)
             + (plan.sinRot?.Count ?? 0);
    }

    /// <summary>
    /// Executes saving multiple plans (runs) to SQLite database using AdnlHookloadPlan.savePlanEx.
    /// </summary>
    public bool SavePlans(
        IDataServiceDIntel dataService,
        IEnumerable<AdnlHookloadPlan> plans,
        bool importByRange,
        double fromDepth,
        double toDepth,
        out string errorMessage)
    {
        errorMessage = string.Empty;
        if (dataService == null)
        {
            errorMessage = "Data service connection is not initialized.";
            return false;
        }

        var planList = plans?.ToList() ?? new List<AdnlHookloadPlan>();
        if (planList.Count == 0)
        {
            errorMessage = "No plan objects to save.";
            return false;
        }

        foreach (var p in planList)
        {
            string err = string.Empty;
            bool ok = AdnlHookloadPlan.savePlanEx(dataService, p, ref err, importByRange, fromDepth, toDepth);
            if (!ok)
            {
                errorMessage = $"Error saving run '{p.RunNo}': {err}";
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Executes the plan save to SQLite database using AdnlHookloadPlan.savePlanEx.
    /// </summary>
    public bool SavePlan(
        IDataServiceDIntel dataService,
        AdnlHookloadPlan plan,
        bool importByRange,
        double fromDepth,
        double toDepth,
        out string errorMessage)
    {
        return SavePlans(dataService, new[] { plan }, importByRange, fromDepth, toDepth, out errorMessage);
    }

    #region Excel Parsing
    private void ParseExcelFile(string filePath, AdnlHookloadPlan plan, string targetCurve, string preferredSheet, PlanImportResult result)
    {
        using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
        {
            ConfigureDataTable = _ => new ExcelDataTableConfiguration
            {
                UseHeaderRow = false // Raw row reading so multi-row headers/units can be processed accurately
            }
        });

        // Rule 1: Sheets except sheet0 all other sheets are treated as RunNumbers(Sheet Name) and Target Curve will be all by default.
        // Under "Plan Name" -> SheetName as RunNumber -> under Each RunNumber -> All Target Curve Data.
        if (dataSet.Tables.Count > 1)
        {
            bool firstTable = true;
            for (int i = 1; i < dataSet.Tables.Count; i++)
            {
                DataTable table = dataSet.Tables[i];
                string sheetName = table.TableName?.Trim() ?? $"Run {i}";
                if (IsDocumentationSheet(sheetName)) continue;
                if (table.Rows.Count < 2 || table.Columns.Count < 2) continue;

                var runPlan = new AdnlHookloadPlan
                {
                    WellID = plan.WellID,
                    WellboreID = plan.WellboreID,
                    LogID = plan.LogID,
                    PlanID = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                    Name = plan.Name,
                    PlanType = plan.PlanType,
                    RunNo = sheetName,
                    Type = plan.Type
                };

                ParseRawDataTable(table, runPlan, "All", result, isFirstTable: firstTable);
                int ptCount = CountPlanPoints(runPlan);
                if (ptCount > 0)
                {
                    result.ParsedPlans.Add(runPlan);
                    firstTable = false;
                }
            }

            if (result.ParsedPlans.Count == 0)
            {
                result.Success = false;
                result.Message = "No valid data could be parsed from any of the run sheets in the Excel workbook.";
                return;
            }

            result.SelectedSheetName = string.Join(", ", result.ParsedPlans.Select(p => p.RunNo));
        }
        else
        {
            // Single sheet workbook: parse sheet 0 as Run 1
            DataTable table = dataSet.Tables[0];
            string sheetName = table.TableName?.Trim() ?? "1";
            var runPlan = new AdnlHookloadPlan
            {
                WellID = plan.WellID,
                WellboreID = plan.WellboreID,
                LogID = plan.LogID,
                PlanID = Guid.NewGuid().ToString("N").ToUpperInvariant(),
                Name = plan.Name,
                PlanType = plan.PlanType,
                RunNo = !string.IsNullOrWhiteSpace(plan.RunNo) ? plan.RunNo : sheetName,
                Type = plan.Type
            };

            ParseRawDataTable(table, runPlan, "All", result, isFirstTable: true);
            if (CountPlanPoints(runPlan) > 0)
            {
                result.ParsedPlans.Add(runPlan);
            }
            result.SelectedSheetName = sheetName;
        }
    }

    public static bool IsDocumentationSheet(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        string n = name.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");
        if (n.StartsWith("all") || n.StartsWith("allexcept")) return false;
        return n.Contains("howto") || n.Contains("readme") || n.Contains("instruction") || n.Contains("notes") || n.Contains("cover");
    }
    #endregion

    #region CSV Parsing
    private void ParseCsvFile(string filePath, AdnlHookloadPlan plan, string targetCurve, PlanImportResult result)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            MissingFieldFound = null,
            BadDataFound = null,
            DetectDelimiter = true,
            TrimOptions = TrimOptions.Trim
        };

        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var streamReader = new StreamReader(stream, Encoding.UTF8);
        using var csv = new CsvReader(streamReader, config);

        var dataTable = new DataTable("CsvData");
        bool columnsCreated = false;

        while (csv.Read())
        {
            if (!columnsCreated)
            {
                int colCount = csv.Parser.Count;
                for (int i = 0; i < colCount; i++)
                {
                    dataTable.Columns.Add($"Col_{i}");
                }
                columnsCreated = true;
            }

            var row = dataTable.NewRow();
            for (int i = 0; i < dataTable.Columns.Count; i++)
            {
                row[i] = csv.GetField(i) ?? string.Empty;
            }
            dataTable.Rows.Add(row);
        }

        result.SelectedSheetName = Path.GetFileName(filePath);
        ParseRawDataTable(dataTable, plan, targetCurve, result);
    }
    #endregion

    #region Unified Raw DataTable Parser
    private void ParseRawDataTable(DataTable table, AdnlHookloadPlan plan, string targetCurve, PlanImportResult result, bool isFirstTable = true)
    {
        if (table.Rows.Count == 0 || table.Columns.Count == 0) return;

        // 1. Locate the first numeric data row
        int dataStartRow = -1;
        int depthCol = -1;

        // First pass: scan top 5 rows to identify depth column candidate
        for (int c = 0; c < table.Columns.Count; c++)
        {
            for (int r = 0; r < Math.Min(4, table.Rows.Count); r++)
            {
                string text = (table.Rows[r][c]?.ToString() ?? "").Trim().ToLowerInvariant();
                if (IsDepthHeader(text))
                {
                    depthCol = c;
                    break;
                }
            }
            if (depthCol >= 0) break;
        }

        // Default depth column to 0 if not matched by text
        if (depthCol < 0) depthCol = 0;

        // Find the first row where depth column contains a valid number
        for (int r = 0; r < table.Rows.Count; r++)
        {
            string depthStr = (table.Rows[r][depthCol]?.ToString() ?? "").Trim();
            if (double.TryParse(depthStr, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            {
                dataStartRow = r;
                break;
            }
        }

        if (dataStartRow < 0) return; // No numeric data rows found

        // 2. Build composite header descriptors for each column from rows prior to dataStartRow
        int headerRowsToScan = Math.Min(dataStartRow, 4);
        var columnHeaders = new string[table.Columns.Count];
        var columnUnits = new string[table.Columns.Count];

        for (int c = 0; c < table.Columns.Count; c++)
        {
            var headerTokens = new List<string>();
            string unit = string.Empty;

            for (int r = 0; r < headerRowsToScan; r++)
            {
                string val = (table.Rows[r][c]?.ToString() ?? "").Trim();
                if (string.IsNullOrWhiteSpace(val)) continue;

                if (val.StartsWith("(") && val.EndsWith(")"))
                {
                    unit = val;
                }
                headerTokens.Add(val);
            }

            columnHeaders[c] = string.Join(" ", headerTokens).Trim();
            columnUnits[c] = unit;
        }

        // 3. Map columns to Broomstick curve roles
        int slackCol = -1;
        int pickupCol = -1;
        int robCol = -1;        // Rotating On Bottom (Torque)
        int rotCol = -1;        // Rotating Off Bottom (Hookload)
        int offTorqueCol = -1;  // Rotating Off Bottom (Torque)
        int onTorqueCol = -1;   // On Bottom Torque
        int mkTorqueCol = -1;   // Make-up Torque
        int tqLimitCol = -1;    // Torque Limit
        int sinRotCol = -1;     // Sinusoidal While Rotating
        int minTensCol = -1;
        int maxTensCol = -1;
        int genericWeightCol = -1;

        for (int c = 0; c < table.Columns.Count; c++)
        {
            if (c == depthCol) continue;
            string hdr = columnHeaders[c].ToLowerInvariant();
            string unit = columnUnits[c].ToLowerInvariant();

            // SlackOff / Tripping In
            if (hdr.Contains("tripping in") || hdr.Contains("trip in") || hdr.Contains("slack off") || hdr.Contains("slackoff") || hdr.Contains("slack"))
            {
                if (slackCol < 0) slackCol = c;
            }
            // Pickup / Tripping Out
            else if (hdr.Contains("tripping out") || hdr.Contains("trip out") || hdr.Contains("pickup") || hdr.Contains("pick up") || hdr.Contains("pick-up"))
            {
                if (pickupCol < 0) pickupCol = c;
            }
            // Rotating On Bottom (ROB / On-Bottom Torque)
            else if (hdr.Contains("rotating on bottom") || hdr.Contains("on bottom torque") || hdr.Contains("onbottom") || hdr.Contains("rob") || hdr.Contains("on btm torque") || hdr.Contains("ontor"))
            {
                if (onTorqueCol < 0) onTorqueCol = c;
                if (robCol < 0) robCol = c;
            }
            // Rotating Off Bottom
            else if (hdr.Contains("rotating off bottom") || hdr.Contains("off bottom") || hdr.Contains("offbottom") || hdr.Contains("rotate") || hdr.Contains("rotating"))
            {
                if (unit.Contains("ft-lbf") || unit.Contains("nm") || unit.Contains("lbf") || hdr.Contains("torque") || c >= 6)
                {
                    if (offTorqueCol < 0) offTorqueCol = c;
                }
                else
                {
                    if (rotCol < 0) rotCol = c;
                }
            }
            // Make-up Torque
            else if (hdr.Contains("make-up") || hdr.Contains("makeup") || hdr.Contains("make up") || hdr.Contains("mktorque") || hdr.Contains("mut"))
            {
                if (mkTorqueCol < 0) mkTorqueCol = c;
            }
            // Torque Limit
            else if (hdr.Contains("torque limit") || hdr.Contains("tq limit") || hdr.Contains("torquelimit") || hdr.Contains("tqlmt"))
            {
                if (tqLimitCol < 0) tqLimitCol = c;
            }
            // Sinusoidal
            else if (hdr.Contains("sinusoidal") || hdr.Contains("sinrot") || hdr.Contains("snrot"))
            {
                if (sinRotCol < 0) sinRotCol = c;
            }
            // Min Tension / Buckling
            else if (hdr.Contains("min wt") || hdr.Contains("min tension") || hdr.Contains("min tenstion") || hdr.Contains("mintension") || hdr.Contains("buckle"))
            {
                if (minTensCol < 0) minTensCol = c;
            }
            // Max Tension / Yield
            else if (hdr.Contains("max wt") || hdr.Contains("max tension") || hdr.Contains("max tenstion") || hdr.Contains("maxtension") || hdr.Contains("yield"))
            {
                if (maxTensCol < 0) maxTensCol = c;
            }
            // Generic Weight / Load
            else if (hdr.Contains("weight") || hdr.Contains("hookload") || hdr.Contains("hkld") || hdr.Contains("load") || hdr.Contains("value") || hdr.Contains("torque"))
            {
                if (genericWeightCol < 0) genericWeightCol = c;
            }
        }

        // Helper to count valid numbers in a column
        int CountNumericRows(int colIdx)
        {
            if (colIdx < 0 || colIdx >= table.Columns.Count) return 0;
            int cnt = 0;
            for (int r = dataStartRow; r < table.Rows.Count; r++)
            {
                if (TryParseDouble(table.Rows[r][colIdx], out _)) cnt++;
            }
            return cnt;
        }

        // Verify that mapped curve columns actually have data rows; reset if empty
        if (CountNumericRows(pickupCol) == 0) pickupCol = -1;
        if (CountNumericRows(slackCol) == 0) slackCol = -1;
        if (CountNumericRows(rotCol) == 0) rotCol = -1;
        if (CountNumericRows(onTorqueCol) == 0) onTorqueCol = -1;
        if (CountNumericRows(offTorqueCol) == 0) offTorqueCol = -1;
        if (CountNumericRows(mkTorqueCol) == 0) mkTorqueCol = -1;
        if (CountNumericRows(tqLimitCol) == 0) tqLimitCol = -1;
        if (CountNumericRows(sinRotCol) == 0) sinRotCol = -1;

        // If no curve columns have data, find any non-empty numeric column
        if (pickupCol < 0 && slackCol < 0 && rotCol < 0 && onTorqueCol < 0 && offTorqueCol < 0)
        {
            for (int c = 0; c < table.Columns.Count; c++)
            {
                if (c == depthCol) continue;
                if (CountNumericRows(c) > 0)
                {
                    string pName = (plan.Name + " " + plan.PlanType).ToLowerInvariant();
                    if (pName.Contains("torque") || pName.Contains("tor") || pName.Contains("btm") || pName.Contains("rob"))
                    {
                        onTorqueCol = c;
                    }
                    else if (pName.Contains("slack"))
                    {
                        slackCol = c;
                    }
                    else
                    {
                        pickupCol = c;
                    }
                    break;
                }
            }
        }

        // 4. Generate Clean Preview Table (first 10 data rows from first table)
        var previewColIndices = new List<int> { depthCol };
        var previewColNames = new List<string> { CleanColumnHeader(columnHeaders[depthCol], columnUnits[depthCol], "Depth") };

        void RegisterPreviewCol(int colIdx, string defaultTitle)
        {
            if (colIdx >= 0 && !previewColIndices.Contains(colIdx))
            {
                previewColIndices.Add(colIdx);
                previewColNames.Add(CleanColumnHeader(columnHeaders[colIdx], columnUnits[colIdx], defaultTitle));
            }
        }

        RegisterPreviewCol(slackCol, "Slack Off");
        RegisterPreviewCol(pickupCol, "Pickup");
        RegisterPreviewCol(rotCol, "Rotate (Hookload)");
        RegisterPreviewCol(onTorqueCol, "On Bottom Torque / ROB");
        RegisterPreviewCol(offTorqueCol, "Off Bottom Torque");
        RegisterPreviewCol(mkTorqueCol, "Make-up Torque");
        RegisterPreviewCol(tqLimitCol, "Torque Limit");
        RegisterPreviewCol(sinRotCol, "Sinusoidal While Rotating");
        RegisterPreviewCol(minTensCol, "Min Tension");
        RegisterPreviewCol(maxTensCol, "Max Tension");
        RegisterPreviewCol(genericWeightCol, "Value");

        if (isFirstTable || result.PreviewHeaders.Count == 0)
        {
            result.PreviewHeaders = previewColNames;
            result.PreviewRows.Clear();

            int previewLimit = Math.Min(table.Rows.Count, dataStartRow + 10);
            for (int r = dataStartRow; r < previewLimit; r++)
            {
                var pRow = new List<string>();
                foreach (int c in previewColIndices)
                {
                    pRow.Add(table.Rows[r][c]?.ToString()?.Trim() ?? string.Empty);
                }
                result.PreviewRows.Add(pRow);
            }
        }

        // 5. Parse Data Rows into AdnlHookloadPlan Dictionaries
        bool isAll = string.IsNullOrWhiteSpace(targetCurve) || targetCurve.Equals("All", StringComparison.OrdinalIgnoreCase);

        for (int r = dataStartRow; r < table.Rows.Count; r++)
        {
            string depthStr = table.Rows[r][depthCol]?.ToString()?.Trim() ?? string.Empty;
            if (!double.TryParse(depthStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double depth))
                continue;

            double minT = minTensCol >= 0 ? ParseDouble(table.Rows[r][minTensCol]) : 0;
            double maxT = maxTensCol >= 0 ? ParseDouble(table.Rows[r][maxTensCol]) : 0;

            if (isAll)
            {
                // Multi-curve import
                if (pickupCol >= 0 && TryParseDouble(table.Rows[r][pickupCol], out double puVal))
                    AddOrUpdatePoint(plan.pickup, depth, puVal, maxT, minT);

                if (slackCol >= 0 && TryParseDouble(table.Rows[r][slackCol], out double soVal))
                    AddOrUpdatePoint(plan.slackoff, depth, soVal, maxT, minT);

                if (rotCol >= 0 && TryParseDouble(table.Rows[r][rotCol], out double rotVal))
                    AddOrUpdatePoint(plan.rotate, depth, rotVal);

                if (onTorqueCol >= 0 && TryParseDouble(table.Rows[r][onTorqueCol], out double onTqVal))
                    AddOrUpdatePoint(plan.onTorque, depth, onTqVal);

                if (offTorqueCol >= 0 && TryParseDouble(table.Rows[r][offTorqueCol], out double offTqVal))
                    AddOrUpdatePoint(plan.torque, depth, offTqVal);

                if (mkTorqueCol >= 0 && TryParseDouble(table.Rows[r][mkTorqueCol], out double mkTqVal))
                    AddOrUpdatePoint(plan.mkTorque, depth, mkTqVal);

                if (tqLimitCol >= 0 && TryParseDouble(table.Rows[r][tqLimitCol], out double tqLimVal))
                    AddOrUpdatePoint(plan.tqLimit, depth, tqLimVal);

                if (sinRotCol >= 0 && TryParseDouble(table.Rows[r][sinRotCol], out double sinVal))
                    AddOrUpdatePoint(plan.sinRot, depth, sinVal);

                // If no named curves parsed, map generic weight to pickup
                if (plan.pickup.Count == 0 && plan.slackoff.Count == 0 && plan.rotate.Count == 0 && plan.onTorque.Count == 0 && genericWeightCol >= 0)
                {
                    if (TryParseDouble(table.Rows[r][genericWeightCol], out double gVal))
                        AddOrUpdatePoint(plan.pickup, depth, gVal, maxT, minT);
                }
            }
            else
            {
                // Single target curve mode
                var targetDict = GetTargetDictionary(plan, targetCurve);
                int targetCol = ResolveTargetColumn(targetCurve, pickupCol, slackCol, rotCol, onTorqueCol, offTorqueCol, mkTorqueCol, tqLimitCol, sinRotCol, genericWeightCol);

                if (targetCol >= 0 && TryParseDouble(table.Rows[r][targetCol], out double val))
                {
                    AddOrUpdatePoint(targetDict, depth, val, maxT, minT);
                }
            }
        }

        RecordDetectedCurves(plan, result);
    }

    private static int ResolveTargetColumn(
        string targetCurve,
        int pickupCol,
        int slackCol,
        int rotCol,
        int onTorqueCol,
        int offTorqueCol,
        int mkTorqueCol,
        int tqLimitCol,
        int sinRotCol,
        int genericCol)
    {
        string norm = targetCurve.ToLowerInvariant();
        if (norm.Contains("slack")) return slackCol >= 0 ? slackCol : genericCol;
        if (norm.Contains("pick")) return pickupCol >= 0 ? pickupCol : genericCol;
        if (norm.Contains("rotat") && !norm.Contains("bottom")) return rotCol >= 0 ? rotCol : genericCol;
        if (norm.Contains("on bottom") || norm.Contains("onbottom") || norm.Contains("rob")) return onTorqueCol >= 0 ? onTorqueCol : genericCol;
        if (norm.Contains("off bottom") || norm.Contains("offbottom")) return offTorqueCol >= 0 ? offTorqueCol : genericCol;
        if (norm.Contains("make")) return mkTorqueCol >= 0 ? mkTorqueCol : genericCol;
        if (norm.Contains("limit")) return tqLimitCol >= 0 ? tqLimitCol : genericCol;
        if (norm.Contains("sin")) return sinRotCol >= 0 ? sinRotCol : genericCol;

        return pickupCol >= 0 ? pickupCol : (genericCol >= 0 ? genericCol : slackCol);
    }

    private static bool IsDepthHeader(string text)
    {
        return text.Contains("depth") || text.Contains("md") || text.Contains("dept") || text.Contains("measured");
    }

    private static string CleanColumnHeader(string header, string unit, string defaultTitle)
    {
        string title = string.IsNullOrWhiteSpace(header) ? defaultTitle : header;
        if (!string.IsNullOrWhiteSpace(unit) && !title.Contains(unit))
        {
            title = $"{title} {unit}";
        }
        return title;
    }

    private static bool TryParseDouble(object? val, out double result)
    {
        result = 0;
        if (val == null || val == DBNull.Value) return false;
        string s = val.ToString()?.Trim() ?? "";
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
    }

    private static double ParseDouble(object? val)
    {
        TryParseDouble(val, out double d);
        return d;
    }

    private static void AddOrUpdatePoint(Dictionary<int, HookloadPlanData> dict, double depth, double weight, double maxTens = 0, double minTens = 0, double maxComp = 0, double minComp = 0)
    {
        var existing = dict.Values.FirstOrDefault(p => Math.Abs(p.Depth - depth) < 0.001);
        if (existing != null)
        {
            if (existing.Weight == 0 && weight != 0) existing.Weight = weight;
            if (existing.MaxTension == 0 && maxTens != 0) existing.MaxTension = maxTens;
            if (existing.MinTension == 0 && minTens != 0) existing.MinTension = minTens;
            if (existing.MaxCompress == 0 && maxComp != 0) existing.MaxCompress = maxComp;
            if (existing.MinCompress == 0 && minComp != 0) existing.MinCompress = minComp;
        }
        else
        {
            dict.Add(dict.Count + 1, new HookloadPlanData
            {
                Depth = depth,
                Weight = weight,
                MaxTension = maxTens,
                MinTension = minTens,
                MaxCompress = maxComp,
                MinCompress = minComp
            });
        }
    }

    private static void AddPoint(Dictionary<int, HookloadPlanData> dict, double depth, double weight, double maxTens = 0, double minTens = 0, double maxComp = 0, double minComp = 0)
    {
        AddOrUpdatePoint(dict, depth, weight, maxTens, minTens, maxComp, minComp);
    }

    private static Dictionary<int, HookloadPlanData> GetTargetDictionary(AdnlHookloadPlan plan, string curveName)
    {
        return curveName?.ToLowerInvariant() switch
        {
            "slackoff" or "slof" or "slack" => plan.slackoff,
            "rotate" or "rot" => plan.rotate,
            "off bottom torque" or "offbottom" or "tor" => plan.torque,
            "on bottom torque" or "onbottom" or "ontor" => plan.onTorque,
            "make-up torque" or "makeup" or "mktor" => plan.mkTorque,
            "torque limit" or "tqlmt" => plan.tqLimit,
            "sinusoidal while rotating" or "sinrot" or "snrot" => plan.sinRot,
            _ => plan.pickup
        };
    }

    private static void RecordDetectedCurves(AdnlHookloadPlan plan, PlanImportResult result)
    {
        result.DetectedCurves.Clear();
        if ((plan.pickup?.Count ?? 0) > 0) result.DetectedCurves.Add($"Pickup ({plan.pickup!.Count})");
        if ((plan.slackoff?.Count ?? 0) > 0) result.DetectedCurves.Add($"SlackOff ({plan.slackoff!.Count})");
        if ((plan.rotate?.Count ?? 0) > 0) result.DetectedCurves.Add($"Rotate ({plan.rotate!.Count})");
        if ((plan.onTorque?.Count ?? 0) > 0) result.DetectedCurves.Add($"On Bottom Torque ({plan.onTorque!.Count})");
        if ((plan.torque?.Count ?? 0) > 0) result.DetectedCurves.Add($"Off Bottom Torque ({plan.torque!.Count})");
        if ((plan.mkTorque?.Count ?? 0) > 0) result.DetectedCurves.Add($"Make-Up Torque ({plan.mkTorque!.Count})");
        if ((plan.tqLimit?.Count ?? 0) > 0) result.DetectedCurves.Add($"Torque Limit ({plan.tqLimit!.Count})");
        if ((plan.sinRot?.Count ?? 0) > 0) result.DetectedCurves.Add($"Sinusoidal ({plan.sinRot!.Count})");
    }
    #endregion
}
