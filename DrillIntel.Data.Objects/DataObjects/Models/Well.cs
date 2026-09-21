using System;
using System.Collections.Generic;
using System.Data;
using DrillIntel.Data;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    public enum vmxSortOn
    {
        Name = 0,
        DisplayOrder = 1
    }

    public class Well
    {
        public const string wDateFormatLocal = "Local";
        public const string wDateFormatUTC = "UTC";

        public string ObjectID { get; set; } = "";
        public string name { get; set; } = "";
        public string WellName { get => name; set => name = value; }
        public string nameLegal { get; set; } = "";
        public string numLicense { get; set; } = "";
        public string numGovt { get; set; } = "";
        public string dTimeLicense { get; set; } = "";
        public string field { get; set; } = "";
        public string FieldName { get => field; set => field = value; }
        public string country { get; set; } = "";
        public string county { get; set; } = "";
        public string state { get; set; } = "";
        public string region { get; set; } = "";
        public string district { get; set; } = "";
        public string block { get; set; } = "";
        public string timeZone { get; set; } = "";
        public string operatorName { get; set; } = "";
        public string operatorDiv { get; set; } = "";
        public string pcInterest { get; set; } = "";
        public string numAPI { get; set; } = "";
        public string statusWell { get; set; } = "";
        public string purposeWell { get; set; } = "";
        public string dTimSpud { get; set; } = "";
        public string dTimPa { get; set; } = "";
        public double wellheadElevation { get; set; } = 0;
        public double groundElevation { get; set; } = 0;
        public double waterDepth { get; set; } = 0;
        public double latitude { get; set; } = 0;
        public double longitude { get; set; } = 0;
        public double xCoOrd { get; set; } = 0;
        public double yCoOrd { get; set; } = 0;
        public string dtmPermanent { get; set; } = "";
        public string ServerKey { get; set; } = "";
        public string wmlsurl { get; set; } = "";
        public string wmlpurl { get; set; } = "";
        public DateTime lastDataReceived { get; set; }
        public DateTime lastRestartStarted { get; set; }
        public string AlarmHistoryTableName { get; set; } = "";
        public string RigName { get; set; } = "";
        public string EDRProvider { get; set; } = "";
        public string DataSource { get; set; } = "";

        public string DrillingSupr { get; set; } = "";
        public string DrillingEng { get; set; } = "";

        public bool Historical { get; set; } = false;

        public Dictionary<string, OffsetWell> offsetWells { get; set; } = new Dictionary<string, OffsetWell>();
        public Dictionary<string, Wellbore> wellbores { get; set; } = new Dictionary<string, Wellbore>();
        public AlarmPanel objAlarmPanel { get; set; } = new AlarmPanel();

        public int DisplayOrder { get; set; } = 0;

        public vmxSortOn SortOn { get; set; } = vmxSortOn.Name;

        public string AlarmProfileID { get; set; } = "";

        public string __timeLogWellboreID { get; set; } = "";
        public string __timeLogLogID { get; set; } = "";
        public bool __isActive { get; set; } = false;
        public string __timeLogDataTableName { get; set; } = "";

        public string wellDateFormat { get; set; } = wDateFormatLocal;

        public bool isExistingWell { get; set; } = false;

        public string dTimCreation { get; set; } = "";

        public bool hasRemarksLog { get; set; } = false;
        public DateTime __lastRemarksDate { get; set; }
        public TimeLog? __objRemarksLog { get; set; }

        public string SEC { get; set; } = "";
        public string TWP { get; set; } = "";
        public string RGE { get; set; } = "";
        public string LegalDesc { get; set; } = "";
        public string ContType { get; set; } = "";
        public string RigType { get; set; } = "";
        public string Pump1Model { get; set; } = "";
        public string Pump1Stroke { get; set; } = "";
        public string Pump1Liner { get; set; } = "";
        public string Pump2Model { get; set; } = "";
        public string Pump2Stroke { get; set; } = "";
        public string Pump2Liner { get; set; } = "";
        public string Pump3Model { get; set; } = "";
        public string Pump3Stroke { get; set; } = "";
        public string Pump3Liner { get; set; } = "";
        public string Rep { get; set; } = "";
        public string ToolPusher { get; set; } = "";
        public string TightHoleNo { get; set; } = "";
        public string ReEntryNo { get; set; } = "";
        public string Comments { get; set; } = "";
        public string Contractor { get; set; } = "";
        public string Objective { get; set; } = "";
        public string TDDate { get; set; } = "";
        public string TDFormation { get; set; } = "";

        public string Pump1 { get; set; } = "";
        public string Pump2 { get; set; } = "";
        public string Pump3 { get; set; } = "";

        public double RigCost { get; set; } = 0;
        public double DrlgConnTime { get; set; } = 0;
        public double TripConnTime { get; set; } = 0;

        public double BTSTime { get; set; } = 0;
        public double STSTime { get; set; } = 0;
        public double STBTime { get; set; } = 0;
        public double TripInSpeed { get; set; } = 0;
        public double TripOutSpeed { get; set; } = 0;

        public double PlannedDays { get; set; } = 0;
        public double PipeLength { get; set; } = 30;
        public double StandLength { get; set; } = 90;
        public double PlannedDepth { get; set; } = 0;

        public string DrlgEngDept { get; set; } = "";
        public string DrlgOpDept { get; set; } = "";
        public string WellOpType { get; set; } = "";
        public string BI { get; set; } = "";
        public string WellLocation { get; set; } = "";

        public double ROPBenchmark { get; set; } = 0;
        public double PipeMoveBenchmark { get; set; } = 0;
        public double TripStandBenchmark { get; set; } = 0;
        public double DrlgConnDeviation { get; set; } = 0;
        public double DrlgSTSDeviation { get; set; } = 0;
        public double ROPDeviation { get; set; } = 0;
        public double TripConnDeviation { get; set; } = 0;
        public double PipeMoveDeviation { get; set; } = 0;
        public double TripStandDeviation { get; set; } = 0;

        public Well GetCopy()
        {
            try
            {
                var objNew = new Well();

                objNew.ObjectID = this.ObjectID;
                objNew.name = this.name;
                objNew.nameLegal = this.nameLegal;
                objNew.numLicense = this.numLicense;
                objNew.numGovt = this.numGovt;
                objNew.dTimeLicense = this.dTimeLicense;
                objNew.field = this.field;
                objNew.country = this.country;
                objNew.county = this.county;
                objNew.state = this.state;
                objNew.region = this.region;
                objNew.district = this.district;
                objNew.block = this.block;
                objNew.timeZone = this.timeZone;
                objNew.operatorName = this.operatorName;
                objNew.operatorDiv = this.operatorDiv;
                objNew.pcInterest = this.pcInterest;
                objNew.numAPI = this.numAPI;
                objNew.statusWell = this.statusWell;
                objNew.purposeWell = this.purposeWell;
                objNew.dTimSpud = this.dTimSpud;
                objNew.dTimPa = this.dTimPa;
                objNew.wellheadElevation = this.wellheadElevation;
                objNew.groundElevation = this.groundElevation;
                objNew.waterDepth = this.waterDepth;
                objNew.latitude = this.latitude;
                objNew.longitude = this.longitude;
                objNew.xCoOrd = this.xCoOrd;
                objNew.yCoOrd = this.yCoOrd;
                objNew.dtmPermanent = this.dtmPermanent;
                objNew.ServerKey = this.ServerKey;
                objNew.wmlsurl = this.wmlsurl;
                objNew.wmlpurl = this.wmlpurl;
                objNew.lastDataReceived = this.lastDataReceived;
                objNew.lastRestartStarted = this.lastRestartStarted;
                objNew.AlarmHistoryTableName = this.AlarmHistoryTableName;
                objNew.RigName = this.RigName;
                objNew.DisplayOrder = this.DisplayOrder;
                objNew.__timeLogLogID = this.__timeLogLogID;
                objNew.__timeLogWellboreID = this.__timeLogWellboreID;
                objNew.__timeLogDataTableName = this.__timeLogDataTableName;
                objNew.wellDateFormat = this.wellDateFormat;
                objNew.isExistingWell = this.isExistingWell;
                objNew.dTimCreation = this.dTimCreation;
                objNew.DataSource = this.DataSource;
                objNew.DrillingSupr = this.DrillingSupr;
                objNew.Historical = this.Historical;

                objNew.hasRemarksLog = this.hasRemarksLog;
                objNew.__lastRemarksDate = this.__lastRemarksDate;

                objNew.SEC = this.SEC;
                objNew.TWP = this.TWP;
                objNew.RGE = this.RGE;
                objNew.LegalDesc = this.LegalDesc;
                objNew.ContType = this.ContType;
                objNew.RigType = this.RigType;
                objNew.Pump1Model = this.Pump1Model;
                objNew.Pump1Stroke = this.Pump1Stroke;
                objNew.Pump1Liner = this.Pump1Liner;
                objNew.Pump2Model = this.Pump2Model;
                objNew.Pump2Stroke = this.Pump2Stroke;
                objNew.Pump2Liner = this.Pump2Liner;
                objNew.Pump3Model = this.Pump3Model;
                objNew.Pump3Stroke = this.Pump3Stroke;
                objNew.Pump3Liner = this.Pump3Liner;
                objNew.Rep = this.Rep;
                objNew.ToolPusher = this.ToolPusher;
                objNew.TightHoleNo = this.TightHoleNo;
                objNew.ReEntryNo = this.ReEntryNo;
                objNew.Comments = this.Comments;
                objNew.Contractor = this.Contractor;
                objNew.Objective = this.Objective;
                objNew.TDDate = this.TDDate;
                objNew.TDFormation = this.TDFormation;

                objNew.Pump1 = this.Pump1;
                objNew.Pump2 = this.Pump2;
                objNew.Pump3 = this.Pump3;

                objNew.RigCost = this.RigCost;
                objNew.DrlgConnTime = this.DrlgConnTime;
                objNew.TripConnTime = this.TripConnTime;

                objNew.BTSTime = this.BTSTime;
                objNew.STSTime = this.STSTime;
                objNew.STBTime = this.STBTime;
                objNew.TripInSpeed = this.TripInSpeed;
                objNew.TripOutSpeed = this.TripOutSpeed;

                foreach (string objKey in this.wellbores.Keys)
                {
                    objNew.wellbores.Add(objKey, this.wellbores[objKey]);
                }

                if (this.objAlarmPanel != null)
                {
                    objNew.objAlarmPanel = this.objAlarmPanel.GetCopy();
                }

                foreach (string objKey in this.offsetWells.Keys)
                {
                    objNew.offsetWells.Add(objKey, this.offsetWells[objKey].GetCopy());
                }

                objNew.PlannedDays = this.PlannedDays;
                objNew.PipeLength = this.PipeLength;
                objNew.StandLength = this.StandLength;
                objNew.PlannedDepth = this.PlannedDepth;

                objNew.DrlgEngDept = this.DrlgEngDept;
                objNew.DrlgOpDept = this.DrlgOpDept;
                objNew.WellOpType = this.WellOpType;
                objNew.BI = this.BI;
                objNew.WellLocation = this.WellLocation;

                return objNew;
            }
            catch (Exception)
            {
                return new Well();
            }
        }

        public static bool IsWellExist(IDataServiceDIntel objDataService, string wellID)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(wellID)) return false;
                return objDataService.IsRecordExist("SELECT WELL_ID FROM VMX_WELL WHERE WELL_ID='" + wellID.Replace("'", "''") + "'");
            }
            catch
            {
                return false;
            }
        }

        public static bool CreateAlarmHistoryTable(IDataServiceDIntel objDataService, string WellID, string paramTableName)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(paramTableName)) return false;

                string tableName = paramTableName;

                string strSQL = "CREATE TABLE IF NOT EXISTS [" + tableName + "] ("
                    + "WELL_ID VARCHAR(100) NOT NULL,"
                    + " DATA_INDEX DECIMAL(5) NOT NULL,"
                    + " OBJECT_TYPE DECIMAL(1) NOT NULL,"
                    + " OBJECT_ID VARCHAR(100) NOT NULL,"
                    + " DATE_TIME DATETIME,"
                    + " DEPTH NUMERIC(16,5),"
                    + " MNEMONIC VARCHAR(100) NOT NULL,"
                    + " CHANNEL_NAME VARCHAR(100), "
                    + " VALUE DECIMAL(16,5),"
                    + " RIG_STATE DECIMAL(3),"
                    + " ALARM_STATUS DECIMAL(2),"
                    + " ACK_REQUIRED DECIMAL(1),"
                    + " ACK_STATUS DECIMAL(1),"
                    + " ACK_BY VARCHAR(50),"
                    + " ACK_COMMENT VARCHAR(4000),"
                    + " ALARM_DETAILS VARCHAR(4000),"
                    + " ACK_DATETIME DATETIME,"
                    + " ALARM_CONTAINERS VARCHAR(255),"
                    + " ALARM_CATEGORY_ID VARCHAR(50),"
                    + " ALARM_CATEGORY_NAME VARCHAR(50),"
                    + " PRIMARY KEY (WELL_ID, DATA_INDEX))";

                objDataService.ExecuteNonQuery(strSQL);

                strSQL = "CREATE INDEX IF NOT EXISTS [" + tableName + "_DATETIME] ON [" + tableName + "](WELL_ID,OBJECT_TYPE,DATE_TIME)";
                objDataService.ExecuteNonQuery(strSQL);

                strSQL = "CREATE INDEX IF NOT EXISTS [" + tableName + "_DEPTH] ON [" + tableName + "](WELL_ID,OBJECT_TYPE,DEPTH)";
                objDataService.ExecuteNonQuery(strSQL);

                strSQL = "CREATE INDEX IF NOT EXISTS [IDX1_" + tableName + "] ON [" + tableName + "](MNEMONIC,OBJECT_TYPE,DATE_TIME)";
                objDataService.ExecuteNonQuery(strSQL);

                strSQL = "CREATE INDEX IF NOT EXISTS [IDX2_" + tableName + "] ON [" + tableName + "](ACK_REQUIRED,ACK_STATUS)";
                objDataService.ExecuteNonQuery(strSQL);

                strSQL = "CREATE INDEX IF NOT EXISTS [IDX3_" + tableName + "] ON [" + tableName + "](OBJECT_TYPE,MNEMONIC)";
                objDataService.ExecuteNonQuery(strSQL);

                strSQL = "CREATE INDEX IF NOT EXISTS [IDX4_" + tableName + "] ON [" + tableName + "](OBJECT_TYPE,MNEMONIC,ALARM_STATUS)";
                objDataService.ExecuteNonQuery(strSQL);

                strSQL = "CREATE INDEX IF NOT EXISTS [IDX5_" + tableName + "] ON [" + tableName + "](OBJECT_TYPE)";
                objDataService.ExecuteNonQuery(strSQL);

                strSQL = "CREATE INDEX IF NOT EXISTS [IDX6_" + tableName + "] ON [" + tableName + "](DATE_TIME,OBJECT_TYPE,ALARM_CONTAINERS)";
                objDataService.ExecuteNonQuery(strSQL);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool IsWellActive(IDataServiceDIntel objDataService, string wellID)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(wellID)) return false;
                return objDataService.IsRecordExist("SELECT WELL_ID FROM VMX_WELL WHERE WELL_ID='" + wellID.Replace("'", "''") + "' AND UPPER(STATUS)='ACTIVE'");
            }
            catch
            {
                return false;
            }
        }
    }
}
