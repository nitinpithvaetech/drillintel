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
        public string WellID { get; set; } = "";

        public string PUChannel { get; set; } = "SPPA";
        public double PUPumpCutOff { get; set; } = 99;
        public double PURPMCutOff { get; set; } = 12;
        public double PUMaxMovement { get; set; } = 70;
        public double PUMinMovement { get; set; } = 5;
        public int PUDynMethod { get; set; } = 0;
        public bool PUFilterMaxLocal { get; set; } = false;

        public string SOChannel { get; set; } = "SPPA";
        public double SOPumpCutOff { get; set; } = 99;
        public double SORPMCutOff { get; set; } = 12;
        public double SOMaxMovement { get; set; } = 70;
        public double SOMinMovement { get; set; } = 5;
        public int SODynMethod { get; set; } = 0;
        public bool SOFilterMaxLocal { get; set; } = false;

        public string ROTChannel { get; set; } = "SPPA";
        public double ROTPumpCutOff { get; set; } = 99;
        public double ROTMinRPM { get; set; } = 12;
        public double ROTMaxRPM { get; set; } = 30;
        public double ROTTolerance { get; set; } = 1;
        public int ROTPoints { get; set; } = 1;
        public int DepthMovementTime { get; set; } = 1;
        public string RunNo { get; set; } = "";

        public string ModifiedBy { get; set; } = "";
        public DateTime ModifiedDate { get; set; } = DateTime.Now;

        public WellPUSOSettings() { }

        public WellPUSOSettings(string wellId)
        {
            WellID = wellId;
        }

        public WellPUSOSettings GetCopy()
        {
            return new WellPUSOSettings
            {
                WellID = this.WellID,
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

        public static void EnsureTableExists(IDataServiceDIntel objDataService)
        {
            if (objDataService == null) return;
            try
            {
                objDataService.ExecuteNonQuery(@"
                    CREATE TABLE IF NOT EXISTS VMX_WELL_PUSO_SETTINGS (
                        WELL_ID TEXT PRIMARY KEY,
                        PU_CHANNEL TEXT, PU_PUMP_CUTOFF REAL, PU_RPM_CUTOFF REAL, PU_MAX_MOVEMENT REAL, PU_MIN_MOVEMENT REAL, PU_DYN_METHOD INTEGER, PU_FILTER_LOCAL_MAX INTEGER,
                        SO_CHANNEL TEXT, SO_PUMP_CUTOFF REAL, SO_RPM_CUTOFF REAL, SO_MAX_MOVEMENT REAL, SO_MIN_MOVEMENT REAL, SO_DYN_METHOD INTEGER, SO_FILTER_LOCAL_MAX INTEGER,
                        ROT_CHANNEL TEXT, ROT_PUMP_CUTOFF REAL, ROT_MIN_RPM REAL, ROT_MAX_RPM REAL, ROT_TOLERANCE REAL, ROT_POINTS INTEGER, DEPTH_MOVEMENT_TIME INTEGER,
                        MODIFIED_BY TEXT, MODIFIED_DATE TEXT, RUN_NO TEXT
                    );");
            }
            catch { }
        }

        public static bool SaveSettings(IDataServiceDIntel objDataService, WellPUSOSettings objSettings)
        {
            if (objDataService == null || objSettings == null) return false;
            try
            {
                EnsureTableExists(objDataService);

                string safeWellId = objSettings.WellID.Replace("'", "''");
                objDataService.ExecuteNonQuery($"DELETE FROM VMX_WELL_PUSO_SETTINGS WHERE WELL_ID='{safeWellId}';");

                string user = !string.IsNullOrWhiteSpace(objSettings.ModifiedBy)
                    ? objSettings.ModifiedBy
                    : (!string.IsNullOrWhiteSpace(objDataService.UserName) ? objDataService.UserName : "SYSTEM");

                string strSQL = "INSERT INTO VMX_WELL_PUSO_SETTINGS " +
                    "(WELL_ID,PU_CHANNEL,PU_PUMP_CUTOFF,PU_RPM_CUTOFF,PU_MAX_MOVEMENT,PU_MIN_MOVEMENT,PU_DYN_METHOD,PU_FILTER_LOCAL_MAX," +
                    "SO_CHANNEL,SO_PUMP_CUTOFF,SO_RPM_CUTOFF,SO_MAX_MOVEMENT,SO_MIN_MOVEMENT,SO_DYN_METHOD,SO_FILTER_LOCAL_MAX," +
                    "ROT_CHANNEL,ROT_PUMP_CUTOFF,ROT_MIN_RPM,ROT_MAX_RPM,ROT_TOLERANCE,ROT_POINTS,DEPTH_MOVEMENT_TIME,MODIFIED_BY,MODIFIED_DATE,RUN_NO) VALUES("
                    + $"'{safeWellId}',"
                    + $"'{objSettings.PUChannel.Replace("'", "''")}',"
                    + $"{objSettings.PUPumpCutOff.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.PURPMCutOff.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.PUMaxMovement.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.PUMinMovement.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.PUDynMethod},"
                    + $"{(objSettings.PUFilterMaxLocal ? 1 : 0)},"
                    + $"'{objSettings.SOChannel.Replace("'", "''")}',"
                    + $"{objSettings.SOPumpCutOff.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.SORPMCutOff.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.SOMaxMovement.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.SOMinMovement.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.SODynMethod},"
                    + $"{(objSettings.SOFilterMaxLocal ? 1 : 0)},"
                    + $"'{objSettings.ROTChannel.Replace("'", "''")}',"
                    + $"{objSettings.ROTPumpCutOff.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.ROTMinRPM.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.ROTMaxRPM.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.ROTTolerance.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.ROTPoints},"
                    + $"{objSettings.DepthMovementTime},"
                    + $"'{user.Replace("'", "''")}',"
                    + $"'{DateTime.Now:dd-MMM-yyyy HH:mm:ss}',"
                    + $"'{objSettings.RunNo.Replace("'", "''")}');";

                return objDataService.ExecuteNonQuery(strSQL);
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

        
        public static void EnsureTableExists(IDbConnection conn)
        {
            if (conn == null) return;
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS VMX_WELL_PUSO_SETTINGS (
                        WELL_ID TEXT PRIMARY KEY,
                        PU_CHANNEL TEXT, PU_PUMP_CUTOFF REAL, PU_RPM_CUTOFF REAL, PU_MAX_MOVEMENT REAL, PU_MIN_MOVEMENT REAL, PU_DYN_METHOD INTEGER, PU_FILTER_LOCAL_MAX INTEGER,
                        SO_CHANNEL TEXT, SO_PUMP_CUTOFF REAL, SO_RPM_CUTOFF REAL, SO_MAX_MOVEMENT REAL, SO_MIN_MOVEMENT REAL, SO_DYN_METHOD INTEGER, SO_FILTER_LOCAL_MAX INTEGER,
                        ROT_CHANNEL TEXT, ROT_PUMP_CUTOFF REAL, ROT_MIN_RPM REAL, ROT_MAX_RPM REAL, ROT_TOLERANCE REAL, ROT_POINTS INTEGER, DEPTH_MOVEMENT_TIME INTEGER,
                        MODIFIED_BY TEXT, MODIFIED_DATE TEXT, RUN_NO TEXT
                    );";
                cmd.ExecuteNonQuery();
            }
            catch { }
        }
        public static bool SaveSettings(IDbConnection conn, WellPUSOSettings objSettings, string user = "")
        {
            if (conn == null || objSettings == null) return false;
            try
            {
                EnsureTableExists(conn);
                using var cmd = conn.CreateCommand();
                string safeWellId = objSettings.WellID.Replace("'", "''");
                cmd.CommandText = $"DELETE FROM VMX_WELL_PUSO_SETTINGS WHERE WELL_ID='{safeWellId}';";
                cmd.ExecuteNonQuery();

                string safeUser = !string.IsNullOrWhiteSpace(user) ? user : (!string.IsNullOrWhiteSpace(objSettings.ModifiedBy) ? objSettings.ModifiedBy : "SYSTEM");

                cmd.CommandText = "INSERT INTO VMX_WELL_PUSO_SETTINGS " +
                    "(WELL_ID,PU_CHANNEL,PU_PUMP_CUTOFF,PU_RPM_CUTOFF,PU_MAX_MOVEMENT,PU_MIN_MOVEMENT,PU_DYN_METHOD,PU_FILTER_LOCAL_MAX," +
                    "SO_CHANNEL,SO_PUMP_CUTOFF,SO_RPM_CUTOFF,SO_MAX_MOVEMENT,SO_MIN_MOVEMENT,SO_DYN_METHOD,SO_FILTER_LOCAL_MAX," +
                    "ROT_CHANNEL,ROT_PUMP_CUTOFF,ROT_MIN_RPM,ROT_MAX_RPM,ROT_TOLERANCE,ROT_POINTS,DEPTH_MOVEMENT_TIME,MODIFIED_BY,MODIFIED_DATE,RUN_NO) VALUES("
                    + $"'{safeWellId}',"
                    + $"'{objSettings.PUChannel.Replace("'", "''")}',"
                    + $"{objSettings.PUPumpCutOff.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.PURPMCutOff.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.PUMaxMovement.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.PUMinMovement.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.PUDynMethod},"
                    + $"{(objSettings.PUFilterMaxLocal ? 1 : 0)},"
                    + $"'{objSettings.SOChannel.Replace("'", "''")}',"
                    + $"{objSettings.SOPumpCutOff.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.SORPMCutOff.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.SOMaxMovement.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.SOMinMovement.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.SODynMethod},"
                    + $"{(objSettings.SOFilterMaxLocal ? 1 : 0)},"
                    + $"'{objSettings.ROTChannel.Replace("'", "''")}',"
                    + $"{objSettings.ROTPumpCutOff.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.ROTMinRPM.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.ROTMaxRPM.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.ROTTolerance.ToString(CultureInfo.InvariantCulture)},"
                    + $"{objSettings.ROTPoints},"
                    + $"{objSettings.DepthMovementTime},"
                    + $"'{safeUser.Replace("'", "''")}',"
                    + $"'{DateTime.Now:dd-MMM-yyyy HH:mm:ss}',"
                    + $"'{objSettings.RunNo.Replace("'", "''")}');";

                return cmd.ExecuteNonQuery() > 0;
            }
            catch
            {
                return false;
            }
        }

        public static WellPUSOSettings LoadSettings(IDataServiceDIntel objDataService, string paramWellID)
        {
            try
            {
                WellPUSOSettings objSettings = new WellPUSOSettings(paramWellID);

                DataTable objData = objDataService.GetTable(
                    "SELECT * FROM VMX_WELL_PUSO_SETTINGS WHERE WELL_ID='" +
                    paramWellID.Replace("'", "''") + "' LIMIT 1;");

                if (objData != null && objData.Rows.Count > 0)
                {
                    DataRow objRow = objData.Rows[0];
                    PopulateFromRow(objSettings, objRow);
                }

                return objSettings;
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

        public static WellPUSOSettings LoadSettings(IDbConnection conn, string paramWellID)
        {
            try
            {
                WellPUSOSettings objSettings = new WellPUSOSettings(paramWellID);
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT * FROM VMX_WELL_PUSO_SETTINGS WHERE WELL_ID='" + paramWellID.Replace("'", "''") + "' LIMIT 1;";
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    objSettings.WellID = paramWellID;
                    objSettings.PUChannel = Convert.ToString(reader["PU_CHANNEL"]) ?? "SPPA";
                    objSettings.PUPumpCutOff = Convert.ToDouble(reader["PU_PUMP_CUTOFF"]);
                    objSettings.PURPMCutOff = Convert.ToDouble(reader["PU_RPM_CUTOFF"]);
                    objSettings.PUMaxMovement = Convert.ToDouble(reader["PU_MAX_MOVEMENT"]);
                    objSettings.PUMinMovement = Convert.ToDouble(reader["PU_MIN_MOVEMENT"]);
                    objSettings.PUDynMethod = Convert.ToInt32(reader["PU_DYN_METHOD"]);
                    objSettings.PUFilterMaxLocal = Convert.ToInt32(reader["PU_FILTER_LOCAL_MAX"]) == 1;

                    objSettings.SOChannel = Convert.ToString(reader["SO_CHANNEL"]) ?? "SPPA";
                    objSettings.SOPumpCutOff = Convert.ToDouble(reader["SO_PUMP_CUTOFF"]);
                    objSettings.SORPMCutOff = Convert.ToDouble(reader["SO_RPM_CUTOFF"]);
                    objSettings.SOMaxMovement = Convert.ToDouble(reader["SO_MAX_MOVEMENT"]);
                    objSettings.SOMinMovement = Convert.ToDouble(reader["SO_MIN_MOVEMENT"]);
                    objSettings.SODynMethod = Convert.ToInt32(reader["SO_DYN_METHOD"]);
                    objSettings.SOFilterMaxLocal = Convert.ToInt32(reader["SO_FILTER_LOCAL_MAX"]) == 1;

                    objSettings.ROTChannel = Convert.ToString(reader["ROT_CHANNEL"]) ?? "SPPA";
                    objSettings.ROTPumpCutOff = Convert.ToDouble(reader["ROT_PUMP_CUTOFF"]);
                    objSettings.ROTMinRPM = Convert.ToDouble(reader["ROT_MIN_RPM"]);
                    objSettings.ROTMaxRPM = Convert.ToDouble(reader["ROT_MAX_RPM"]);
                    objSettings.ROTTolerance = Convert.ToDouble(reader["ROT_TOLERANCE"]);
                    objSettings.ROTPoints = Convert.ToInt32(reader["ROT_POINTS"]);
                    objSettings.DepthMovementTime = Convert.ToInt32(reader["DEPTH_MOVEMENT_TIME"]);
                    objSettings.RunNo = Convert.ToString(reader["RUN_NO"]) ?? "";
                    objSettings.ModifiedBy = Convert.ToString(reader["MODIFIED_BY"]) ?? "";
                }
                return objSettings;
            }
            catch
            {
                return new WellPUSOSettings(paramWellID);
            }
        }

        private static void PopulateFromRow(WellPUSOSettings objSettings, DataRow objRow)
        {
            objSettings.PUChannel = DataService.checkNull(objRow["PU_CHANNEL"], "SPPA").ToString();
            objSettings.PUPumpCutOff = Convert.ToDouble(DataService.checkNull(objRow["PU_PUMP_CUTOFF"], 99.0));
            objSettings.PURPMCutOff = Convert.ToDouble(DataService.checkNull(objRow["PU_RPM_CUTOFF"], 12.0));
            objSettings.PUMaxMovement = Convert.ToDouble(DataService.checkNull(objRow["PU_MAX_MOVEMENT"], 70.0));
            objSettings.PUMinMovement = Convert.ToDouble(DataService.checkNull(objRow["PU_MIN_MOVEMENT"], 5.0));
            objSettings.PUDynMethod = Convert.ToInt32(DataService.checkNull(objRow["PU_DYN_METHOD"], 0));
            objSettings.PUFilterMaxLocal = Convert.ToInt32(DataService.checkNull(objRow["PU_FILTER_LOCAL_MAX"], 0)) == 1;

            objSettings.SOChannel = DataService.checkNull(objRow["SO_CHANNEL"], "SPPA").ToString();
            objSettings.SOPumpCutOff = Convert.ToDouble(DataService.checkNull(objRow["SO_PUMP_CUTOFF"], 99.0));
            objSettings.SORPMCutOff = Convert.ToDouble(DataService.checkNull(objRow["SO_RPM_CUTOFF"], 12.0));
            objSettings.SOMaxMovement = Convert.ToDouble(DataService.checkNull(objRow["SO_MAX_MOVEMENT"], 70.0));
            objSettings.SOMinMovement = Convert.ToDouble(DataService.checkNull(objRow["SO_MIN_MOVEMENT"], 5.0));
            objSettings.SODynMethod = Convert.ToInt32(DataService.checkNull(objRow["SO_DYN_METHOD"], 0));
            objSettings.SOFilterMaxLocal = Convert.ToInt32(DataService.checkNull(objRow["SO_FILTER_LOCAL_MAX"], 0)) == 1;

            objSettings.ROTChannel = DataService.checkNull(objRow["ROT_CHANNEL"], "SPPA").ToString();
            objSettings.ROTPumpCutOff = Convert.ToDouble(DataService.checkNull(objRow["ROT_PUMP_CUTOFF"], 99.0));
            objSettings.ROTMinRPM = Convert.ToDouble(DataService.checkNull(objRow["ROT_MIN_RPM"], 12.0));
            objSettings.ROTMaxRPM = Convert.ToDouble(DataService.checkNull(objRow["ROT_MAX_RPM"], 30.0));
            objSettings.ROTTolerance = Convert.ToDouble(DataService.checkNull(objRow["ROT_TOLERANCE"], 1.0));
            objSettings.ROTPoints = Convert.ToInt32(DataService.checkNull(objRow["ROT_POINTS"], 1));
            objSettings.DepthMovementTime = Convert.ToInt32(DataService.checkNull(objRow["DEPTH_MOVEMENT_TIME"], 1));
            objSettings.RunNo = DataService.checkNull(objRow["RUN_NO"], "").ToString();
            objSettings.ModifiedBy = DataService.checkNull(objRow["MODIFIED_BY"], "").ToString();
        }
    }
}

