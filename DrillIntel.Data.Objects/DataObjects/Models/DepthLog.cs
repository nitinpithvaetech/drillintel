using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    public enum enumDuplicateAction
    {
        OverwriteDuplicates = 0,
        SkipDuplicates = 1,
        MergeDuplicates = 2,
        MergeColumns = 2,
        MergeBoth = 3,
        MergeColumnsNoDuplicate = 4,
        MergeBothNoDuplicate = 5
    }


    public class DepthLog
    {
        public string ObjectID { get; set; } = "";
        public string WellID { get; set; } = "";
        public string WellboreID { get; set; } = "";
        public string nameWell { get; set; } = "";
        public string nameWellbore { get; set; } = "";
        public string nameLog { get; set; } = "";
        public string serviceCompany { get; set; } = "";
        public string runNumber { get; set; } = "";
        public string creationDate { get; set; } = "";
        public string description { get; set; } = "";
        public string indexType { get; set; } = "";
        public string startIndex { get; set; } = "";
        public string endIndex { get; set; } = "";
        public string lastDataIndex { get; set; } = "";
        public string stepIncrement { get; set; } = "";
        public string direction { get; set; } = "";
        public string indexCurve { get; set; } = "";
        public string columnIndex { get; set; } = "";
        public string indexUnits { get; set; } = "";
        public string uomNamingSystem { get; set; } = "";
        public string otherData { get; set; } = "";
        public string nullValue { get; set; } = "";
        public string dTimCreation { get; set; } = "";
        public string dTimLastChange { get; set; } = "";
        public string itemState { get; set; } = "";
        public string comments { get; set; } = "";
        public string WITSMLColumnIndex { get; set; } = "";
        public string ServerKey { get; set; } = "";
        public string wmlsurl { get; set; } = "";
        public string wmlpurl { get; set; } = "";
        public DateTime lastDataReceived { get; set; }
        public DateTime lastRestartStarted { get; set; }
        public string vshalChannelMnemonic { get; set; } = "";
        public string EDRProvider { get; set; } = "";
        public string __WellName { get; set; } = "";

        public string PiWellID { get; set; } = "";
        public string PiWellboreID { get; set; } = "";
        public string PiLogID { get; set; } = "";

        public string __dataTableName { get; set; } = "";

        public enumDuplicateAction DuplicateAction { get; set; } = enumDuplicateAction.OverwriteDuplicates;

        public bool PrimaryLog { get; set; } = false;
        // Collections
        public Dictionary<string, LogChannel> LogCurves { get; set; } = new Dictionary<string, LogChannel>();

        //public Dictionary<string, LogVariable> Variables { get; set; } = new Dictionary<string, LogVariable>();
        //public Dictionary<string, QCRule> QCRules { get; set; } = new Dictionary<string, QCRule>();
        //public Dictionary<string, ImageLogDataSet> ImageLogDataSets { get; set; } = new Dictionary<string, ImageLogDataSet>();


        //public SystemSettings __objSystemSettings { get; set; } = new SystemSettings();
        //public UnitConverter __objUnitConverter { get; set; } = new UnitConverter();
        //public UnitConverter __objDepthUnitConverter { get; set; } = new UnitConverter();
        //public bool __doConvertDepthUnits { get; set; } = false;
        //public bool __doLogSaveErrors { get; set; } = false;
        //public string __logFileName { get; set; } = "";

        public bool LinkToParent { get; set; } = false;
        public string LinkWellID { get; set; } = "";
        public string LinkWellboreID { get; set; } = "";
        public string LinkLogID { get; set; } = "";

        public DepthLog GetCopy()
        {
            try
            {
                var objNew = new DepthLog();

                objNew.ObjectID = this.ObjectID;
                objNew.WellID = this.WellID;
                objNew.WellboreID = this.WellboreID;
                objNew.nameWell = this.nameWell;
                objNew.nameWellbore = this.nameWellbore;
                objNew.nameLog = this.nameLog;
                objNew.serviceCompany = this.serviceCompany;
                objNew.runNumber = this.runNumber;
                objNew.creationDate = this.creationDate;
                objNew.description = this.description;
                objNew.indexType = this.indexType;
                objNew.startIndex = this.startIndex;
                objNew.endIndex = this.endIndex;
                objNew.lastDataIndex = this.lastDataIndex;
                objNew.stepIncrement = this.stepIncrement;
                objNew.direction = this.direction;
                objNew.indexCurve = this.indexCurve;
                objNew.columnIndex = this.columnIndex;
                objNew.indexUnits = this.indexUnits;
                objNew.uomNamingSystem = this.uomNamingSystem;
                objNew.otherData = this.otherData;
                objNew.nullValue = this.nullValue;
                objNew.dTimCreation = this.dTimCreation;
                objNew.dTimLastChange = this.dTimLastChange;
                objNew.itemState = this.itemState;
                objNew.comments = this.comments;
                objNew.WITSMLColumnIndex = this.WITSMLColumnIndex;
                objNew.ServerKey = this.ServerKey;
                objNew.wmlsurl = this.wmlsurl;
                objNew.wmlpurl = this.wmlpurl;
                objNew.lastDataReceived = this.lastDataReceived;
                objNew.lastRestartStarted = this.lastRestartStarted;
                objNew.vshalChannelMnemonic = this.vshalChannelMnemonic;
                objNew.EDRProvider = this.EDRProvider;
                objNew.__WellName = this.__WellName;
                objNew.PiWellID = this.PiWellID;
                objNew.PiWellboreID = this.PiWellboreID;
                objNew.PiLogID = this.PiLogID;
                objNew.__dataTableName = this.__dataTableName;
                objNew.DuplicateAction = this.DuplicateAction;
                objNew.PrimaryLog = this.PrimaryLog;

                if (this.LogCurves != null)
                {
                    foreach (var kvp in this.LogCurves)
                    {
                        if (kvp.Value != null)
                        {
                            objNew.LogCurves.Add(kvp.Key, kvp.Value.GetCopy());
                        }
                    }
                }

                objNew.LinkToParent = this.LinkToParent;
                objNew.LinkWellID = this.LinkWellID;
                objNew.LinkWellboreID = this.LinkWellboreID;
                objNew.LinkLogID = this.LinkLogID;

                return objNew;
            }
            catch (Exception)
            {
                return new DepthLog();
            }
        }
    }
}
