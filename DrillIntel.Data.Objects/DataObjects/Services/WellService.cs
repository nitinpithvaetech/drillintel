using System;
using System.Data;
using System.Globalization;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Models.Util;

namespace DrillIntel.Data.Objects.DataObjects.Services
{
    public class WellService
    {
        public static bool AddWell(IDataServiceDIntel objDataService, Well objWell, ref string LastError)
        {
            try
            {
                if (Well.IsWellExist(objDataService, objWell.ObjectID))
                {
                    LastError = "Well already exist";
                    return false;
                }

                string AlarmHistoryTable = ObjectIDFactory.generateAlarmHistoryTableName();

                objWell.wellDateFormat = Well.wDateFormatUTC;

                string strSQL = "INSERT INTO VMX_WELL (WELL_ID,UWI,WELL_NAME,LEGAL_NAME,BLOCK,FIELD,COUNTY,DISTRICT,REGION,STATE,COUNTRY,OPERATOR,OPERATOR_DIV,LICENSE_NO,"
                    + "LICENSE_DATE,PURPOSE,STATUS,SPUD_DATE,PA_DATE,TIME_ZONE,LONGITUDE,LATITUDE,X_COORDINATE,Y_COORDINATE,PERM_DATUM,WELL_HEAD_ELEVATION,"
                    + "GROUND_ELEVATION,WATER_DEPTH,WMLS_URL,WMLP_URL,ALARM_HISTORY_TABLE,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE,RIG_NAME,DATE_FORMAT,EDR_PROVIDER,DATA_SOURCE,DRILLING_SUP,DRILLING_ENG,HISTORICAL,"
                    + "SEC,TWP,RGE,LEGAL_DESC,CONT_TYPE,RIG_TYPE,PUMP1_MODEL,PUMP1_STROKE,PUMP1_LINER,PUMP2_MODEL,PUMP2_STROKE,PUMP2_LINER,PUMP3_MODEL,PUMP3_STROKE,PUMP3_LINER,REP,TOOL_PUSHER,TIGHT_HOLE_NO,REENTRY_NO,COMMENTS,"
                    + "CONTRACTOR,OBJECTIVE,TD_DATE,TD_FORMATION,PUMP1,PUMP2,PUMP3,RIG_COST,DRLG_CONN_TIME,TRIP_CONN_TIME,BTS_TIME,STS_TIME,STB_TIME,TRIP_IN_SPEED,TRIP_OUT_SPEED,PLANNED_DAYS,PIPE_LENGTH,STAND_LENGTH,PLANNED_DEPTH,DRLG_ENG_DEPT,DRLG_OP_DEPT,WELL_OP_TYPE,"
                    + "BI,WELL_LOCATION,ROP_BENCHMARK,PIPE_MOVE_BENCHMARK,TRIP_STAND_BENCHMARK,DRLG_CONN_DEVIATION,DRLG_STS_DEVIATION,ROP_DEVIATION,TRIP_CONN_DEVIATION,PIPE_MOVE_DEVIATION,TRIP_STAND_DEVIATION"
                    + ") VALUES( ";

                strSQL += "'" + objWell.ObjectID.Replace("'", "''") + "',";
                strSQL += "'" + objWell.numAPI.Replace("'", "''") + "',";
                strSQL += "'" + objWell.name.Replace("'", "''") + "',";
                strSQL += "'" + objWell.nameLegal.Replace("'", "''") + "',";
                strSQL += "'" + objWell.block.Replace("'", "''") + "',";
                strSQL += "'" + objWell.field.Replace("'", "''") + "',";
                strSQL += "'" + objWell.county.Replace("'", "''") + "',";
                strSQL += "'" + objWell.district.Replace("'", "''") + "',";
                strSQL += "'" + objWell.region.Replace("'", "''") + "',";
                strSQL += "'" + objWell.state.Replace("'", "''") + "',";
                strSQL += "'" + objWell.country.Replace("'", "''") + "',";
                strSQL += "'" + objWell.operatorName.Replace("'", "''") + "',";
                strSQL += "'" + objWell.operatorDiv.Replace("'", "''") + "',";
                strSQL += "'" + objWell.numLicense.Replace("'", "''") + "',";
                strSQL += "" + utilFunctions.parseDateForDB(objWell.dTimeLicense) + ",";
                strSQL += "'" + objWell.purposeWell.Replace("'", "''") + "',";
                strSQL += "'" + objWell.statusWell.Replace("'", "''") + "',";
                strSQL += "" + utilFunctions.parseDateForDB(objWell.dTimSpud) + ",";
                strSQL += "" + utilFunctions.parseDateForDB(objWell.dTimPa) + ",";
                strSQL += "'" + objWell.timeZone.Replace("'", "''") + "',";
                strSQL += "" + objWell.longitude.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.latitude.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.xCoOrd.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.yCoOrd.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "'" + objWell.dtmPermanent + "',";
                strSQL += "" + objWell.wellheadElevation.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.groundElevation.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.waterDepth.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "'" + objWell.wmlsurl + "',";
                strSQL += "'" + objWell.wmlpurl + "',";
                strSQL += "'" + AlarmHistoryTable + "',";
                strSQL += "'" + objDataService.UserName + "',";
                strSQL += "'" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "',";
                strSQL += "'" + objDataService.UserName + "',";
                strSQL += "'" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "',";
                strSQL += "'" + objWell.RigName.Replace("'", "''") + "',";
                strSQL += "'" + objWell.wellDateFormat + "',";
                strSQL += "'" + objWell.EDRProvider.Replace("'", "''") + "',";
                strSQL += "'" + objWell.DataSource.Replace("'", "''") + "',";
                strSQL += "'" + objWell.DrillingSupr.Replace("'", "''") + "',";
                strSQL += "'" + objWell.DrillingEng.Replace("'", "''") + "',";
                strSQL += "" + (objWell.Historical ? 1 : 0).ToString() + ",";
                strSQL += "'" + utilFunctions.quote(objWell.SEC) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.TWP) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.RGE) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.LegalDesc) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.ContType) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.RigType) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.Pump1Model) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.Pump1Stroke) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.Pump1Liner) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.Pump2Model) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.Pump2Stroke) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.Pump2Liner) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.Pump3Model) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.Pump3Stroke) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.Pump3Liner) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.Rep) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.ToolPusher) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.TightHoleNo) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.ReEntryNo) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.Comments) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.Contractor) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.Objective) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.TDDate) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.TDFormation) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.Pump1) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.Pump2) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.Pump3) + "',";
                strSQL += "" + objWell.RigCost.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.DrlgConnTime.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.TripConnTime.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.BTSTime.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.STSTime.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.STBTime.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.TripInSpeed.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.TripOutSpeed.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.PlannedDays.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.PipeLength.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.StandLength.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.PlannedDepth.ToString(CultureInfo.InvariantCulture) + ",";

                strSQL += "'" + utilFunctions.quote(objWell.DrlgEngDept) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.DrlgOpDept) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.WellOpType) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.BI) + "',";
                strSQL += "'" + utilFunctions.quote(objWell.WellLocation) + "',";

                strSQL += "" + objWell.ROPBenchmark.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.PipeMoveBenchmark.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.TripStandBenchmark.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.DrlgConnDeviation.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.DrlgSTSDeviation.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.ROPDeviation.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.TripConnDeviation.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.PipeMoveDeviation.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += "" + objWell.TripStandDeviation.ToString(CultureInfo.InvariantCulture) + ")";

                if (objDataService.ExecuteNonQuery(strSQL))
                {
                    rigState? rigRigStateSetup = null;

                    if (!string.IsNullOrWhiteSpace(objWell.RigName))
                    {
                        rigRigStateSetup = rigState.LoadRigRigStateSetup(objDataService, objWell.RigName);
                    }

                    if (rigRigStateSetup != null)
                    {
                        rigState.SaveWellRigStateSetup(objDataService, objWell.ObjectID, rigRigStateSetup, "");
                    }
                    else
                    {
                        rigState? objRigState = rigState.LoadCommonRigStateSetup(objDataService);

                        if (objRigState != null)
                        {
                            rigState.SaveWellRigStateSetup(objDataService, objWell.ObjectID, objRigState, "");
                        }
                    }

                    // Create a table to store alarm history
                    Well.CreateAlarmHistoryTable(objDataService, objWell.ObjectID, AlarmHistoryTable);

                    // Add offset wells
                    foreach (OffsetWell objOffset in objWell.offsetWells.Values)
                    {
                        strSQL = "INSERT INTO VMX_WELL_OFFSET (WELL_ID,OFFSET_WELL_ID,OFFSET_WELL_UWI,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE) VALUES(";
                        strSQL += "'" + objWell.ObjectID + "',";
                        strSQL += "'" + objOffset.OffsetWellID + "',";
                        strSQL += "'" + objOffset.OffsetWellUWI + "',";
                        strSQL += "'" + objDataService.UserName + "',";
                        strSQL += "'" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "',";
                        strSQL += "'" + objDataService.UserName + "',";
                        strSQL += "'" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "')";

                        objDataService.ExecuteNonQuery(strSQL);
                    }

                    return true;
                }
                else
                {
                    LastError = objDataService.LastError;
                    return false;
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message + ":" + ex.StackTrace;
                return false;
            }
        }

        public static bool UpdateWell(IDataServiceDIntel objDataService, Well objWell, ref string LastError)
        {
            try
            {
                string strSQL = "UPDATE VMX_WELL SET ";
                strSQL += " UWI='" + objWell.numAPI.Replace("'", "''") + "',";
                strSQL += " WELL_NAME='" + objWell.name.Replace("'", "''") + "',";
                strSQL += " LEGAL_NAME='" + objWell.nameLegal.Replace("'", "''") + "',";
                strSQL += " BLOCK='" + objWell.block.Replace("'", "''") + "',";
                strSQL += " FIELD='" + objWell.field.Replace("'", "''") + "',";
                strSQL += " COUNTY='" + objWell.county.Replace("'", "''") + "',";
                strSQL += " DISTRICT='" + objWell.district.Replace("'", "''") + "',";
                strSQL += " REGION='" + objWell.region.Replace("'", "''") + "',";
                strSQL += " STATE='" + objWell.state.Replace("'", "''") + "',";
                strSQL += " COUNTRY='" + objWell.country.Replace("'", "''") + "',";
                strSQL += " OPERATOR='" + objWell.operatorName.Replace("'", "''") + "',";
                strSQL += " OPERATOR_DIV='" + objWell.operatorDiv.Replace("'", "''") + "',";
                strSQL += " LICENSE_NO='" + objWell.numLicense.Replace("'", "''") + "',";

                if (utilFunctions.parseDate(objWell.dTimeLicense).Trim() != "")
                {
                    strSQL += " LICENSE_DATE='" + utilFunctions.parseDate(objWell.dTimeLicense) + "',";
                }

                strSQL += " PURPOSE='" + objWell.purposeWell.Replace("'", "''") + "',";
                strSQL += " STATUS='" + objWell.statusWell.Replace("'", "''") + "',";

                if (utilFunctions.parseDate(objWell.dTimSpud).Trim() != "")
                {
                    strSQL += " SPUD_DATE='" + utilFunctions.parseDate(objWell.dTimSpud) + "',";
                }

                if (utilFunctions.parseDate(objWell.dTimPa).Trim() != "")
                {
                    strSQL += " PA_DATE='" + utilFunctions.parseDate(objWell.dTimPa) + "',";
                }

                strSQL += " TIME_ZONE='" + objWell.timeZone.Replace("'", "''") + "',";
                strSQL += " LONGITUDE=" + objWell.longitude.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += " LATITUDE=" + objWell.latitude.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += " X_COORDINATE=" + objWell.xCoOrd.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += " Y_COORDINATE=" + objWell.yCoOrd.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += " PERM_DATUM='" + objWell.dtmPermanent + "',";
                strSQL += " WELL_HEAD_ELEVATION=" + objWell.wellheadElevation.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += " GROUND_ELEVATION=" + objWell.groundElevation.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += " WATER_DEPTH=" + objWell.waterDepth.ToString(CultureInfo.InvariantCulture) + ",";
                strSQL += " WMLS_URL='" + objWell.wmlsurl + "',";
                strSQL += " WMLP_URL='" + objWell.wmlpurl + "', ";
                strSQL += " MODIFIED_BY='" + objDataService.UserName + "',";
                strSQL += " MODIFIED_DATE='" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "', ";
                strSQL += " RIG_NAME='" + objWell.RigName.Replace("'", "''") + "', ";
                strSQL += " DATE_FORMAT='" + objWell.wellDateFormat + "', ";
                strSQL += " EDR_PROVIDER='" + objWell.EDRProvider.Replace("'", "''") + "', ";
                strSQL += " DATA_SOURCE='" + objWell.DataSource.Replace("'", "''") + "', ";

                strSQL += " DRILLING_SUP='" + objWell.DrillingSupr.Replace("'", "''") + "', ";
                strSQL += " DRILLING_ENG='" + objWell.DrillingEng.Replace("'", "''") + "', ";

                strSQL += " SEC='" + utilFunctions.quote(objWell.SEC) + "', ";
                strSQL += " TWP='" + utilFunctions.quote(objWell.TWP) + "', ";
                strSQL += " RGE='" + utilFunctions.quote(objWell.RGE) + "', ";
                strSQL += " LEGAL_DESC='" + utilFunctions.quote(objWell.LegalDesc) + "', ";
                strSQL += " CONT_TYPE='" + utilFunctions.quote(objWell.ContType) + "', ";
                strSQL += " RIG_TYPE='" + utilFunctions.quote(objWell.RigType) + "', ";
                strSQL += " PUMP1_MODEL='" + utilFunctions.quote(objWell.Pump1Model) + "', ";
                strSQL += " PUMP1_STROKE='" + utilFunctions.quote(objWell.Pump1Stroke) + "', ";
                strSQL += " PUMP1_LINER='" + utilFunctions.quote(objWell.Pump1Liner) + "', ";

                strSQL += " PUMP2_MODEL='" + utilFunctions.quote(objWell.Pump2Model) + "', ";
                strSQL += " PUMP2_STROKE='" + utilFunctions.quote(objWell.Pump2Stroke) + "', ";
                strSQL += " PUMP2_LINER='" + utilFunctions.quote(objWell.Pump2Liner) + "', ";

                strSQL += " PUMP3_MODEL='" + utilFunctions.quote(objWell.Pump3Model) + "', ";
                strSQL += " PUMP3_STROKE='" + utilFunctions.quote(objWell.Pump3Stroke) + "', ";
                strSQL += " PUMP3_LINER='" + utilFunctions.quote(objWell.Pump3Liner) + "', ";

                strSQL += " REP='" + utilFunctions.quote(objWell.Rep) + "', ";
                strSQL += " TOOL_PUSHER='" + utilFunctions.quote(objWell.ToolPusher) + "', ";
                strSQL += " TIGHT_HOLE_NO='" + utilFunctions.quote(objWell.TightHoleNo) + "', ";
                strSQL += " REENTRY_NO='" + utilFunctions.quote(objWell.ReEntryNo) + "', ";
                strSQL += " COMMENTS='" + utilFunctions.quote(objWell.Comments) + "', ";

                strSQL += " CONTRACTOR='" + utilFunctions.quote(objWell.Contractor) + "', ";
                strSQL += " OBJECTIVE='" + utilFunctions.quote(objWell.Objective) + "', ";
                strSQL += " TD_DATE='" + utilFunctions.quote(objWell.TDDate) + "', ";
                strSQL += " TD_FORMATION='" + utilFunctions.quote(objWell.TDFormation) + "', ";

                strSQL += " PUMP1='" + utilFunctions.quote(objWell.Pump1) + "', ";
                strSQL += " PUMP2='" + utilFunctions.quote(objWell.Pump2) + "', ";
                strSQL += " PUMP3='" + utilFunctions.quote(objWell.Pump3) + "', ";

                strSQL += " RIG_COST=" + objWell.RigCost.ToString(CultureInfo.InvariantCulture) + ", ";

                strSQL += " DRLG_CONN_TIME=" + objWell.DrlgConnTime.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " TRIP_CONN_TIME=" + objWell.TripConnTime.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " BTS_TIME=" + objWell.BTSTime.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " STS_TIME=" + objWell.STSTime.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " STB_TIME=" + objWell.STBTime.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " TRIP_IN_SPEED=" + objWell.TripInSpeed.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " TRIP_OUT_SPEED=" + objWell.TripOutSpeed.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " PLANNED_DAYS=" + objWell.PlannedDays.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " HISTORICAL=" + (objWell.Historical ? 1 : 0).ToString() + ", ";
                strSQL += " PIPE_LENGTH=" + objWell.PipeLength.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " STAND_LENGTH=" + objWell.StandLength.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " PLANNED_DEPTH=" + objWell.PlannedDepth.ToString(CultureInfo.InvariantCulture) + ", ";

                strSQL += " DRLG_ENG_DEPT='" + utilFunctions.quote(objWell.DrlgEngDept) + "', ";
                strSQL += " DRLG_OP_DEPT='" + utilFunctions.quote(objWell.DrlgOpDept) + "', ";
                strSQL += " WELL_OP_TYPE='" + utilFunctions.quote(objWell.WellOpType) + "', ";
                strSQL += " BI='" + utilFunctions.quote(objWell.BI) + "', ";
                strSQL += " WELL_LOCATION='" + utilFunctions.quote(objWell.WellLocation) + "', ";

                strSQL += " ROP_BENCHMARK=" + objWell.ROPBenchmark.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " PIPE_MOVE_BENCHMARK=" + objWell.PipeMoveBenchmark.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " TRIP_STAND_BENCHMARK=" + objWell.TripStandBenchmark.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " DRLG_CONN_DEVIATION=" + objWell.DrlgConnDeviation.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " DRLG_STS_DEVIATION=" + objWell.DrlgSTSDeviation.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " ROP_DEVIATION=" + objWell.ROPDeviation.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " TRIP_CONN_DEVIATION=" + objWell.TripConnDeviation.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " PIPE_MOVE_DEVIATION=" + objWell.PipeMoveDeviation.ToString(CultureInfo.InvariantCulture) + ", ";
                strSQL += " TRIP_STAND_DEVIATION=" + objWell.TripStandDeviation.ToString(CultureInfo.InvariantCulture) + " ";

                strSQL += " WHERE WELL_ID='" + objWell.ObjectID.Replace("'", "''") + "'";

                if (objDataService.ExecuteNonQuery(strSQL))
                {
                    objDataService.ExecuteNonQuery("DELETE FROM VMX_WELL_OFFSET WHERE WELL_ID='" + objWell.ObjectID.Replace("'", "''") + "'");

                    // Add offset wells
                    foreach (OffsetWell objOffset in objWell.offsetWells.Values)
                    {
                        strSQL = "INSERT INTO VMX_WELL_OFFSET (WELL_ID,OFFSET_WELL_ID,OFFSET_WELL_UWI,CREATED_BY,CREATED_DATE,MODIFIED_BY,MODIFIED_DATE) VALUES(";
                        strSQL += "'" + objWell.ObjectID + "',";
                        strSQL += "'" + objOffset.OffsetWellID + "',";
                        strSQL += "'" + objOffset.OffsetWellUWI + "',";
                        strSQL += "'" + objDataService.UserName + "',";
                        strSQL += "'" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "',";
                        strSQL += "'" + objDataService.UserName + "',";
                        strSQL += "'" + DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") + "')";

                        objDataService.ExecuteNonQuery(strSQL);
                    }

                    return true;
                }
                else
                {
                    LastError = objDataService.LastError;
                    return false;
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message + ":" + ex.StackTrace;
                return false;
            }
        }

        /// <summary>
        /// Write Logic to load Well from the database
        /// </summary>
        /// <param name="objDataService"></param>
        /// <param name="wellID"></param>
        /// <param name="LastError"></param>
        /// <returns></returns>
        public static Well? LoadObject(IDataServiceDIntel objDataService, string wellID, ref string LastError)
        {
            try
            {
                DataTable objData = objDataService.GetTable("SELECT * FROM VMX_WELL WHERE WELL_ID='" + wellID.Replace("'", "''") + "'");

                if (objData != null && objData.Rows.Count > 0)
                {
                    DataRow objRow = objData.Rows[0];

                    Well objWell = new Well();
                    objWell.ObjectID = wellID;
                    objWell.numAPI = DataService.checkNull(objRow["UWI"], "");
                    objWell.name = DataService.checkNull(objRow["WELL_NAME"], "");
                    objWell.nameLegal = DataService.checkNull(objRow["LEGAL_NAME"], "");
                    objWell.block = DataService.checkNull(objRow["BLOCK"], "");
                    objWell.field = DataService.checkNull(objRow["FIELD"], "");
                    objWell.county = DataService.checkNull(objRow["COUNTY"], "");
                    objWell.district = DataService.checkNull(objRow["DISTRICT"], "");
                    objWell.region = DataService.checkNull(objRow["REGION"], "");
                    objWell.state = DataService.checkNull(objRow["STATE"], "");
                    objWell.country = DataService.checkNull(objRow["COUNTRY"], "");
                    objWell.operatorName = DataService.checkNull(objRow["OPERATOR"], "");
                    objWell.operatorDiv = DataService.checkNull(objRow["OPERATOR_DIV"], "");
                    objWell.numLicense = DataService.checkNull(objRow["LICENSE_NO"], "");
                    objWell.dTimeLicense = DataService.checkNull(objRow["LICENSE_DATE"], "");
                    objWell.purposeWell = DataService.checkNull(objRow["PURPOSE"], "");
                    objWell.statusWell = DataService.checkNull(objRow["STATUS"], "");
                    objWell.dTimSpud = DataService.checkNull(objRow["SPUD_DATE"], "");
                    objWell.dTimPa = DataService.checkNull(objRow["PA_DATE"], "");
                    objWell.timeZone = DataService.checkNull(objRow["TIME_ZONE"], "");
                    objWell.longitude = DataService.checkNull(objRow["LONGITUDE"], 0.0);
                    objWell.latitude = DataService.checkNull(objRow["LATITUDE"], 0.0);
                    objWell.xCoOrd = DataService.checkNull(objRow["X_COORDINATE"], 0.0);
                    objWell.yCoOrd = DataService.checkNull(objRow["Y_COORDINATE"], 0.0);
                    objWell.dtmPermanent = DataService.checkNull(objRow["PERM_DATUM"], "");
                    objWell.wellheadElevation = DataService.checkNull(objRow["WELL_HEAD_ELEVATION"], 0.0);
                    objWell.groundElevation = DataService.checkNull(objRow["GROUND_ELEVATION"], 0.0);
                    objWell.waterDepth = DataService.checkNull(objRow["WATER_DEPTH"], 0.0);
                    objWell.wmlsurl = DataService.checkNull(objRow["WMLS_URL"], "");
                    objWell.wmlpurl = DataService.checkNull(objRow["WMLP_URL"], "");
                    objWell.RigName = DataService.checkNull(objRow["RIG_NAME"], "");
                    objWell.DataSource = DataService.checkNull(objRow["DATA_SOURCE"], "");
                    objWell.DrillingSupr = DataService.checkNull(objRow["DRILLING_SUP"], "");
                    objWell.DrillingEng = DataService.checkNull(objRow["DRILLING_ENG"], "");

                    objWell.SEC = DataService.checkNull(objRow["SEC"], "");
                    objWell.TWP = DataService.checkNull(objRow["TWP"], "");
                    objWell.RGE = DataService.checkNull(objRow["RGE"], "");
                    objWell.LegalDesc = DataService.checkNull(objRow["LEGAL_DESC"], "");
                    objWell.ContType = DataService.checkNull(objRow["CONT_TYPE"], "");
                    objWell.RigType = DataService.checkNull(objRow["RIG_TYPE"], "");
                    objWell.Pump1Model = DataService.checkNull(objRow["PUMP1_MODEL"], "");
                    objWell.Pump1Stroke = DataService.checkNull(objRow["PUMP1_STROKE"], "");
                    objWell.Pump1Liner = DataService.checkNull(objRow["PUMP1_LINER"], "");

                    objWell.Pump2Model = DataService.checkNull(objRow["PUMP2_MODEL"], "");
                    objWell.Pump2Stroke = DataService.checkNull(objRow["PUMP2_STROKE"], "");
                    objWell.Pump2Liner = DataService.checkNull(objRow["PUMP2_LINER"], "");

                    objWell.Pump3Model = DataService.checkNull(objRow["PUMP3_MODEL"], "");
                    objWell.Pump3Stroke = DataService.checkNull(objRow["PUMP3_STROKE"], "");
                    objWell.Pump3Liner = DataService.checkNull(objRow["PUMP3_LINER"], "");

                    objWell.Rep = DataService.checkNull(objRow["REP"], "");
                    objWell.ToolPusher = DataService.checkNull(objRow["TOOL_PUSHER"], "");
                    objWell.TightHoleNo = DataService.checkNull(objRow["TIGHT_HOLE_NO"], "");
                    objWell.ReEntryNo = DataService.checkNull(objRow["REENTRY_NO"], "");
                    objWell.Comments = DataService.checkNull(objRow["COMMENTS"], "");

                    objWell.Contractor = DataService.checkNull(objRow["CONTRACTOR"], "");
                    objWell.Objective = DataService.checkNull(objRow["OBJECTIVE"], "");
                    objWell.TDDate = DataService.checkNull(objRow["TD_DATE"], "");
                    objWell.TDFormation = DataService.checkNull(objRow["TD_FORMATION"], "");

                    objWell.Pump1 = DataService.checkNull(objRow["PUMP1"], "");
                    objWell.Pump2 = DataService.checkNull(objRow["PUMP2"], "");
                    objWell.Pump3 = DataService.checkNull(objRow["PUMP3"], "");

                    objWell.__isActive = Well.IsWellActive(objDataService, objWell.ObjectID);

                    if (objData.Columns.Contains("DATE_FORMAT"))
                    {
                        objWell.wellDateFormat = DataService.checkNull(objRow["DATE_FORMAT"], "");
                    }

                    if (objData.Columns.Contains("EDR_PROVIDER"))
                    {
                        objWell.EDRProvider = DataService.checkNull(objRow["EDR_PROVIDER"], "");
                    }

                    if (objData.Columns.Contains("HISTORICAL"))
                    {
                        objWell.Historical = DataService.checkNull(objRow["HISTORICAL"], 0) == 1;
                    }

                    if (objData.Columns.Contains("RIG_COST"))
                    {
                        objWell.RigCost = DataService.checkNull(objRow["RIG_COST"], 0.0);
                    }

                    if (objData.Columns.Contains("DRLG_CONN_TIME"))
                    {
                        objWell.DrlgConnTime = DataService.checkNull(objRow["DRLG_CONN_TIME"], 0.0);
                    }

                    if (objData.Columns.Contains("TRIP_CONN_TIME"))
                    {
                        objWell.TripConnTime = DataService.checkNull(objRow["TRIP_CONN_TIME"], 0.0);
                    }

                    if (objData.Columns.Contains("BTS_TIME"))
                    {
                        objWell.BTSTime = DataService.checkNull(objRow["BTS_TIME"], 0.0);
                    }

                    if (objData.Columns.Contains("STS_TIME"))
                    {
                        objWell.STSTime = DataService.checkNull(objRow["STS_TIME"], 0.0);
                    }

                    if (objData.Columns.Contains("STB_TIME"))
                    {
                        objWell.STBTime = DataService.checkNull(objRow["STB_TIME"], 0.0);
                    }

                    if (objData.Columns.Contains("TRIP_IN_SPEED"))
                    {
                        objWell.TripInSpeed = DataService.checkNull(objRow["TRIP_IN_SPEED"], 0.0);
                    }

                    if (objData.Columns.Contains("TRIP_OUT_SPEED"))
                    {
                        objWell.TripOutSpeed = DataService.checkNull(objRow["TRIP_OUT_SPEED"], 0.0);
                    }

                    if (objData.Columns.Contains("PLANNED_DAYS"))
                    {
                        objWell.PlannedDays = DataService.checkNull(objRow["PLANNED_DAYS"], 0.0);
                    }

                    if (objData.Columns.Contains("PIPE_LENGTH"))
                    {
                        objWell.PipeLength = DataService.checkNull(objRow["PIPE_LENGTH"], 0.0);
                    }

                    if (objData.Columns.Contains("STAND_LENGTH"))
                    {
                        objWell.StandLength = DataService.checkNull(objRow["STAND_LENGTH"], 0.0);
                    }

                    if (objData.Columns.Contains("PLANNED_DEPTH"))
                    {
                        objWell.PlannedDepth = DataService.checkNull(objRow["PLANNED_DEPTH"], 0.0);
                    }

                    if (objData.Columns.Contains("DRLG_ENG_DEPT"))
                    {
                        objWell.DrlgEngDept = DataService.checkNull(objRow["DRLG_ENG_DEPT"], "");
                    }

                    if (objData.Columns.Contains("DRLG_OP_DEPT"))
                    {
                        objWell.DrlgOpDept = DataService.checkNull(objRow["DRLG_OP_DEPT"], "");
                    }

                    if (objData.Columns.Contains("WELL_OP_TYPE"))
                    {
                        objWell.WellOpType = DataService.checkNull(objRow["WELL_OP_TYPE"], "");
                    }

                    if (objData.Columns.Contains("BI"))
                    {
                        objWell.BI = DataService.checkNull(objRow["BI"], "");
                    }

                    if (objData.Columns.Contains("WELL_LOCATION"))
                    {
                        objWell.WellLocation = DataService.checkNull(objRow["WELL_LOCATION"], "");
                    }

                    if (objData.Columns.Contains("ROP_BENCHMARK"))
                    {
                        objWell.ROPBenchmark = DataService.checkNull(objRow["ROP_BENCHMARK"], 0.0);
                    }

                    if (objData.Columns.Contains("PIPE_MOVE_BENCHMARK"))
                    {
                        objWell.PipeMoveBenchmark = DataService.checkNull(objRow["PIPE_MOVE_BENCHMARK"], 0.0);
                    }

                    if (objData.Columns.Contains("TRIP_STAND_BENCHMARK"))
                    {
                        objWell.TripStandBenchmark = DataService.checkNull(objRow["TRIP_STAND_BENCHMARK"], 0.0);
                    }

                    if (objData.Columns.Contains("DRLG_CONN_DEVIATION"))
                    {
                        objWell.DrlgConnDeviation = DataService.checkNull(objRow["DRLG_CONN_DEVIATION"], 0.0);
                    }

                    if (objData.Columns.Contains("DRLG_STS_DEVIATION"))
                    {
                        objWell.DrlgSTSDeviation = DataService.checkNull(objRow["DRLG_STS_DEVIATION"], 0.0);
                    }

                    if (objData.Columns.Contains("ROP_DEVIATION"))
                    {
                        objWell.ROPDeviation = DataService.checkNull(objRow["ROP_DEVIATION"], 0.0);
                    }

                    if (objData.Columns.Contains("TRIP_CONN_DEVIATION"))
                    {
                        objWell.TripConnDeviation = DataService.checkNull(objRow["TRIP_CONN_DEVIATION"], 0.0);
                    }

                    if (objData.Columns.Contains("PIPE_MOVE_DEVIATION"))
                    {
                        objWell.PipeMoveDeviation = DataService.checkNull(objRow["PIPE_MOVE_DEVIATION"], 0.0);
                    }

                    if (objData.Columns.Contains("TRIP_STAND_DEVIATION"))
                    {
                        objWell.TripStandDeviation = DataService.checkNull(objRow["TRIP_STAND_DEVIATION"], 0.0);
                    }

                    objData.Dispose();

                    DataTable objOffsetData = objDataService.GetTable("SELECT * FROM VMX_WELL_OFFSET WHERE WELL_ID='" + wellID.Replace("'", "''") + "'");

                    if (objOffsetData != null)
                    {
                        foreach (DataRow row in objOffsetData.Rows)
                        {
                            string OffsetWellID = DataService.checkNull(row["OFFSET_WELL_ID"], "");
                            string OffsetWellUWI = DataService.checkNull(row["OFFSET_WELL_UWI"], "");

                            OffsetWell objItem = new OffsetWell();
                            objItem.OffsetWellID = OffsetWellID;
                            objItem.OffsetWellUWI = OffsetWellUWI;

                            objWell.offsetWells[objItem.OffsetWellID] = objItem.GetCopy();
                        }

                        objOffsetData.Dispose();
                    }

                    return objWell;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                LastError = ex.Message + ":" + ex.StackTrace;
                return null;
            }
        }

        public static bool CreateAlarmHistoryTable(IDataServiceDIntel objDataService, string wellID, string paramTableName) => Well.CreateAlarmHistoryTable(objDataService, wellID, paramTableName);
    }
}

