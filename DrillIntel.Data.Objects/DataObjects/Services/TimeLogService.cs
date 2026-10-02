using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Models.Util;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrillIntel.Data.Objects.DataObjects.Services
{
    public class TimeLogService
    {
        // --- [NEW LOGIC (Overload without ref for standard IDataServiceDIntel calling)] ---
        public static bool isTimeLogExist(IDataServiceDIntel objDataService, string WellID, string WellboreID, string LogID)
        {
            return IsTimeLogExist(objDataService, WellID, WellboreID, LogID);
        }

        public static bool isTimeLogExist(ref IDataServiceDIntel objDataService, string WellID, string WellboreID, string LogID)
        {
            return IsTimeLogExist(objDataService, WellID, WellboreID, LogID);
        }

        public static bool IsTimeLogExist(IDataServiceDIntel objDataService, string WellID, string WellboreID, string LogID)
        {
            try
            {
                // --- [OLD LOGIC (Unbound parameters and stray block caused failure in SQLite)] ---
                // string sql = "SELECT LOG_ID FROM VMX_TIME_LOG WHERE WELL_ID=@WellID AND WELLBORE_ID=@WellboreID AND LOG_ID=@LogID";
                // return objDataService.IsRecordExist(sql);
                // {
                // };

                // --- [NEW LOGIC (Sanitized SQLite parameter query matching DepthLogService)] ---
                if (objDataService == null || string.IsNullOrWhiteSpace(WellID) || string.IsNullOrWhiteSpace(WellboreID) || string.IsNullOrWhiteSpace(LogID))
                    return false;

                string sql = "SELECT LOG_ID FROM VMX_TIME_LOG " +
                             "WHERE WELL_ID='" + WellID.Replace("'", "''") + "' " +
                             "AND WELLBORE_ID='" + WellboreID.Replace("'", "''") + "' " +
                             "AND LOG_ID='" + LogID.Replace("'", "''") + "';";

                return objDataService.IsRecordExist(sql);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool addTimeLog(IDataServiceDIntel objDataService, TimeLog objTimeLog, ref string LastError)
        {
            try
            {
                // --- [NEW LOGIC (Auto-initialize IDs if missing, matching DepthLogService)] ---
                if (objDataService == null)
                {
                    LastError = "Data service is not initialized.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(objTimeLog.ObjectID))
                {
                    objTimeLog.ObjectID = Guid.NewGuid().ToString();
                }

                if (string.IsNullOrWhiteSpace(objTimeLog.WellID))
                {
                    var wellIdObj = objDataService.GetValue("SELECT WELL_ID FROM VMX_WELL LIMIT 1;");
                    if (wellIdObj != null)
                    {
                        objTimeLog.WellID = Convert.ToString(wellIdObj) ?? string.Empty;
                    }
                }

                if (string.IsNullOrWhiteSpace(objTimeLog.WellboreID) && !string.IsNullOrWhiteSpace(objTimeLog.WellID))
                {
                    var wbIdObj = objDataService.GetValue("SELECT WELLBORE_ID FROM VMX_WELLBORE WHERE WELL_ID='" 
                        + objTimeLog.WellID.Replace("'", "''") + "' LIMIT 1;");
                    if (wbIdObj != null)
                    {
                        objTimeLog.WellboreID = Convert.ToString(wbIdObj) ?? string.Empty;
                    }
                }

                // --- [OLD LOGIC (Prevented updating existing logs)] ---
                // if (isTimeLogExist(ref objDataService, objTimeLog.WellID, objTimeLog.WellboreID, objTimeLog.ObjectID))
                // {
                //     LastError = "The time log already exist";
                //     return false;
                // }

                //Later Use if Needed
                //SystemSettings objSystemSettings = new SystemSettings();
                //objSystemSettings.LoadSettings(objDataService);
                ////End Later Use if Needed

                // Drop the table

                // (1) Create a data table ==================================================================
                // Create the table

                // string dataTableName = TimeLog.getDataTableNameLegacy(objTimeLog.WellID, objTimeLog.WellboreID, objTimeLog.ObjectID);

                // --- [OLD LOGIC (Generated table name was not saved back to objTimeLog)] ---
                // string dataTableName = ObjectIDFactory.generateTimeLogTableName();

                // --- [NEW LOGIC (Save generated table name on objTimeLog so caller can access it)] ---
                string dataTableName = string.IsNullOrWhiteSpace(objTimeLog.__dataTableName)
                    ? ObjectIDFactory.generateTimeLogTableName()
                    : objTimeLog.__dataTableName;
                objTimeLog.__dataTableName = dataTableName;

                string strSQL = string.Empty;

                // Remove the table, if it exists ...
                // --- [OLD LOGIC (DROP TABLE unconditionally wiped out data on metadata update calls)] ---
                // objDataService.ExecuteNonQuery("DROP TABLE " + dataTableName);

                // --- [NEW LOGIC (Only create table and indexes if table does not already exist, mirroring DepthLogService)] ---
                if (objTimeLog.logCurves != null && objTimeLog.logCurves.Count > 0)
                {
                    bool tableExists = objDataService.IsRecordExist("SELECT name FROM sqlite_master WHERE type='table' AND name='" + dataTableName.Replace("'", "''") + "';");
                    if (!tableExists)
                    {
                        strSQL = "CREATE TABLE [" + dataTableName + "] (";

                        string strFields = "DATA_INDEX DECIMAL(8) ";

                        foreach (LogChannel objCurve in objTimeLog.logCurves.Values)
                        {
                            if (objCurve.typeLogData.Trim() != "")
                            {
                                switch (objCurve.typeLogData.ToUpper())
                                {
                                    case "DATE TIME":
                                    case "DATETIME":
                                    case "SYSTEM.DATETIME":
                                        strFields += ",[" + objCurve.mnemonic + "] DATETIME ";

                                        if (objCurve.mnemonic.Equals("DATETIME", StringComparison.OrdinalIgnoreCase))
                                        {
                                            strFields += " NOT NULL";
                                        }
                                        break;

                                    case "DOUBLE":
                                    case "LONG":
                                    case "FLOAT":
                                    case "INT":
                                    case "SHORT":
                                    case "SYSTEM.DOUBLE":
                                    case "SYSTEM.LONG":
                                    case "SYSTEM.FLOAT":
                                    case "SYSTEM.INT":
                                    case "SYSTEM.SHORT":
                                        strFields += ",[" + objCurve.mnemonic + "] DECIMAL(16,5) ";
                                        break;

                                    case "STRING":
                                    case "STRING40":
                                    case "STRING16":
                                    case "SYSTEM.STRING":
                                    case "SYSTEM.STRING16":
                                    case "SYSTEM.STRING40":
                                        strFields += ",[" + objCurve.mnemonic + "] VARCHAR(1000) ";
                                        break;

                                    default:
                                        strFields += ",[" + objCurve.mnemonic + "] VARCHAR(500) ";
                                        break;
                                }
                            }
                            else
                            {
                                if (objCurve.mnemonic.Equals("DATETIME", StringComparison.OrdinalIgnoreCase))
                                {
                                    objCurve.typeLogData = "DATETIME";
                                    strFields += ",[" + objCurve.mnemonic + "] DATETIME NOT NULL";
                                }
                                else
                                {
                                    objCurve.typeLogData = "Double";
                                    strFields += ",[" + objCurve.mnemonic + "] DECIMAL(16,5)";
                                }
                            }
                        }

                        strSQL += strFields + ")";

                        // VuMaxLogger.logMessage("Create Table SQL: " + strSQL, objSystemSettings);

                        if (objDataService.ExecuteNonQuery(strSQL))
                        {
                            // ok
                        }
                        else
                        {
                            LastError = objDataService.LastError;
                            return false;
                        }

                        // --- [OLD LOGIC (Unique index on DATETIME without bracket wrapping)] ---
                        // strSQL = "CREATE UNIQUE INDEX " + dataTableName + "_PK ON " + dataTableName + "(DATETIME)";

                        // --- [NEW LOGIC (Create unique index on primary DATETIME or index curve if present)] ---
                        bool hasDateTimeCurve = objTimeLog.logCurves.Values.Any(c => c.mnemonic.Equals("DATETIME", StringComparison.OrdinalIgnoreCase));
                        if (hasDateTimeCurve)
                        {
                            strSQL = "CREATE INDEX IF NOT EXISTS [" + dataTableName + "_PK] ON [" + dataTableName + "](DATETIME);";
                            objDataService.ExecuteNonQuery(strSQL);
                        }

                        strSQL = "CREATE INDEX IF NOT EXISTS [" + dataTableName + "_DATAINDEX] ON [" + dataTableName + "](DATA_INDEX);";
                        objDataService.ExecuteNonQuery(strSQL);

                        // ** Create New Indexes for speed improvement ****************************************************************
                        // --- [OLD LOGIC (Unconditional index on RIG_STATE / DEPTH failed when column did not exist in CSV)] ---
                        // strSQL = "CREATE INDEX IDX1_" + dataTableName + " ON " + dataTableName + "(DATETIME)";
                        // objDataService.ExecuteNonQuery(strSQL);
                        // strSQL = "CREATE INDEX IDX2_" + dataTableName + " ON " + dataTableName + "(RIG_STATE)";
                        // objDataService.ExecuteNonQuery(strSQL);
                        // strSQL = "CREATE INDEX IDX3_" + dataTableName + " ON " + dataTableName + "(RIG_STATE,DATETIME)";
                        // objDataService.ExecuteNonQuery(strSQL);
                        // strSQL = "CREATE INDEX IDX13_" + dataTableName + " ON " + dataTableName + "(DEPTH)";
                        // objDataService.ExecuteNonQuery(strSQL);

                        // --- [NEW LOGIC (Conditional index creation only when column exists)] ---
                        if (objTimeLog.logCurves.ContainsKey("RIG_STATE"))
                        {
                            strSQL = "CREATE INDEX IF NOT EXISTS IDX2_" + dataTableName + " ON [" + dataTableName + "](RIG_STATE);";
                            objDataService.ExecuteNonQuery(strSQL);

                            if (hasDateTimeCurve)
                            {
                                strSQL = "CREATE INDEX IF NOT EXISTS IDX3_" + dataTableName + " ON [" + dataTableName + "](RIG_STATE,DATETIME);";
                                objDataService.ExecuteNonQuery(strSQL);
                            }
                        }

                        if (objTimeLog.logCurves.ContainsKey("DEPTH"))
                        {
                            strSQL = "CREATE INDEX IF NOT EXISTS IDX13_" + dataTableName + " ON [" + dataTableName + "](DEPTH);";
                            objDataService.ExecuteNonQuery(strSQL);
                        }
                        // ************************************************************************************************************

                        // Create Custom Indexes specified by the user
                        Dictionary<string, TimeLogIndex> indexList = TimeLogIndexService.getActiveList(objDataService);

                        foreach (TimeLogIndex objItem in indexList.Values)
                        {
                            strSQL = "CREATE INDEX IF NOT EXISTS IDX" + objItem.Prefix + "_" + dataTableName + " ON [" + dataTableName + "](" + objItem.Channels.Replace("'", "''") + ");";
                            objDataService.ExecuteNonQuery(strSQL);
                        }
                    }
                }

                // --- [OLD LOGIC (ALTER TABLE ADD CONSTRAINT is NOT supported in SQLite and causes fatal error)] ---
                // strSQL = "ALTER TABLE " + dataTableName + " ADD CONSTRAINT PK_" + dataTableName + " PRIMARY KEY (DATETIME)";
                // if (objDataService.ExecuteNonQuery(strSQL))
                // {
                //     // ok
                // }
                // else
                // {
                //     LastError = objDataService.LastError;
                //     return false;
                // }
                // --- [NEW LOGIC (SQLite indexing handled via CREATE INDEX above)] ---

                // --- [OLD LOGIC (SQL Server permission GRANT and VMX_USER table do not exist in SQLite)] ---
                // DataTable objUsers = objDataService.GetTable("SELECT USER_NAME FROM VMX_USER");
                // foreach (DataRow objRow in objUsers.Rows)
                // {
                //     string strUserName = (string)objRow["USER_NAME"];
                //     strSQL = " GRANT SELECT,INSERT,UPDATE,DELETE ON OBJECT::dbo." + dataTableName + " TO [VMX_" + strUserName + "]";
                //     objDataService.ExecuteNonQuery(strSQL);
                // }
                // --- [NEW LOGIC (No-op in SQLite)] ---
                // =========================================================================================

                int lnDuplicateAction = Convert.ToInt16(objTimeLog.DuplicateAction);

                // (2) Update Meta Data ====================================================================
                // prath 23-07-2019
                // --- [OLD LOGIC (INSERT INTO fails on duplicate/update)] ---
                // strSQL = "INSERT INTO VMX_TIME_LOG (...) VALUES(";

                // --- [NEW LOGIC (INSERT OR REPLACE INTO allows upserting metadata)] ---
                strSQL = "INSERT OR REPLACE INTO VMX_TIME_LOG (WELL_ID,WELLBORE_ID,LOG_ID,LOG_NAME,RUN_NO,SERVICE_COMPANY,COMMENTS,DATA_TABLE_NAME,DESCRIPTION,WMLS_URL,WMLP_URL,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE,EDR_PROVIDER,PRIMARY_LOG,REMARKS_LOG,PI_WELL_ID,PI_WELLBORE_ID,PI_LOG_ID,LINK_TO_PARENT,LINK_WELL_ID,LINK_WELLBORE_ID,LINK_LOG_ID,DUPLICATE_ACTION,DONT_CALC_HDTH,STARTING_HDTH,DETECT_SPIKES,TL_PERCENT,TIME_PERIOD,NR_BTM_DISTANCE,CMP_WINDOW,OPEN_SPIKE,ACTION_TYPE,MAX_CLOSE_TIME,CREA_REP_ON_FORMATION,SNAP_JOB_ID,FORMATION_TOP,DEPTH_THRESHOLD,FREQUENCY,DONT_MOVE_AHEAD) VALUES(";
                // -------------
                strSQL += "'" + objTimeLog.WellID + "',";
                strSQL += "'" + objTimeLog.WellboreID + "',";
                strSQL += "'" + objTimeLog.ObjectID + "',";
                strSQL += "'" + objTimeLog.nameLog.Replace("'", "''") + "',";
                strSQL += "'" + objTimeLog.runNumber.Replace("'", "''") + "',";
                strSQL += "'" + objTimeLog.serviceCompany.Replace("'", "''") + "',";
                strSQL += "'" + objTimeLog.comments.Replace("'", "''") + "',";
                strSQL += "'" + dataTableName + "',";
                strSQL += "'" + objTimeLog.description.Replace("'", "''") + "',";
                strSQL += "'" + objTimeLog.wmlsurl + "',";
                strSQL += "'" + objTimeLog.wmlpurl + "',";
                strSQL += "'" + objDataService.UserName + "',";
                strSQL += "'" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "',";
                strSQL += "'" + objDataService.UserName + "',";
                strSQL += "'" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "',";
                strSQL += "'" + objTimeLog.EDRProvider.Replace("'", "''") + "',";
                strSQL += "" + (objTimeLog.PrimaryLog ? 1 : 0).ToString() + ",";
                strSQL += "" + (objTimeLog.RemarksLog ? 1 : 0).ToString() + ",";
                strSQL += "'" + objTimeLog.PiWellID.Replace("'", "''") + "',";
                strSQL += "'" + objTimeLog.PiWellboreID.Replace("'", "''") + "',";
                strSQL += "'" + objTimeLog.PiLogID.Replace("'", "''") + "',";
                strSQL += "" + (objTimeLog.LinkToParent == true ? 1 : 0).ToString() + ",";
                strSQL += "'" + objTimeLog.LinkWellID + "',";
                strSQL += "'" + objTimeLog.LinkWellboreID + "',";
                strSQL += "'" + objTimeLog.LinkLogID + "',";
                strSQL += "" + lnDuplicateAction.ToString() + ",";
                strSQL += "" + (objTimeLog.DontCalcHoleDepth == true ? 1 : 0).ToString() + ",";
                strSQL += "" + objTimeLog.StartingHoleDepth.ToString() + ",";

                strSQL += "" + (objTimeLog.DetectSpike == true ? 1 : 0).ToString() + ",";
                strSQL += "" + objTimeLog.TolerancePc.ToString() + ",";
                strSQL += "" + objTimeLog.CheckTimePeriod.ToString() + ",";
                strSQL += "" + objTimeLog.NearBottomDistance.ToString() + ",";
                strSQL += "" + objTimeLog.CompareWindow.ToString() + ",";
                strSQL += "" + (objTimeLog.IsOpenSpike == true ? 1 : 0).ToString() + ",";
                strSQL += "" + objTimeLog.ActionType.ToString() + ",";
                strSQL += "" + objTimeLog.MaxCloseTime.ToString() + ",";

                // prath 23-07-2019
                strSQL += "" + (objTimeLog.CreateRepOnFormation == true ? 1 : 0).ToString() + ",";
                strSQL += "'" + objTimeLog.SnapJobID.ToString() + "',";
                strSQL += "'" + objTimeLog.FormationTop.ToString() + "',";
                strSQL += "" + objTimeLog.DepthThreshold.ToString() + ",";
                strSQL += "" + objTimeLog.Frequency.ToString() + ",";
                strSQL += "" + (objTimeLog.DontMoveAhead == true ? 1 : 0).ToString() + ")";
                // strSQL += "'" + objTimeLog.LastSnapSendDatetime.ToString() + "',";
                // ----------

                if (objDataService.ExecuteNonQuery(strSQL))
                {
                   

                    // --- [NEW LOGIC (SQLite LIMIT 1 query with null check)] ---
                    DataTable objCheckData = objDataService.GetTable("SELECT * FROM VMX_TIME_LOG_COLUMNS LIMIT 1;");

                    if (objCheckData != null && objCheckData.Columns.Contains("NO_INTERPOLATE"))
                    {
                        // Nothing to do ...
                    }
                    else
                    {
                        // Add this column ...
                        objDataService.ExecuteNonQuery("ALTER TABLE VMX_TIME_LOG_COLUMNS ADD NO_INTERPOLATE NUMERIC(1);");
                    }

                    foreach (LogChannel objChannel in objTimeLog.logCurves.Values)
                    {
                        // --- [OLD LOGIC (INSERT INTO)] ---
                        // strSQL = "INSERT INTO VMX_TIME_LOG_COLUMNS (...) VALUES(";

                        // --- [NEW LOGIC (INSERT OR REPLACE INTO avoids primary key conflicts)] ---
                        strSQL = "INSERT OR REPLACE INTO VMX_TIME_LOG_COLUMNS (WELL_ID,WELLBORE_ID,LOG_ID,MNEMONIC,CHANNEL_NAME,DATA_TYPE,UNIT,UNIT_ID,VUMAX_UNIT_ID,VALUE_TYPE,VALUE_QUERY,WITSML_MNEMONIC,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE,COLUMN_ORDER,OFFSET,NO_INTERPOLATE,WRITE_BACK,PI_MNEMONIC,IS_STOR_PROC,STOR_PROC_PARAM) VALUES(";
                        strSQL += "'" + objTimeLog.WellID + "',";
                        strSQL += "'" + objTimeLog.WellboreID + "',";
                        strSQL += "'" + objTimeLog.ObjectID + "',";
                        strSQL += "'" + objChannel.mnemonic.Replace("'", "''") + "',";
                        strSQL += "'" + objChannel.curveDescription.Replace("'", "''") + "',";
                        strSQL += "'" + objChannel.typeLogData.Replace("'", "''") + "',";
                        strSQL += "'" + objChannel.unit + "',";
                        strSQL += "'" + objChannel.UnitID + "',";
                        strSQL += "'" + objChannel.VuMaxUnitID + "',";
                        strSQL += "" + objChannel.valueType.ToString() + ",";
                        strSQL += "'" + objChannel.valueQuery.Replace("'", "''") + "',";
                        strSQL += "'" + objChannel.witsmlMnemonic.Replace("'", "''") + "',";
                        strSQL += "'" + objDataService.UserName + "',";
                        strSQL += "'" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "',";
                        strSQL += "'" + objDataService.UserName + "',";
                        strSQL += "'" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "',";
                        strSQL += "" + objChannel.ColumnOrder.ToString() + ",";
                        strSQL += "" + VBVal(objChannel.sensorOffset).ToString() + ",";
                        strSQL += "" + objChannel.DoNotInterpolate.ToString() + ",";
                        strSQL += "" + (objChannel.WriteBack == true ? 1 : 0).ToString() + ",";
                        strSQL += "'" + objChannel.PiMnemonic.Replace("'", "''") + "',";
                        strSQL += "" + (objChannel.isStoredProc == true ? 1 : 0).ToString() + ",";
                        strSQL += "'" + objChannel.StoredProcParams.Replace("'", "''") + "')";

                        objDataService.ExecuteNonQuery(strSQL);
                    }

                    // --- [NEW LOGIC (Update MIN_DATE / MAX_DATE extents in VMX_TIME_LOG if provided)] ---
                    if (!string.IsNullOrWhiteSpace(objTimeLog.startIndex) || !string.IsNullOrWhiteSpace(objTimeLog.endIndex))
                    {
                        string updExtentsSql = "UPDATE VMX_TIME_LOG SET MIN_DATE = '" + (objTimeLog.startIndex ?? "").Replace("'", "''") +
                                               "', MAX_DATE = '" + (objTimeLog.endIndex ?? "").Replace("'", "''") +
                                               "' WHERE LOG_ID = '" + objTimeLog.ObjectID.Replace("'", "''") + "';";
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
                LastError = ex.Message + ex.StackTrace;
                return false;
            }
        }

        /// <summary>
        /// Static convenience helper to insert or update a TimeLog record without instantiating TimeLogService.
        /// </summary>
        public static bool AddLog(TimeLog log, IDataServiceDIntel dataService)
        {
            string lastError = "";
            return addTimeLog(dataService, log, ref lastError);
        }

        /// <summary>
        /// Generates a random data table name formatted as: timeLog{8digits}#{8digits}
        /// </summary>
        public static string GenerateDataTableName()
        {
            return ObjectIDFactory.generateTimeLogTableName();
        }

        /// <summary>
        /// Reads all TimeLog entries from VMX_TIME_LOG for a well (or all wells if wellID is blank).
        /// Matches DepthLogService.LoadDepthLogs pattern.
        /// </summary>
        public static List<TimeLog> LoadTimeLogs(IDataServiceDIntel objDataService, string wellID, ref string lastError)
        {
            var list = new List<TimeLog>();
            try
            {
                if (objDataService == null)
                {
                    lastError = "Data service is not initialized.";
                    return list;
                }

                string sql = string.IsNullOrWhiteSpace(wellID)
                    ? "SELECT * FROM VMX_TIME_LOG ORDER BY CREATED_DATE DESC;"
                    : "SELECT * FROM VMX_TIME_LOG WHERE WELL_ID='" + wellID.Replace("'", "''") + "' ORDER BY CREATED_DATE DESC;";

                DataTable dt = objDataService.GetTable(sql);
                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(MapRowToTimeLog(row));
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

        /// <summary>
        /// Loads a single TimeLog object from VMX_TIME_LOG matching DepthLogService.LoadObject.
        /// </summary>
        public static TimeLog? LoadObject(IDataServiceDIntel objDataService, string wellID, string wellboreID, string logID, ref string lastError)
        {
            try
            {
                if (objDataService == null)
                {
                    lastError = "Data service is not initialized.";
                    return null;
                }

                // --- [OLD LOGIC (Rigid WHERE WELL_ID='' AND WELLBORE_ID='' failed when well IDs were omitted)] ---
                // DataTable dt = objDataService.GetTable("SELECT * FROM VMX_TIME_LOG WHERE WELL_ID='" 
                //     + wellID.Replace("'", "''") + "' AND WELLBORE_ID='" 
                //     + wellboreID.Replace("'", "''") + "' AND LOG_ID='" 
                //     + logID.Replace("'", "''") + "';");

                // --- [NEW LOGIC (Flexible where clause matching DepthLogService: queries by LOG_ID and filters by WELL_ID / WELLBORE_ID only if provided)] ---
                string whereClause = "LOG_ID='" + logID.Replace("'", "''") + "'";
                if (!string.IsNullOrWhiteSpace(wellID))
                    whereClause += " AND WELL_ID='" + wellID.Replace("'", "''") + "'";
                if (!string.IsNullOrWhiteSpace(wellboreID))
                    whereClause += " AND WELLBORE_ID='" + wellboreID.Replace("'", "''") + "'";

                DataTable dt = objDataService.GetTable("SELECT * FROM VMX_TIME_LOG WHERE " + whereClause + " LIMIT 1;");

                if (dt != null && dt.Rows.Count > 0)
                {
                    // --- [OLD LOGIC (Did not populate logCurves dictionary from VMX_TIME_LOG_COLUMNS)] ---
                    // var log = MapRowToTimeLog(dt.Rows[0]);
                    // dt.Dispose();
                    // return log;

                    // --- [NEW LOGIC (Populate logCurves dictionary from VMX_TIME_LOG_COLUMNS so channel metadata is available)] ---
                    var log = MapRowToTimeLog(dt.Rows[0]);
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

        public static TimeLog? LoadObject(IDataServiceDIntel objDataService, string logID, ref string lastError)
        {
            return LoadObject(objDataService, "", "", logID, ref lastError);
        }

        // --- [NEW LOGIC (RemoveTimeLog: static method to delete a TimeLog, its channels, summaries, and timeseries table)] ---
        public static bool RemoveTimeLog(IDataServiceDIntel objDataService, string wellID, string wellboreID, string logID, bool dropDataTable, ref string lastError)
        {
            if (objDataService == null)
            {
                lastError = "Data service is not initialized.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(logID))
            {
                lastError = "LogID cannot be empty.";
                return false;
            }

            try
            {
                // 1. Discover data table name before deleting metadata
                string dataTableName = "";
                string whereClause = "LOG_ID='" + logID.Replace("'", "''") + "'";
                if (!string.IsNullOrWhiteSpace(wellID))
                    whereClause += " AND WELL_ID='" + wellID.Replace("'", "''") + "'";
                if (!string.IsNullOrWhiteSpace(wellboreID))
                    whereClause += " AND WELLBORE_ID='" + wellboreID.Replace("'", "''") + "'";

                var dtObj = objDataService.GetValue("SELECT DATA_TABLE_NAME FROM VMX_TIME_LOG WHERE " + whereClause + " LIMIT 1;");
                if (dtObj != null && dtObj != DBNull.Value)
                {
                    dataTableName = Convert.ToString(dtObj) ?? "";
                }

                // 2. Delete metadata from VMX_TIME_LOG
                objDataService.ExecuteNonQuery("DELETE FROM VMX_TIME_LOG WHERE " + whereClause + ";");

                // 3. Delete channel definitions from VMX_TIME_LOG_COLUMNS
                string colWhere = "LOG_ID='" + logID.Replace("'", "''") + "'";
                if (!string.IsNullOrWhiteSpace(wellID))
                    colWhere += " AND WELL_ID='" + wellID.Replace("'", "''") + "'";
                if (!string.IsNullOrWhiteSpace(wellboreID))
                    colWhere += " AND WELLBORE_ID='" + wellboreID.Replace("'", "''") + "'";
                objDataService.ExecuteNonQuery("DELETE FROM VMX_TIME_LOG_COLUMNS WHERE " + colWhere + ";");

                // 4. Delete from VMX_TIME_LOG_SUMMARY if table exists
                try
                {
                    bool hasSummary = objDataService.IsRecordExist("SELECT name FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG_SUMMARY';");
                    if (hasSummary)
                    {
                        string sumWhere = "LogId='" + logID.Replace("'", "''") + "'";
                        if (!string.IsNullOrWhiteSpace(dataTableName))
                            sumWhere += " OR DataTableName='" + dataTableName.Replace("'", "''") + "'";
                        objDataService.ExecuteNonQuery("DELETE FROM VMX_TIME_LOG_SUMMARY WHERE " + sumWhere + ";");
                    }
                }
                catch { }

                // 5. Delete from VMX_TIMELOG_INDEXES if table exists
                try
                {
                    bool hasIndexes = objDataService.IsRecordExist("SELECT name FROM sqlite_master WHERE type='table' AND name='VMX_TIMELOG_INDEXES';");
                    if (hasIndexes)
                    {
                        objDataService.ExecuteNonQuery("DELETE FROM VMX_TIMELOG_INDEXES WHERE LOG_ID='" + logID.Replace("'", "''") + "';");
                    }
                }
                catch { }

                // 6. Drop the physical time series data table if requested
                if (dropDataTable && !string.IsNullOrWhiteSpace(dataTableName))
                {
                    objDataService.ExecuteNonQuery("DROP TABLE IF EXISTS [" + dataTableName.Replace("'", "''") + "];");
                }

                return true;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ":" + ex.StackTrace;
                return false;
            }
        }

        public static bool RemoveTimeLog(IDataServiceDIntel objDataService, string logID, bool dropDataTable = true)
        {
            string lastError = "";
            return RemoveTimeLog(objDataService, "", "", logID, dropDataTable, ref lastError);
        }

        // --- [OLD LOGIC (Redundant: replaced by single RemoveTimeLog(objDataService, logID) and timeLog.Remove(objDataService))] ---
        // public static bool RemoveTimeLog(IDataServiceDIntel objDataService, TimeLog log, bool dropDataTable = true)
        // {
        //     if (log == null) return false;
        //     string lastError = "";
        //     return RemoveTimeLog(objDataService, log.WellID, log.WellboreID, log.ObjectID, dropDataTable, ref lastError);
        // }

        private static TimeLog MapRowToTimeLog(DataRow row)
        {
            var log = new TimeLog
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
                PrimaryLog = Convert.ToInt32(DataService.checkNull(row["PRIMARY_LOG"], 0)) == 1,
                RemarksLog = Convert.ToInt32(DataService.checkNull(row["REMARKS_LOG"], 0)) == 1
            };

            // --- [NEW LOGIC (Map hole depth and movement parameters from VMX_TIME_LOG)] ---
            if (row.Table.Columns.Contains("DONT_CALC_HDTH"))
                log.DontCalcHoleDepth = Convert.ToInt32(DataService.checkNull(row["DONT_CALC_HDTH"], 0)) == 1;
            else if (row.Table.Columns.Contains("no_auto_calc"))
                log.DontCalcHoleDepth = Convert.ToInt32(DataService.checkNull(row["no_auto_calc"], 0)) == 1;

            if (row.Table.Columns.Contains("STARTING_HDTH"))
                log.StartingHoleDepth = DataService.checkNull(row["STARTING_HDTH"], 0.0);
            else if (row.Table.Columns.Contains("starting_hole_depth"))
                log.StartingHoleDepth = DataService.checkNull(row["starting_hole_depth"], 0.0);

            if (row.Table.Columns.Contains("DONT_MOVE_AHEAD"))
                log.DontMoveAhead = Convert.ToInt32(DataService.checkNull(row["DONT_MOVE_AHEAD"], 0)) == 1;

            if (row.Table.Columns.Contains("MIN_DATE"))
                log.startIndex = DataService.checkNull(row["MIN_DATE"], "");
            if (row.Table.Columns.Contains("MAX_DATE"))
                log.endIndex = DataService.checkNull(row["MAX_DATE"], "");

            return log;
        }

        /// <summary>
        /// Mimics VB.NET's Val() function: parses the leading numeric portion of a string,
        /// returning 0 if no valid number is found. Used to replicate Val(objChannel.sensorOffset).
        /// </summary>
        private static double VBVal(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return 0;

            input = input.Trim();
            int i = 0;
            int len = input.Length;
            bool seenDigitOrDot = false;

            if (i < len && (input[i] == '+' || input[i] == '-'))
                i++;

            int start = i;

            while (i < len && (char.IsDigit(input[i]) || input[i] == '.'))
            {
                seenDigitOrDot = true;
                i++;
            }

            if (!seenDigitOrDot)
                return 0;

            string numericPart = input.Substring(0, i);

            if (double.TryParse(numericPart, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                return result;

            return 0;
        }

        private static double checkNumericNull(object? val)
        {
            if (val == null || val == DBNull.Value || string.IsNullOrWhiteSpace(val.ToString()))
                return 0.0;
            if (double.TryParse(val.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                return d;
            return 0.0;
        }

        // --- [NEW LOGIC (LoadLogCurves: loads channel definitions from custom table, VMX_TIME_LOG_COLUMNS, and timeseries table columns)] ---
        public static void LoadLogCurves(IDataServiceDIntel objDataService, TimeLog log)
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
                                        if (!string.IsNullOrWhiteSpace(ch.Mnemonic) && !log.logCurves.ContainsKey(ch.Mnemonic))
                                        {
                                            log.logCurves.Add(ch.Mnemonic, ch);
                                        }
                                    }
                                    dtDirect.Dispose();
                                }
                            }
                        }
                    }
                    catch { }
                }

                // 2. Query VMX_TIME_LOG_COLUMNS
                if (!customTableHasChannels)
                {
                    string colWhere = "LOG_ID='" + log.ObjectID.Replace("'", "''") + "'";
                    if (!string.IsNullOrWhiteSpace(log.WellID))
                        colWhere += " AND WELL_ID='" + log.WellID.Replace("'", "''") + "'";
                    if (!string.IsNullOrWhiteSpace(log.WellboreID))
                        colWhere += " AND WELLBORE_ID='" + log.WellboreID.Replace("'", "''") + "'";

                    DataTable dtCols = objDataService.GetTable("SELECT * FROM VMX_TIME_LOG_COLUMNS WHERE " + colWhere + " ORDER BY COLUMN_ORDER ASC;");

                    if (dtCols != null)
                    {
                        foreach (DataRow r in dtCols.Rows)
                        {
                            var channel = new LogChannel
                            {
                                mnemonic = DataService.checkNull(r["MNEMONIC"], ""),
                                curveDescription = DataService.checkNull(r["CHANNEL_NAME"], ""),
                                typeLogData = DataService.checkNull(r["DATA_TYPE"], ""),
                                unit = DataService.checkNull(r["UNIT"], ""),
                                UnitID = r.Table.Columns.Contains("UNIT_ID") ? DataService.checkNull(r["UNIT_ID"], "") : "",
                                VuMaxUnitID = r.Table.Columns.Contains("VUMAX_UNIT_ID") ? DataService.checkNull(r["VUMAX_UNIT_ID"], "") : "",
                                valueType = Convert.ToInt32(DataService.checkNull(r["VALUE_TYPE"], 0)),
                                valueQuery = r.Table.Columns.Contains("VALUE_QUERY") ? DataService.checkNull(r["VALUE_QUERY"], "") : "",
                                witsmlMnemonic = r.Table.Columns.Contains("WITSML_MNEMONIC") ? DataService.checkNull(r["WITSML_MNEMONIC"], "") : "",
                                WriteBack = r.Table.Columns.Contains("WRITE_BACK") && Convert.ToInt32(DataService.checkNull(r["WRITE_BACK"], 0)) == 1,
                                DoNotInterpolate = r.Table.Columns.Contains("NO_INTERPOLATE") ? Convert.ToInt32(DataService.checkNull(r["NO_INTERPOLATE"], 0)) : 0,
                                ColumnOrder = r.Table.Columns.Contains("COLUMN_ORDER") ? Convert.ToInt32(DataService.checkNull(r["COLUMN_ORDER"], 0)) : 0
                            };
                            channel.OriginalMnemonic = channel.mnemonic;
                            if (!string.IsNullOrWhiteSpace(channel.mnemonic) && !log.logCurves.ContainsKey(channel.mnemonic))
                            {
                                log.logCurves.Add(channel.mnemonic, channel);
                            }
                        }
                        dtCols.Dispose();
                    }

                    // 3. Supplement any missing columns from physical time-series table
                    if (!string.IsNullOrWhiteSpace(log.__dataTableName))
                    {
                        try
                        {
                            DataTable pragmaCols = objDataService.GetTable("PRAGMA table_info('" + log.__dataTableName.Replace("'", "''") + "');");
                            if (pragmaCols != null)
                            {
                                int order = log.logCurves.Count + 1;
                                foreach (DataRow r in pragmaCols.Rows)
                                {
                                    string col = Convert.ToString(r["name"]) ?? "";
                                    if (string.IsNullOrWhiteSpace(col) || col.Equals("DATA_INDEX", StringComparison.OrdinalIgnoreCase)) continue;

                                    if (!log.logCurves.ContainsKey(col))
                                    {
                                        bool isDt = col.Equals("DATETIME", StringComparison.OrdinalIgnoreCase) ||
                                                    col.Equals("DATE_TIME", StringComparison.OrdinalIgnoreCase) ||
                                                    col.Equals("TIME", StringComparison.OrdinalIgnoreCase) ||
                                                    col.Equals("DATE", StringComparison.OrdinalIgnoreCase);

                                        var ch = new LogChannel
                                        {
                                            mnemonic = col,
                                            curveDescription = col,
                                            typeLogData = isDt ? "DateTime" : "Double",
                                            unit = isDt ? "" : (col.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) ? "m" : ""),
                                            UnitID = isDt ? "" : (col.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) ? "m" : ""),
                                            VuMaxUnitID = isDt ? "" : (col.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) ? "m" : ""),
                                            witsmlMnemonic = col,
                                            WriteBack = true,
                                            processChannel = true,
                                            ColumnOrder = order++,
                                            OriginalMnemonic = col
                                        };
                                        log.logCurves.Add(col, ch);
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

        // --- [NEW LOGIC (SaveTimeLog: persists TimeLog metadata, summary, and channel definitions into SQLite)] ---
        public static bool SaveTimeLog(IDataServiceDIntel objDataService, TimeLog log, IEnumerable<LogChannel> channels, ref string lastError)
        {
            if (objDataService == null)
            {
                lastError = "Data service is not initialized.";
                return false;
            }

            if (log == null || string.IsNullOrWhiteSpace(log.ObjectID))
            {
                lastError = "TimeLog or LogID cannot be empty.";
                return false;
            }

            try
            {
                int dupCode = log.DuplicateAction switch
                {
                    enumDuplicateAction.SkipDuplicates => 1,
                    enumDuplicateAction.OverwriteDuplicates => 0,
                    _ => 2
                };

                // Check schema of VMX_TIME_LOG
                DataTable dtCols = objDataService.GetTable("PRAGMA table_info('VMX_TIME_LOG');");
                var colNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (dtCols != null)
                {
                    foreach (DataRow r in dtCols.Rows)
                    {
                        colNames.Add(Convert.ToString(r["name"]) ?? "");
                    }
                    dtCols.Dispose();
                }

                bool hasNoAutoCalc = colNames.Contains("no_auto_calc");
                bool hasDontCalcHdth = colNames.Contains("DONT_CALC_HDTH");
                bool hasStartingHoleDepth = colNames.Contains("starting_hole_depth");
                bool hasStartingHdth = colNames.Contains("STARTING_HDTH");

                string modDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");

                string sql = "UPDATE VMX_TIME_LOG SET "
                    + "LOG_NAME='" + (log.nameLog ?? "").Replace("'", "''") + "', "
                    + "SERVICE_COMPANY='" + (log.serviceCompany ?? "").Replace("'", "''") + "', "
                    + "EDR_PROVIDER='" + (log.EDRProvider ?? "").Replace("'", "''") + "', "
                    + "RUN_NO='" + (log.runNumber ?? "").Replace("'", "''") + "', "
                    + "DESCRIPTION='" + (log.description ?? "").Replace("'", "''") + "', "
                    + "PRIMARY_LOG=" + (log.PrimaryLog ? 1 : 0) + ", "
                    + "REMARKS_LOG=" + (log.RemarksLog ? 1 : 0) + ", "
                    + "LINK_TO_PARENT=" + (log.LinkToParent ? 1 : 0) + ", "
                    + "LINK_WELL_ID='" + (log.LinkWellID ?? "").Replace("'", "''") + "', "
                    + "LINK_WELLBORE_ID='" + (log.LinkWellboreID ?? "").Replace("'", "''") + "', "
                    + "LINK_LOG_ID='" + (log.LinkLogID ?? "").Replace("'", "''") + "', "
                    + "DONT_MOVE_AHEAD=" + (log.DontMoveAhead ? 1 : 0) + ", "
                    + "DUPLICATE_ACTION=" + dupCode + ", "
                    + "MODIFIED_DATE='" + modDate + "'";

                if (hasDontCalcHdth) sql += ", DONT_CALC_HDTH=" + (log.DontCalcHoleDepth ? 1 : 0);
                if (hasNoAutoCalc) sql += ", no_auto_calc=" + (log.DontCalcHoleDepth ? 1 : 0);
                if (hasStartingHdth) sql += ", STARTING_HDTH=" + log.StartingHoleDepth.ToString(CultureInfo.InvariantCulture);
                if (hasStartingHoleDepth) sql += ", starting_hole_depth=" + log.StartingHoleDepth.ToString(CultureInfo.InvariantCulture);

                sql += " WHERE LOG_ID='" + log.ObjectID.Replace("'", "''") + "';";

                objDataService.ExecuteNonQuery(sql);

                // 2. Update summary table if exists
                try
                {
                    if (objDataService.IsRecordExist("SELECT name FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG_SUMMARY';"))
                    {
                        objDataService.ExecuteNonQuery("UPDATE VMX_TIME_LOG_SUMMARY SET LogName='" 
                            + (log.nameLog ?? "").Replace("'", "''") + "' WHERE LogId='" 
                            + log.ObjectID.Replace("'", "''") + "';");
                    }
                }
                catch { }

                // 3. Persist channels into VMX_TIME_LOG_COLUMNS
                bool hasVmxCols = objDataService.IsRecordExist("SELECT name FROM sqlite_master WHERE type='table' AND name='VMX_TIME_LOG_COLUMNS';");
                if (hasVmxCols)
                {
                    objDataService.ExecuteNonQuery("DELETE FROM VMX_TIME_LOG_COLUMNS WHERE LOG_ID='" + log.ObjectID.Replace("'", "''") + "';");

                    int order = 1;
                    foreach (var ch in channels)
                    {
                        int vt = ch.valueType;
                        string insertCol = "INSERT INTO VMX_TIME_LOG_COLUMNS ("
                            + "WELL_ID, WELLBORE_ID, LOG_ID, MNEMONIC, CHANNEL_NAME, DATA_TYPE, "
                            + "UNIT, UNIT_ID, VUMAX_UNIT_ID, VALUE_TYPE, VALUE_QUERY, WITSML_MNEMONIC, "
                            + "CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE, COLUMN_ORDER, "
                            + "OFFSET, NO_INTERPOLATE, WRITE_BACK, PI_MNEMONIC) VALUES ("
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
                            + VBVal(ch.sensorOffset) + ", "
                            + (ch.DoNotInterpol ? 1 : 0) + ", "
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

                // 5. Update log.logCurves in memory
                log.logCurves.Clear();
                foreach (var ch in channels)
                {
                    log.logCurves[ch.Mnemonic] = ch;
                }

                return true;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ":" + ex.StackTrace;
                return false;
            }
        }

        public static bool SaveTimeLog(IDataServiceDIntel objDataService, TimeLog log, ref string lastError)
        {
            return SaveTimeLog(objDataService, log, log.logCurves.Values, ref lastError);
        }

        // --- [NEW LOGIC (updateData: ported from legacy VB to C# and SQLite for updating timelog data tables)] ---
        public static bool updateData(
            IDataServiceDIntel objDataService,
            string WellID,
            string WellboreID,
            string LogID,
            DataTable objData,
            string TimeZone,
            ref string LastError)
        {
            try
            {
                TimeLog? objTimeLog = TimeLog.loadTimeLog(objDataService, WellID, WellboreID, LogID, "");
                var objBulkExecutor = new BulkCommandExecutor(objDataService, 100);

                if (objTimeLog == null)
                {
                    LastError = "Time Log not found";
                    return false;
                }

                string wellDateFormat = Well.getWellDateFormat(objDataService, WellID);
                string dataTableName = TimeLog.getDataTableName(objDataService, WellID, WellboreID, LogID);
                if (string.IsNullOrWhiteSpace(dataTableName))
                {
                    dataTableName = objTimeLog.__dataTableName;
                }

                if (string.IsNullOrWhiteSpace(dataTableName))
                {
                    LastError = "Data table name not found for Time Log";
                    return false;
                }

                string dateTimeIndexMnemonic = objTimeLog.getIndexWITSMLMnemonic();

                DateTime rawDataMinDate = DateTime.MinValue;
                DateTime rawDataMaxDate = DateTime.MinValue;

                if (objData != null && objData.Rows.Count > 0 && objData.Columns.Contains(dateTimeIndexMnemonic))
                {
                    if (wellDateFormat == Well.wDateFormatUTC)
                    {
                        rawDataMinDate = utilFunctions.parseDateToUTC(objData.Rows[0][dateTimeIndexMnemonic]);
                        rawDataMaxDate = utilFunctions.parseDateToUTC(objData.Rows[objData.Rows.Count - 1][dateTimeIndexMnemonic]);
                    }
                    else
                    {
                        rawDataMinDate = utilFunctions.convertDate(objData.Rows[0][dateTimeIndexMnemonic], TimeZone);
                        rawDataMaxDate = utilFunctions.convertDate(objData.Rows[objData.Rows.Count - 1][dateTimeIndexMnemonic], TimeZone);
                    }
                }

                var objSystemSettings = new SystemSettings();
                objSystemSettings.LoadSettings(objDataService);

                // --- Unit Conversion Date Extents Recalculation ---
                if (objTimeLog != null && objData != null && objData.Rows.Count > 0 && objData.Columns.Contains(dateTimeIndexMnemonic))
                {
                    dateTimeIndexMnemonic = objTimeLog.getIndexWITSMLMnemonic();

                    if (wellDateFormat == Well.wDateFormatUTC)
                    {
                        rawDataMinDate = utilFunctions.parseDateToUTC(objData.Rows[0][dateTimeIndexMnemonic]);
                        rawDataMaxDate = utilFunctions.parseDateToUTC(objData.Rows[objData.Rows.Count - 1][dateTimeIndexMnemonic]);
                    }
                    else
                    {
                        rawDataMinDate = utilFunctions.convertDate(objData.Rows[0][dateTimeIndexMnemonic], TimeZone);
                        rawDataMaxDate = utilFunctions.convertDate(objData.Rows[objData.Rows.Count - 1][dateTimeIndexMnemonic], TimeZone);
                    }
                }

                var Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (objData != null)
                {
                    foreach (DataColumn objColumn in objData.Columns)
                    {
                        string WITSMLMnemonic = objColumn.ColumnName;
                        string VuMaxMnemonic = objTimeLog.getVuMaxMnemonic(WITSMLMnemonic);

                        if (!string.IsNullOrWhiteSpace(VuMaxMnemonic) &&
                            !VuMaxMnemonic.Trim().Equals("DATETIME", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!Fields.ContainsKey(VuMaxMnemonic))
                            {
                                Fields.Add(VuMaxMnemonic, WITSMLMnemonic);
                            }
                        }
                    }
                }

                DateTime dtDateTime;

                if (objData != null)
                {
                    foreach (DataRow objRow in objData.Rows)
                    {
                        if (wellDateFormat == Well.wDateFormatUTC)
                        {
                            dtDateTime = utilFunctions.parseDateToUTC(DataService.checkNull(objRow[dateTimeIndexMnemonic], ""));
                        }
                        else
                        {
                            dtDateTime = utilFunctions.convertDate(DataService.checkNull(objRow[dateTimeIndexMnemonic], ""), TimeZone);
                        }

                        if (dtDateTime == DateTime.MinValue) continue;

                        string strSQL = "UPDATE [" + dataTableName + "] SET ";
                        string FieldList = "";

                        foreach (string VuMaxMnemonic in Fields.Keys)
                        {
                            if (!objTimeLog.logCurves.ContainsKey(VuMaxMnemonic)) continue;
                            LogChannel objChannel = objTimeLog.logCurves[VuMaxMnemonic];

                            if (!string.IsNullOrWhiteSpace(objChannel.witsmlMnemonic))
                            {
                                string typeLogData = (objChannel.typeLogData ?? "").ToUpperInvariant().Trim();
                                switch (typeLogData)
                                {
                                    case "DATE TIME":
                                    case "DATETIME":
                                        if (!string.IsNullOrWhiteSpace(DataService.checkNull(objRow[objChannel.witsmlMnemonic], "")))
                                        {
                                            if (wellDateFormat == Well.wDateFormatUTC)
                                            {
                                                FieldList = FieldList + ",[" + objChannel.mnemonic + "]='" + utilFunctions.parseDateToUTC(DataService.checkNull(objRow[objChannel.witsmlMnemonic], "")).ToString("dd-MMM-yyyy HH:mm:ss") + "' ";
                                            }
                                            else
                                            {
                                                FieldList = FieldList + ",[" + objChannel.mnemonic + "]='" + utilFunctions.convertDate(DataService.checkNull(objRow[objChannel.witsmlMnemonic], ""), TimeZone).ToString("dd-MMM-yyyy HH:mm:ss") + "' ";
                                            }
                                        }
                                        else
                                        {
                                            FieldList = FieldList + ",[" + objChannel.mnemonic + "]=NULL ";
                                        }
                                        break;

                                    case "DOUBLE":
                                    case "LONG":
                                    case "FLOAT":
                                    case "INT":
                                    case "SHORT":
                                        var numVal = objRow[objChannel.witsmlMnemonic];
                                        if (numVal == null || numVal == DBNull.Value || string.IsNullOrWhiteSpace(numVal.ToString()))
                                        {
                                            FieldList = FieldList + ",[" + objChannel.mnemonic + "]=NULL ";
                                        }
                                        else if (double.TryParse(numVal.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double dVal))
                                        {
                                            FieldList = FieldList + ",[" + objChannel.mnemonic + "]=" + dVal.ToString(CultureInfo.InvariantCulture) + " ";
                                        }
                                        else
                                        {
                                            FieldList = FieldList + ",[" + objChannel.mnemonic + "]=" + checkNumericNull(numVal).ToString(CultureInfo.InvariantCulture) + " ";
                                        }
                                        break;

                                    case "STRING":
                                    case "STRING40":
                                    case "STRING16":
                                    default:
                                        FieldList = FieldList + ",[" + objChannel.mnemonic + "]='" + DataService.checkNull(objRow[objChannel.witsmlMnemonic], "").Replace("'", "''") + "' ";
                                        break;
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(FieldList))
                        {
                            FieldList = FieldList.Substring(1); // Remove leading comma
                            string dt1 = dtDateTime.ToString("dd-MMM-yyyy HH:mm:ss");
                            string dt2 = dtDateTime.ToString("yyyy-MM-dd HH:mm:ss");
                            strSQL = strSQL + FieldList + " WHERE (DATETIME = '" + dt1 + "' OR DATETIME = '" + dt2 + "');";
                            objBulkExecutor.ExecuteCommandWithPause(strSQL);
                        }
                    }

                    objBulkExecutor.FlushBuffer();
                }

                // --- Unit Conversion Routine ---
                if (objTimeLog != null)
                {
                    foreach (LogChannel objChannel in objTimeLog.logCurves.Values)
                    {
                        string typeLogData = (objChannel.typeLogData ?? "").ToUpperInvariant().Trim();
                        if (typeLogData == "DOUBLE" || typeLogData == "LONG" || typeLogData == "FLOAT" || typeLogData == "INT" || typeLogData == "SHORT")
                        {
                            if (!string.IsNullOrWhiteSpace(objChannel.UnitID) &&
                                !string.IsNullOrWhiteSpace(objChannel.VuMaxUnitID) &&
                                !objChannel.mnemonic.Equals("DEPTH", StringComparison.OrdinalIgnoreCase) &&
                                !objChannel.mnemonic.Equals("HDTH", StringComparison.OrdinalIgnoreCase) &&
                                !objChannel.mnemonic.Equals("NEXT_DEPTH", StringComparison.OrdinalIgnoreCase) &&
                                !objChannel.mnemonic.Equals("FOOTAGE", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!string.Equals(objChannel.UnitID.Trim(), objChannel.VuMaxUnitID.Trim(), StringComparison.OrdinalIgnoreCase))
                                {
                                    if (Fields.ContainsKey(objChannel.mnemonic))
                                    {
                                        TimeLog.updateUnits(objDataService, objTimeLog, objChannel.mnemonic, objChannel.UnitID, objChannel.VuMaxUnitID, rawDataMinDate, rawDataMaxDate);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    LastError = "Couldn't load Time Log";
                    return false;
                }

                updateCalculatedColumns(objDataService, objTimeLog, rawDataMinDate, rawDataMaxDate);

                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message + ex.StackTrace;
                return false;
            }
        }

        // --- [NEW LOGIC (updateData: overload with ref IDataServiceDIntel for legacy compatibility)] ---
        public static bool updateData(
            ref IDataServiceDIntel objDataService,
            string WellID,
            string WellboreID,
            string LogID,
            DataTable objData,
            string TimeZone,
            ref string LastError)
        {
            return updateData(objDataService, WellID, WellboreID, LogID, objData, TimeZone, ref LastError);
        }

        // --- [NEW LOGIC (updateCalculatedColumns: evaluates calculated channel expressions on the SQLite data table)] ---
        public static bool updateCalculatedColumns(IDataServiceDIntel objDataService, TimeLog objTimeLog, DateTime minDate, DateTime maxDate)
        {
            if (objDataService == null || objTimeLog == null || string.IsNullOrWhiteSpace(objTimeLog.__dataTableName))
                return true;

            if (objTimeLog.logCurves == null || objTimeLog.logCurves.Count == 0)
                return true;

            string dataTable = objTimeLog.__dataTableName;
            foreach (var ch in objTimeLog.logCurves.Values)
            {
                if (ch.valueType == 1 && !string.IsNullOrWhiteSpace(ch.valueQuery))
                {
                    try
                    {
                        string sql;
                        if (minDate > DateTime.MinValue && maxDate > DateTime.MinValue)
                        {
                            sql = "UPDATE [" + dataTable + "] SET [" + ch.mnemonic + "] = (" + ch.valueQuery + ") WHERE DATETIME >= '" + minDate.ToString("dd-MMM-yyyy HH:mm:ss") + "' AND DATETIME <= '" + maxDate.ToString("dd-MMM-yyyy HH:mm:ss") + "';";
                        }
                        else
                        {
                            sql = "UPDATE [" + dataTable + "] SET [" + ch.mnemonic + "] = (" + ch.valueQuery + ");";
                        }
                        objDataService.ExecuteNonQuery(sql);
                    }
                    catch { }
                }
            }
            return true;
        }

        #region First and Last Index Optimized (SQLite)

        /// <summary>
        /// Retrieves the first index (MIN_DATE) as an OADate double from VMX_TIME_LOG for the specified time log.
        /// Converted from legacy VB getFirstIndexOptimized using SQLite.
        /// </summary>
        public static double getFirstIndexOptimized(IDataServiceDIntel objDataService, string wellID, string wellboreID, string logID)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(wellID) || string.IsNullOrWhiteSpace(wellboreID) || string.IsNullOrWhiteSpace(logID))
                {
                    return new DateTime().ToOADate();
                }

                string sql = "SELECT MIN_DATE, DATA_TABLE_NAME FROM VMX_TIME_LOG WHERE WELL_ID='" + wellID.Replace("'", "''") +
                             "' AND WELLBORE_ID='" + wellboreID.Replace("'", "''") +
                             "' AND LOG_ID='" + logID.Replace("'", "''") + "';";

                using (DataTable objData = objDataService.GetTable(sql))
                {
                    if (objData != null && objData.Rows.Count > 0)
                    {
                        DateTime dtValue = DataService.checkNull(objData.Rows[0]["MIN_DATE"], new DateTime());
                        if (dtValue == DateTime.MinValue || dtValue == default)
                        {
                            string dataTableName = DataService.checkNull(objData.Rows[0]["DATA_TABLE_NAME"], "");
                            if (!string.IsNullOrWhiteSpace(dataTableName))
                            {
                                dtValue = RigStateService.GetMinDateFromTable(objDataService, dataTableName);
                                if (dtValue != DateTime.MinValue)
                                {
                                    try
                                    {
                                        string updateSql = $"UPDATE VMX_TIME_LOG SET MIN_DATE = '{dtValue:dd-MMM-yyyy HH:mm:ss}' WHERE WELL_ID='{wellID.Replace("'", "''")}' AND WELLBORE_ID='{wellboreID.Replace("'", "''")}' AND LOG_ID='{logID.Replace("'", "''")}';";
                                        objDataService.ExecuteNonQuery(updateSql);
                                    }
                                    catch { }
                                }
                            }
                        }
                        return dtValue.ToOADate();
                    }
                    else
                    {
                        return new DateTime().ToOADate();
                    }
                }
            }
            catch (Exception)
            {
                return new DateTime().ToOADate();
            }
        }

        /// <summary>
        /// Retrieves the last index (MAX_DATE) as an OADate double from VMX_TIME_LOG for the specified time log.
        /// Converted from legacy VB getLastIndexOptimized using SQLite.
        /// If MAX_DATE is missing or corrupted, falls back to the data table and auto-heals VMX_TIME_LOG.
        /// </summary>
        public static double getLastIndexOptimized(IDataServiceDIntel objDataService, string wellID, string wellboreID, string logID)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(wellID) || string.IsNullOrWhiteSpace(wellboreID) || string.IsNullOrWhiteSpace(logID))
                {
                    return new DateTime().ToOADate();
                }

                string sql = "SELECT MAX_DATE, DATA_TABLE_NAME FROM VMX_TIME_LOG WHERE WELL_ID='" + wellID.Replace("'", "''") +
                             "' AND WELLBORE_ID='" + wellboreID.Replace("'", "''") +
                             "' AND LOG_ID='" + logID.Replace("'", "''") + "';";

                using (DataTable objData = objDataService.GetTable(sql))
                {
                    if (objData != null && objData.Rows.Count > 0)
                    {
                        DateTime dtValue = DataService.checkNull(objData.Rows[0]["MAX_DATE"], new DateTime());
                        if (dtValue == DateTime.MinValue || dtValue == default)
                        {
                            string dataTableName = DataService.checkNull(objData.Rows[0]["DATA_TABLE_NAME"], "");
                            if (!string.IsNullOrWhiteSpace(dataTableName))
                            {
                                dtValue = RigStateService.GetMaxDateFromTable(objDataService, dataTableName);
                                if (dtValue != DateTime.MinValue)
                                {
                                    try
                                    {
                                        string updateSql = $"UPDATE VMX_TIME_LOG SET MAX_DATE = '{dtValue:dd-MMM-yyyy HH:mm:ss}' WHERE WELL_ID='{wellID.Replace("'", "''")}' AND WELLBORE_ID='{wellboreID.Replace("'", "''")}' AND LOG_ID='{logID.Replace("'", "''")}';";
                                        objDataService.ExecuteNonQuery(updateSql);
                                    }
                                    catch { }
                                }
                            }
                        }
                        return dtValue.ToOADate();
                    }
                    else
                    {
                        return new DateTime().ToOADate();
                    }
                }
            }
            catch (Exception)
            {
                return new DateTime().ToOADate();
            }
        }

        #endregion






    }//Class
}//namespace

