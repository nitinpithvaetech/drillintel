using System;
using System.Collections.Generic;
using System.Data;
using DrillIntel.Data;

namespace DrillIntel.Data.Objects.DataObjects.Models.DrillingConnectionsProcessor
{
    public class DrlgConnectionLabel
    {
        public double Depth { get; set; } = 0.0;
        public string Comment { get; set; } = "";

        public DrlgConnectionLabel getCopy()
        {
            try
            {
                return new DrlgConnectionLabel
                {
                    Depth = this.Depth,
                    Comment = this.Comment
                };
            }
            catch
            {
                return new DrlgConnectionLabel();
            }
        }

        public static void EnsureTableExists(IDataServiceDIntel objDataService)
        {
            if (objDataService == null) return;
            try
            {
                const string sql = @"
CREATE TABLE IF NOT EXISTS VMX_CON_ANNOTATIONS (
    WELL_ID      TEXT NOT NULL,
    DEPTH        REAL NOT NULL,
    COMMENT      TEXT,
    CREATED_BY   TEXT,
    CREATED_DATE TEXT,
    MODIFIED_BY  TEXT,
    MODIFIED_DATE TEXT,
    PRIMARY KEY (WELL_ID, DEPTH)
);";
                objDataService.ExecuteNonQuery(sql);
            }
            catch
            {
            }
        }

        public static void saveConnectionLabel(IDataServiceDIntel objDataService, DrlgConnectionLabel objItem, string paramWellID, ref string lastError)
        {
            try
            {
                if (objDataService == null || objItem == null || string.IsNullOrWhiteSpace(paramWellID))
                    return;

                EnsureTableExists(objDataService);

                int roundedDepth = (int)Math.Round(Math.Floor(objItem.Depth));

                string deleteSql = "DELETE FROM VMX_CON_ANNOTATIONS WHERE WELL_ID = @WellID AND DEPTH = @Depth;";
                objDataService.ExecuteNonQuery(deleteSql, new Dictionary<string, object?>
                {
                    ["@WellID"] = paramWellID,
                    ["@Depth"] = roundedDepth
                });

                string insertSql = @"
INSERT INTO VMX_CON_ANNOTATIONS (WELL_ID, DEPTH, COMMENT, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE)
VALUES (@WellID, @Depth, @Comment, @CreatedBy, @CreatedDate, @ModifiedBy, @ModifiedDate);";

                string userName = objDataService.UserName ?? "DrillIntel";
                string now = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");

                objDataService.ExecuteNonQuery(insertSql, new Dictionary<string, object?>
                {
                    ["@WellID"] = paramWellID,
                    ["@Depth"] = roundedDepth,
                    ["@Comment"] = objItem.Comment ?? "",
                    ["@CreatedBy"] = userName,
                    ["@CreatedDate"] = now,
                    ["@ModifiedBy"] = userName,
                    ["@ModifiedDate"] = now
                });
            }
            catch (Exception ex)
            {
                lastError = ex.Message + Environment.NewLine + ex.StackTrace;
            }
        }

        public static string getComment(IDataServiceDIntel objDataService, double paramDepth, string paramWellID)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(paramWellID))
                    return "";

                EnsureTableExists(objDataService);

                int roundedDepth = (int)Math.Round(Math.Floor(paramDepth));

                string sql = "SELECT COMMENT FROM VMX_CON_ANNOTATIONS WHERE WELL_ID = @WellID AND DEPTH = @Depth LIMIT 1;";
                DataTable objData = objDataService.GetTable(sql, new Dictionary<string, object?>
                {
                    ["@WellID"] = paramWellID,
                    ["@Depth"] = roundedDepth
                });

                if (objData != null && objData.Rows.Count > 0)
                {
                    return DrillIntel.Data.Objects.DataObjects.Models.DataService.checkNull(objData.Rows[0]["COMMENT"], "").ToString()!;
                }

                return "";
            }
            catch
            {
                return "";
            }
        }
    }
}

