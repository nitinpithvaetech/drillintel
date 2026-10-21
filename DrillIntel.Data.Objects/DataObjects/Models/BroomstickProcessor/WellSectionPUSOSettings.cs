using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using DrillIntel.Data;

namespace DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor
{
    public class WellSectionPUSOSettings
    {
        public string WellID = "";

        // Added by Prath
        public string SectionID = "";

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

        public static void SaveSettings(
            IDataServiceDIntel objDataService,
            WellSectionPUSOSettings objSettings)
        {
            try
            {
                string strSQL = "";

                strSQL =
                    "DELETE FROM VMX_WELL_SECTION_PUSO_SETTINGS " +
                    "WHERE WELL_ID='" + objSettings.WellID +
                    "' AND SECTION_ID='" + objSettings.SectionID + "'";

                objDataService.ExecuteNonQuery(strSQL);

                strSQL =
                    "INSERT INTO VMX_WELL_SECTION_PUSO_SETTINGS " +
                    "(WELL_ID,SECTION_ID,PU_CHANNEL,PU_PUMP_CUTOFF,PU_RPM_CUTOFF,PU_MAX_MOVEMENT,PU_MIN_MOVEMENT,PU_DYN_METHOD,PU_FILTER_LOCAL_MAX," +
                    "SO_CHANNEL,SO_PUMP_CUTOFF,SO_RPM_CUTOFF,SO_MAX_MOVEMENT,SO_MIN_MOVEMENT,SO_DYN_METHOD,SO_FILTER_LOCAL_MAX," +
                    "ROT_CHANNEL,ROT_PUMP_CUTOFF,ROT_MIN_RPM,ROT_MAX_RPM,ROT_TOLERANCE,ROT_POINTS,DEPTH_MOVEMENT_TIME,MODIFIED_BY,MODIFIED_DATE,RUN_NO) VALUES(";

                strSQL += $"'{objSettings.WellID}',";
                strSQL += $"'{objSettings.SectionID}',";
                strSQL += $"'{objSettings.PUChannel}',";
                strSQL += $"{objSettings.PUPumpCutOff},";
                strSQL += $"{objSettings.PURPMCutOff},";
                strSQL += $"{objSettings.PUMaxMovement},";
                strSQL += $"{objSettings.PUMinMovement},";
                strSQL += $"{objSettings.PUDynMethod},";
                strSQL += $"{(objSettings.PUFilterMaxLocal ? 1 : 0)},";
                strSQL += $"'{objSettings.SOChannel}',";
                strSQL += $"{objSettings.SOPumpCutOff},";
                strSQL += $"{objSettings.SORPMCutOff},";
                strSQL += $"{objSettings.SOMaxMovement},";
                strSQL += $"{objSettings.SOMinMovement},";
                strSQL += $"{objSettings.SODynMethod},";
                strSQL += $"{(objSettings.SOFilterMaxLocal ? 1 : 0)},";
                strSQL += $"'{objSettings.ROTChannel}',";
                strSQL += $"{objSettings.ROTPumpCutOff},";
                strSQL += $"{objSettings.ROTMinRPM},";
                strSQL += $"{objSettings.ROTMaxRPM},";
                strSQL += $"{objSettings.ROTTolerance},";
                strSQL += $"{objSettings.ROTPoints},";
                strSQL += $"{objSettings.DepthMovementTime},";
                strSQL += $"'{objDataService.UserName.Replace("'", "''")}',";
            strSQL += $"'{DateTime.Now:dd-MMM-yyyy HH:mm:ss}',";
            strSQL += $"'{objSettings.RunNo.Replace("'", "''")}')";

            objDataService.ExecuteNonQuery(strSQL);
        }
        catch (Exception)
        {
        }
    }

    public static WellSectionPUSOSettings LoadSettings(
        IDataServiceDIntel objDataService,
        string paramWellID,
        string paramSectionID)
    {
        try
        {
            WellSectionPUSOSettings objSettings = new WellSectionPUSOSettings();

            DataTable objData = objDataService.GetTable(
                "SELECT * FROM VMX_WELL_SECTION_PUSO_SETTINGS " +
                "WHERE WELL_ID='" + paramWellID +
                "' AND SECTION_ID='" + paramSectionID + "'");

            if (objData.Rows.Count > 0)
            {
                DataRow objRow = objData.Rows[0];

                objSettings.WellID = paramWellID;
                objSettings.SectionID = paramSectionID;

                objSettings.PUChannel = DataService.checkNull(objRow["PU_CHANNEL"], "");
                objSettings.PUPumpCutOff = DataService.checkNull(objRow["PU_PUMP_CUTOFF"], 0);
                objSettings.PURPMCutOff = DataService.checkNull(objRow["PU_RPM_CUTOFF"], 0);
                objSettings.PUMaxMovement = DataService.checkNull(objRow["PU_MAX_MOVEMENT"], 0);
                objSettings.PUMinMovement = DataService.checkNull(objRow["PU_MIN_MOVEMENT"], 0);
                objSettings.PUDynMethod = DataService.checkNull(objRow["PU_DYN_METHOD"], 0);
                objSettings.PUFilterMaxLocal =
                    DataService.checkNull(objRow["PU_FILTER_LOCAL_MAX"], 0) == 1;

                objSettings.SOChannel = DataService.checkNull(objRow["SO_CHANNEL"], "");
                objSettings.SOPumpCutOff = DataService.checkNull(objRow["SO_PUMP_CUTOFF"], 0);
                objSettings.SORPMCutOff = DataService.checkNull(objRow["SO_RPM_CUTOFF"], 0);
                objSettings.SOMaxMovement = DataService.checkNull(objRow["SO_MAX_MOVEMENT"], 0);
                objSettings.SOMinMovement = DataService.checkNull(objRow["SO_MIN_MOVEMENT"], 0);
                objSettings.SODynMethod = DataService.checkNull(objRow["SO_DYN_METHOD"], 0);
                objSettings.SOFilterMaxLocal =
                    DataService.checkNull(objRow["SO_FILTER_LOCAL_MAX"], 0) == 1;

                objSettings.ROTChannel = DataService.checkNull(objRow["ROT_CHANNEL"], "");
                objSettings.ROTPumpCutOff = DataService.checkNull(objRow["ROT_PUMP_CUTOFF"], 0);
                objSettings.ROTMinRPM = DataService.checkNull(objRow["ROT_MIN_RPM"], 0);
                objSettings.ROTMaxRPM = DataService.checkNull(objRow["ROT_MAX_RPM"], 0);
                objSettings.ROTTolerance = DataService.checkNull(objRow["ROT_TOLERANCE"], 0);
                objSettings.ROTPoints = DataService.checkNull(objRow["ROT_POINTS"], 0);
                objSettings.DepthMovementTime = DataService.checkNull(objRow["DEPTH_MOVEMENT_TIME"], 0);
                objSettings.RunNo = DataService.checkNull(objRow["RUN_NO"], "");
            }

            return objSettings;
        }
            catch (Exception)
            {
                return new WellSectionPUSOSettings();
            }
        }

        public static bool RemoveSetting(
            ref IDataServiceDIntel objDataService,
            string paramWellID,
            string paramSectionID)
        {
            try
            {
                string strSQL =
                    "DELETE FROM VMX_WELL_SECTION_PUSO_SETTINGS " +
                    "WHERE WELL_ID='" + paramWellID +
                    "' AND SECTION_ID='" + paramSectionID + "'";

                objDataService.ExecuteNonQuery(strSQL);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool RemoveSetting(
            ref IDataServiceDIntel objDataService,
            string paramWellID)
        {
            try
            {
                string strSQL =
                    "DELETE FROM VMX_WELL_SECTION_PUSO_SETTINGS " +
                    "WHERE WELL_ID='" + paramWellID + "'";

                objDataService.ExecuteNonQuery(strSQL);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }

}

