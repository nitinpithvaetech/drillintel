using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Models.Util;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;

namespace DrillIntel.Data.Objects.DataObjects.Services
{
    /// <summary>
    /// Service responsible for persistence and management of DepthLog objects.
    /// Uses IDataServiceDIntel from DrillIntel.DataService for SQLite operations.
    /// </summary>
    public class DepthLogService
    {
        /// <summary>
        /// Inserts or updates a DepthLog record in the VMX_DEPTH_LOG table.
        /// If ObjectID or __dataTableName are blank, they will be auto-generated on the log instance.
        /// </summary>
        /// <param name="log">The DepthLog object containing log metadata.</param>
        /// <param name="dataService">The DataService instance connected to the SQLite project database.</param>
        /// <returns>True if successfully executed; False otherwise.</returns>
        //public bool AddDepthLog(DepthLog log, IDataServiceDIntel dataService)
        //{
        //    if (log == null) throw new ArgumentNullException(nameof(log));
        //    if (dataService == null) throw new ArgumentNullException(nameof(dataService));

        //    if (!dataService.IsConnectionOpen() && !dataService.CheckConnection())
        //    {
        //        throw new InvalidOperationException($"Database connection is not open: {dataService.LastError}");
        //    }

        //    // Auto-generate ObjectID if not already provided
        //    if (string.IsNullOrWhiteSpace(log.ObjectID))
        //    {
        //        log.ObjectID = Guid.NewGuid().ToString();
        //    }

        //    // Auto-generate DataTableName if blank (Format: depthLog{random8}#{random8})
        //    if (string.IsNullOrWhiteSpace(log.__dataTableName))
        //    {
        //        log.__dataTableName = GenerateDataTableName();
        //    }

        //    // Parse numeric depth limits safely
        //    double.TryParse(log.startIndex, NumberStyles.Any, CultureInfo.InvariantCulture, out double minDepth);
        //    double.TryParse(log.endIndex, NumberStyles.Any, CultureInfo.InvariantCulture, out double maxDepth);

        //    // Select SQL statement based on DuplicateAction
        //    string sql;
        //    if (log.DuplicateAction == enumDuplicateAction.SkipDuplicates)
        //    {
        //        sql = @"
        //            INSERT OR IGNORE INTO VMX_DEPTH_LOG (
        //                WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, RUN_NO, SERVICE_COMPANY,
        //                COMMENTS, DATA_TABLE_NAME, DESCRIPTION, MIN_DEPTH, MAX_DEPTH,
        //                WMLS_URL, WMLP_URL, LAST_DATA_RECEIVED_ON, CREATED_DATE, MODIFIED_DATE,
        //                EDR_PROVIDER, PI_WELL_ID, PI_WELLBORE_ID, PI_LOG_ID,
        //                DUPLICATE_ACTION, PRIMARY_LOG
        //            ) VALUES (
        //                @WellID, @WellboreID, @LogID, @LogName, @RunNo, @ServiceCompany,
        //                @Comments, @DataTableName, @Description, @MinDepth, @MaxDepth,
        //                @WmlsUrl, @WmlpUrl, @LastReceived, @CreatedDate, @ModifiedDate,
        //                @EdrProvider, @PiWellId, @PiWellboreId, @PiLogId,
        //                @DuplicateAction, @PrimaryLog
        //            );";
        //    }
        //    else
        //    {
        //        // OverwriteDuplicates (default) or MergeDuplicates using SQLite UPSERT
        //        sql = @"
        //            INSERT INTO VMX_DEPTH_LOG (
        //                WELL_ID, WELLBORE_ID, LOG_ID, LOG_NAME, RUN_NO, SERVICE_COMPANY,
        //                COMMENTS, DATA_TABLE_NAME, DESCRIPTION, MIN_DEPTH, MAX_DEPTH,
        //                WMLS_URL, WMLP_URL, LAST_DATA_RECEIVED_ON, CREATED_DATE, MODIFIED_DATE,
        //                EDR_PROVIDER, PI_WELL_ID, PI_WELLBORE_ID, PI_LOG_ID,
        //                DUPLICATE_ACTION, PRIMARY_LOG
        //            ) VALUES (
        //                @WellID, @WellboreID, @LogID, @LogName, @RunNo, @ServiceCompany,
        //                @Comments, @DataTableName, @Description, @MinDepth, @MaxDepth,
        //                @WmlsUrl, @WmlpUrl, @LastReceived, @CreatedDate, @ModifiedDate,
        //                @EdrProvider, @PiWellId, @PiWellboreId, @PiLogId,
        //                @DuplicateAction, @PrimaryLog
        //            )
        //            ON CONFLICT(LOG_ID, WELLBORE_ID, WELL_ID) DO UPDATE SET
        //                LOG_NAME = excluded.LOG_NAME,
        //                RUN_NO = excluded.RUN_NO,
        //                SERVICE_COMPANY = excluded.SERVICE_COMPANY,
        //                COMMENTS = excluded.COMMENTS,
        //                DATA_TABLE_NAME = excluded.DATA_TABLE_NAME,
        //                DESCRIPTION = excluded.DESCRIPTION,
        //                MIN_DEPTH = excluded.MIN_DEPTH,
        //                MAX_DEPTH = excluded.MAX_DEPTH,
        //                WMLS_URL = excluded.WMLS_URL,
        //                WMLP_URL = excluded.WMLP_URL,
        //                LAST_DATA_RECEIVED_ON = excluded.LAST_DATA_RECEIVED_ON,
        //                MODIFIED_DATE = excluded.MODIFIED_DATE,
        //                EDR_PROVIDER = excluded.EDR_PROVIDER,
        //                PI_WELL_ID = excluded.PI_WELL_ID,
        //                PI_WELLBORE_ID = excluded.PI_WELLBORE_ID,
        //                PI_LOG_ID = excluded.PI_LOG_ID,
        //                DUPLICATE_ACTION = excluded.DUPLICATE_ACTION,
        //                PRIMARY_LOG = excluded.PRIMARY_LOG;";
        //    }

        //    var parameters = new Dictionary<string, object?>
        //    {
        //        ["@WellID"] = log.WellID ?? string.Empty,
        //        ["@WellboreID"] = log.WellboreID ?? string.Empty,
        //        ["@LogID"] = log.ObjectID,
        //        ["@LogName"] = log.nameLog ?? string.Empty,
        //        ["@RunNo"] = log.runNumber ?? string.Empty,
        //        ["@ServiceCompany"] = log.serviceCompany ?? string.Empty,
        //        ["@Comments"] = log.comments ?? string.Empty,
        //        ["@DataTableName"] = log.__dataTableName ?? string.Empty,
        //        ["@Description"] = log.description ?? string.Empty,
        //        ["@MinDepth"] = minDepth,
        //        ["@MaxDepth"] = maxDepth,
        //        ["@WmlsUrl"] = log.wmlsurl ?? string.Empty,
        //        ["@WmlpUrl"] = log.wmlpurl ?? string.Empty,
        //        ["@LastReceived"] = log.lastDataReceived != default ? log.lastDataReceived.ToString("o") : null,
        //        ["@CreatedDate"] = string.IsNullOrWhiteSpace(log.creationDate) ? DateTime.UtcNow.ToString("o") : log.creationDate,
        //        ["@ModifiedDate"] = DateTime.UtcNow.ToString("o"),
        //        ["@EdrProvider"] = log.EDRProvider ?? string.Empty,
        //        ["@PiWellId"] = log.PiWellID ?? string.Empty,
        //        ["@PiWellboreId"] = log.PiWellboreID ?? string.Empty,
        //        ["@PiLogId"] = log.PiLogID ?? string.Empty,
        //        ["@DuplicateAction"] = (int)log.DuplicateAction,
        //        ["@PrimaryLog"] = log.PrimaryLog ? 1 : 0
        //    };

        //    return dataService.ExecuteNonQuery(sql, parameters);
        //}

        public static bool AddDepthLog(IDataServiceDIntel objDataService, DepthLog objDepthLog, ref string LastError)
        {
            try
            {
                if (objDataService == null)
                {
                    LastError = "Data service is not initialized.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(objDepthLog.ObjectID))
                {
                    objDepthLog.ObjectID = Guid.NewGuid().ToString();
                }

                if (string.IsNullOrWhiteSpace(objDepthLog.WellID))
                {
                    var wellIdObj = objDataService.GetValue("SELECT WELL_ID FROM VMX_WELL LIMIT 1;");
                    if (wellIdObj != null)
                    {
                        objDepthLog.WellID = Convert.ToString(wellIdObj) ?? string.Empty;
                    }
                }

                if (string.IsNullOrWhiteSpace(objDepthLog.WellboreID) && !string.IsNullOrWhiteSpace(objDepthLog.WellID))
                {
                    var wbIdObj = objDataService.GetValue("SELECT WELLBORE_ID FROM VMX_WELLBORE WHERE WELL_ID='" 
                        + objDepthLog.WellID.Replace("'", "''") + "' LIMIT 1;");
                    if (wbIdObj != null)
                    {
                        objDepthLog.WellboreID = Convert.ToString(wbIdObj) ?? string.Empty;
                    }
                }

                // Auto-generate DataTableName if blank
                string dataTableName = string.IsNullOrWhiteSpace(objDepthLog.__dataTableName)
                    ? ObjectIDFactory.generateDepthLogTableName()
                    : objDepthLog.__dataTableName;
                objDepthLog.__dataTableName = dataTableName;

                // (1) Create a data table if LogCurves are provided and table doesn't already exist
                if (objDepthLog.LogCurves != null && objDepthLog.LogCurves.Count > 0)
                {
                    bool tableExists = objDataService.IsRecordExist("SELECT name FROM sqlite_master WHERE type='table' AND name='" + dataTableName.Replace("'", "''") + "';");
                    if (!tableExists)
                    {
                        string createTableSQL = "CREATE TABLE [" + dataTableName + "] (";

                        string strFields = "DATA_INDEX DECIMAL(8) ";

                        foreach (LogChannel objCurve in objDepthLog.LogCurves.Values)
                        {
                            if (objCurve.typeLogData.Trim() != "")
                            {
                                switch (objCurve.typeLogData.ToUpper())
                                {
                                    case "DATE TIME":
                                    case "DATETIME":
                                        strFields += ",[" + objCurve.mnemonic + "] DATETIME ";
                                        break;

                                    case "DOUBLE":
                                    case "LONG":
                                    case "FLOAT":
                                    case "INT":
                                    case "SHORT":
                                        strFields += ",[" + objCurve.mnemonic + "] DECIMAL(11,2) ";

                                        if (objCurve.mnemonic == "DEPTH")
                                        {
                                            strFields += " NOT NULL ";
                                        }
                                        break;

                                    case "STRING":
                                    case "STRING40":
                                    case "STRING16":
                                        strFields += ",[" + objCurve.mnemonic + "] VARCHAR(1000) ";
                                        break;

                                    default:
                                        strFields += ",[" + objCurve.mnemonic + "] VARCHAR(500) ";
                                        break;
                                }
                            }
                            else
                            {
                                if (objCurve.mnemonic == "DEPTH")
                                {
                                    strFields += ",[" + objCurve.mnemonic + "] DECIMAL(11,2) NOT NULL";
                                }
                                else
                                {
                                    strFields += ",[" + objCurve.mnemonic + "] DECIMAL(11,2) ";
                                }
                                objCurve.typeLogData = "Double";
                            }
                        }

                        var newCurves = new Dictionary<string, LogChannel>();

                        // ##### Add additional fields to store Axis Data ##############################
                        foreach (LogChannel objCurve in objDepthLog.LogCurves.Values)
                        {
                            if (objCurve.AxisDataCount > 1)
                            {
                                for (int i = 1; i <= objCurve.AxisDataCount; i++)
                                {
                                    strFields += ",[" + objCurve.mnemonic + "_" + i.ToString() + "] DECIMAL(11,2) ";
                                }

                                // Add the curve too ...
                                for (int i = 1; i <= objCurve.AxisDataCount; i++)
                                {
                                    string axisMnemonic = objCurve.mnemonic + "_" + i.ToString();

                                    LogChannel newCurve = objCurve.GetCopy();
                                    newCurve.mnemonic = axisMnemonic;
                                    newCurve.curveDescription = newCurve.curveDescription + "_" + i.ToString();
                                    newCurve.AxisDataCount = 0;
                                    newCurve.witsmlMnemonic = "";

                                    newCurves.Add(axisMnemonic, newCurve);
                                }
                            }
                        }

                        foreach (LogChannel objCurve in newCurves.Values)
                        {
                            if (!objDepthLog.LogCurves.ContainsKey(objCurve.mnemonic))
                            {
                                objDepthLog.LogCurves.Add(objCurve.mnemonic, objCurve);
                            }
                        }
                        // #############################################################################

                        createTableSQL += strFields + ")";

                        if (!objDataService.ExecuteNonQuery(createTableSQL))
                        {
                            LastError = objDataService.LastError;
                            return false;
                        }

                        string idxSql = "CREATE UNIQUE INDEX IF NOT EXISTS [" + dataTableName + "_PK] ON [" + dataTableName + "](DEPTH);";
                        if (!objDataService.ExecuteNonQuery(idxSql))
                        {
                            LastError = objDataService.LastError;
                            return false;
                        }

                        idxSql = "CREATE INDEX IF NOT EXISTS [" + dataTableName + "_DATAINDEX] ON [" + dataTableName + "](DATA_INDEX);";
                        if (!objDataService.ExecuteNonQuery(idxSql))
                        {
                            LastError = objDataService.LastError;
                            return false;
                        }
                    }
                }
                // =========================================================================================

                // (2) Update Meta Data ====================================================================
                string strSQL = "INSERT OR REPLACE INTO VMX_DEPTH_LOG (WELL_ID,WELLBORE_ID,LOG_ID,LOG_NAME,RUN_NO,SERVICE_COMPANY,COMMENTS,DATA_TABLE_NAME,DESCRIPTION,WMLS_URL,WMLP_URL,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE,EDR_PROVIDER,PI_WELL_ID,PI_WELLBORE_ID,PI_LOG_ID,LINK_TO_PARENT,LINK_WELL_ID,LINK_WELLBORE_ID,LINK_LOG_ID,DUPLICATE_ACTION,PRIMARY_LOG) VALUES(";
                strSQL += "'" + objDepthLog.WellID + "',";
                strSQL += "'" + objDepthLog.WellboreID + "',";
                strSQL += "'" + objDepthLog.ObjectID + "',";
                strSQL += "'" + objDepthLog.nameLog.Replace("'", "''") + "',";
                strSQL += "'" + objDepthLog.runNumber.Replace("'", "''") + "',";
                strSQL += "'" + objDepthLog.serviceCompany.Replace("'", "''") + "',";
                strSQL += "'" + objDepthLog.comments.Replace("'", "''") + "',";
                strSQL += "'" + dataTableName + "',";
                strSQL += "'" + objDepthLog.description.Replace("'", "''") + "',";
                strSQL += "'" + objDepthLog.wmlsurl + "',";
                strSQL += "'" + objDepthLog.wmlpurl + "',";
                strSQL += "'" + objDataService.UserName + "',";
                strSQL += "'" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "',";
                strSQL += "'" + objDataService.UserName + "',";
                strSQL += "'" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "',";
                strSQL += "'" + objDepthLog.EDRProvider.Replace("'", "''") + "',";
                strSQL += "'" + objDepthLog.PiWellID.Replace("'", "''") + "',";
                strSQL += "'" + objDepthLog.PiWellboreID.Replace("'", "''") + "',";
                strSQL += "'" + objDepthLog.PiLogID.Replace("'", "''") + "',";
                strSQL += "" + (objDepthLog.LinkToParent ? 1 : 0).ToString() + ",";
                strSQL += "'" + objDepthLog.LinkWellID.Replace("'", "''") + "',";
                strSQL += "'" + objDepthLog.LinkWellboreID.Replace("'", "''") + "',";
                strSQL += "'" + objDepthLog.LinkLogID.Replace("'", "''") + "',";

                int lnDuplicateAction = (int)objDepthLog.DuplicateAction;

                strSQL += "" + lnDuplicateAction.ToString() + ",";
                strSQL += "" + (objDepthLog.PrimaryLog ? 1 : 0).ToString() + ")";

                if (objDataService.ExecuteNonQuery(strSQL))
                {
                    if (objDepthLog.LogCurves != null)
                    {
                        foreach (LogChannel objChannel in objDepthLog.LogCurves.Values)
                        {
                        strSQL = "INSERT OR REPLACE INTO VMX_DEPTH_LOG_COLUMNS (WELL_ID,WELLBORE_ID,LOG_ID,MNEMONIC,CHANNEL_NAME,DATA_TYPE,UNIT,UNIT_ID,VUMAX_UNIT_ID,VALUE_TYPE,VALUE_QUERY,WITSML_MNEMONIC,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE,WRITE_BACK,PI_MNEMONIC,AXIS_DATA_COUNT,COLUMN_ORDER,PARENT_MNEMONIC) VALUES(";
                        strSQL += "'" + objDepthLog.WellID + "',";
                        strSQL += "'" + objDepthLog.WellboreID + "',";
                        strSQL += "'" + objDepthLog.ObjectID + "',";
                        strSQL += "'" + objChannel.mnemonic.Replace("'", "''") + "',";
                        strSQL += "'" + objChannel.curveDescription.Replace("'", "''") + "',";
                        strSQL += "'" + objChannel.typeLogData.Replace("'", "''") + "',";
                        strSQL += "'" + objChannel.unit + "',";
                        strSQL += "'" + objChannel.UnitID + "',";
                        strSQL += "'" + objChannel.VuMaxUnitID + "',";
                        strSQL += "" + objChannel.valueType.ToString() + ",";
                        strSQL += "'" + objChannel.valueQuery + "',";
                        strSQL += "'" + objChannel.witsmlMnemonic.Replace("'", "''") + "',";
                        strSQL += "'" + objDataService.UserName + "',";
                        strSQL += "'" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "',";
                        strSQL += "'" + objDataService.UserName + "',";
                        strSQL += "'" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "',";
                        strSQL += "" + (objChannel.WriteBack ? 1 : 0).ToString() + ",";
                        strSQL += "'" + objChannel.PiMnemonic.Replace("'", "''") + "',";
                        strSQL += "" + objChannel.AxisDataCount.ToString() + ",";
                        strSQL += "" + objChannel.ColumnOrder.ToString() + ",";
                        strSQL += "'" + objChannel.parentMnemonic.Replace("'", "''") + "')";

                        objDataService.ExecuteNonQuery(strSQL);
                    }
                }

                    //foreach (QCRule objRule in objDepthLog.QCRules.Values)
                    //{
                    //    strSQL = "INSERT INTO VMX_DEPTH_LOG_QC_RULES (WELL_ID,WELLBORE_ID,LOG_ID,RULE_ID,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE) VALUES(";
                    //    strSQL += "'" + objDepthLog.WellID + "',";
                    //    strSQL += "'" + objDepthLog.WellboreID + "',";
                    //    strSQL += "'" + objDepthLog.ObjectID + "',";
                    //    strSQL += "'" + objRule.RuleID + "',";
                    //    strSQL += "'" + objDataService.UserName + "',";
                    //    strSQL += "'" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "',";
                    //    strSQL += "'" + objDataService.UserName + "',";
                    //    strSQL += "'" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "')";

                    //    objDataService.executeNonQuery(strSQL);
                    //}

                    //foreach (LogVariable objVar in objDepthLog.Variables.Values)
                    //{
                    //    strSQL = "INSERT INTO VMX_DEPTH_LOG_VAR (WELL_ID,WELLBORE_ID,LOG_ID,VAR_ID,VAR_NAME,VAR_VALUE) VALUES(";
                    //    strSQL += "'" + objDepthLog.WellID + "',";
                    //    strSQL += "'" + objDepthLog.WellboreID + "',";
                    //    strSQL += "'" + objDepthLog.ObjectID + "',";
                    //    strSQL += "'" + objVar.VarID + "',";
                    //    strSQL += "'" + objVar.VarName.Replace("'", "''") + "',";
                    //    strSQL += "" + objVar.VarValue.ToString() + ")";

                    //    objDataService.executeNonQuery(strSQL);
                    //}

                    // ### Image Log Data Sets ##########################################################
                    //foreach (ImageLogDataSet objImageLogDataSet in objDepthLog.ImageLogDataSets.Values)
                    //{
                    //    strSQL = "INSERT INTO VMX_IMAGE_LOG_DATASET (WELL_ID,WELLBORE_ID,LOG_ID,DATASET_ID,DATASET_NAME,START_COLOR,MIDDLE_COLOR,END_COLOR) VALUES(";
                    //    strSQL += "'" + objDepthLog.WellID + "',";
                    //    strSQL += "'" + objDepthLog.WellboreID + "',";
                    //    strSQL += "'" + objDepthLog.ObjectID + "',";
                    //    strSQL += "'" + objImageLogDataSet.DataSetID + "',";
                    //    strSQL += "'" + objImageLogDataSet.DataSetName.Replace("'", "''") + "',";
                    //    strSQL += "" + objImageLogDataSet.StartColor.ToArgb().ToString() + ",";
                    //    strSQL += "" + objImageLogDataSet.MiddleColor.ToArgb().ToString() + ",";
                    //    strSQL += "" + objImageLogDataSet.EndColor.ToArgb().ToString() + ")";

                    //    objDataService.executeNonQuery(strSQL);

                    //    foreach (ImageLogChannels objImageLogChannel in objImageLogDataSet.channels.Values)
                    //    {
                    //        strSQL = "INSERT INTO VMX_IMAGE_LOG_CHANNELS (WELL_ID,WELLBORE_ID,LOG_ID,DATASET_ID,MNEMONIC,DISPLAY_ORDER) VALUES(";
                    //        strSQL += "'" + objDepthLog.WellID + "',";
                    //        strSQL += "'" + objDepthLog.WellboreID + "',";
                    //        strSQL += "'" + objDepthLog.ObjectID + "',";
                    //        strSQL += "'" + objImageLogDataSet.DataSetID + "',";
                    //        strSQL += "'" + objImageLogChannel.Mnemonic.Replace("'", "''") + "',";
                    //        strSQL += "" + objImageLogChannel.DisplayOrder.ToString() + ")";

                    //        objDataService.executeNonQuery(strSQL);
                    //    }
                    //}
                    // #####################################################################################

                    if (double.TryParse(objDepthLog.startIndex, NumberStyles.Any, CultureInfo.InvariantCulture, out double minD) &&
                        double.TryParse(objDepthLog.endIndex, NumberStyles.Any, CultureInfo.InvariantCulture, out double maxD))
                    {
                        string updExtentsSql = "UPDATE VMX_DEPTH_LOG SET MIN_DEPTH = " + minD.ToString(CultureInfo.InvariantCulture) +
                                               ", MAX_DEPTH = " + maxD.ToString(CultureInfo.InvariantCulture) +
                                               " WHERE LOG_ID = '" + objDepthLog.ObjectID.Replace("'", "''") + "';";
                        objDataService.ExecuteNonQuery(updExtentsSql);
                    }

                    return true;
                }
                else
                {
                    LastError = objDataService.LastError;
                    return false;
                }
                // =========================================================================================
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }


        /// <summary>
        /// Static convenience helper to insert or update a DepthLog record without instantiating DepthLogService.
        /// </summary>
        public static bool AddLog(DepthLog log, IDataServiceDIntel dataService)
        {
            string lastError = "";
            return AddDepthLog(dataService, log, ref lastError);
        }

        /// <summary>
        /// Generates a random data table name formatted as: depthLog{8digits}#{8digits}
        /// Example: depthLog12449827#24541087
        /// </summary>
        public static string GenerateDataTableName()
        {
            int num1 = Random.Shared.Next(10000000, 100000000);
            int num2 = Random.Shared.Next(10000000, 100000000);
            return $"depthLog{num1}#{num2}";
        }

        public static bool IsDepthLogExist(IDataServiceDIntel objDataService, string WellID, string WellboreID, string LogID)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(WellID) || string.IsNullOrWhiteSpace(WellboreID) || string.IsNullOrWhiteSpace(LogID))
                    return false;

                return objDataService.IsRecordExist(
                    "SELECT LOG_ID FROM VMX_DEPTH_LOG " +
                    "WHERE WELL_ID='" + WellID.Replace("'", "''") + "' " +
                    "AND WELLBORE_ID='" + WellboreID.Replace("'", "''") + "' " +
                    "AND LOG_ID='" + LogID.Replace("'", "''") + "'"
                );
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static List<DepthLog> LoadDepthLogs(IDataServiceDIntel objDataService, string wellID, ref string lastError)
        {
            var list = new List<DepthLog>();
            try
            {
                if (objDataService == null)
                {
                    lastError = "Data service is not initialized.";
                    return list;
                }

                string sql = string.IsNullOrWhiteSpace(wellID)
                    ? "SELECT * FROM VMX_DEPTH_LOG ORDER BY CREATED_DATE DESC;"
                    : "SELECT * FROM VMX_DEPTH_LOG WHERE WELL_ID='" + wellID.Replace("'", "''") + "' ORDER BY CREATED_DATE DESC;";

                DataTable dt = objDataService.GetTable(sql);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(MapRowToDepthLog(row));
                    }
                    dt.Dispose();
                }
                return list;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ":" + ex.StackTrace;
                return list;
            }
        }

        public static DepthLog? LoadObject(IDataServiceDIntel objDataService, string wellID, string wellboreID, string logID, ref string lastError)
        {
            try
            {
                if (objDataService == null)
                {
                    lastError = "Data service is not initialized.";
                    return null;
                }

                DataTable dt = objDataService.GetTable("SELECT * FROM VMX_DEPTH_LOG WHERE WELL_ID='" 
                    + wellID.Replace("'", "''") + "' AND WELLBORE_ID='" 
                    + wellboreID.Replace("'", "''") + "' AND LOG_ID='" 
                    + logID.Replace("'", "''") + "';");

                if (dt != null && dt.Rows.Count > 0)
                {
                    var log = MapRowToDepthLog(dt.Rows[0]);
                    dt.Dispose();
                    return log;
                }
                return null;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ":" + ex.StackTrace;
                return null;
            }
        }

        public static DepthLog? LoadObject(IDataServiceDIntel objDataService, string logID, ref string lastError)
        {
            try
            {
                if (objDataService == null)
                {
                    lastError = "Data service is not initialized.";
                    return null;
                }

                DataTable dt = objDataService.GetTable("SELECT * FROM VMX_DEPTH_LOG WHERE LOG_ID='" 
                    + logID.Replace("'", "''") + "' LIMIT 1;");

                if (dt == null || dt.Rows.Count == 0)
                {
                    dt = objDataService.GetTable("SELECT * FROM VMX_DEPTH_LOG WHERE LOG_NAME='" 
                        + logID.Replace("'", "''") + "' OR DATA_TABLE_NAME='" 
                        + logID.Replace("'", "''") + "' LIMIT 1;");
                }

                if (dt != null && dt.Rows.Count > 0)
                {
                    var log = MapRowToDepthLog(dt.Rows[0]);
                    dt.Dispose();
                    LoadLogCurves(objDataService, log);
                    return log;
                }
                return null;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ":" + ex.StackTrace;
                return null;
            }
        }

        public static void LoadLogCurves(IDataServiceDIntel objDataService, DepthLog log)
        {
            if (objDataService == null || log == null) return;
            try
            {
                bool customTableHasChannels = false;

                // 1. If DATA_TABLE_NAME stores channel rows directly (contains MNEMONIC column)
                if (!string.IsNullOrWhiteSpace(log.__dataTableName))
                {
                    try
                    {
                        bool tableExists = objDataService.IsRecordExist("SELECT name FROM sqlite_master WHERE type='table' AND name='" 
                            + log.__dataTableName.Replace("'", "''") + "';");

                        if (tableExists)
                        {
                            DataTable pragmaCols = objDataService.GetTable("PRAGMA table_info('" + log.__dataTableName.Replace("'", "''") + "');");
                            bool hasMnemCol = false;
                            if (pragmaCols != null)
                            {
                                foreach (DataRow r in pragmaCols.Rows)
                                {
                                    string colName = Convert.ToString(r["name"]) ?? "";
                                    if (colName.Equals("MNEMONIC", StringComparison.OrdinalIgnoreCase))
                                    {
                                        hasMnemCol = true;
                                        break;
                                    }
                                }
                                pragmaCols.Dispose();
                            }

                            if (hasMnemCol)
                            {
                                customTableHasChannels = true;
                                DataTable dtDirect = objDataService.GetTable("SELECT * FROM [" + log.__dataTableName.Replace("'", "''") + "];");
                                if (dtDirect != null)
                                {
                                    int order = 1;
                                    foreach (DataRow r in dtDirect.Rows)
                                    {
                                        var ch = new LogChannel();
                                        foreach (DataColumn dc in dtDirect.Columns)
                                        {
                                            object v = r[dc];
                                            if (v == null || v == DBNull.Value) continue;
                                            string col = dc.ColumnName;

                                            if (col.Equals("Upload", StringComparison.OrdinalIgnoreCase) || col.Equals("WRITE_BACK", StringComparison.OrdinalIgnoreCase))
                                                ch.Upload = Convert.ToInt64(v) != 0 || (v is bool b && b);
                                            else if (col.Equals("Mnemonic", StringComparison.OrdinalIgnoreCase) || col.Equals("MNEMONIC", StringComparison.OrdinalIgnoreCase))
                                                ch.Mnemonic = Convert.ToString(v) ?? "";
                                            else if (col.Equals("Unit", StringComparison.OrdinalIgnoreCase) || col.Equals("UNIT", StringComparison.OrdinalIgnoreCase))
                                                ch.Unit = Convert.ToString(v) ?? "";
                                            else if (col.Equals("VuMax Unit ID", StringComparison.OrdinalIgnoreCase) || col.Equals("VUMAX_UNIT_ID", StringComparison.OrdinalIgnoreCase) || col.Equals("UnitID", StringComparison.OrdinalIgnoreCase))
                                                ch.VuMaxUnitId = Convert.ToString(v) ?? "";
                                            else if (col.Equals("Description", StringComparison.OrdinalIgnoreCase) || col.Equals("CHANNEL_NAME", StringComparison.OrdinalIgnoreCase) || col.Equals("curveDescription", StringComparison.OrdinalIgnoreCase))
                                                ch.Description = Convert.ToString(v) ?? "";
                                            else if (col.Equals("Upload Mnemonic", StringComparison.OrdinalIgnoreCase) || col.Equals("WITSML_MNEMONIC", StringComparison.OrdinalIgnoreCase) || col.Equals("PI_MNEMONIC", StringComparison.OrdinalIgnoreCase))
                                                ch.UploadMnemonic = Convert.ToString(v) ?? "";
                                            else if (col.Equals("Value Type", StringComparison.OrdinalIgnoreCase) || col.Equals("VALUE_TYPE", StringComparison.OrdinalIgnoreCase))
                                                ch.ValueType = Convert.ToString(v) ?? "0";
                                            else if (col.Equals("Expression", StringComparison.OrdinalIgnoreCase) || col.Equals("VALUE_QUERY", StringComparison.OrdinalIgnoreCase))
                                                ch.Expression = Convert.ToString(v) ?? "";
                                            else if (col.Equals("Do Not Interpol", StringComparison.OrdinalIgnoreCase) || col.Equals("NO_INTERPOLATE", StringComparison.OrdinalIgnoreCase))
                                                ch.DoNotInterpol = Convert.ToInt64(v) != 0 || (v is bool b && b);
                                        }

                                        ch.OriginalMnemonic = ch.Mnemonic;
                                        ch.ColumnOrder = order++;
                                        if (!string.IsNullOrWhiteSpace(ch.Mnemonic) && !log.LogCurves.ContainsKey(ch.Mnemonic))
                                        {
                                            log.LogCurves.Add(ch.Mnemonic, ch);
                                        }
                                    }
                                    dtDirect.Dispose();
                                }
                            }
                        }
                    }
                    catch { }
                }

                // 2. Query VMX_DEPTH_LOG_COLUMNS
                if (!customTableHasChannels)
                {
                    string colWhere = "LOG_ID='" + log.ObjectID.Replace("'", "''") + "'";
                    if (!string.IsNullOrWhiteSpace(log.WellID))
                        colWhere += " AND WELL_ID='" + log.WellID.Replace("'", "''") + "'";
                    if (!string.IsNullOrWhiteSpace(log.WellboreID))
                        colWhere += " AND WELLBORE_ID='" + log.WellboreID.Replace("'", "''") + "'";

                    bool hasColsTable = objDataService.IsRecordExist("SELECT name FROM sqlite_master WHERE type='table' AND name='VMX_DEPTH_LOG_COLUMNS';");
                    if (hasColsTable)
                    {
                        DataTable dtCols = objDataService.GetTable("SELECT * FROM VMX_DEPTH_LOG_COLUMNS WHERE " + colWhere + " ORDER BY COLUMN_ORDER ASC;");

                        if (dtCols != null)
                        {
                            foreach (DataRow r in dtCols.Rows)
                            {
                                var channel = new LogChannel
                                {
                                    mnemonic = DataService.checkNull(r["MNEMONIC"], ""),
                                    curveDescription = r.Table.Columns.Contains("CHANNEL_NAME") ? DataService.checkNull(r["CHANNEL_NAME"], "") : "",
                                    typeLogData = r.Table.Columns.Contains("DATA_TYPE") ? DataService.checkNull(r["DATA_TYPE"], "") : "Double",
                                    unit = r.Table.Columns.Contains("UNIT") ? DataService.checkNull(r["UNIT"], "") : "",
                                    UnitID = r.Table.Columns.Contains("UNIT_ID") ? DataService.checkNull(r["UNIT_ID"], "") : "",
                                    VuMaxUnitID = r.Table.Columns.Contains("VUMAX_UNIT_ID") ? DataService.checkNull(r["VUMAX_UNIT_ID"], "") : "",
                                    valueType = r.Table.Columns.Contains("VALUE_TYPE") ? Convert.ToInt32(DataService.checkNull(r["VALUE_TYPE"], 0)) : 0,
                                    valueQuery = r.Table.Columns.Contains("VALUE_QUERY") ? DataService.checkNull(r["VALUE_QUERY"], "") : "",
                                    witsmlMnemonic = r.Table.Columns.Contains("WITSML_MNEMONIC") ? DataService.checkNull(r["WITSML_MNEMONIC"], "") : "",
                                    WriteBack = !r.Table.Columns.Contains("WRITE_BACK") || Convert.ToInt32(DataService.checkNull(r["WRITE_BACK"], 0)) == 1,
                                    DoNotInterpolate = r.Table.Columns.Contains("NO_INTERPOLATE") ? Convert.ToInt32(DataService.checkNull(r["NO_INTERPOLATE"], 0)) : 0,
                                    ColumnOrder = r.Table.Columns.Contains("COLUMN_ORDER") ? Convert.ToInt32(DataService.checkNull(r["COLUMN_ORDER"], 0)) : 0
                                };
                                channel.OriginalMnemonic = channel.mnemonic;
                                if (!string.IsNullOrWhiteSpace(channel.mnemonic) && !log.LogCurves.ContainsKey(channel.mnemonic))
                                {
                                    log.LogCurves.Add(channel.mnemonic, channel);
                                }
                            }
                            dtCols.Dispose();
                        }
                    }

                    // 3. Supplement any missing columns from physical depth-series table
                    if (!string.IsNullOrWhiteSpace(log.__dataTableName))
                    {
                        try
                        {
                            DataTable pragmaCols = objDataService.GetTable("PRAGMA table_info('" + log.__dataTableName.Replace("'", "''") + "');");
                            if (pragmaCols != null)
                            {
                                int order = log.LogCurves.Count + 1;
                                foreach (DataRow r in pragmaCols.Rows)
                                {
                                    string col = Convert.ToString(r["name"]) ?? "";
                                    if (string.IsNullOrWhiteSpace(col) || col.Equals("DATA_INDEX", StringComparison.OrdinalIgnoreCase)) continue;

                                    if (!log.LogCurves.ContainsKey(col))
                                    {
                                        bool isDepth = col.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) ||
                                                       col.Equals("DEPT", StringComparison.OrdinalIgnoreCase) ||
                                                       col.Equals("MD", StringComparison.OrdinalIgnoreCase);

                                        var ch = new LogChannel
                                        {
                                            mnemonic = col,
                                            curveDescription = col,
                                            typeLogData = "Double",
                                            unit = isDepth ? "m" : "",
                                            UnitID = isDepth ? "m" : "",
                                            VuMaxUnitID = isDepth ? "m" : "",
                                            witsmlMnemonic = col,
                                            WriteBack = true,
                                            processChannel = true,
                                            ColumnOrder = order++,
                                            OriginalMnemonic = col
                                        };
                                        log.LogCurves.Add(col, ch);
                                    }
                                }
                                pragmaCols.Dispose();
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public static bool SaveDepthLog(IDataServiceDIntel objDataService, DepthLog log, IEnumerable<LogChannel> channels, ref string lastError)
        {
            if (objDataService == null)
            {
                lastError = "Data service is not initialized.";
                return false;
            }

            if (log == null || string.IsNullOrWhiteSpace(log.ObjectID))
            {
                lastError = "DepthLog or LogID cannot be empty.";
                return false;
            }

            try
            {
                int dupCode = (int)log.DuplicateAction;
                string modDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");

                double.TryParse(log.startIndex, NumberStyles.Any, CultureInfo.InvariantCulture, out double minDepth);
                double.TryParse(log.endIndex, NumberStyles.Any, CultureInfo.InvariantCulture, out double maxDepth);

                string sql = "UPDATE VMX_DEPTH_LOG SET "
                    + "LOG_NAME='" + (log.nameLog ?? "").Replace("'", "''") + "', "
                    + "SERVICE_COMPANY='" + (log.serviceCompany ?? "").Replace("'", "''") + "', "
                    + "EDR_PROVIDER='" + (log.EDRProvider ?? "").Replace("'", "''") + "', "
                    + "RUN_NO='" + (log.runNumber ?? "").Replace("'", "''") + "', "
                    + "DESCRIPTION='" + (log.description ?? "").Replace("'", "''") + "', "
                    + "COMMENTS='" + (log.comments ?? "").Replace("'", "''") + "', "
                    + "PRIMARY_LOG=" + (log.PrimaryLog ? 1 : 0) + ", "
                    + "LINK_TO_PARENT=" + (log.LinkToParent ? 1 : 0) + ", "
                    + "LINK_WELL_ID='" + (log.LinkWellID ?? "").Replace("'", "''") + "', "
                    + "LINK_WELLBORE_ID='" + (log.LinkWellboreID ?? "").Replace("'", "''") + "', "
                    + "LINK_LOG_ID='" + (log.LinkLogID ?? "").Replace("'", "''") + "', "
                    + "DUPLICATE_ACTION=" + dupCode + ", "
                    + "MIN_DEPTH=" + minDepth.ToString(CultureInfo.InvariantCulture) + ", "
                    + "MAX_DEPTH=" + maxDepth.ToString(CultureInfo.InvariantCulture) + ", "
                    + "MODIFIED_DATE='" + modDate + "' "
                    + "WHERE LOG_ID='" + log.ObjectID.Replace("'", "''") + "';";

                objDataService.ExecuteNonQuery(sql);

                // 2. Update summary table if exists
                try
                {
                    if (objDataService.IsRecordExist("SELECT name FROM sqlite_master WHERE type='table' AND name='VMX_DEPTH_LOG_SUMMARY';"))
                    {
                        objDataService.ExecuteNonQuery("UPDATE VMX_DEPTH_LOG_SUMMARY SET LogName='" 
                            + (log.nameLog ?? "").Replace("'", "''") + "' WHERE LogId='" 
                            + log.ObjectID.Replace("'", "''") + "';");
                    }
                }
                catch { }

                // 3. Persist channels into VMX_DEPTH_LOG_COLUMNS
                bool hasVmxCols = objDataService.IsRecordExist("SELECT name FROM sqlite_master WHERE type='table' AND name='VMX_DEPTH_LOG_COLUMNS';");
                if (hasVmxCols)
                {
                    objDataService.ExecuteNonQuery("DELETE FROM VMX_DEPTH_LOG_COLUMNS WHERE LOG_ID='" + log.ObjectID.Replace("'", "''") + "';");

                    int order = 1;
                    foreach (var ch in channels)
                    {
                        int vt = ch.valueType;
                        string insertCol = "INSERT INTO VMX_DEPTH_LOG_COLUMNS ("
                            + "WELL_ID, WELLBORE_ID, LOG_ID, MNEMONIC, CHANNEL_NAME, DATA_TYPE, "
                            + "UNIT, UNIT_ID, VUMAX_UNIT_ID, VALUE_TYPE, VALUE_QUERY, WITSML_MNEMONIC, "
                            + "CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE, COLUMN_ORDER, "
                            + "WRITE_BACK, PI_MNEMONIC) VALUES ("
                            + "'" + (log.WellID ?? "").Replace("'", "''") + "', "
                            + "'" + (log.WellboreID ?? "").Replace("'", "''") + "', "
                            + "'" + log.ObjectID.Replace("'", "''") + "', "
                            + "'" + ch.Mnemonic.Replace("'", "''") + "', "
                            + "'" + ch.Description.Replace("'", "''") + "', "
                            + "'" + (string.IsNullOrWhiteSpace(ch.DataType) ? "Double" : ch.DataType.Replace("'", "''")) + "', "
                            + "'" + ch.Unit.Replace("'", "''") + "', "
                            + "'" + ch.VuMaxUnitId.Replace("'", "''") + "', "
                            + "'" + ch.VuMaxUnitId.Replace("'", "''") + "', "
                            + vt + ", "
                            + "'" + ch.Expression.Replace("'", "''") + "', "
                            + "'" + (string.IsNullOrWhiteSpace(ch.UploadMnemonic) ? ch.Mnemonic : ch.UploadMnemonic).Replace("'", "''") + "', "
                            + "'System', '" + modDate + "', 'System', '" + modDate + "', "
                            + (ch.ColumnOrder > 0 ? ch.ColumnOrder : order++) + ", "
                            + (ch.Upload ? 1 : 0) + ", "
                            + "'" + (ch.PiMnemonic ?? "").Replace("'", "''") + "');";

                        objDataService.ExecuteNonQuery(insertCol);
                    }
                }

                // 4. If DATA_TABLE_NAME stores channel rows directly (contains MNEMONIC column)
                if (!string.IsNullOrWhiteSpace(log.__dataTableName))
                {
                    try
                    {
                        DataTable dtDirectCols = objDataService.GetTable("PRAGMA table_info('" + log.__dataTableName.Replace("'", "''") + "');");
                        bool hasMnemCol = false;
                        if (dtDirectCols != null)
                        {
                            foreach (DataRow r in dtDirectCols.Rows)
                            {
                                if (string.Equals(Convert.ToString(r["name"]), "MNEMONIC", StringComparison.OrdinalIgnoreCase))
                                {
                                    hasMnemCol = true;
                                    break;
                                }
                            }
                            dtDirectCols.Dispose();
                        }

                        if (hasMnemCol)
                        {
                            foreach (var ch in channels)
                            {
                                string targetKey = !string.IsNullOrWhiteSpace(ch.OriginalMnemonic) ? ch.OriginalMnemonic : ch.Mnemonic;
                                string updDirect = "UPDATE [" + log.__dataTableName.Replace("'", "''") + "] SET "
                                    + "MNEMONIC='" + ch.Mnemonic.Replace("'", "''") + "', "
                                    + "UNIT='" + ch.Unit.Replace("'", "''") + "', "
                                    + "DESCRIPTION='" + ch.Description.Replace("'", "''") + "' "
                                    + "WHERE MNEMONIC='" + targetKey.Replace("'", "''") + "';";
                                objDataService.ExecuteNonQuery(updDirect);
                            }
                        }
                    }
                    catch { }
                }

                // 5. Update log.LogCurves in memory
                log.LogCurves.Clear();
                foreach (var ch in channels)
                {
                    log.LogCurves[ch.Mnemonic] = ch;
                }

                return true;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ":" + ex.StackTrace;
                return false;
            }
        }

        public static bool SaveDepthLog(IDataServiceDIntel objDataService, DepthLog log, ref string lastError)
        {
            return SaveDepthLog(objDataService, log, log.LogCurves.Values, ref lastError);
        }

        private static DepthLog MapRowToDepthLog(DataRow row)
        {
            var log = new DepthLog
            {
                WellID = DataService.checkNull(row["WELL_ID"], ""),
                WellboreID = DataService.checkNull(row["WELLBORE_ID"], ""),
                ObjectID = DataService.checkNull(row["LOG_ID"], ""),
                nameLog = DataService.checkNull(row["LOG_NAME"], ""),
                runNumber = DataService.checkNull(row["RUN_NO"], ""),
                serviceCompany = DataService.checkNull(row["SERVICE_COMPANY"], ""),
                comments = DataService.checkNull(row["COMMENTS"], ""),
                __dataTableName = DataService.checkNull(row["DATA_TABLE_NAME"], ""),
                description = DataService.checkNull(row["DESCRIPTION"], ""),
                wmlsurl = DataService.checkNull(row["WMLS_URL"], ""),
                wmlpurl = DataService.checkNull(row["WMLP_URL"], ""),
                creationDate = DataService.checkNull(row["CREATED_DATE"], ""),
                dTimLastChange = DataService.checkNull(row["MODIFIED_DATE"], ""),
                EDRProvider = DataService.checkNull(row["EDR_PROVIDER"], ""),
                PiWellID = DataService.checkNull(row["PI_WELL_ID"], ""),
                PiWellboreID = DataService.checkNull(row["PI_WELLBORE_ID"], ""),
                PiLogID = DataService.checkNull(row["PI_LOG_ID"], ""),
                LinkToParent = Convert.ToInt32(DataService.checkNull(row["LINK_TO_PARENT"], 0)) == 1,
                LinkWellID = DataService.checkNull(row["LINK_WELL_ID"], ""),
                LinkWellboreID = DataService.checkNull(row["LINK_WELLBORE_ID"], ""),
                LinkLogID = DataService.checkNull(row["LINK_LOG_ID"], ""),
                DuplicateAction = (enumDuplicateAction)Convert.ToInt32(DataService.checkNull(row["DUPLICATE_ACTION"], 0)),
                PrimaryLog = Convert.ToInt32(DataService.checkNull(row["PRIMARY_LOG"], 0)) == 1
            };

            if (row.Table.Columns.Contains("MIN_DEPTH"))
                log.startIndex = Convert.ToString(DataService.checkNull(row["MIN_DEPTH"], 0.0), CultureInfo.InvariantCulture);
            if (row.Table.Columns.Contains("MAX_DEPTH"))
                log.endIndex = Convert.ToString(DataService.checkNull(row["MAX_DEPTH"], 0.0), CultureInfo.InvariantCulture);

            return log;
        }
    }
}
