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

                if (IsDepthLogExist(objDataService, objDepthLog.WellID, objDepthLog.WellboreID, objDepthLog.ObjectID))
                {
                    LastError = "The depth log already exist";
                    return false;
                }

                // (1) Create a data table ==================================================================
                string dataTableName = ObjectIDFactory.generateDepthLogTableName();
                objDepthLog.__dataTableName = dataTableName;

                // Remove the table, if it exists ...
                objDataService.ExecuteNonQuery("DROP TABLE IF EXISTS [" + dataTableName + "];");

                string strSQL = "CREATE TABLE [" + dataTableName + "] (";

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

                strSQL += strFields + ")";

                if (!objDataService.ExecuteNonQuery(strSQL))
                {
                    LastError = objDataService.LastError;
                    return false;
                }

                strSQL = "CREATE UNIQUE INDEX IF NOT EXISTS [" + dataTableName + "_PK] ON [" + dataTableName + "](DEPTH);";
                if (!objDataService.ExecuteNonQuery(strSQL))
                {
                    LastError = objDataService.LastError;
                    return false;
                }

                strSQL = "CREATE INDEX IF NOT EXISTS [" + dataTableName + "_DATAINDEX] ON [" + dataTableName + "](DATA_INDEX);";
                if (!objDataService.ExecuteNonQuery(strSQL))
                {
                    LastError = objDataService.LastError;
                    return false;
                }
                // =========================================================================================

                // (2) Update Meta Data ====================================================================
                strSQL = "INSERT INTO VMX_DEPTH_LOG (WELL_ID,WELLBORE_ID,LOG_ID,LOG_NAME,RUN_NO,SERVICE_COMPANY,COMMENTS,DATA_TABLE_NAME,DESCRIPTION,WMLS_URL,WMLP_URL,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE,EDR_PROVIDER,PI_WELL_ID,PI_WELLBORE_ID,PI_LOG_ID,LINK_TO_PARENT,LINK_WELL_ID,LINK_WELLBORE_ID,LINK_LOG_ID,DUPLICATE_ACTION,PRIMARY_LOG) VALUES(";
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
                    foreach (LogChannel objChannel in objDepthLog.LogCurves.Values)
                    {
                        strSQL = "INSERT INTO VMX_DEPTH_LOG_COLUMNS (WELL_ID,WELLBORE_ID,LOG_ID,MNEMONIC,CHANNEL_NAME,DATA_TYPE,UNIT,UNIT_ID,VUMAX_UNIT_ID,VALUE_TYPE,VALUE_QUERY,WITSML_MNEMONIC,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE,WRITE_BACK,PI_MNEMONIC,AXIS_DATA_COUNT,COLUMN_ORDER,PARENT_MNEMONIC) VALUES(";
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
    }
}
