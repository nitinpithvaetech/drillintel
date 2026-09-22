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

                DataTable dt = objDataService.GetTable("SELECT * FROM VMX_TIME_LOG WHERE WELL_ID='" 
                    + wellID.Replace("'", "''") + "' AND WELLBORE_ID='" 
                    + wellboreID.Replace("'", "''") + "' AND LOG_ID='" 
                    + logID.Replace("'", "''") + "';");

                if (dt != null && dt.Rows.Count > 0)
                {
                    var log = MapRowToTimeLog(dt.Rows[0]);
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

    }//Class
}//namespace

