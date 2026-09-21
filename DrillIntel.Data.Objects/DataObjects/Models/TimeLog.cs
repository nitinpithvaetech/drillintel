using System;
using System.Collections.Generic;
using System.Data;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    public class TimeLog
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
        public string EDRProvider { get; set; } = "";
        public bool PrimaryLog { get; set; } = false;
        public bool RemarksLog { get; set; } = false;
        public bool DontMoveAhead { get; set; } = false;

        public string PiWellID { get; set; } = "";
        public string PiWellboreID { get; set; } = "";
        public string PiLogID { get; set; } = "";

        // Collection of log channel objects
        public Dictionary<string, LogChannel> logCurves { get; set; } = new Dictionary<string, LogChannel>();
        public Dictionary<string, LogChannel> LogCurves
        {
            get => logCurves;
            set => logCurves = value;
        }

        // Side Tracks
        public Dictionary<string, SideTrack> sideTracks { get; set; } = new Dictionary<string, SideTrack>();

        public Dictionary<string, LogVariable> Variables { get; set; } = new Dictionary<string, LogVariable>();

        public Dictionary<string, QCRule> QCRules { get; set; } = new Dictionary<string, QCRule>();
        public HookloadPlan objHookload { get; set; } = new HookloadPlan();
        public WOBPlan objWOBPlan { get; set; } = new WOBPlan();
        public double LastDate { get; set; } = 0;

        public bool CalcInProgress { get; set; } = true;

        public Dictionary<string, AdnlHookloadPlan> AdnlHookloadPlans { get; set; } = new Dictionary<string, AdnlHookloadPlan>();
        public Dictionary<string, AdnlHookloadPlan> ActualHkldData { get; set; } = new Dictionary<string, AdnlHookloadPlan>();
        public Dictionary<string, AdnlHookloadPlan> TorquePlans { get; set; } = new Dictionary<string, AdnlHookloadPlan>();
        public Dictionary<string, AdnlHookloadPlan> ActualTorqueData { get; set; } = new Dictionary<string, AdnlHookloadPlan>();

        public BulkCommandExecutor? objBulkExecutor { get; set; }

        public DataTable CHKShotWellStatusData { get; set; } = new DataTable();
        public bool ReadWellStatusFromCHKShot { get; set; } = false;

        public string __WellName { get; set; } = "";

        public string __dataTableName { get; set; } = "";
        public SystemSettings __objSystemSettings { get; set; } = new SystemSettings();
        public UnitConverter __objUnitConverter { get; set; } = new UnitConverter();
        public string __wellDateFormat { get; set; } = "";
        public rigState? __objWellRigStateSetup { get; set; }
        public bool __doLogSaveErrors { get; set; } = false;
        public string __logFileName { get; set; } = "";

        public long MAX_SECONDS_DIFF { get; set; } = 100000000;

        public Dictionary<int, wellSection> WellSectionList { get; set; } = new Dictionary<int, wellSection>(); // Nishant

        public bool LinkToParent { get; set; } = false;
        public string LinkWellID { get; set; } = "";
        public string LinkWellboreID { get; set; } = "";
        public string LinkLogID { get; set; } = "";
        public enumDuplicateAction DuplicateAction { get; set; } = enumDuplicateAction.OverwriteDuplicates;

        public bool DontCalcHoleDepth { get; set; } = true;
        public double StartingHoleDepth { get; set; } = 0;

        public bool DetectSpike { get; set; } = false;
        public double NearBottomDistance { get; set; } = 90;
        public double TolerancePc { get; set; } = 0.7;
        public double CheckTimePeriod { get; set; } = 60;
        public double CompareWindow { get; set; } = 0.1;
        public bool IsOpenSpike { get; set; } = false;
        public int ActionType { get; set; } = 0;
        public double MaxCloseTime { get; set; } = 720;

        public Dictionary<string, double> goodValues { get; set; } = new Dictionary<string, double>();

        // prath 23-07-2019
        public bool CreateRepOnFormation { get; set; } = false;
        public string SnapJobID { get; set; } = "";
        public string FormationTop { get; set; } = "";
        public Dictionary<string, string> FormationTops { get; set; } = new Dictionary<string, string>();
        public double DepthThreshold { get; set; } = 0;
        public double Frequency { get; set; } = 0;
        public DateTime LastSnapSendDatetime { get; set; }

        public class hookloadCalcParams
        {
            public string hWellID { get; set; } = "";
            public string hWellboreID { get; set; } = "";
            public string hLogID { get; set; } = "";
            public DataService? hobjDataService { get; set; }
        }

        public TimeLog GetCopy()
        {
            try
            {
                var objNew = new TimeLog();

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
                objNew.EDRProvider = this.EDRProvider;
                objNew.PrimaryLog = this.PrimaryLog;
                objNew.RemarksLog = this.RemarksLog;
                objNew.DontMoveAhead = this.DontMoveAhead;

                objNew.PiWellID = this.PiWellID;
                objNew.PiWellboreID = this.PiWellboreID;
                objNew.PiLogID = this.PiLogID;

                if (this.logCurves != null)
                {
                    foreach (var kvp in this.logCurves)
                    {
                        if (kvp.Value != null)
                        {
                            objNew.logCurves.Add(kvp.Key, kvp.Value.GetCopy());
                        }
                    }
                }

                if (this.sideTracks != null)
                {
                    foreach (var kvp in this.sideTracks)
                    {
                        if (kvp.Value != null)
                        {
                            objNew.sideTracks.Add(kvp.Key, kvp.Value.GetCopy());
                        }
                    }
                }

                if (this.Variables != null)
                {
                    foreach (var kvp in this.Variables)
                    {
                        if (kvp.Value != null)
                        {
                            objNew.Variables.Add(kvp.Key, kvp.Value.GetCopy());
                        }
                    }
                }

                if (this.QCRules != null)
                {
                    foreach (var kvp in this.QCRules)
                    {
                        if (kvp.Value != null)
                        {
                            objNew.QCRules.Add(kvp.Key, kvp.Value.GetCopy());
                        }
                    }
                }

                if (this.objHookload != null)
                {
                    objNew.objHookload = this.objHookload.GetCopy();
                }

                if (this.objWOBPlan != null)
                {
                    objNew.objWOBPlan = this.objWOBPlan.GetCopy();
                }

                objNew.LastDate = this.LastDate;
                objNew.CalcInProgress = this.CalcInProgress;

                if (this.AdnlHookloadPlans != null)
                {
                    foreach (var kvp in this.AdnlHookloadPlans)
                    {
                        if (kvp.Value != null)
                        {
                            objNew.AdnlHookloadPlans.Add(kvp.Key, kvp.Value.GetCopy());
                        }
                    }
                }

                if (this.ActualHkldData != null)
                {
                    foreach (var kvp in this.ActualHkldData)
                    {
                        if (kvp.Value != null)
                        {
                            objNew.ActualHkldData.Add(kvp.Key, kvp.Value.GetCopy());
                        }
                    }
                }

                if (this.TorquePlans != null)
                {
                    foreach (var kvp in this.TorquePlans)
                    {
                        if (kvp.Value != null)
                        {
                            objNew.TorquePlans.Add(kvp.Key, kvp.Value.GetCopy());
                        }
                    }
                }

                if (this.ActualTorqueData != null)
                {
                    foreach (var kvp in this.ActualTorqueData)
                    {
                        if (kvp.Value != null)
                        {
                            objNew.ActualTorqueData.Add(kvp.Key, kvp.Value.GetCopy());
                        }
                    }
                }

                if (this.objBulkExecutor != null)
                {
                    objNew.objBulkExecutor = this.objBulkExecutor.GetCopy();
                }

                if (this.CHKShotWellStatusData != null)
                {
                    objNew.CHKShotWellStatusData = this.CHKShotWellStatusData.Copy();
                }

                objNew.ReadWellStatusFromCHKShot = this.ReadWellStatusFromCHKShot;
                objNew.__WellName = this.__WellName;
                objNew.__dataTableName = this.__dataTableName;

                if (this.__objSystemSettings != null)
                {
                    objNew.__objSystemSettings = this.__objSystemSettings.GetCopy();
                }

                if (this.__objUnitConverter != null)
                {
                    objNew.__objUnitConverter = this.__objUnitConverter.GetCopy();
                }

                objNew.__wellDateFormat = this.__wellDateFormat;

                if (this.__objWellRigStateSetup != null)
                {
                    objNew.__objWellRigStateSetup = this.__objWellRigStateSetup.GetCopy();
                }

                objNew.__doLogSaveErrors = this.__doLogSaveErrors;
                objNew.__logFileName = this.__logFileName;
                objNew.MAX_SECONDS_DIFF = this.MAX_SECONDS_DIFF;

                if (this.WellSectionList != null)
                {
                    foreach (var kvp in this.WellSectionList)
                    {
                        if (kvp.Value != null)
                        {
                            objNew.WellSectionList.Add(kvp.Key, kvp.Value.GetCopy());
                        }
                    }
                }

                objNew.LinkToParent = this.LinkToParent;
                objNew.LinkWellID = this.LinkWellID;
                objNew.LinkWellboreID = this.LinkWellboreID;
                objNew.LinkLogID = this.LinkLogID;
                objNew.DuplicateAction = this.DuplicateAction;

                objNew.DontCalcHoleDepth = this.DontCalcHoleDepth;
                objNew.StartingHoleDepth = this.StartingHoleDepth;
                objNew.DetectSpike = this.DetectSpike;
                objNew.NearBottomDistance = this.NearBottomDistance;
                objNew.TolerancePc = this.TolerancePc;
                objNew.CheckTimePeriod = this.CheckTimePeriod;
                objNew.CompareWindow = this.CompareWindow;
                objNew.IsOpenSpike = this.IsOpenSpike;
                objNew.ActionType = this.ActionType;
                objNew.MaxCloseTime = this.MaxCloseTime;

                if (this.goodValues != null)
                {
                    foreach (var kvp in this.goodValues)
                    {
                        objNew.goodValues.Add(kvp.Key, kvp.Value);
                    }
                }

                objNew.CreateRepOnFormation = this.CreateRepOnFormation;
                objNew.SnapJobID = this.SnapJobID;
                objNew.FormationTop = this.FormationTop;

                if (this.FormationTops != null)
                {
                    foreach (var kvp in this.FormationTops)
                    {
                        objNew.FormationTops.Add(kvp.Key, kvp.Value);
                    }
                }

                objNew.DepthThreshold = this.DepthThreshold;
                objNew.Frequency = this.Frequency;
                objNew.LastSnapSendDatetime = this.LastSnapSendDatetime;

                return objNew;
            }
            catch (Exception)
            {
                return new TimeLog();
            }
        }
    }
}

