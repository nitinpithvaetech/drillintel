using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace DrillIntel.Data.Objects.DataObjects.Services
{
    public class RigStateService
    {
        /// <summary>
        /// Gets or sets the last error message encountered by RigStateService operations.
        /// </summary>
        public static string LastError { get; set; } = string.Empty;

        /// <summary>
        /// Standard 28 default rig state items mapping number, name, and color.
        /// </summary>
        public static readonly (int Number, string Name, int Color)[] DefaultRigStateItems = new[]
        {
            (0, "Rotary Drill", -16711936),
            (1, "Slide Drill", -16732672),
            (2, "In Slips", -8355712),
            (3, "Ream", -16776961),
            (4, "Pump In", -8355585),
            (5, "Rotate In 1", -29696),
            (6, "Trip In", -16711808),
            (7, "Back Ream", -16777216),
            (8, "Pump out", -11579569),
            (9, "Rotate Out", -26522),
            (10, "Trip Out", -4144960),
            (11, "Rotate and circ", -32704),
            (12, "Circ", -32768),
            (13, "Rotate", -2987776),
            (14, "Stationary", -32513),
            (16, "No Data", -1),
            (17, "Packed Off", -65536),
            (18, "Pressure Testing", -989556),
            (19, "Auto Slide Drilling", -7357301),
            (20, "Air Drilling", -3355648),
            (21, "Rotary Dust Air Drilling", -4260861),
            (22, "Slide Dust Air Drilling", -6837246),
            (23, "Auto Slide Dust Air Drilling", -4801142),
            (24, "Rotary Mist Air Drilling", -16515900),
            (25, "Slide Mist Air Drilling", -16601957),
            (26, "Auto Slide Mist Air Drilling", -7684675),
            (27, "Pipe Move In", -8355776),
            (28, "Pipe Move Out", -8388353)
        };

        /// <summary>
        /// Returns a dictionary of the standard 28 default rig state items with hex color codes computed.
        /// </summary>
        public static Dictionary<int, rigStateItem> GetDefaultRigStateItems()
        {
            var items = new Dictionary<int, rigStateItem>();
            foreach (var item in DefaultRigStateItems)
            {
                items[item.Number] = new rigStateItem
                {
                    Number = item.Number,
                    Name = item.Name,
                    Color = item.Color,
                    ColorHex = ConvertColorToHex(item.Color)
                };
            }
            return items;
        }

        /// <summary>
        public static string ResolveSetupTableName(IDataServiceDIntel objDataService)
        {
            if (objDataService != null && objDataService.TableExists("APP_RIGSTATE_COMMON_SETUP"))
                return "APP_RIGSTATE_COMMON_SETUP";
            return "VMX_COMMON_RIGSTATE_SETUP";
        }

        public static string ResolveItemsTableName(IDataServiceDIntel objDataService)
        {
            if (objDataService != null && objDataService.TableExists("APP_RIGSTATE_COMMON_ITEMS"))
                return "APP_RIGSTATE_COMMON_ITEMS";
            return "VMX_COMMON_RIGSTATE_ITEMS";
        }

        /// <summary>
        /// Loads the common rig state setup from APP_RIGSTATE_COMMON_SETUP/VMX_COMMON_RIGSTATE_SETUP and ITEMS.
        /// If no setup record exists, auto-seeds default values and standard rig state items into the database.
        /// </summary>
        /// <param name="objDataService">Database service implementing IDataServiceDIntel</param>
        /// <returns>Populated rigState object, or null on error.</returns>
        public static rigState? LoadCommonRigStateSetup(IDataServiceDIntel objDataService)
        {
            LastError = string.Empty;
            try
            {
                if (objDataService == null)
                {
                    LastError = "DataService is null";
                    return null;
                }

                string setupTable = ResolveSetupTableName(objDataService);
                string itemsTable = ResolveItemsTableName(objDataService);

                DataTable objData = objDataService.GetTable($"SELECT * FROM [{setupTable}];");

                if (objData != null && objData.Rows.Count > 0)
                {
                    DataRow objRow = objData.Rows[0];

                    var objRigStateSetup = new rigState();
                    objRigStateSetup.UnknownName = DataService.checkNull(objRow["UNKNOWN_NAME"], "");
                    objRigStateSetup.UnknownNumber = (float)DataService.checkNull(objRow["UNKNOWN_NUMBER"], 0.0);

                    int unknownColorVal = DataService.checkNull(objRow["UNKNOWN_COLOR"], 0);
                    objRigStateSetup.UnknownColor = unknownColorVal;

                    // React side hex conversion
                    objRigStateSetup.UnknownColorHex = ConvertColorToHex(unknownColorVal);

                    objRigStateSetup.HookloadCutOff = DataService.checkNull(objRow["HOOKLOAD_CUTOFF"], 0.0);
                    objRigStateSetup.RPMCutOff = DataService.checkNull(objRow["RPM_CUTOFF"], 0.0);
                    objRigStateSetup.CIRCCutOff = DataService.checkNull(objRow["CIRC_CUTOFF"], 0.0);
                    objRigStateSetup.Sensitivity = DataService.checkNull(objRow["SENSITIVITY"], 0.0);
                    objRigStateSetup.PumpPressureCutOff = DataService.checkNull(objRow["PUMP_PRESSURE_CUTOFF"], 0.0);
                    objRigStateSetup.DepthComparisonSens = DataService.checkNull(objRow["DEPTH_COMP_SENSITIVITY"], 0.0);
                    objRigStateSetup.DetectAutoSlideDrilling = DataService.checkNull(objRow["DETECT_AUTO_SLIDE"], 0) == 1;
                    objRigStateSetup.SelectedSet = DataService.checkNull(objRow["SELECTED_SET"], 1);

                    // Set 1
                    objRigStateSetup.TorqueMin = DataService.checkNull(objRow["MIN_TORQUE"], 0.0);
                    objRigStateSetup.TorqueMax = DataService.checkNull(objRow["MAX_TORQUE"], 0.0);
                    objRigStateSetup.CalibrationRows = DataService.checkNull(objRow["CALIBRATION_ROWS"], 0);
                    objRigStateSetup.MinTorqueDifference = DataService.checkNull(objRow["MIN_TORQUE_DIFF"], 0.0);
                    objRigStateSetup.MinRPM = DataService.checkNull(objRow["MIN_RPM"], 0.0);
                    objRigStateSetup.MaxRPM = DataService.checkNull(objRow["MAX_RPM"], 0.0);

                    // Set 2
                    objRigStateSetup.TorqueMin2 = DataService.checkNull(objRow["MIN_TORQUE2"], 0.0);
                    objRigStateSetup.TorqueMax2 = DataService.checkNull(objRow["MAX_TORQUE2"], 0.0);
                    objRigStateSetup.CalibrationRows2 = DataService.checkNull(objRow["CALIBRATION_ROWS2"], 0);
                    objRigStateSetup.MinTorqueDifference2 = DataService.checkNull(objRow["MIN_TORQUE_DIFF2"], 0.0);
                    objRigStateSetup.MinRPM2 = DataService.checkNull(objRow["MIN_RPM2"], 0.0);
                    objRigStateSetup.MaxRPM2 = DataService.checkNull(objRow["MAX_RPM2"], 0.0);

                    // Set 3
                    objRigStateSetup.TorqueMin3 = DataService.checkNull(objRow["MIN_TORQUE3"], 0.0);
                    objRigStateSetup.TorqueMax3 = DataService.checkNull(objRow["MAX_TORQUE3"], 0.0);
                    objRigStateSetup.CalibrationRows3 = DataService.checkNull(objRow["CALIBRATION_ROWS3"], 0);
                    objRigStateSetup.MinTorqueDifference3 = DataService.checkNull(objRow["MIN_TORQUE_DIFF3"], 0.0);
                    objRigStateSetup.MinRPM3 = DataService.checkNull(objRow["MIN_RPM3"], 0.0);
                    objRigStateSetup.MaxRPM3 = DataService.checkNull(objRow["MAX_RPM3"], 0.0);

                    objRigStateSetup.DetectAirDrilling = DataService.checkNull(objRow["DETECT_AIR_DRILLING"], 0) == 1;
                    objRigStateSetup.AirPressure = DataService.checkNull(objRow["AIR_PRESSURE"], 0.0);
                    objRigStateSetup.TorqueCutOff = DataService.checkNull(objRow["TORQUE_CUTOFF"], 0.0);
                    objRigStateSetup.TorqueCycles = DataService.checkNull(objRow["TORQUE_CYCLES"], 0);

                    objRigStateSetup.CalibrationTime = DataService.checkNull(objRow["CALB_TIME"], 0);
                    objRigStateSetup.PercentWindow = DataService.checkNull(objRow["PERCENT_WINDOW"], 0.0);

                    if (objData.Columns.Contains("MIST_CUTOFF"))
                    {
                        objRigStateSetup.MistFlowCutOff = DataService.checkNull(objRow["MIST_CUTOFF"], 0.0);
                    }
                    else
                    {
                        objRigStateSetup.DetectAirDrilling = false;
                    }

                    objRigStateSetup.DetectPipeMovement = DataService.checkNull(objRow["DETECT_PIPE_MOVE"], 0) == 1;
                    objRigStateSetup.PipeMovementThreshold = DataService.checkNull(objRow["PIPE_MOVE_THRESHOLD"], 0.0);

                    // Load Items from items table
                    DataTable objItemsData = objDataService.GetTable($"SELECT * FROM [{itemsTable}];");

                    if (objItemsData != null && objItemsData.Rows.Count > 0)
                    {
                        foreach (DataRow itemRow in objItemsData.Rows)
                        {
                            int itemNumber = DataService.checkNull(itemRow["RIG_STATE_NUMBER"], 0);
                            int itemColorVal = DataService.checkNull(itemRow["RIG_STATE_COLOR"], 0);

                            var objItem = new rigStateItem
                            {
                                Number = itemNumber,
                                Name = DataService.checkNull(itemRow["RIG_STATE_NAME"], ""),
                                Color = itemColorVal,
                                ColorHex = ConvertColorToHex(itemColorVal)
                            };

                            objRigStateSetup.rigStates[objItem.Number] = objItem;
                        }
                    }
                    else
                    {
                        // Auto-seed default rig state items if VMX_COMMON_RIGSTATE_ITEMS has no records
                        objRigStateSetup.rigStates = GetDefaultRigStateItems();
                        SaveCommonRigStateSetup(objDataService, objRigStateSetup);
                    }

                    return objRigStateSetup;
                }

                // If VMX_COMMON_RIGSTATE_SETUP has no records, auto-seed with default values and default items
                var defaultSetup = rigState.CreateDefault();
                defaultSetup.rigStates = GetDefaultRigStateItems();
                SaveCommonRigStateSetup(objDataService, defaultSetup);
                return defaultSetup;
            }
            catch (Exception ex)
            {
                LastError = ex.Message + (string.IsNullOrEmpty(ex.StackTrace) ? "" : ": " + ex.StackTrace);
                return null;
            }
        }

      

        /// <summary>
        /// Checks whether a column exists in the specified table.
        /// </summary>
        /// <param name="objDataService">Database service implementing IDataServiceDIntel</param>
        /// <param name="tableName">Table name to inspect</param>
        /// <param name="columnName">Column name to check</param>
        /// <returns>True if column exists, false otherwise.</returns>
        public static bool IsColumnAvailable(IDataServiceDIntel objDataService, string tableName, string columnName)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(columnName))
                    return false;

                DataTable dt = objDataService.GetTable($"PRAGMA table_info([{tableName}]);");
                if (dt != null && dt.Rows.Count > 0)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        if (string.Equals(row["name"]?.ToString(), columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }

                DataTable schemaDt = objDataService.GetTable($"SELECT * FROM [{tableName}] LIMIT 0;");
                return schemaDt != null && schemaDt.Columns.Contains(columnName);
            }
            catch
            {
                return false;
            }
        }

      
        /// <summary>
        /// Saves common rig state setup to VMX_COMMON_RIGSTATE_SETUP and VMX_COMMON_RIGSTATE_ITEMS.
        /// Any errors are recorded in RigStateService.LastError.
        /// </summary>
        /// <param name="objDataService">Database service implementing IDataServiceDIntel</param>
        /// <param name="objRigState">rigState instance to persist</param>
        /// <returns>True if successful, false otherwise.</returns>
        public static bool SaveCommonRigStateSetup(IDataServiceDIntel objDataService, rigState objRigState)
        {
            LastError = string.Empty;
            try
            {
                if (objDataService == null)
                {
                    LastError = "DataService is null";
                    return false;
                }

                if (objRigState == null)
                {
                    LastError = "RigState object is null";
                    return false;
                }

                string setupTable = ResolveSetupTableName(objDataService);
                string itemsTable = ResolveItemsTableName(objDataService);

                string strSQL = "";
                if (!objDataService.IsRecordExist($"SELECT * FROM [{setupTable}] "))
                {
                    strSQL = $"INSERT INTO [{setupTable}] (UNKNOWN_NAME) VALUES('Unknown');";
                    objDataService.ExecuteNonQuery(strSQL);
                }

                bool hasMistCutOff = IsColumnAvailable(objDataService, setupTable, "MIST_CUTOFF");

                string userName = (objDataService.UserName ?? "").Replace("'", "''");
                string modifiedDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");

                strSQL = $"UPDATE [{setupTable}] SET "
                    + " UNKNOWN_NAME='" + (objRigState.UnknownName ?? "").Replace("'", "''") + "',"
                    + " UNKNOWN_NUMBER=" + objRigState.UnknownNumber.ToString(CultureInfo.InvariantCulture) + ","
                    + " UNKNOWN_COLOR=" + objRigState.UnknownColor.ToString(CultureInfo.InvariantCulture) + ","
                    + " HOOKLOAD_CUTOFF=" + objRigState.HookloadCutOff.ToString(CultureInfo.InvariantCulture) + ","
                    + " RPM_CUTOFF=" + objRigState.RPMCutOff.ToString(CultureInfo.InvariantCulture) + ","
                    + " CIRC_CUTOFF=" + objRigState.CIRCCutOff.ToString(CultureInfo.InvariantCulture) + ","
                    + " SENSITIVITY=" + objRigState.Sensitivity.ToString(CultureInfo.InvariantCulture) + ","
                    + " PUMP_PRESSURE_CUTOFF=" + objRigState.PumpPressureCutOff.ToString(CultureInfo.InvariantCulture) + ","
                    + " DEPTH_COMP_SENSITIVITY=" + objRigState.DepthComparisonSens.ToString(CultureInfo.InvariantCulture) + ","
                    + " DETECT_AUTO_SLIDE=" + (objRigState.DetectAutoSlideDrilling ? 1 : 0).ToString() + ","
                    + " MIN_TORQUE=" + objRigState.TorqueMin.ToString(CultureInfo.InvariantCulture) + ","
                    + " MAX_TORQUE=" + objRigState.TorqueMax.ToString(CultureInfo.InvariantCulture) + ","
                    + " CALIBRATION_ROWS=" + objRigState.CalibrationRows.ToString(CultureInfo.InvariantCulture) + ","
                    + " MIN_TORQUE_DIFF=" + objRigState.MinTorqueDifference.ToString(CultureInfo.InvariantCulture) + ","
                    + " MIN_RPM=" + objRigState.MinRPM.ToString(CultureInfo.InvariantCulture) + ","
                    + " MAX_RPM=" + objRigState.MaxRPM.ToString(CultureInfo.InvariantCulture) + ","
                    + " MIN_TORQUE2=" + objRigState.TorqueMin2.ToString(CultureInfo.InvariantCulture) + ","
                    + " MAX_TORQUE2=" + objRigState.TorqueMax2.ToString(CultureInfo.InvariantCulture) + ","
                    + " CALIBRATION_ROWS2=" + objRigState.CalibrationRows2.ToString(CultureInfo.InvariantCulture) + ","
                    + " MIN_TORQUE_DIFF2=" + objRigState.MinTorqueDifference2.ToString(CultureInfo.InvariantCulture) + ","
                    + " MIN_RPM2=" + objRigState.MinRPM2.ToString(CultureInfo.InvariantCulture) + ","
                    + " MAX_RPM2=" + objRigState.MaxRPM2.ToString(CultureInfo.InvariantCulture) + ","
                    + " MIN_TORQUE3=" + objRigState.TorqueMin3.ToString(CultureInfo.InvariantCulture) + ","
                    + " MAX_TORQUE3=" + objRigState.TorqueMax3.ToString(CultureInfo.InvariantCulture) + ","
                    + " CALIBRATION_ROWS3=" + objRigState.CalibrationRows3.ToString(CultureInfo.InvariantCulture) + ","
                    + " MIN_TORQUE_DIFF3=" + objRigState.MinTorqueDifference3.ToString(CultureInfo.InvariantCulture) + ","
                    + " MIN_RPM3=" + objRigState.MinRPM3.ToString(CultureInfo.InvariantCulture) + ","
                    + " MAX_RPM3=" + objRigState.MaxRPM3.ToString(CultureInfo.InvariantCulture) + ","
                    + " MODIFIED_BY='" + userName + "',"
                    + " MODIFIED_DATE='" + modifiedDate + "',"
                    + " DETECT_AIR_DRILLING=" + (objRigState.DetectAirDrilling ? 1 : 0).ToString() + ","
                    + " AIR_PRESSURE=" + objRigState.AirPressure.ToString(CultureInfo.InvariantCulture) + ", "
                    + " TORQUE_CUTOFF=" + objRigState.TorqueCutOff.ToString(CultureInfo.InvariantCulture) + ", ";

                if (hasMistCutOff)
                {
                    strSQL += " MIST_CUTOFF=" + objRigState.MistFlowCutOff.ToString(CultureInfo.InvariantCulture) + ", ";
                }

                strSQL += " TORQUE_CYCLES=" + objRigState.TorqueCycles.ToString(CultureInfo.InvariantCulture) + ", "
                    + " CALB_TIME=" + objRigState.CalibrationTime.ToString(CultureInfo.InvariantCulture) + ", "
                    + " PERCENT_WINDOW=" + objRigState.PercentWindow.ToString(CultureInfo.InvariantCulture) + ", "
                    + " DETECT_PIPE_MOVE=" + (objRigState.DetectPipeMovement ? 1 : 0).ToString() + ","
                    + " PIPE_MOVE_THRESHOLD=" + objRigState.PipeMovementThreshold.ToString(CultureInfo.InvariantCulture) + " ";

                if (IsColumnAvailable(objDataService, setupTable, "SELECTED_SET"))
                {
                    strSQL += ", SELECTED_SET=" + objRigState.SelectedSet.ToString(CultureInfo.InvariantCulture) + " ";
                }

                if (objDataService.ExecuteNonQuery(strSQL))
                {
                    objDataService.ExecuteNonQuery($"DELETE FROM [{itemsTable}];");

                    if (objRigState.rigStates != null)
                    {
                        foreach (rigStateItem objItem in objRigState.rigStates.Values)
                        {
                            if (objItem == null) continue;

                            string itemSQL = $"INSERT INTO [{itemsTable}] (RIG_STATE_NUMBER,RIG_STATE_NAME,RIG_STATE_COLOR,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE) VALUES("
                                + objItem.Number.ToString(CultureInfo.InvariantCulture) + ","
                                + "'" + (objItem.Name ?? "").Replace("'", "''") + "',"
                                + objItem.Color.ToString(CultureInfo.InvariantCulture) + ","
                                + "'" + userName + "',"
                                + "'" + modifiedDate + "',"
                                + "'" + userName + "',"
                                + "'" + modifiedDate + "');";

                            objDataService.ExecuteNonQuery(itemSQL);
                        }
                    }

                    return true;
                }
                else
                {
                    LastError = objDataService.LastError;
                    return false;
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message + (string.IsNullOrEmpty(ex.StackTrace) ? "" : ": " + ex.StackTrace);
                return false;
            }
        }

       

        /// <summary>
        /// Converts integer ARGB color values to HTML hex format (#RRGGBB).
        /// Equivalent to ColorTranslator.ToHtml(Color.FromArgb(colorVal)).
        /// </summary>
        public static string ConvertColorToHex(int colorVal)
        {
            try
            {
                Color c = Color.FromArgb(colorVal);
                return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            }
            catch
            {
                return "#000000";
            }
        }

        /// <summary>
        /// Converts HTML hex string (#RRGGBB or #AARRGGBB) to integer ARGB color.
        /// </summary>
        public static int ConvertHexToColor(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return 0;
            try
            {
                string cleanHex = hex.Trim().TrimStart('#');
                if (cleanHex.Length == 6)
                {
                    if (int.TryParse(cleanHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
                    {
                        return unchecked((int)(0xFF000000 | (uint)rgb));
                    }
                }
                else if (cleanHex.Length == 8)
                {
                    if (uint.TryParse(cleanHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint argb))
                    {
                        return unchecked((int)argb);
                    }
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        #region Recalculate Rig State Methods (Converted from VB.NET)

        // =========================================================================================
        // RecalculateRigState Entry Point
        // =========================================================================================
        /// <summary>
        /// Recalculates rig states for a specified TimeLog over an optional date range.
        /// </summary>
        /// <param name="objDataService">SQLite data service implementing IDataServiceDIntel</param>
        /// <param name="timeLog">Target TimeLog object</param>
        /// <param name="startDate">Optional start date. If null, queries earliest timestamp in the log</param>
        /// <param name="endDate">Optional end date. If null, queries latest timestamp in the log</param>
        /// <param name="progress">Reports 0-100% completion to UI dispatcher</param>
        /// <param name="ct">Cancellation token for user cancellation</param>
        public static async Task<bool> RecalculateRigStateAsync(
            IDataServiceDIntel objDataService,
            TimeLog timeLog,
            DateTime? startDate = null,
            DateTime? endDate = null,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            try
            {
                if (objDataService == null)
                {
                    LastError = "DataService is not initialized.";
                    return false;
                }

                if (timeLog == null)
                {
                    LastError = "TimeLog instance is null.";
                    return false;
                }

                string dataTableName = timeLog.__dataTableName;
                if (string.IsNullOrWhiteSpace(dataTableName))
                {
                    LastError = "TimeLog data table name is missing.";
                    return false;
                }

                // --- [ORIGINAL VB LOGIC: objRigState = rigState.loadCommonRigStateSetup(ref objDataService, WellID)] ---
                // --- [NEW C# / SQLITE LOGIC: Load common rig state setup from SQLite] ---
                rigState? objRigState = LoadCommonRigStateSetup(objDataService);
                if (objRigState == null)
                {
                    objRigState = rigState.CreateDefault();
                }

                objRigState.DoNotPause = true;

                DateTime effectiveStart = startDate ?? DateTime.MinValue;
                DateTime effectiveEnd = endDate ?? DateTime.MaxValue;

                if (startDate.HasValue && endDate.HasValue && startDate.Value != DateTime.MinValue && endDate.Value != DateTime.MaxValue && startDate.Value > endDate.Value)
                {
                    LastError = "Start date cannot be after end date.";
                    return false;
                }

                // Run background processing asynchronously to keep UI responsive
                return await Task.Run(() =>
                {
                    return UpdateRigStateNoBreakSmall(objDataService, objRigState, timeLog, effectiveStart, effectiveEnd, progress, ct);
                }, ct);
            }
            catch (OperationCanceledException)
            {
                LastError = "Recalculation was cancelled by the user.";
                return false;
            }
            catch (Exception ex)
            {
                LastError = ex.Message + (ex.InnerException != null ? ": " + ex.InnerException.Message : "");
                return false;
            }
        }

        // =========================================================================================
        // Main Chunking & Recalculation Loop
        // =========================================================================================
        public static bool UpdateRigStateNoBreakSmall(
            IDataServiceDIntel objDataService,
            rigState objRigState,
            TimeLog objTimeLog,
            DateTime startDate,
            DateTime endDate,
            IProgress<double>? progress = null,
            CancellationToken ct = default)
        {
            try
            {
                string dataTableName = objTimeLog.__dataTableName;
                EnsureRigStateColumnsExist(objDataService, dataTableName);

                string idxCol = GetDataIndexColumnName(objDataService, dataTableName);
                var minMaxTable = objDataService.GetTable($"SELECT MIN([{idxCol}]), MAX([{idxCol}]) FROM [{dataTableName}];");
                if (minMaxTable == null || minMaxTable.Rows.Count == 0 || minMaxTable.Rows[0][0] == DBNull.Value)
                {
                    progress?.Report(100.0);
                    return true;
                }

                long minIdx = Convert.ToInt64(minMaxTable.Rows[0][0]);
                long maxIdx = Convert.ToInt64(minMaxTable.Rows[0][1]);

                long startDataIndex = minIdx;
                long endDataIndex = maxIdx;

                DateTime minDate = GetMinDateFromTable(objDataService, dataTableName);
                DateTime maxDate = GetMaxDateFromTable(objDataService, dataTableName);

                bool isEntireLog = (startDate == DateTime.MinValue || (minDate != DateTime.MinValue && startDate <= minDate)) &&
                                   (endDate == DateTime.MaxValue || (maxDate != DateTime.MinValue && endDate >= maxDate));

                if (!isEntireLog && minDate != DateTime.MinValue && maxDate != DateTime.MinValue)
                {
                    startDataIndex = FindDataIndexForDate(objDataService, dataTableName, startDate, true, minIdx, maxIdx);
                    endDataIndex = FindDataIndexForDate(objDataService, dataTableName, endDate, false, minIdx, maxIdx);
                }

                if (startDataIndex > endDataIndex)
                {
                    LastError = "No timelog records found in the specified date range.";
                    return false;
                }

                long totalRowsToProcess = endDataIndex - startDataIndex + 1;
                long rowsProcessed = 0;
                long currentIndex = startDataIndex;

                const int batchSize = 5000;
                const int bufferRows = 5;

                string selectColsSql = BuildSelectColumnsSql(objDataService, dataTableName);

                while (currentIndex <= endDataIndex)
                {
                    ct.ThrowIfCancellationRequested();

                    long chunkStart = Math.Max(minIdx, currentIndex - bufferRows);
                    long chunkEnd = Math.Min(endDataIndex, currentIndex + batchSize - 1);
                    bool isLastChunk = (chunkEnd >= endDataIndex);

                    string selectSql = $"SELECT {selectColsSql} FROM [{dataTableName}] " +
                                       $"WHERE [{idxCol}] >= {chunkStart} AND [{idxCol}] <= {chunkEnd} ORDER BY [{idxCol}] ASC;";
                    DataTable objData = objDataService.GetTable(selectSql);

                    if (objData != null && objData.Rows.Count > 0)
                    {
                        DateTime targetDate = ParseDate(objData.Rows[0]["DATETIME"]);
                        bool ok = UpdateRigStateEx(objDataService, objRigState, objTimeLog, objData, targetDate, currentIndex, isLastChunk, idxCol);
                        if (!ok && !string.IsNullOrWhiteSpace(LastError))
                        {
                            return false;
                        }

                        if (objRigState.DetectPipeMovement)
                        {
                            RevertPipeMovementRigStatesByIndex(objDataService, objRigState, dataTableName, idxCol, chunkStart, chunkEnd);
                            DoProcessPipeMovementByIndex(objDataService, objRigState, dataTableName, idxCol, chunkStart, chunkEnd);
                        }
                    }

                    long processedInThisBatch = chunkEnd - currentIndex + 1;
                    rowsProcessed += processedInThisBatch;
                    currentIndex = chunkEnd + 1;

                    if (totalRowsToProcess > 0 && progress != null)
                    {
                        double percentDone = Math.Min(100.0, Math.Round((rowsProcessed * 100.0) / totalRowsToProcess, 1));
                        progress.Report(percentDone);
                    }
                }

                progress?.Report(100.0);
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                LastError = ex.Message + (ex.InnerException != null ? ": " + ex.InnerException.Message : "");
                return false;
            }
        }

        // =========================================================================================
        // Revert Pipe Movement Rig States
        // =========================================================================================
        public static void RevertPipeMovementRigStates(
            IDataServiceDIntel objDataService,
            rigState objRigState,
            string dataTableName,
            DateTime fromDate,
            DateTime toDate)
        {
            string idxCol = GetDataIndexColumnName(objDataService, dataTableName);
            RevertPipeMovementRigStatesByIndex(objDataService, objRigState, dataTableName, idxCol, 0, long.MaxValue);
        }

        public static void RevertPipeMovementRigStatesByIndex(
            IDataServiceDIntel objDataService,
            rigState objRigState,
            string dataTableName,
            string indexColName,
            long fromIndex,
            long toIndex)
        {
            try
            {
                int tripInColor = GetRigStateColor(objRigState, 6);
                int pumpInColor = GetRigStateColor(objRigState, 4);
                int tripOutColor = GetRigStateColor(objRigState, 10);
                int pumpOutColor = GetRigStateColor(objRigState, 8);

                double circCutOff = objRigState.CIRCCutOff;

                objDataService.BeginTransaction();

                string tripInSql = $"UPDATE [{dataTableName}] SET RIG_STATE=6, RIG_STATE_COLOR={tripInColor} " +
                                   $"WHERE CIRC < {circCutOff.ToString(CultureInfo.InvariantCulture)} AND RIG_STATE=27 AND [{indexColName}] >= {fromIndex} AND [{indexColName}] <= {toIndex};";
                objDataService.ExecuteNonQuery(tripInSql);

                string pumpInSql = $"UPDATE [{dataTableName}] SET RIG_STATE=4, RIG_STATE_COLOR={pumpInColor} " +
                                   $"WHERE CIRC >= {circCutOff.ToString(CultureInfo.InvariantCulture)} AND RIG_STATE=27 AND [{indexColName}] >= {fromIndex} AND [{indexColName}] <= {toIndex};";
                objDataService.ExecuteNonQuery(pumpInSql);

                string tripOutSql = $"UPDATE [{dataTableName}] SET RIG_STATE=10, RIG_STATE_COLOR={tripOutColor} " +
                                    $"WHERE CIRC < {circCutOff.ToString(CultureInfo.InvariantCulture)} AND RIG_STATE=28 AND [{indexColName}] >= {fromIndex} AND [{indexColName}] <= {toIndex};";
                objDataService.ExecuteNonQuery(tripOutSql);

                string pumpOutSql = $"UPDATE [{dataTableName}] SET RIG_STATE=8, RIG_STATE_COLOR={pumpOutColor} " +
                                    $"WHERE CIRC >= {circCutOff.ToString(CultureInfo.InvariantCulture)} AND RIG_STATE=28 AND [{indexColName}] >= {fromIndex} AND [{indexColName}] <= {toIndex};";
                objDataService.ExecuteNonQuery(pumpOutSql);

                objDataService.Commit();
            }
            catch
            {
                objDataService.RollBack();
            }
        }

        // =========================================================================================
        // Process Pipe Movement (Detection of States 27 & 28)
        // =========================================================================================
        public static void DoProcessPipeMovement(
            IDataServiceDIntel objDataService,
            rigState objRigState,
            string dataTableName,
            DateTime fromDate,
            DateTime toDate)
        {
            string idxCol = GetDataIndexColumnName(objDataService, dataTableName);
            DoProcessPipeMovementByIndex(objDataService, objRigState, dataTableName, idxCol, 0, long.MaxValue);
        }

        public static void DoProcessPipeMovementByIndex(
            IDataServiceDIntel objDataService,
            rigState objRigState,
            string dataTableName,
            string indexColName,
            long fromIndex,
            long toIndex)
        {
            try
            {
                string dtCol = GetDateTimeColumnSql(objDataService, dataTableName);
                string datasetSql = $"SELECT [{indexColName}] AS DATA_INDEX, {dtCol}, DEPTH, RIG_STATE FROM [{dataTableName}] " +
                                    $"WHERE [{indexColName}] >= {fromIndex} AND [{indexColName}] <= {toIndex} AND RIG_STATE IS NOT NULL ORDER BY [{indexColName}] ASC;";
                DataTable objData = objDataService.GetTable(datasetSql);

                if (objData == null || objData.Rows.Count == 0) return;

                double threshold = objRigState.PipeMovementThreshold > 0 ? objRigState.PipeMovementThreshold : 20.0;

                int count = objData.Rows.Count;
                long[] arrIndex = new long[count];
                DateTime[] arrDateTime = new DateTime[count];
                double[] arrDepth = new double[count];
                int[] arrRigState = new int[count];

                for (int r = 0; r < count; r++)
                {
                    DataRow row = objData.Rows[r];
                    arrIndex[r] = Convert.ToInt64(row["DATA_INDEX"]);
                    arrDateTime[r] = ParseDate(row["DATETIME"]);
                    arrDepth[r] = DataService.checkNull(row["DEPTH"], 0.0);
                    arrRigState[r] = DataService.checkNull(row["RIG_STATE"], 0);
                }

                var bulkExecutor = new BulkCommandExecutor(objDataService, 500);
                int moveInColor = GetRigStateColor(objRigState, 27);
                int moveOutColor = GetRigStateColor(objRigState, 28);

                for (int i = 0; i < count; i++)
                {
                    int state = arrRigState[i];

                    // Process Pipe Move In candidates: RigState 6 (Trip In) or 4 (Pump In)
                    if (state == 6 || state == 4)
                    {
                        int targetState = state;
                        double startDepth = (i > 0) ? arrDepth[i - 1] : arrDepth[i];
                        long startIdx = arrIndex[i];

                        for (int j = i + 1; j < count; j++)
                        {
                            if (arrRigState[j] != targetState && arrRigState[j] != 27)
                            {
                                int rangeEnd = j;
                                double endDepth = arrDepth[rangeEnd - 1];
                                long endIdx = arrIndex[rangeEnd - 1];
                                double tripDepth = Math.Abs(endDepth - startDepth);

                                if ((j - i) == 1 && tripDepth == 0 && rangeEnd - 2 >= 0)
                                {
                                    tripDepth = Math.Abs(arrDepth[rangeEnd - 2] - endDepth);
                                }

                                if (tripDepth <= threshold)
                                {
                                    string updateSql = $"UPDATE [{dataTableName}] SET RIG_STATE=27, RIG_STATE_COLOR={moveInColor} " +
                                                       $"WHERE [{indexColName}] >= {startIdx} AND [{indexColName}] <= {endIdx};";
                                    bulkExecutor.ExecuteCommand(updateSql);
                                }

                                i = j - 1;
                                break;
                            }
                        }
                    }
                    // Process Pipe Move Out candidates: RigState 10 (Trip Out) or 8 (Pump Out)
                    else if (state == 10 || state == 8)
                    {
                        int targetState = state;
                        double startDepth = (i > 0) ? arrDepth[i - 1] : arrDepth[i];
                        long startIdx = arrIndex[i];

                        for (int j = i + 1; j < count; j++)
                        {
                            if (arrRigState[j] != targetState && arrRigState[j] != 28)
                            {
                                int rangeEnd = j;
                                double endDepth = arrDepth[rangeEnd - 1];
                                long endIdx = arrIndex[rangeEnd - 1];
                                double tripDepth = Math.Abs(endDepth - startDepth);

                                if ((j - i) == 1 && tripDepth == 0 && rangeEnd - 2 >= 0)
                                {
                                    tripDepth = Math.Abs(arrDepth[rangeEnd - 2] - endDepth);
                                }

                                if (tripDepth <= threshold)
                                {
                                    string updateSql = $"UPDATE [{dataTableName}] SET RIG_STATE=28, RIG_STATE_COLOR={moveOutColor} " +
                                                       $"WHERE [{indexColName}] >= {startIdx} AND [{indexColName}] <= {endIdx};";
                                    bulkExecutor.ExecuteCommand(updateSql);
                                }

                                i = j - 1;
                                break;
                            }
                        }
                    }
                }

                bulkExecutor.FlushBuffer();
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
            }
        }

        public static void EnsureRigStateColumnsExist(IDataServiceDIntel objDataService, string tableName)
        {
            try
            {
                if (!IsColumnAvailable(objDataService, tableName, "RIG_STATE"))
                {
                    objDataService.ExecuteNonQuery($"ALTER TABLE [{tableName}] ADD COLUMN [RIG_STATE] INTEGER DEFAULT -999;");
                }
                if (!IsColumnAvailable(objDataService, tableName, "RIG_STATE_COLOR"))
                {
                    objDataService.ExecuteNonQuery($"ALTER TABLE [{tableName}] ADD COLUMN [RIG_STATE_COLOR] INTEGER DEFAULT 0;");
                }
            }
            catch { }
        }

        public static string GetDateTimeColumnSql(IDataServiceDIntel db, string tableName)
        {
            if (IsColumnAvailable(db, tableName, "DATETIME"))
            {
                return "[DATETIME]";
            }
            if (IsColumnAvailable(db, tableName, "DATE") && IsColumnAvailable(db, tableName, "TIME"))
            {
                return "(DATE || ' ' || TIME) AS [DATETIME]";
            }
            if (IsColumnAvailable(db, tableName, "DATE"))
            {
                return "[DATE] AS [DATETIME]";
            }
            return "'' AS [DATETIME]";
        }

        public static string GetDataIndexColumnName(IDataServiceDIntel db, string tableName)
        {
            return IsColumnAvailable(db, tableName, "DATA_INDEX") ? "DATA_INDEX" : "rowid";
        }

        public static string GetDataIndexColumnSql(IDataServiceDIntel db, string tableName)
        {
            if (IsColumnAvailable(db, tableName, "DATA_INDEX"))
            {
                return "[DATA_INDEX]";
            }
            return "rowid AS [DATA_INDEX]";
        }

        public static string BuildSelectColumnsSql(IDataServiceDIntel db, string tableName)
        {
            string idxSql = GetDataIndexColumnSql(db, tableName);
            string dtSql = GetDateTimeColumnSql(db, tableName);

            string rpmCol = ResolveChannelColumn(db, tableName, "RPM", new[] { "RPM" });
            string storCol = ResolveChannelColumn(db, tableName, "STOR", new[] { "STOR", "TORQUE", "TORQ", "TQA" });
            string circCol = ResolveChannelColumn(db, tableName, "CIRC", new[] { "CIRC", "FLOW", "FLWI", "PUMP_RATE" });
            string depthCol = ResolveChannelColumn(db, tableName, "DEPTH", new[] { "DEPTH", "BIT_DEPTH", "DBTM" });
            string hdthCol = ResolveChannelColumn(db, tableName, "HDTH", new[] { "HDTH", "HOLE_DEPTH", "DMEA" });
            string hkldCol = ResolveChannelColumn(db, tableName, "HKLD", new[] { "HKLD", "HOOK_LOAD", "HOOKLOAD" });
            string sppaCol = ResolveChannelColumn(db, tableName, "SPPA", new[] { "SPPA", "PUMP_PRESS", "PRESSURE", "SPP" });
            string rigStateCol = ResolveChannelColumn(db, tableName, "RIG_STATE", new[] { "RIG_STATE" }, defaultValue: "-999");

            return $"{idxSql}, {dtSql}, {rpmCol}, {storCol}, {circCol}, {depthCol}, {hdthCol}, {hkldCol}, {sppaCol}, {rigStateCol}";
        }

        private static string ResolveChannelColumn(IDataServiceDIntel db, string tableName, string targetAlias, string[] possibleNames, string defaultValue = "0.0")
        {
            foreach (var name in possibleNames)
            {
                if (IsColumnAvailable(db, tableName, name))
                {
                    return $"[{name}] AS [{targetAlias}]";
                }
            }
            return $"{defaultValue} AS [{targetAlias}]";
        }

        public static long FindDataIndexForDate(IDataServiceDIntel db, string tableName, DateTime targetDate, bool findFirst, long minIdx, long maxIdx)
        {
            long low = minIdx;
            long high = maxIdx;
            long result = findFirst ? minIdx : maxIdx;
            string dtCol = GetDateTimeColumnSql(db, tableName);
            string idxCol = GetDataIndexColumnName(db, tableName);

            while (low <= high)
            {
                long mid = low + (high - low) / 2;
                var dtRow = db.GetTable($"SELECT {dtCol} FROM [{tableName}] WHERE [{idxCol}] = {mid};");
                if (dtRow == null || dtRow.Rows.Count == 0)
                {
                    break;
                }
                DateTime midDate = ParseDate(dtRow.Rows[0]["DATETIME"]);
                if (findFirst)
                {
                    if (midDate >= targetDate)
                    {
                        result = mid;
                        high = mid - 1;
                    }
                    else
                    {
                        low = mid + 1;
                    }
                }
                else
                {
                    if (midDate <= targetDate)
                    {
                        result = mid;
                        low = mid + 1;
                    }
                    else
                    {
                        high = mid - 1;
                    }
                }
            }
            return result;
        }

        // =========================================================================================
        // Core Classification Engine (updateRigStateEx)
        // =========================================================================================
        public static bool UpdateRigStateEx(
            IDataServiceDIntel objDataService,
            rigState objRigState,
            TimeLog objTimeLog,
            DataTable objData,
            DateTime targetDate)
        {
            return UpdateRigStateEx(objDataService, objRigState, objTimeLog, objData, targetDate, -1, false, "DATA_INDEX");
        }

        public static bool UpdateRigStateEx(
            IDataServiceDIntel objDataService,
            rigState objRigState,
            TimeLog objTimeLog,
            DataTable objData,
            DateTime targetDate,
            long targetDataIndex,
            bool isLastChunk = false,
            string indexColName = "DATA_INDEX")
        {
            rigState.enumDirection bitDirection = rigState.enumDirection.Stall;
            rigState.enumDirection lastBitDirection = rigState.enumDirection.Stall;
            rigState.enumDirection currentBitDirection = rigState.enumDirection.Stall;
            var objBulkExecutor = new BulkCommandExecutor(objDataService, 500);

            try
            {
                string dataTableName = objTimeLog.__dataTableName;

                if (objData == null || objData.Rows.Count == 0)
                {
                    return false;
                }

                DateTime startingDate = objData.Columns.Contains(rigState.cnDATETIME)
                    ? ParseDate(DataService.checkNull(objData.Rows[0][rigState.cnDATETIME], ""))
                    : DateTime.MinValue;
                double maxDepthReached = 0;
                int currentRigState = 0;

                double lnPrevDepth = 0;
                if (objData.Columns.Contains("DATA_INDEX") && objData.Rows[0]["DATA_INDEX"] != DBNull.Value)
                {
                    long firstIdx = Convert.ToInt64(objData.Rows[0]["DATA_INDEX"]);
                    lnPrevDepth = GetLastChannelValueByIndex(objDataService, dataTableName, rigState.cnDEPTH, firstIdx);
                }
                else
                {
                    lnPrevDepth = GetLastChannelValue(objDataService, dataTableName, rigState.cnDEPTH, startingDate);
                }

                int rowIndex = 0;
                int rowCount = objData.Rows.Count;
                int uRowCounter = 0;

                // Find the target date or index from the dataset
                if (targetDataIndex >= 0 && objData.Columns.Contains("DATA_INDEX"))
                {
                    for (int i = 0; i < rowCount; i++)
                    {
                        long rowIdx = Convert.ToInt64(objData.Rows[i]["DATA_INDEX"]);
                        if (rowIdx >= targetDataIndex)
                        {
                            if (i > 0)
                            {
                                lnPrevDepth = Math.Round(DataService.checkNull(GetRowValue(objData, objData.Rows[i - 1], rigState.cnDEPTH), 0.0), 4);
                            }
                            rowIndex = i;
                            break;
                        }
                    }
                }
                else if (objData.Columns.Contains(rigState.cnDATETIME))
                {
                    for (int i = 0; i < rowCount; i++)
                    {
                        DateTime dataDateTime = ParseDate(objData.Rows[i][rigState.cnDATETIME]);
                        if (dataDateTime >= targetDate)
                        {
                            if (i > 0)
                            {
                                lnPrevDepth = Math.Round(DataService.checkNull(GetRowValue(objData, objData.Rows[i - 1], rigState.cnDEPTH), 0.0), 4);
                            }
                            rowIndex = i;
                            break;
                        }
                    }
                }

                wellSection? objWellsection = null;

                int stopIndex = isLastChunk ? rowCount : (rowCount >= 3 ? rowCount - 3 : rowCount);
                if (stopIndex < rowIndex) stopIndex = rowCount;

                for (int dataRowIndex = rowIndex; dataRowIndex < stopIndex; dataRowIndex++)
                {
                    DataRow objRow = objData.Rows[dataRowIndex];

                    double lnRPM = 0;
                    double lnSTOR = 0;
                    double lnCIRC = 0;
                    double lnDepth = 0;
                    double lnHDepth = 0;
                    double lnHkld = 0;
                    double lnSPPA = 0;
                    DateTime dtDateTime;
                    double lnDataFlag = 0;
                    double lnAirPressure = 0;
                    double lnMistFlow = 0;
                    double hSensitivity = objRigState.DepthComparisonSens;
                    bool isNotDrilling = false;
                    bool bitOnBottom = false;
                    double movementSensitivity = 0.05;

                    currentRigState = -999;

                    // --- [ORIGINAL VB LOGIC: lnRPM = DataService.checkNull(getValue(objData, objRow, cnRPM), 0)...] ---
                    // --- [NEW C# / SQLITE LOGIC: Safe column extraction with GetRowValue helper] ---
                    lnRPM = DataService.checkNull(GetRowValue(objData, objRow, rigState.cnRPM), 0.0);
                    lnSTOR = DataService.checkNull(GetRowValue(objData, objRow, rigState.cnSTOR), 0.0);
                    lnCIRC = DataService.checkNull(GetRowValue(objData, objRow, rigState.cnCIRC), 0.0);
                    lnDepth = Math.Round(DataService.checkNull(GetRowValue(objData, objRow, rigState.cnDEPTH), 0.0), 3);
                    lnHDepth = Math.Round(DataService.checkNull(GetRowValue(objData, objRow, rigState.cnHDTH), 0.0), 3);
                    lnHkld = DataService.checkNull(GetRowValue(objData, objRow, rigState.cnHKLD), 0.0);
                    lnSPPA = DataService.checkNull(GetRowValue(objData, objRow, rigState.cnSPPA), 0.0);
                    dtDateTime = ParseDate(DataService.checkNull(objRow[rigState.cnDATETIME], ""));

                    if (objData.Columns.Contains("AIR_PRESSURE"))
                    {
                        lnAirPressure = DataService.checkNull(GetRowValue(objData, objRow, rigState.cnAirPressure), 0.0);
                    }
                    if (objData.Columns.Contains("MIST_FLOW"))
                    {
                        lnMistFlow = DataService.checkNull(GetRowValue(objData, objRow, rigState.cnMistFlow), 0.0);
                    }
                    else
                    {
                        // ''Recent database changes not applied. Turn it off ...
                        objRigState.DetectAirDrilling = false;
                    }

                    // ''//################################################################################//
                    // ''//Re-initialize rig state thresholds from the backup
                    // ''//################################################################################//
                    // In C# static context, thresholds are read directly from objRigState.

                    // ''*********************************************************************************''
                    // ''****** Check if section related rig states are set to used **********************''
                    // ''*********************************************************************************''
                    if (objRigState.UseWellSectionRigState)
                    {
                        objWellsection = GetRigStateFromWellSection(objRigState, lnHDepth);
                        if (objWellsection != null)
                        {
                            // WellSection override if present
                        }
                    }

                    if (lnDepth >= maxDepthReached)
                    {
                        maxDepthReached = lnDepth;
                    }
                    if (lnHDepth < maxDepthReached)
                    {
                        maxDepthReached = lnHDepth;
                    }

                    // --- [BIT DIRECTION DETECTION] ---
                    if (lnDepth > lnPrevDepth)
                    {
                        if (Math.Round(Math.Abs(lnDepth - lnPrevDepth), 3) <= objRigState.Sensitivity)
                        {
                            double nextDepth = (rowIndex + 1 < objData.Rows.Count)
                                ? Math.Round(DataService.checkNull(GetRowValue(objData, objData.Rows[rowIndex + 1], rigState.cnDEPTH), 0.0), 3)
                                : lnDepth;

                            if (nextDepth >= lnDepth)
                            {
                                bitDirection = rigState.enumDirection.Down;
                            }
                            else
                            {
                                bitDirection = lastBitDirection;
                            }
                        }
                        else
                        {
                            bitDirection = rigState.enumDirection.Down;
                        }
                    }
                    else
                    {
                        if (lnDepth < lnPrevDepth)
                        {
                            if (Math.Round(Math.Abs(lnDepth - lnPrevDepth), 3) <= objRigState.Sensitivity)
                            {
                                double nextDepth = (rowIndex + 1 < objData.Rows.Count)
                                    ? Math.Round(DataService.checkNull(GetRowValue(objData, objData.Rows[rowIndex + 1], rigState.cnDEPTH), 0.0), 3)
                                    : lnDepth;

                                if (nextDepth <= lnDepth)
                                {
                                    bitDirection = rigState.enumDirection.Up;
                                }
                                else
                                {
                                    bitDirection = lastBitDirection;
                                }
                            }
                            else
                            {
                                bitDirection = rigState.enumDirection.Up;
                            }
                        }
                        else
                        {
                            if (rowIndex < rowCount - 1)
                            {
                                bitDirection = rigState.enumDirection.Stall;
                                double nextDepth = Math.Round(DataService.checkNull(GetRowValue(objData, objData.Rows[rowIndex + 1], rigState.cnDEPTH), 0.0), 3);

                                if (nextDepth > lnDepth)
                                {
                                    bitDirection = rigState.enumDirection.Down;
                                    if (lastBitDirection == rigState.enumDirection.Stall)
                                    {
                                        bitDirection = rigState.enumDirection.Stall;
                                    }
                                }
                                if (nextDepth < lnDepth)
                                {
                                    bitDirection = rigState.enumDirection.Up;
                                    if (lastBitDirection == rigState.enumDirection.Stall)
                                    {
                                        bitDirection = rigState.enumDirection.Stall;
                                    }
                                }
                            }
                            else
                            {
                                bitDirection = lastBitDirection;
                            }
                        }
                    }

                    // ''Record current bit direction
                    currentBitDirection = bitDirection;

                    // ''>>>First check the equality of hole depth and bit depth
                    if (Math.Round(lnHDepth - lnDepth, 3) <= hSensitivity)
                    {
                        bitOnBottom = true;
                        // ''>>>Looks Equal 
                        // ''Now check previous data and try to find the depth greater than this
                        if ((lnDepth < lnPrevDepth) && Math.Round(Math.Abs(lnDepth - lnPrevDepth), 3) > objRigState.Sensitivity)
                        {
                            bitOnBottom = false;
                        }
                    }
                    else
                    {
                        bitOnBottom = false;
                    }

                    if (bitOnBottom)
                    {
                        if (bitDirection == rigState.enumDirection.Up)
                        {
                            // ''Wrong Detection of direction, it must be going down
                            bitDirection = rigState.enumDirection.Down;
                        }
                    }

                    // =============================================================================
                    // Pass 1: Standard Rig State Cutoffs
                    // =============================================================================
                    // ''#0 Rotary Drill
                    if (bitDirection == rigState.enumDirection.Down && lnRPM > objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && bitOnBottom)
                    {
                        currentRigState = 0;
                        goto SetRigState;
                    }

                    // ''#1 Slide Drill
                    if ((bitDirection == rigState.enumDirection.Down || bitDirection == rigState.enumDirection.Stall) && lnRPM <= objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && bitOnBottom)
                    {
                        currentRigState = 1;
                        goto SetRigState;
                    }

                    // ''#2 In Slips
                    if (bitDirection == rigState.enumDirection.Stall && (lnRPM <= objRigState.RPMCutOff || Math.Floor(lnSTOR) <= objRigState.TorqueCutOff) && lnHkld <= objRigState.HookloadCutOff && !bitOnBottom)
                    {
                        currentRigState = 2;
                        goto SetRigState;
                    }

                    // ''#3 Ream
                    if (bitDirection == rigState.enumDirection.Down && lnRPM > objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && !bitOnBottom)
                    {
                        currentRigState = 3;
                        goto SetRigState;
                    }

                    // ''#4 Run in Pump
                    if (bitDirection == rigState.enumDirection.Down && lnRPM <= objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && !bitOnBottom)
                    {
                        currentRigState = 4;
                        goto SetRigState;
                    }

                    // ''#5 Rotate In (Note: preserved from VB logic)
                    if (bitDirection == rigState.enumDirection.Down && lnRPM > objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && !bitOnBottom)
                    {
                        currentRigState = 5;
                        goto SetRigState;
                    }

                    // ''#6 Trip In
                    if (bitDirection == rigState.enumDirection.Down && lnRPM <= objRigState.RPMCutOff && lnCIRC <= objRigState.CIRCCutOff && !bitOnBottom)
                    {
                        currentRigState = 6;
                        goto SetRigState;
                    }

                    // ''#7 Back Ream
                    if (bitDirection == rigState.enumDirection.Up && lnRPM > objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && !bitOnBottom)
                    {
                        currentRigState = 7;
                        goto SetRigState;
                    }

                    // ''#8 Pump Out
                    if (bitDirection == rigState.enumDirection.Up && lnRPM <= objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && !bitOnBottom)
                    {
                        currentRigState = 8;
                        goto SetRigState;
                    }

                    // ''#9 Rotate Out
                    if (bitDirection == rigState.enumDirection.Up && lnRPM > objRigState.RPMCutOff && lnCIRC <= objRigState.CIRCCutOff && !bitOnBottom)
                    {
                        currentRigState = 9;
                        goto SetRigState;
                    }

                    // ''#10 Trip Out
                    if (bitDirection == rigState.enumDirection.Up && lnRPM <= objRigState.RPMCutOff && lnCIRC <= objRigState.CIRCCutOff && !bitOnBottom)
                    {
                        currentRigState = 10;
                        goto SetRigState;
                    }

                    // ''#11 Rotate and Circ
                    if (lnRPM > objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && !bitOnBottom)
                    {
                        currentRigState = 11;
                        goto SetRigState;
                    }

                    // ''#12 Circ
                    if (lnRPM <= objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && !bitOnBottom)
                    {
                        currentRigState = 12;
                        goto SetRigState;
                    }

                    // ''#13 Rotate
                    if (lnRPM > objRigState.RPMCutOff && lnCIRC <= objRigState.CIRCCutOff && !bitOnBottom)
                    {
                        currentRigState = 13;
                        goto SetRigState;
                    }

                    // ''#14 Stationary
                    if (lnRPM <= objRigState.RPMCutOff && lnCIRC <= objRigState.CIRCCutOff && !bitOnBottom)
                    {
                        currentRigState = 14;
                        goto SetRigState;
                    }

                SetRigState:
                    if (currentRigState == -999)
                    {
                        currentRigState = 15;
                    }

                    // =============================================================================
                    // Pass 2: Lower Sensitivity with lastBitDirection & Pressure Checks
                    // =============================================================================
                    if (currentRigState == 15)
                    {
                        // ''Try one more time with less sensitivity
                        bitDirection = lastBitDirection;

                        // '#18 Pressure Testing
                        if (objRigState.PumpPressureCutOff > 0)
                        {
                            if (bitDirection == rigState.enumDirection.Stall && lnSPPA > objRigState.PumpPressureCutOff && lnCIRC <= objRigState.CIRCCutOff)
                            {
                                currentRigState = 18;
                                goto SetRigState;
                            }
                        }

                        // ''#17 Packed Off
                        if (objRigState.PumpPressureCutOff > 0)
                        {
                            if (lnSPPA > objRigState.PumpPressureCutOff && lnCIRC <= objRigState.CIRCCutOff)
                            {
                                currentRigState = 17;
                                goto SetRigState;
                            }
                        }

                        // ''#0 Rotary Drill
                        if (bitDirection == rigState.enumDirection.Down && lnRPM > objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && bitOnBottom)
                        {
                            currentRigState = 0;
                            goto SetRigState2;
                        }
                        // ''#1 Slide Drill
                        if (bitDirection == rigState.enumDirection.Down && lnRPM <= objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && bitOnBottom)
                        {
                            currentRigState = 1;
                            goto SetRigState2;
                        }
                        // ''#2 In Slips
                        if (bitDirection == rigState.enumDirection.Stall && (lnRPM <= objRigState.RPMCutOff || Math.Floor(lnSTOR) <= objRigState.TorqueCutOff) && lnHkld <= objRigState.HookloadCutOff && !bitOnBottom)
                        {
                            currentRigState = 2;
                            goto SetRigState2;
                        }
                        // ''#3 Ream
                        if (bitDirection == rigState.enumDirection.Down && lnRPM > objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && !bitOnBottom)
                        {
                            currentRigState = 3;
                            goto SetRigState2;
                        }
                        // ''#4 Run in Pump
                        if (bitDirection == rigState.enumDirection.Down && lnRPM <= objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && !bitOnBottom)
                        {
                            currentRigState = 4;
                            goto SetRigState2;
                        }
                        // ''#5 Rotate In
                        if (bitDirection == rigState.enumDirection.Down && lnRPM > objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && !bitOnBottom)
                        {
                            currentRigState = 5;
                            goto SetRigState2;
                        }
                        // ''#6 Trip In
                        if (bitDirection == rigState.enumDirection.Down && lnRPM <= objRigState.RPMCutOff && lnCIRC <= objRigState.CIRCCutOff && !bitOnBottom)
                        {
                            currentRigState = 6;
                            goto SetRigState2;
                        }
                        // ''#7 Back Ream
                        if (bitDirection == rigState.enumDirection.Up && lnRPM > objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && !bitOnBottom)
                        {
                            currentRigState = 7;
                            goto SetRigState2;
                        }
                        // ''#8 Pump Out
                        if (bitDirection == rigState.enumDirection.Up && lnRPM <= objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && !bitOnBottom)
                        {
                            currentRigState = 8;
                            goto SetRigState2;
                        }
                        // ''#9 Rotate Out
                        if (bitDirection == rigState.enumDirection.Up && lnRPM > objRigState.RPMCutOff && lnCIRC <= objRigState.CIRCCutOff && !bitOnBottom)
                        {
                            currentRigState = 9;
                            goto SetRigState2;
                        }
                        // ''#10 Trip Out
                        if (bitDirection == rigState.enumDirection.Up && lnRPM <= objRigState.RPMCutOff && lnCIRC <= objRigState.CIRCCutOff && !bitOnBottom)
                        {
                            currentRigState = 10;
                            goto SetRigState2;
                        }
                        // ''#11 Rotate and Circ
                        if (lnRPM > objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && !bitOnBottom)
                        {
                            currentRigState = 11;
                            goto SetRigState2;
                        }
                        // ''#12 Circ
                        if (lnRPM <= objRigState.RPMCutOff && lnCIRC > objRigState.CIRCCutOff && !bitOnBottom)
                        {
                            currentRigState = 12;
                            goto SetRigState2;
                        }
                        // ''#13 Rotate
                        if (lnRPM > objRigState.RPMCutOff && lnCIRC <= objRigState.CIRCCutOff && !bitOnBottom)
                        {
                            currentRigState = 13;
                            goto SetRigState2;
                        }
                        // ''#14 Stationary
                        if (lnRPM <= objRigState.RPMCutOff && lnCIRC <= objRigState.CIRCCutOff && !bitOnBottom)
                        {
                            currentRigState = 14;
                            goto SetRigState2;
                        }
                    }

                SetRigState2:
                    if (currentRigState == -999)
                    {
                        currentRigState = 15;
                    }

                    // =============================================================================
                    // Pass 3: Cutoff Values at Zero
                    // =============================================================================
                    if (currentRigState == 15)
                    {
                        // ''Try one more time by taking cutoff values zero
                        if (bitDirection == rigState.enumDirection.Down && lnRPM > 0 && lnCIRC > 0 && bitOnBottom)
                        {
                            currentRigState = 0;
                            goto SetRigState3;
                        }
                        if (bitDirection == rigState.enumDirection.Down && lnRPM <= 0 && lnCIRC > 0 && bitOnBottom)
                        {
                            currentRigState = 1;
                            goto SetRigState3;
                        }
                        if (bitDirection == rigState.enumDirection.Stall && (lnRPM <= 0 || Math.Floor(lnSTOR) <= objRigState.TorqueCutOff) && lnHkld <= objRigState.HookloadCutOff && !bitOnBottom)
                        {
                            currentRigState = 2;
                            goto SetRigState3;
                        }
                        if (bitDirection == rigState.enumDirection.Down && lnRPM > 0 && lnCIRC > 0 && !bitOnBottom)
                        {
                            currentRigState = 3;
                            goto SetRigState3;
                        }
                        if (bitDirection == rigState.enumDirection.Down && lnRPM <= 0 && lnCIRC > 0 && !bitOnBottom)
                        {
                            currentRigState = 4;
                            goto SetRigState3;
                        }
                        if (bitDirection == rigState.enumDirection.Down && lnRPM > 0 && lnCIRC > 0 && !bitOnBottom)
                        {
                            currentRigState = 5;
                            goto SetRigState3;
                        }
                        if (bitDirection == rigState.enumDirection.Down && lnRPM <= 0 && lnCIRC <= 0 && !bitOnBottom)
                        {
                            currentRigState = 6;
                            goto SetRigState3;
                        }
                        if (bitDirection == rigState.enumDirection.Up && lnRPM > 0 && lnCIRC > 0 && !bitOnBottom)
                        {
                            currentRigState = 7;
                            goto SetRigState3;
                        }
                        if (bitDirection == rigState.enumDirection.Up && lnRPM <= 0 && lnCIRC > 0 && !bitOnBottom)
                        {
                            currentRigState = 8;
                            goto SetRigState3;
                        }
                        if (bitDirection == rigState.enumDirection.Up && lnRPM > 0 && lnCIRC <= 0 && !bitOnBottom)
                        {
                            currentRigState = 9;
                            goto SetRigState3;
                        }
                        if (bitDirection == rigState.enumDirection.Up && lnRPM <= 0 && lnCIRC <= 0 && !bitOnBottom)
                        {
                            currentRigState = 10;
                            goto SetRigState3;
                        }
                        if (lnRPM > 0 && lnCIRC > 0 && !bitOnBottom)
                        {
                            currentRigState = 11;
                            goto SetRigState3;
                        }
                        if (lnRPM <= 0 && lnCIRC > 0 && !bitOnBottom)
                        {
                            currentRigState = 12;
                            goto SetRigState3;
                        }
                        if (lnRPM > 0 && lnCIRC <= 0 && !bitOnBottom)
                        {
                            currentRigState = 13;
                            goto SetRigState3;
                        }
                        if (lnRPM <= 0 && lnCIRC <= 0 && !bitOnBottom)
                        {
                            currentRigState = 14;
                            goto SetRigState3;
                        }
                    }

                SetRigState3:
                    if (currentRigState == -999)
                    {
                        currentRigState = 15;
                    }

                    // =============================================================================
                    // Pass 4: Zero Cutoffs with currentBitDirection
                    // =============================================================================
                    if (currentRigState == 15)
                    {
                        bitDirection = currentBitDirection;
                        if (bitDirection == rigState.enumDirection.Down && lnRPM > 0 && lnCIRC > 0 && bitOnBottom)
                        {
                            currentRigState = 0;
                            goto SetRigState4;
                        }
                        if (bitDirection == rigState.enumDirection.Down && lnRPM <= 0 && lnCIRC > 0 && bitOnBottom)
                        {
                            currentRigState = 1;
                            goto SetRigState4;
                        }
                        if (bitDirection == rigState.enumDirection.Stall && (lnRPM <= 0 || Math.Floor(lnSTOR) <= objRigState.TorqueCutOff) && lnHkld <= objRigState.HookloadCutOff && !bitOnBottom)
                        {
                            currentRigState = 2;
                            goto SetRigState4;
                        }
                        if (bitDirection == rigState.enumDirection.Down && lnRPM > 0 && lnCIRC > 0 && !bitOnBottom)
                        {
                            currentRigState = 3;
                            goto SetRigState4;
                        }
                        if (bitDirection == rigState.enumDirection.Down && lnRPM <= 0 && lnCIRC > 0 && !bitOnBottom)
                        {
                            currentRigState = 4;
                            goto SetRigState4;
                        }
                        if (bitDirection == rigState.enumDirection.Down && lnRPM > 0 && lnCIRC > 0 && !bitOnBottom)
                        {
                            currentRigState = 5;
                            goto SetRigState4;
                        }
                        if (bitDirection == rigState.enumDirection.Down && lnRPM <= 0 && lnCIRC <= 0 && !bitOnBottom)
                        {
                            currentRigState = 6;
                            goto SetRigState4;
                        }
                        if (bitDirection == rigState.enumDirection.Up && lnRPM > 0 && lnCIRC > 0 && !bitOnBottom)
                        {
                            currentRigState = 7;
                            goto SetRigState4;
                        }
                        if (bitDirection == rigState.enumDirection.Up && lnRPM <= 0 && lnCIRC > 0 && !bitOnBottom)
                        {
                            currentRigState = 8;
                            goto SetRigState4;
                        }
                        if (bitDirection == rigState.enumDirection.Up && lnRPM > 0 && lnCIRC <= 0 && !bitOnBottom)
                        {
                            currentRigState = 9;
                            goto SetRigState4;
                        }
                        if (bitDirection == rigState.enumDirection.Up && lnRPM <= 0 && lnCIRC <= 0 && !bitOnBottom)
                        {
                            currentRigState = 10;
                            goto SetRigState4;
                        }
                        if (lnRPM > 0 && lnCIRC > 0 && !bitOnBottom)
                        {
                            currentRigState = 11;
                            goto SetRigState4;
                        }
                        if (lnRPM <= 0 && lnCIRC > 0 && !bitOnBottom)
                        {
                            currentRigState = 12;
                            goto SetRigState4;
                        }
                        if (lnRPM > 0 && lnCIRC <= 0 && !bitOnBottom)
                        {
                            currentRigState = 13;
                            goto SetRigState4;
                        }
                        if (lnRPM <= 0 && lnCIRC <= 0 && !bitOnBottom)
                        {
                            currentRigState = 14;
                            goto SetRigState4;
                        }
                    }

                SetRigState4:
                    // =============================================================================
                    // Pass 5: Fallback Unknown Deductions
                    // =============================================================================
                    if (currentRigState == 15) // Unknown
                    {
                        if (bitDirection == rigState.enumDirection.Stall && (lnRPM <= objRigState.RPMCutOff || Math.Floor(lnSTOR) <= objRigState.TorqueCutOff) && lnHkld <= objRigState.HookloadCutOff)
                        {
                            currentRigState = 2;
                            goto SetRigState5;
                        }
                        if (bitDirection == rigState.enumDirection.Stall && lnRPM <= objRigState.RPMCutOff && lnCIRC <= objRigState.CIRCCutOff)
                        {
                            currentRigState = 14;
                            goto SetRigState5;
                        }
                        if (bitDirection == rigState.enumDirection.Stall && lnRPM >= objRigState.RPMCutOff && lnCIRC >= objRigState.CIRCCutOff && bitOnBottom)
                        {
                            currentRigState = 0;
                            goto SetRigState5;
                        }
                        if (bitDirection == rigState.enumDirection.Down && (lnRPM >= objRigState.RPMCutOff) && bitOnBottom)
                        {
                            currentRigState = 0;
                            goto SetRigState5;
                        }
                        if (bitDirection == rigState.enumDirection.Down && (lnRPM <= objRigState.RPMCutOff) && bitOnBottom)
                        {
                            currentRigState = 1;
                            goto SetRigState5;
                        }
                        if (bitDirection == rigState.enumDirection.Stall && lnRPM >= objRigState.RPMCutOff && lnCIRC <= objRigState.CIRCCutOff && bitOnBottom)
                        {
                            currentRigState = 13;
                            goto SetRigState5;
                        }
                    }

                SetRigState5:
                    if (currentRigState == 0 || currentRigState == 1)
                    {
                        // ''Check at least 5 samples to determine if rig state is being detected wrong
                        if (rowIndex > 1 && (rowIndex < rowCount - 2))
                        {
                            double dMinus2 = DataService.checkNull(GetRowValue(objData, objData.Rows[rowIndex - 2], rigState.cnDEPTH), 0.0);
                            double dMinus1 = DataService.checkNull(GetRowValue(objData, objData.Rows[rowIndex - 1], rigState.cnDEPTH), 0.0);
                            double dCurr = DataService.checkNull(GetRowValue(objData, objData.Rows[rowIndex], rigState.cnDEPTH), 0.0);
                            double dPlus1 = DataService.checkNull(GetRowValue(objData, objData.Rows[rowIndex + 1], rigState.cnDEPTH), 0.0);
                            double dPlus2 = DataService.checkNull(GetRowValue(objData, objData.Rows[rowIndex + 2], rigState.cnDEPTH), 0.0);

                            if (dMinus2 == lnDepth && dMinus1 == lnDepth && dCurr == lnDepth && dPlus1 == lnDepth && dPlus2 == lnDepth)
                            {
                                if (!bitOnBottom)
                                {
                                    if ((lnRPM <= objRigState.RPMCutOff || Math.Floor(lnSTOR) == 0) && lnHkld <= objRigState.HookloadCutOff)
                                    {
                                        currentRigState = 2;
                                    }
                                    else
                                    {
                                        currentRigState = 14;
                                    }
                                }
                            }
                        }
                    }

                    // ''Detecting Auto Slide Drilling
                    if (currentRigState == 0 || currentRigState == 1)
                    {
                        if (objRigState.DetectAutoSlideDrilling)
                        {
                            bool isWithinDepthRange = objRigState.autoSlideSetupList == null || objRigState.autoSlideSetupList.Count <= 0;
                            if (objRigState.autoSlideSetupList != null)
                            {
                                foreach (AutoSlideSettings objItem in objRigState.autoSlideSetupList.Values)
                                {
                                    if (lnDepth >= objItem.FromDepth && lnDepth <= objItem.ToDepth)
                                    {
                                        isWithinDepthRange = true;
                                        break;
                                    }
                                }
                            }
                            if (isWithinDepthRange)
                            {
                                if (IsAutoSlide(objData, rowIndex, currentRigState))
                                {
                                    currentRigState = 19;
                                }
                            }
                        }
                    }

                    // ''*** ===================== Detect Air Drilling Rig States =================================== ****''
                    if (objRigState.DetectAirDrilling)
                    {
                        if ((bitDirection == rigState.enumDirection.Down || bitDirection == rigState.enumDirection.Stall) && lnCIRC < objRigState.CIRCCutOff && bitOnBottom)
                        {
                            if (lnMistFlow < objRigState.MistFlowCutOff)
                            {
                                // ''It's a dust air drilling ... *******>>>>>>>>>>>>>>>>
                                if (currentRigState == 19)
                                {
                                    currentRigState = 23;
                                }
                                else
                                {
                                    if (lnRPM > objRigState.RPMCutOff) currentRigState = 21;
                                    if (lnRPM <= objRigState.RPMCutOff) currentRigState = 22;
                                }
                            }
                            else
                            {
                                // ''It's a mist air drilling ... ******>>>>>>>>>>>>>>>>>
                                if (currentRigState == 19)
                                {
                                    currentRigState = 26;
                                }
                                else
                                {
                                    if (lnRPM > objRigState.RPMCutOff) currentRigState = 24;
                                    if (lnRPM <= objRigState.RPMCutOff) currentRigState = 25;
                                }
                            }
                        }
                    }

                    // ''Check the data flag
                    if (currentRigState == 15 && lnDataFlag == 0)
                    {
                        currentRigState = 16; // ''No Data
                    }

                    // ''##Special check when rig performs InSlips by remaining at the bottom
                    if (currentRigState == 1 || currentRigState == 0)
                    {
                        if ((lnRPM <= objRigState.RPMCutOff || Math.Floor(lnSTOR) <= objRigState.TorqueCutOff) && lnHkld <= objRigState.HookloadCutOff && lnCIRC < objRigState.CIRCCutOff)
                        {
                            currentRigState = 2;
                        }
                    }
                    if (currentRigState == 6 || currentRigState == 10)
                    {
                        if (Math.Abs(lnDepth - lnPrevDepth) == 0)
                        {
                            if ((lnRPM <= objRigState.RPMCutOff || Math.Floor(lnSTOR) <= objRigState.TorqueCutOff) && lnHkld <= objRigState.HookloadCutOff && lnCIRC < objRigState.CIRCCutOff)
                            {
                                currentRigState = 2;
                            }
                        }
                    }

                    // ''Record Last Values
                    lnPrevDepth = lnDepth;
                    lastBitDirection = bitDirection;

                    if (objData.Columns.Contains(rigState.cnRIGSTATE))
                    {
                        objRow[rigState.cnRIGSTATE] = currentRigState;
                    }

                    // --- [ORIGINAL VB LOGIC: Concatenated SQL UPDATE with dd-MMM-yyyy] ---
                    // objBulkExecutor.executeCommand("UPDATE " + dataTableName + " SET RIG_STATE=" + rigState.ToString + ",RIG_STATE_COLOR=" + getColor(rigState).ToString + " WHERE DATETIME='" + dtDateTime.ToString("dd-MMM-yyyy HH:mm:ss") + "'")

                    // --- [NEW C# / SQLITE LOGIC: Safe formatted SQLite update via BulkCommandExecutor] ---
                    int rigColor = GetRigStateColor(objRigState, currentRigState);
                    object? dataIndexVal = GetRowValue(objData, objRow, "DATA_INDEX");
                    string whereClause = dataIndexVal != null
                        ? $"[{indexColName}]={dataIndexVal}"
                        : (objData.Columns.Contains("DATETIME") ? $"DATETIME='{FormatDateForDb(dtDateTime)}'" : $"rowid={dataRowIndex + 1}");
                    string updateSql = $"UPDATE [{dataTableName}] SET RIG_STATE={currentRigState}, RIG_STATE_COLOR={rigColor} WHERE {whereClause};";
                    objBulkExecutor.ExecuteCommand(updateSql);

                    rowIndex++;
                }

                // ''Flush the buffer
                if (!objBulkExecutor.FlushBuffer())
                {
                    LastError = objBulkExecutor.LastError;
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        // =========================================================================================
        // Supporting Helper Methods
        // =========================================================================================
        public static string FormatDateForDb(DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        public static DateTime ParseDate(object? val)
        {
            if (val == null || val == DBNull.Value) return DateTime.MinValue;
            if (val is DateTime dt) return dt;
            if (val is double d && d > 1.0 && d < 100000.0)
            {
                try { return DateTime.FromOADate(d); } catch { }
            }
            string str = val.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(str)) return DateTime.MinValue;

            string[] formats =
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm:ss.fff",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss.fff",
                "dd/MM/yyyy HH:mm:ss",
                "dd/MM/yyyy HH:mm",
                "d/M/yyyy HH:mm:ss",
                "d/M/yyyy HH:mm",
                "dd-MM-yyyy HH:mm:ss",
                "dd-MM-yyyy HH:mm",
                "dd-MMM-yyyy HH:mm:ss",
                "d-MMM-yyyy HH:mm:ss",
                "dd-MMM-yyyy HH:mm",
                "d-MMM-yyyy HH:mm",
                "yyyy/MM/dd HH:mm:ss",
                "yyyy-MM-dd",
                "dd/MM/yyyy",
                "dd-MMM-yyyy"
            };

            if (DateTime.TryParseExact(str, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dExact))
                return dExact;

            if (DateTime.TryParse(str, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1)) return d1;
            if (DateTime.TryParse(str, CultureInfo.CurrentCulture, DateTimeStyles.None, out var d2)) return d2;
            if (DateTime.TryParse(str, out var d3)) return d3;

            if (double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedD) && parsedD > 1.0 && parsedD < 100000.0)
            {
                try { return DateTime.FromOADate(parsedD); } catch { }
            }

            return DateTime.MinValue;
        }

        public static object? GetRowValue(DataTable dt, DataRow row, string colName)
        {
            if (dt.Columns.Contains(colName) && row[colName] != DBNull.Value)
                return row[colName];
            return null;
        }

        public static int GetRigStateColor(rigState setup, int stateNumber)
        {
            if (setup?.rigStates != null && setup.rigStates.TryGetValue(stateNumber, out var item))
            {
                return (int)item.Color;
            }
            foreach (var defaultItem in DefaultRigStateItems)
            {
                if (defaultItem.Number == stateNumber)
                    return defaultItem.Color;
            }
            return -16711936; // Default Green
        }

        private static bool IsIsoFormat(string? str)
        {
            if (string.IsNullOrWhiteSpace(str) || str.Length < 10) return false;
            return char.IsDigit(str[0]) && char.IsDigit(str[1]) && char.IsDigit(str[2]) && char.IsDigit(str[3]) &&
                   str[4] == '-' && char.IsDigit(str[5]) && char.IsDigit(str[6]) && str[7] == '-';
        }

        public static (DateTime minDate, DateTime maxDate, int count) GetMinMaxDateFromTableWithCount(IDataServiceDIntel db, string tableName)
        {
            try
            {
                if (db == null || string.IsNullOrWhiteSpace(tableName))
                    return (DateTime.MinValue, DateTime.MinValue, 0);

                if (!db.TableExists(tableName))
                    return (DateTime.MinValue, DateTime.MinValue, 0);

                // 1. Try DATETIME column
                if (IsColumnAvailable(db, tableName, "DATETIME"))
                {
                    var countObj = db.GetValueFromDatabase($"SELECT COUNT(*) FROM [{tableName}] WHERE [DATETIME] IS NOT NULL AND [DATETIME] != '';");
                    int totalCount = Convert.ToInt32(countObj ?? 0);
                    if (totalCount == 0)
                    {
                        return (DateTime.MinValue, DateTime.MinValue, 0);
                    }
                    if (totalCount == 1)
                    {
                        var singleDtObj = db.GetValueFromDatabase($"SELECT [DATETIME] FROM [{tableName}] WHERE [DATETIME] IS NOT NULL AND [DATETIME] != '' LIMIT 1;");
                        DateTime singleDt = ParseDate(singleDtObj);
                        return (singleDt, singleDt, 1);
                    }

                    // Check sample to see if ISO formatted
                    var sampleObj = db.GetValueFromDatabase($"SELECT [DATETIME] FROM [{tableName}] WHERE [DATETIME] IS NOT NULL AND [DATETIME] != '' LIMIT 1;");
                    string sampleStr = Convert.ToString(sampleObj)?.Trim() ?? string.Empty;

                    if (IsIsoFormat(sampleStr))
                    {
                        var minObj = db.GetValueFromDatabase($"SELECT [DATETIME] FROM [{tableName}] WHERE [DATETIME] IS NOT NULL AND [DATETIME] != '' ORDER BY [DATETIME] ASC LIMIT 1;");
                        var maxObj = db.GetValueFromDatabase($"SELECT [DATETIME] FROM [{tableName}] WHERE [DATETIME] IS NOT NULL AND [DATETIME] != '' ORDER BY [DATETIME] DESC LIMIT 1;");
                        DateTime minVal = ParseDate(minObj);
                        DateTime maxVal = ParseDate(maxObj);
                        if (minVal != DateTime.MinValue && maxVal != DateTime.MinValue && minVal > maxVal)
                        {
                            (minVal, maxVal) = (maxVal, minVal);
                        }
                        return (minVal, maxVal, totalCount);
                    }
                    else
                    {
                        // Non-ISO format (e.g. dd-MMM-yyyy or dd/MM/yyyy)
                        var dtDistinct = db.GetTable($"SELECT DISTINCT [DATETIME] FROM [{tableName}] WHERE [DATETIME] IS NOT NULL AND [DATETIME] != '';");
                        if (dtDistinct != null && dtDistinct.Rows.Count > 0)
                        {
                            var dates = new List<DateTime>();
                            foreach (DataRow r in dtDistinct.Rows)
                            {
                                var d = ParseDate(r["DATETIME"]);
                                if (d != DateTime.MinValue) dates.Add(d);
                            }
                            if (dates.Count > 0)
                            {
                                dates.Sort();
                                return (dates[0], dates[^1], totalCount);
                            }
                        }
                    }
                }

                // 2. Try DATE and TIME columns
                if (IsColumnAvailable(db, tableName, "DATE") && IsColumnAvailable(db, tableName, "TIME"))
                {
                    var countObj = db.GetValueFromDatabase($"SELECT COUNT(*) FROM [{tableName}] WHERE [DATE] IS NOT NULL AND [DATE] != '';");
                    int totalCount = Convert.ToInt32(countObj ?? 0);
                    if (totalCount == 0) return (DateTime.MinValue, DateTime.MinValue, 0);

                    var dtDistinct = db.GetTable($"SELECT DISTINCT (DATE || ' ' || TIME) AS [DATETIME] FROM [{tableName}] WHERE [DATE] IS NOT NULL AND [DATE] != '';");
                    if (dtDistinct != null && dtDistinct.Rows.Count > 0)
                    {
                        var dates = new List<DateTime>();
                        foreach (DataRow r in dtDistinct.Rows)
                        {
                            var d = ParseDate(r["DATETIME"]);
                            if (d != DateTime.MinValue) dates.Add(d);
                        }
                        if (dates.Count > 0)
                        {
                            dates.Sort();
                            return (dates[0], dates[^1], totalCount);
                        }
                    }
                }

                // 3. Try INDEX_DOUBLE (OADate)
                if (IsColumnAvailable(db, tableName, "INDEX_DOUBLE"))
                {
                    var minIdx = db.GetValueFromDatabase($"SELECT MIN(INDEX_DOUBLE) FROM [{tableName}] WHERE INDEX_DOUBLE IS NOT NULL AND INDEX_DOUBLE > 0;");
                    var maxIdx = db.GetValueFromDatabase($"SELECT MAX(INDEX_DOUBLE) FROM [{tableName}] WHERE INDEX_DOUBLE IS NOT NULL AND INDEX_DOUBLE > 0;");
                    var countObj = db.GetValueFromDatabase($"SELECT COUNT(*) FROM [{tableName}] WHERE INDEX_DOUBLE IS NOT NULL AND INDEX_DOUBLE > 0;");
                    int totalCount = Convert.ToInt32(countObj ?? 0);

                    double minD = DataService.checkNull(minIdx, 0.0);
                    double maxD = DataService.checkNull(maxIdx, 0.0);
                    DateTime minDt = minD > 0 ? DateTime.FromOADate(minD) : DateTime.MinValue;
                    DateTime maxDt = maxD > 0 ? DateTime.FromOADate(maxD) : DateTime.MinValue;
                    if (minDt != DateTime.MinValue && maxDt != DateTime.MinValue && minDt > maxDt)
                    {
                        (minDt, maxDt) = (maxDt, minDt);
                    }
                    return (minDt, maxDt, totalCount);
                }

                // 4. Fallback: use rowid / DATA_INDEX
                string dtCol = GetDateTimeColumnSql(db, tableName);
                string idxCol = GetDataIndexColumnName(db, tableName);
                var dtAsc = db.GetTable($"SELECT {dtCol} FROM [{tableName}] ORDER BY [{idxCol}] ASC LIMIT 1;");
                var dtDesc = db.GetTable($"SELECT {dtCol} FROM [{tableName}] ORDER BY [{idxCol}] DESC LIMIT 1;");
                DateTime fDt = (dtAsc != null && dtAsc.Rows.Count > 0) ? ParseDate(dtAsc.Rows[0]["DATETIME"]) : DateTime.MinValue;
                DateTime lDt = (dtDesc != null && dtDesc.Rows.Count > 0) ? ParseDate(dtDesc.Rows[0]["DATETIME"]) : DateTime.MinValue;
                if (fDt != DateTime.MinValue && lDt != DateTime.MinValue && fDt > lDt)
                {
                    (fDt, lDt) = (lDt, fDt);
                }
                return (fDt, lDt, fDt != DateTime.MinValue ? 1 : 0);
            }
            catch
            {
                return (DateTime.MinValue, DateTime.MinValue, 0);
            }
        }

        public static (DateTime minDate, DateTime maxDate) GetMinMaxDateFromTable(IDataServiceDIntel db, string tableName)
        {
            var (min, max, _) = GetMinMaxDateFromTableWithCount(db, tableName);
            return (min, max);
        }

        public static DateTime GetMinDateFromTable(IDataServiceDIntel db, string tableName)
        {
            return GetMinMaxDateFromTable(db, tableName).minDate;
        }

        public static DateTime GetMaxDateFromTable(IDataServiceDIntel db, string tableName)
        {
            return GetMinMaxDateFromTable(db, tableName).maxDate;
        }

        public static double GetLastChannelValueByIndex(IDataServiceDIntel db, string dataTableName, string channelName, long beforeIndex)
        {
            try
            {
                string idxCol = GetDataIndexColumnName(db, dataTableName);
                string sql = $"SELECT [{channelName}] FROM [{dataTableName}] " +
                             $"WHERE [{idxCol}] < {beforeIndex} AND [{channelName}] IS NOT NULL " +
                             $"ORDER BY [{idxCol}] DESC LIMIT 1;";
                var val = db.GetValueFromDatabase(sql);
                return DataService.checkNull(val, 0.0);
            }
            catch
            {
                return 0.0;
            }
        }

        public static double GetLastChannelValue(IDataServiceDIntel db, string dataTableName, string channelName, DateTime beforeDate)
        {
            try
            {
                string idxCol = GetDataIndexColumnName(db, dataTableName);
                if (IsColumnAvailable(db, dataTableName, "DATETIME"))
                {
                    string sql = $"SELECT [{channelName}] FROM [{dataTableName}] " +
                                 $"WHERE [DATETIME] < '{FormatDateForDb(beforeDate)}' AND [{channelName}] IS NOT NULL " +
                                 $"ORDER BY [DATETIME] DESC LIMIT 1;";
                    var val = db.GetValueFromDatabase(sql);
                    return DataService.checkNull(val, 0.0);
                }
                else
                {
                    string sql = $"SELECT [{channelName}] FROM [{dataTableName}] " +
                                 $"WHERE [{channelName}] IS NOT NULL " +
                                 $"ORDER BY [{idxCol}] DESC LIMIT 1;";
                    var val = db.GetValueFromDatabase(sql);
                    return DataService.checkNull(val, 0.0);
                }
            }
            catch
            {
                return 0.0;
            }
        }

        public static wellSection? GetRigStateFromWellSection(rigState state, double holeDepth)
        {
            return null;
        }

        public static bool IsAutoSlide(DataTable data, int rowIndex, int currentRigState)
        {
            return false;
        }

        #endregion
    }
}

