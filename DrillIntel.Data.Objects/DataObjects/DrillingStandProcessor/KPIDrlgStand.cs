using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using DrillIntel.Data;

namespace DrillIntel.Data.Objects.DataObjects.DrillingStandProcessor
{
    public class KPIDrlgStand
    {
        public string WellID { get; set; } = "";
        public long EntryID { get; set; } = 0;
        public double Depth { get; set; } = 0.0;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public string UserComment { get; set; } = "";
        public string Time { get; set; } = "D";
        public double ROP { get; set; } = 0.0;
        public double RotaryROP { get; set; } = 0.0;
        public double SlideROP { get; set; } = 0.0;

        public KPIDrlgStand getCopy()
        {
            try
            {
                return new KPIDrlgStand
                {
                    WellID = this.WellID,
                    EntryID = this.EntryID,
                    Depth = this.Depth,
                    FromDate = this.FromDate,
                    ToDate = this.ToDate,
                    UserComment = this.UserComment,
                    Time = this.Time,
                    ROP = this.ROP,
                    RotaryROP = this.RotaryROP,
                    SlideROP = this.SlideROP
                };
            }
            catch
            {
                return new KPIDrlgStand();
            }
        }

        public static void EnsureTableExists(IDataServiceDIntel objDataService)
        {
            if (objDataService == null) return;
            try
            {
                const string sql = @"
CREATE TABLE IF NOT EXISTS VMX_AKPI_DRLG_STAND (
    WELL_ID           TEXT NOT NULL,
    ENTRY_ID          INTEGER NOT NULL,
    DEPTH             REAL,
    ROP               REAL,
    ROTARY_ROP        REAL,
    SLIDE_ROP         REAL,
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
CREATE INDEX IF NOT EXISTS IX_VMX_AKPI_DRLG_STAND_DATES ON VMX_AKPI_DRLG_STAND(WELL_ID, TO_DATE);
CREATE INDEX IF NOT EXISTS IX_VMX_AKPI_DRLG_STAND_DEPTH ON VMX_AKPI_DRLG_STAND(WELL_ID, DEPTH);
";
                objDataService.ExecuteNonQuery(sql);
            }
            catch
            {
            }
        }

        public static KPIDrlgStand? createKPIDrlgStand(string wellId, StandPoint stand, IDataServiceDIntel objDataService)
        {
            try
            {
                if (stand == null) return null;

                var objNew = new KPIDrlgStand
                {
                    WellID = wellId,
                    Depth = stand.Depth,
                    FromDate = stand.FromDate,
                    ToDate = stand.ToDate,
                    ROP = stand.ROP,
                    RotaryROP = stand.RotaryROP,
                    SlideROP = stand.SlideROP,
                    UserComment = "",
                    Time = stand.DayNightStatus == StandPoint.cnDayNightTime.DayTime ? "D" : "N"
                };

                return objNew;
            }
            catch
            {
                return null;
            }
        }

        public static bool isDBEntryExist(IDataServiceDIntel objDataService, string paramWellID, StandPoint objStand)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(paramWellID) || objStand == null)
                    return false;

                EnsureTableExists(objDataService);

                string sql = $"SELECT 1 FROM VMX_AKPI_DRLG_STAND WHERE WELL_ID = '{paramWellID.Replace("'", "''")}' AND DEPTH = {objStand.Depth.ToString(CultureInfo.InvariantCulture)} AND FROM_DATE = '{objStand.FromDate.ToString("dd-MMM-yyyy HH:mm:ss")}' AND TO_DATE = '{objStand.ToDate.ToString("dd-MMM-yyyy HH:mm:ss")}' LIMIT 1;";
                return objDataService.IsRecordExist(sql);
            }
            catch
            {
                return false;
            }
        }

        public static bool addToDatabase(IDataServiceDIntel objDataService, KPIDrlgStand objStand, ref long startingEntryID, ref string lastError)
        {
            try
            {
                if (objDataService == null || objStand == null || string.IsNullOrWhiteSpace(objStand.WellID))
                    return false;

                EnsureTableExists(objDataService);

                startingEntryID++;
                long entryId = startingEntryID;

                string insertSql = @"
INSERT INTO VMX_AKPI_DRLG_STAND (
    WELL_ID, ENTRY_ID, DEPTH, ROP, ROTARY_ROP, SLIDE_ROP,
    FROM_DATE, TO_DATE, TIME, USER_COMMENT,
    CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE,
    LOCAL_FROM_DATE, LOCAL_TO_DATE
) VALUES (
    @WellID, @EntryID, @Depth, @ROP, @RotaryROP, @SlideROP,
    @FromDate, @ToDate, @Time, @UserComment,
    @CreatedBy, @CreatedDate, @ModifiedBy, @ModifiedDate,
    @LocalFromDate, @LocalToDate
);";

                string userName = objDataService.UserName ?? "DrillIntel";
                string now = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");

                return objDataService.ExecuteNonQuery(insertSql, new Dictionary<string, object?>
                {
                    ["@WellID"] = objStand.WellID,
                    ["@EntryID"] = entryId,
                    ["@Depth"] = objStand.Depth,
                    ["@ROP"] = objStand.ROP,
                    ["@RotaryROP"] = objStand.RotaryROP,
                    ["@SlideROP"] = objStand.SlideROP,
                    ["@FromDate"] = objStand.FromDate.ToString("dd-MMM-yyyy HH:mm:ss"),
                    ["@ToDate"] = objStand.ToDate.ToString("dd-MMM-yyyy HH:mm:ss"),
                    ["@Time"] = objStand.Time ?? "D",
                    ["@UserComment"] = objStand.UserComment ?? "",
                    ["@CreatedBy"] = userName,
                    ["@CreatedDate"] = now,
                    ["@ModifiedBy"] = userName,
                    ["@ModifiedDate"] = now,
                    ["@LocalFromDate"] = objStand.FromDate.ToLocalTime().ToString("dd-MMM-yyyy HH:mm:ss"),
                    ["@LocalToDate"] = objStand.ToDate.ToLocalTime().ToString("dd-MMM-yyyy HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                lastError = ex.Message + Environment.NewLine + ex.StackTrace;
                return false;
            }
        }

        public static bool clearStandsFromDB(IDataServiceDIntel objDataService, string paramWellID, ref string lastError)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(paramWellID))
                    return false;

                EnsureTableExists(objDataService);

                string sql = "DELETE FROM VMX_AKPI_DRLG_STAND WHERE WELL_ID = @WellID;";
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

        public static int GetStandsCount(IDataServiceDIntel objDataService, string wellId)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(wellId)) return 0;
                EnsureTableExists(objDataService);
                var val = objDataService.GetValue(
                    "SELECT COUNT(*) FROM VMX_AKPI_DRLG_STAND WHERE WELL_ID = @WellID;",
                    new Dictionary<string, object?> { ["@WellID"] = wellId });
                return val != null && int.TryParse(val.ToString(), out int c) ? c : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}

