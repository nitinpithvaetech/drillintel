using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using DrillIntel.Data;

namespace DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor
{
    public class WellPUSOSettings
    {
        public string WellID = "";

        public string PUChannel = "SPPA";
        public double PUPumpCutOff = 99;
        public double PURPMCutOff = 12;
        public double PUMaxMovement = 70;
        public double PUMinMovement = 5;
        public int PUDynMethod = 0;
        public bool PUFilterMaxLocal = false;

        public string SOChannel = "SPPA";
        public double SOPumpCutOff = 99;
        public double SORPMCutOff = 12;
        public double SOMaxMovement = 70;
        public double SOMinMovement = 5;
        public int SODynMethod = 0;
        public bool SOFilterMaxLocal = false;

        public string ROTChannel = "SPPA";
        public double ROTPumpCutOff = 99;
        public double ROTMinRPM = 12;
        public double ROTMaxRPM = 30;
        public double ROTTolerance = 1;
        public int ROTPoints = 1;
        public int DepthMovementTime = 1;
        public string RunNo = "";

        public string ModifiedBy = "";
        public string ModifiedDate = "";

        public WellPUSOSettings()
        {
        }

        public WellPUSOSettings(string wellId)
        {
            WellID = wellId ?? string.Empty;
        }

        public const string CreateTableSql = @"
CREATE TABLE IF NOT EXISTS VMX_WELL_PUSO_SETTINGS (
    WELL_ID                 TEXT NOT NULL PRIMARY KEY,
    PU_CHANNEL              TEXT,
    PU_PUMP_CUTOFF          REAL,
    PU_RPM_CUTOFF           REAL,
    PU_MAX_MOVEMENT         REAL,
    PU_MIN_MOVEMENT         REAL,
    PU_DYN_METHOD           INTEGER,
    PU_FILTER_LOCAL_MAX     INTEGER,
    SO_CHANNEL              TEXT,
    SO_PUMP_CUTOFF          REAL,
    SO_RPM_CUTOFF           REAL,
    SO_MAX_MOVEMENT         REAL,
    SO_MIN_MOVEMENT         REAL,
    SO_DYN_METHOD           INTEGER,
    SO_FILTER_LOCAL_MAX     INTEGER,
    ROT_CHANNEL             TEXT,
    ROT_PUMP_CUTOFF         REAL,
    ROT_MIN_RPM             REAL,
    ROT_MAX_RPM             REAL,
    ROT_TOLERANCE           REAL,
    ROT_POINTS              REAL,
    DEPTH_MOVEMENT_TIME     REAL,
    MODIFIED_BY             TEXT,
    MODIFIED_DATE           TEXT,
    RUN_NO                  TEXT
);
CREATE INDEX IF NOT EXISTS IDX_VMX_WELL_PUSO_WELL ON VMX_WELL_PUSO_SETTINGS(WELL_ID);
";

        public static void EnsureTableExists(IDataServiceDIntel? db)
        {
            if (db == null) return;
            try { db.ExecuteNonQuery(CreateTableSql); } catch { }
        }

        public static void EnsureTableExists(DbConnection? conn, DbTransaction? transaction = null)
        {
            if (conn == null) return;
            try
            {
                if (conn.State != ConnectionState.Open) conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = CreateTableSql;
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        public static bool SaveSettings(IDataServiceDIntel objDataService, WellPUSOSettings objSettings, string userName = "")
        {
            if (objDataService == null || objSettings == null) return false;

            try
            {
                EnsureTableExists(objDataService);

                string delSql = "DELETE FROM VMX_WELL_PUSO_SETTINGS WHERE WELL_ID = @WELL_ID";
                objDataService.ExecuteNonQuery(delSql, new Dictionary<string, object?> { ["@WELL_ID"] = objSettings.WellID });

                string insSql = @"INSERT INTO VMX_WELL_PUSO_SETTINGS (
    WELL_ID, PU_CHANNEL, PU_PUMP_CUTOFF, PU_RPM_CUTOFF, PU_MAX_MOVEMENT, PU_MIN_MOVEMENT, PU_DYN_METHOD, PU_FILTER_LOCAL_MAX,
    SO_CHANNEL, SO_PUMP_CUTOFF, SO_RPM_CUTOFF, SO_MAX_MOVEMENT, SO_MIN_MOVEMENT, SO_DYN_METHOD, SO_FILTER_LOCAL_MAX,
    ROT_CHANNEL, ROT_PUMP_CUTOFF, ROT_MIN_RPM, ROT_MAX_RPM, ROT_TOLERANCE, ROT_POINTS, DEPTH_MOVEMENT_TIME, MODIFIED_BY, MODIFIED_DATE, RUN_NO
) VALUES (
    @WELL_ID, @PU_CHANNEL, @PU_PUMP_CUTOFF, @PU_RPM_CUTOFF, @PU_MAX_MOVEMENT, @PU_MIN_MOVEMENT, @PU_DYN_METHOD, @PU_FILTER_LOCAL_MAX,
    @SO_CHANNEL, @SO_PUMP_CUTOFF, @SO_RPM_CUTOFF, @SO_MAX_MOVEMENT, @SO_MIN_MOVEMENT, @SO_DYN_METHOD, @SO_FILTER_LOCAL_MAX,
    @ROT_CHANNEL, @ROT_PUMP_CUTOFF, @ROT_MIN_RPM, @ROT_MAX_RPM, @ROT_TOLERANCE, @ROT_POINTS, @DEPTH_MOVEMENT_TIME, @MODIFIED_BY, @MODIFIED_DATE, @RUN_NO
)";
                string user = !string.IsNullOrWhiteSpace(userName) ? userName : (objDataService.UserName ?? Environment.UserName);
                string now = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);

                var p = objSettings.ToParameters(user, now);
                return objDataService.ExecuteNonQuery(insSql, p);
            }
            catch
            {
                return false;
            }
        }

        public static bool SaveSettings(DbConnection conn, WellPUSOSettings objSettings, string userName = "")
        {
            if (conn == null || objSettings == null) return false;

            try
            {
                if (conn.State != ConnectionState.Open) conn.Open();
                EnsureTableExists(conn);

                using var delCmd = conn.CreateCommand();
                delCmd.CommandText = "DELETE FROM VMX_WELL_PUSO_SETTINGS WHERE WELL_ID = @WELL_ID";
                var pWell = delCmd.CreateParameter();
                pWell.ParameterName = "@WELL_ID";
                pWell.Value = objSettings.WellID;
                delCmd.Parameters.Add(pWell);
                delCmd.ExecuteNonQuery();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO VMX_WELL_PUSO_SETTINGS (
    WELL_ID, PU_CHANNEL, PU_PUMP_CUTOFF, PU_RPM_CUTOFF, PU_MAX_MOVEMENT, PU_MIN_MOVEMENT, PU_DYN_METHOD, PU_FILTER_LOCAL_MAX,
    SO_CHANNEL, SO_PUMP_CUTOFF, SO_RPM_CUTOFF, SO_MAX_MOVEMENT, SO_MIN_MOVEMENT, SO_DYN_METHOD, SO_FILTER_LOCAL_MAX,
    ROT_CHANNEL, ROT_PUMP_CUTOFF, ROT_MIN_RPM, ROT_MAX_RPM, ROT_TOLERANCE, ROT_POINTS, DEPTH_MOVEMENT_TIME, MODIFIED_BY, MODIFIED_DATE, RUN_NO
) VALUES (
    @WELL_ID, @PU_CHANNEL, @PU_PUMP_CUTOFF, @PU_RPM_CUTOFF, @PU_MAX_MOVEMENT, @PU_MIN_MOVEMENT, @PU_DYN_METHOD, @PU_FILTER_LOCAL_MAX,
    @SO_CHANNEL, @SO_PUMP_CUTOFF, @SO_RPM_CUTOFF, @SO_MAX_MOVEMENT, @SO_MIN_MOVEMENT, @SO_DYN_METHOD, @SO_FILTER_LOCAL_MAX,
    @ROT_CHANNEL, @ROT_PUMP_CUTOFF, @ROT_MIN_RPM, @ROT_MAX_RPM, @ROT_TOLERANCE, @ROT_POINTS, @DEPTH_MOVEMENT_TIME, @MODIFIED_BY, @MODIFIED_DATE, @RUN_NO
)";

                string user = !string.IsNullOrWhiteSpace(userName) ? userName : Environment.UserName;
                string now = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                var dict = objSettings.ToParameters(user, now);

                foreach (var kvp in dict)
                {
                    var param = cmd.CreateParameter();
                    param.ParameterName = kvp.Key;
                    param.Value = kvp.Value ?? DBNull.Value;
                    cmd.Parameters.Add(param);
                }

                cmd.ExecuteNonQuery();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void saveSettings(IDataServiceDIntel objDataService, WellPUSOSettings objSettings)
        {
            SaveSettings(objDataService, objSettings);
        }

        public static WellPUSOSettings LoadSettings(IDataServiceDIntel objDataService, string paramWellID)
        {
            if (objDataService == null || string.IsNullOrWhiteSpace(paramWellID))
                return new WellPUSOSettings(paramWellID ?? string.Empty);

            try
            {
                EnsureTableExists(objDataService);

                string sql = "SELECT * FROM VMX_WELL_PUSO_SETTINGS WHERE WELL_ID = @WELL_ID";
                DataTable dt = objDataService.GetTable(sql, new Dictionary<string, object?> { ["@WELL_ID"] = paramWellID.Trim() });

                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapFromRow(dt.Rows[0], paramWellID);
                }

                return new WellPUSOSettings(paramWellID);
            }
            catch
            {
                return new WellPUSOSettings(paramWellID);
            }
        }

        public static WellPUSOSettings LoadSettings(DbConnection conn, string paramWellID)
        {
            if (conn == null || string.IsNullOrWhiteSpace(paramWellID))
                return new WellPUSOSettings(paramWellID ?? string.Empty);

            try
            {
                if (conn.State != ConnectionState.Open) conn.Open();
                EnsureTableExists(conn);

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM VMX_WELL_PUSO_SETTINGS WHERE WELL_ID = @WELL_ID";
                var p = cmd.CreateParameter();
                p.ParameterName = "@WELL_ID";
                p.Value = paramWellID.Trim();
                cmd.Parameters.Add(p);

                using var reader = cmd.ExecuteReader();
                var dt = new DataTable();
                dt.Load(reader);

                if (dt.Rows.Count > 0)
                {
                    return MapFromRow(dt.Rows[0], paramWellID);
                }

                return new WellPUSOSettings(paramWellID);
            }
            catch
            {
                return new WellPUSOSettings(paramWellID);
            }
        }

        public static WellPUSOSettings loadSettings(IDataServiceDIntel objDataService, string paramWellID)
        {
            return LoadSettings(objDataService, paramWellID);
        }

        public WellPUSOSettings GetCopy()
        {
            return new WellPUSOSettings(WellID)
            {
                PUChannel = this.PUChannel,
                PUPumpCutOff = this.PUPumpCutOff,
                PURPMCutOff = this.PURPMCutOff,
                PUMaxMovement = this.PUMaxMovement,
                PUMinMovement = this.PUMinMovement,
                PUDynMethod = this.PUDynMethod,
                PUFilterMaxLocal = this.PUFilterMaxLocal,
                SOChannel = this.SOChannel,
                SOPumpCutOff = this.SOPumpCutOff,
                SORPMCutOff = this.SORPMCutOff,
                SOMaxMovement = this.SOMaxMovement,
                SOMinMovement = this.SOMinMovement,
                SODynMethod = this.SODynMethod,
                SOFilterMaxLocal = this.SOFilterMaxLocal,
                ROTChannel = this.ROTChannel,
                ROTPumpCutOff = this.ROTPumpCutOff,
                ROTMinRPM = this.ROTMinRPM,
                ROTMaxRPM = this.ROTMaxRPM,
                ROTTolerance = this.ROTTolerance,
                ROTPoints = this.ROTPoints,
                DepthMovementTime = this.DepthMovementTime,
                RunNo = this.RunNo,
                ModifiedBy = this.ModifiedBy,
                ModifiedDate = this.ModifiedDate
            };
        }

        public WellPUSOSettings getCopy() => GetCopy();
        public WellPUSOSettings Clone() => GetCopy();

        public Dictionary<string, object?> ToParameters(string user, string now)
        {
            return new Dictionary<string, object?>
            {
                ["@WELL_ID"] = WellID ?? string.Empty,
                ["@PU_CHANNEL"] = PUChannel ?? string.Empty,
                ["@PU_PUMP_CUTOFF"] = PUPumpCutOff,
                ["@PU_RPM_CUTOFF"] = PURPMCutOff,
                ["@PU_MAX_MOVEMENT"] = PUMaxMovement,
                ["@PU_MIN_MOVEMENT"] = PUMinMovement,
                ["@PU_DYN_METHOD"] = PUDynMethod,
                ["@PU_FILTER_LOCAL_MAX"] = PUFilterMaxLocal ? 1 : 0,
                ["@SO_CHANNEL"] = SOChannel ?? string.Empty,
                ["@SO_PUMP_CUTOFF"] = SOPumpCutOff,
                ["@SO_RPM_CUTOFF"] = SORPMCutOff,
                ["@SO_MAX_MOVEMENT"] = SOMaxMovement,
                ["@SO_MIN_MOVEMENT"] = SOMinMovement,
                ["@SO_DYN_METHOD"] = SODynMethod,
                ["@SO_FILTER_LOCAL_MAX"] = SOFilterMaxLocal ? 1 : 0,
                ["@ROT_CHANNEL"] = ROTChannel ?? string.Empty,
                ["@ROT_PUMP_CUTOFF"] = ROTPumpCutOff,
                ["@ROT_MIN_RPM"] = ROTMinRPM,
                ["@ROT_MAX_RPM"] = ROTMaxRPM,
                ["@ROT_TOLERANCE"] = ROTTolerance,
                ["@ROT_POINTS"] = ROTPoints,
                ["@DEPTH_MOVEMENT_TIME"] = DepthMovementTime,
                ["@MODIFIED_BY"] = user ?? string.Empty,
                ["@MODIFIED_DATE"] = now ?? string.Empty,
                ["@RUN_NO"] = RunNo ?? string.Empty
            };
        }

        private static WellPUSOSettings MapFromRow(DataRow objRow, string paramWellID)
        {
            var s = new WellPUSOSettings(paramWellID)
            {
                PUChannel = DataService.checkNull(objRow["PU_CHANNEL"], "SPPA"),
                PUPumpCutOff = DataService.checkNull(objRow["PU_PUMP_CUTOFF"], 99.0),
                PURPMCutOff = DataService.checkNull(objRow["PU_RPM_CUTOFF"], 12.0),
                PUMaxMovement = DataService.checkNull(objRow["PU_MAX_MOVEMENT"], 70.0),
                PUMinMovement = DataService.checkNull(objRow["PU_MIN_MOVEMENT"], 5.0),
                PUDynMethod = DataService.checkNull(objRow["PU_DYN_METHOD"], 0),
                PUFilterMaxLocal = DataService.checkNull(objRow["PU_FILTER_LOCAL_MAX"], 0) == 1,

                SOChannel = DataService.checkNull(objRow["SO_CHANNEL"], "SPPA"),
                SOPumpCutOff = DataService.checkNull(objRow["SO_PUMP_CUTOFF"], 99.0),
                SORPMCutOff = DataService.checkNull(objRow["SO_RPM_CUTOFF"], 12.0),
                SOMaxMovement = DataService.checkNull(objRow["SO_MAX_MOVEMENT"], 70.0),
                SOMinMovement = DataService.checkNull(objRow["SO_MIN_MOVEMENT"], 5.0),
                SODynMethod = DataService.checkNull(objRow["SO_DYN_METHOD"], 0),
                SOFilterMaxLocal = DataService.checkNull(objRow["SO_FILTER_LOCAL_MAX"], 0) == 1,

                ROTChannel = DataService.checkNull(objRow["ROT_CHANNEL"], "SPPA"),
                ROTPumpCutOff = DataService.checkNull(objRow["ROT_PUMP_CUTOFF"], 99.0),
                ROTMinRPM = DataService.checkNull(objRow["ROT_MIN_RPM"], 12.0),
                ROTMaxRPM = DataService.checkNull(objRow["ROT_MAX_RPM"], 30.0),
                ROTTolerance = DataService.checkNull(objRow["ROT_TOLERANCE"], 1.0),
                ROTPoints = DataService.checkNull(objRow["ROT_POINTS"], 1),
                DepthMovementTime = DataService.checkNull(objRow["DEPTH_MOVEMENT_TIME"], 1),
                RunNo = DataService.checkNull(objRow["RUN_NO"], ""),
                ModifiedBy = DataService.checkNull(objRow["MODIFIED_BY"], ""),
                ModifiedDate = DataService.checkNull(objRow["MODIFIED_DATE"], "")
            };
            return s;
        }
    }
}

