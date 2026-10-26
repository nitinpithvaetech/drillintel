using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using DrillIntel.Data;

namespace DrillIntel.Data.Objects.DataObjects.Models.DrillingConnectionsProcessor
{
    public class KPIDrillingConnection
    {
        public string WellID { get; set; } = "";
        public double Depth { get; set; } = 0.0;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public double BottomToSlips { get; set; } = 0.0;
        public double SlipsToSlips { get; set; } = 0.0;
        public double SlipsToBottom { get; set; } = 0.0;
        public string Time { get; set; } = "D";
        public string UserComment { get; set; } = "";

        public KPIDrillingConnection getCopy()
        {
            try
            {
                return new KPIDrillingConnection
                {
                    WellID = this.WellID,
                    Depth = this.Depth,
                    FromDate = this.FromDate,
                    ToDate = this.ToDate,
                    BottomToSlips = this.BottomToSlips,
                    SlipsToSlips = this.SlipsToSlips,
                    SlipsToBottom = this.SlipsToBottom,
                    Time = this.Time,
                    UserComment = this.UserComment
                };
            }
            catch
            {
                return new KPIDrillingConnection();
            }
        }

        public static void EnsureTableExists(IDataServiceDIntel objDataService)
        {
            if (objDataService == null) return;
            try
            {
                const string sql = @"
CREATE TABLE IF NOT EXISTS VMX_AKPI_DRLG_CONNECTIONS (
    WELL_ID           TEXT NOT NULL,
    ENTRY_ID          INTEGER NOT NULL,
    DEPTH             REAL,
    BOTTOM_TO_SLIPS   REAL,
    SLIPS_TO_SLIPS    REAL,
    SLIPS_TO_BOTTOM   REAL,
    FROM_DATE         TEXT,
    TO_DATE           TEXT,
    TIME              TEXT,
    USER_COMMENT      TEXT,
    CREATED_BY        TEXT,
    CREATED_DATE      TEXT,
    MODIFIED_BY       TEXT,
    MODIFIED_DATE     TEXT,
    LOCAL_FROM_DATE   TEXT,
    LOCAL_TO_DATE     TEXT,
    PRIMARY KEY (WELL_ID, ENTRY_ID)
);
CREATE INDEX IF NOT EXISTS IX_VMX_AKPI_DRLG_CONN_DATES ON VMX_AKPI_DRLG_CONNECTIONS(WELL_ID, TO_DATE);
CREATE INDEX IF NOT EXISTS IX_VMX_AKPI_DRLG_CONN_DEPTH ON VMX_AKPI_DRLG_CONNECTIONS(WELL_ID, DEPTH);
";
                objDataService.ExecuteNonQuery(sql);
            }
            catch
            {
            }
        }

        public static KPIDrillingConnection? createKPIDrlgConnection(string paramWellID, ConnectionLogPoint paramConnection, IDataServiceDIntel objDataService)
        {
            try
            {
                if (paramConnection == null) return null;

                var objNew = new KPIDrillingConnection
                {
                    WellID = paramWellID,
                    Depth = paramConnection.Depth,
                    FromDate = paramConnection.FromDate,
                    ToDate = paramConnection.ToDate,
                    BottomToSlips = paramConnection.BottomToSlipsSeconds,
                    SlipsToSlips = paramConnection.SlipsToSlipsSeconds,
                    SlipsToBottom = paramConnection.SlipsToBottomSeconds,
                    UserComment = DrlgConnectionLabel.getComment(objDataService, paramConnection.Depth, paramWellID),
                    Time = paramConnection.DayNightStatus == ConnectionLogPoint.cnDayNightTime.DayTime ? "D" : "N"
                };

                return objNew;
            }
            catch
            {
                return null;
            }
        }

        public static bool isDBEntryExist(IDataServiceDIntel objDataService, string paramWellID, ConnectionLogPoint objConnection)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(paramWellID) || objConnection == null)
                    return false;

                EnsureTableExists(objDataService);

                string sql = $"SELECT 1 FROM VMX_AKPI_DRLG_CONNECTIONS WHERE WELL_ID = '{paramWellID.Replace("'", "''")}' AND DEPTH = {objConnection.Depth.ToString(CultureInfo.InvariantCulture)} AND FROM_DATE = '{objConnection.FromDate.ToString("dd-MMM-yyyy HH:mm:ss")}' AND TO_DATE = '{objConnection.ToDate.ToString("dd-MMM-yyyy HH:mm:ss")}' LIMIT 1;";
                return objDataService.IsRecordExist(sql);
            }
            catch
            {
                return false;
            }
        }

        public static bool addToDatabase(IDataServiceDIntel objDataService, KPIDrillingConnection objConnection, ref string lastError)
        {
            try
            {
                if (objDataService == null || objConnection == null || string.IsNullOrWhiteSpace(objConnection.WellID))
                    return false;

                EnsureTableExists(objDataService);

                // Check and delete existing record if already present
                string checkSql = $"SELECT 1 FROM VMX_AKPI_DRLG_CONNECTIONS WHERE WELL_ID = '{objConnection.WellID.Replace("'", "''")}' AND DEPTH = {objConnection.Depth.ToString(CultureInfo.InvariantCulture)} AND FROM_DATE = '{objConnection.FromDate.ToString("dd-MMM-yyyy HH:mm:ss")}' AND TO_DATE = '{objConnection.ToDate.ToString("dd-MMM-yyyy HH:mm:ss")}' LIMIT 1;";
                bool exists = objDataService.IsRecordExist(checkSql);

                if (exists)
                {
                    string delSql = "DELETE FROM VMX_AKPI_DRLG_CONNECTIONS WHERE WELL_ID = @WellID AND DEPTH = @Depth AND FROM_DATE = @FromDate AND TO_DATE = @ToDate;";
                    objDataService.ExecuteNonQuery(delSql, new Dictionary<string, object?>
                    {
                        ["@WellID"] = objConnection.WellID,
                        ["@Depth"] = objConnection.Depth,
                        ["@FromDate"] = objConnection.FromDate.ToString("dd-MMM-yyyy HH:mm:ss"),
                        ["@ToDate"] = objConnection.ToDate.ToString("dd-MMM-yyyy HH:mm:ss")
                    });
                }

                // Generate new EntryID
                long maxId = 0;
                try
                {
                    var val = objDataService.GetValue("SELECT MAX(ENTRY_ID) FROM VMX_AKPI_DRLG_CONNECTIONS WHERE WELL_ID = @WellID;",
                        new Dictionary<string, object?> { ["@WellID"] = objConnection.WellID });
                    if (val != null && val != DBNull.Value)
                    {
                        maxId = Convert.ToInt64(val);
                    }
                }
                catch { }

                long entryId = maxId + 1;

                string insertSql = @"
INSERT INTO VMX_AKPI_DRLG_CONNECTIONS (
    WELL_ID, ENTRY_ID, DEPTH, BOTTOM_TO_SLIPS, SLIPS_TO_SLIPS, SLIPS_TO_BOTTOM,
    FROM_DATE, TO_DATE, TIME, USER_COMMENT,
    CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE,
    LOCAL_FROM_DATE, LOCAL_TO_DATE
) VALUES (
    @WellID, @EntryID, @Depth, @BottomToSlips, @SlipsToSlips, @SlipsToBottom,
    @FromDate, @ToDate, @Time, @UserComment,
    @CreatedBy, @CreatedDate, @ModifiedBy, @ModifiedDate,
    @LocalFromDate, @LocalToDate
);";

                string userName = objDataService.UserName ?? "DrillIntel";
                string now = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");

                return objDataService.ExecuteNonQuery(insertSql, new Dictionary<string, object?>
                {
                    ["@WellID"] = objConnection.WellID,
                    ["@EntryID"] = entryId,
                    ["@Depth"] = objConnection.Depth,
                    ["@BottomToSlips"] = objConnection.BottomToSlips,
                    ["@SlipsToSlips"] = objConnection.SlipsToSlips,
                    ["@SlipsToBottom"] = objConnection.SlipsToBottom,
                    ["@FromDate"] = objConnection.FromDate.ToString("dd-MMM-yyyy HH:mm:ss"),
                    ["@ToDate"] = objConnection.ToDate.ToString("dd-MMM-yyyy HH:mm:ss"),
                    ["@Time"] = objConnection.Time ?? "D",
                    ["@UserComment"] = objConnection.UserComment ?? "",
                    ["@CreatedBy"] = userName,
                    ["@CreatedDate"] = now,
                    ["@ModifiedBy"] = userName,
                    ["@ModifiedDate"] = now,
                    ["@LocalFromDate"] = objConnection.FromDate.ToLocalTime().ToString("dd-MMM-yyyy HH:mm:ss"),
                    ["@LocalToDate"] = objConnection.ToDate.ToLocalTime().ToString("dd-MMM-yyyy HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                lastError = ex.Message + Environment.NewLine + ex.StackTrace;
                return false;
            }
        }

        public static bool clearConnectionsFromDB(IDataServiceDIntel objDataService, string paramWellID, ref string lastError)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(paramWellID))
                    return false;

                EnsureTableExists(objDataService);

                string sql = "DELETE FROM VMX_AKPI_DRLG_CONNECTIONS WHERE WELL_ID = @WellID;";
                return objDataService.ExecuteNonQuery(sql, new Dictionary<string, object?>
                {
                    ["@WellID"] = paramWellID
                });
            }
            catch (Exception ex)
            {
                lastError = ex.Message + Environment.NewLine + ex.StackTrace;
                return false;
            }
        }

        public static bool removeExcludedConnections(IDataServiceDIntel objDataService, string paramWellID, ref string lastError)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(paramWellID))
                    return true;

                // Check if VMX_CONN_INFO exists
                bool connInfoExists = objDataService.IsRecordExist(
                    "SELECT 1 FROM sqlite_master WHERE type='table' AND name='VMX_CONN_INFO';");
                if (!connInfoExists) return true;

                DataTable objData = objDataService.GetTable(
                    "SELECT DEPTH FROM VMX_CONN_INFO WHERE WELL_ID = @WellID;",
                    new Dictionary<string, object?> { ["@WellID"] = paramWellID });

                if (objData != null && objData.Rows.Count > 0)
                {
                    foreach (DataRow objRow in objData.Rows)
                    {
                        double depth = Convert.ToDouble(
                            DrillIntel.Data.Objects.DataObjects.Models.DataService.checkNull(objRow["DEPTH"], 0),
                            CultureInfo.InvariantCulture);

                        string delSql = "DELETE FROM VMX_AKPI_DRLG_CONNECTIONS WHERE WELL_ID = @WellID AND DEPTH = @Depth;";
                        objDataService.ExecuteNonQuery(delSql, new Dictionary<string, object?>
                        {
                            ["@WellID"] = paramWellID,
                            ["@Depth"] = depth
                        });
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + Environment.NewLine + ex.StackTrace;
                return false;
            }
        }
    }
}
