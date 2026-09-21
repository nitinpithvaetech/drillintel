using System;
using System.Collections.Generic;
using DrillIntel.Data;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    public class rigState
    {
        public bool UseWellSectionRigState { get; set; } = false; // Nishant
        public Dictionary<int, wellSection> WellSectionList { get; set; } = new Dictionary<int, wellSection>(); // Nishant

        public rigState? objWellRigState { get; set; }

        public SystemSettings? objSystemSettings { get; set; }

        #region Public Members
        // List of rig states
        public Dictionary<int, rigStateItem> rigStates { get; set; } = new Dictionary<int, rigStateItem>();

        public string ID { get; set; } = "";

        // Threshold Values
        public string UnknownName { get; set; } = "Unknown";
        public float UnknownNumber { get; set; } = 15;
        public double UnknownColor { get; set; } = 0;
        public string UnknownColorHex { get; set; } = "";
        public double HookloadCutOff { get; set; } = 0;
        public double RPMCutOff { get; set; } = 0;
        public double CIRCCutOff { get; set; } = 0;
        public double Sensitivity { get; set; } = 0;
        public double PumpPressureCutOff { get; set; } = 0;
        public double DepthComparisonSens { get; set; } = 2;
        public bool DetectAutoSlideDrilling { get; set; } = false;
        public bool DetectAirDrilling { get; set; } = false;
        public double AirPressure { get; set; } = 0;
        public double TorqueCutOff { get; set; } = 0;
        public double MistFlowCutOff { get; set; } = 0;

        public double TorqueMin { get; set; } = 0;
        public double TorqueMax { get; set; } = 0;
        public int CalibrationRows { get; set; } = 2;
        public double MinTorqueDifference { get; set; } = 0;
        public double MinRPM { get; set; } = 0;
        public double MaxRPM { get; set; } = 0;

        public int SelectedSet { get; set; } = 0;

        public double TorqueMin2 { get; set; } = 0;
        public double TorqueMax2 { get; set; } = 0;
        public int CalibrationRows2 { get; set; } = 2;
        public double MinTorqueDifference2 { get; set; } = 0;
        public double MinRPM2 { get; set; } = 0;
        public double MaxRPM2 { get; set; } = 0;

        public double TorqueMin3 { get; set; } = 0;
        public double TorqueMax3 { get; set; } = 0;
        public int CalibrationRows3 { get; set; } = 2;
        public double MinTorqueDifference3 { get; set; } = 0;
        public double MinRPM3 { get; set; } = 0;
        public double MaxRPM3 { get; set; } = 0;

        public const string cnRPM = "RPM";
        public const string cnSTOR = "STOR";
        public const string cnCIRC = "CIRC";
        public const string cnDEPTH = "DEPTH";
        public const string cnHDTH = "HDTH";
        public const string cnHKLD = "HKLD";
        public const string cnSPPA = "SPPA";
        public const string cnRIGSTATE = "RIG_STATE";
        public const string cnRIGSTATECOLOR = "RIG_STATE_COLOR";
        public const string cnDATETIME = "DATETIME";
        public const string cnAirPressure = "AIR_PRESSURE";
        public const string cnMistFlow = "MIST_FLOW";

        public bool DoNotPause { get; set; } = false;

        public Dictionary<int, AutoSlideSettings> autoSlideSetupList { get; set; } = new Dictionary<int, AutoSlideSettings>();

        public int TorqueCycles { get; set; } = 0;
        public int CalibrationTime { get; set; } = 2;
        public double PercentWindow { get; set; } = 20;

        public bool __doLogSaveErrors { get; set; } = false;
        public string __logFileName { get; set; } = "";

        public bool DetectPipeMovement { get; set; } = false;
        public double PipeMovementThreshold { get; set; } = 15;

        public enum enumDirection
        {
            Stall = 0,
            Up = 1,
            Down = 2
        }

        #endregion

        public rigState? GetCopy()
        {
            try
            {
                var objNew = new rigState();
                objNew.UseWellSectionRigState = this.UseWellSectionRigState;

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

                if (this.objWellRigState != null)
                {
                    objNew.objWellRigState = this.objWellRigState.GetCopy();
                }

                if (this.objSystemSettings != null)
                {
                    objNew.objSystemSettings = this.objSystemSettings.GetCopy();
                }

                objNew.ID = this.ID;
                objNew.UnknownName = this.UnknownName;
                objNew.UnknownNumber = this.UnknownNumber;
                objNew.UnknownColor = this.UnknownColor;
                objNew.UnknownColorHex = this.UnknownColorHex; // React Side

                objNew.HookloadCutOff = this.HookloadCutOff;
                objNew.RPMCutOff = this.RPMCutOff;
                objNew.CIRCCutOff = this.CIRCCutOff;
                objNew.Sensitivity = this.Sensitivity;
                objNew.PumpPressureCutOff = this.PumpPressureCutOff;
                objNew.DepthComparisonSens = this.DepthComparisonSens;
                objNew.DetectAutoSlideDrilling = this.DetectAutoSlideDrilling;
                objNew.TorqueMax = this.TorqueMax;
                objNew.TorqueMin = this.TorqueMin;
                objNew.CalibrationRows = this.CalibrationRows;
                objNew.MinTorqueDifference = this.MinTorqueDifference;
                objNew.MinRPM = this.MinRPM;
                objNew.MaxRPM = this.MaxRPM;

                objNew.TorqueMax2 = this.TorqueMax2;
                objNew.TorqueMin2 = this.TorqueMin2;
                objNew.CalibrationRows2 = this.CalibrationRows2;
                objNew.MinTorqueDifference2 = this.MinTorqueDifference2;
                objNew.MinRPM2 = this.MinRPM2;
                objNew.MaxRPM2 = this.MaxRPM2;

                objNew.TorqueMax3 = this.TorqueMax3;
                objNew.TorqueMin3 = this.TorqueMin3;
                objNew.CalibrationRows3 = this.CalibrationRows3;
                objNew.MinTorqueDifference3 = this.MinTorqueDifference3;
                objNew.MinRPM3 = this.MinRPM3;
                objNew.MaxRPM3 = this.MaxRPM3;
                objNew.TorqueCutOff = this.TorqueCutOff;
                objNew.MistFlowCutOff = this.MistFlowCutOff;

                objNew.SelectedSet = this.SelectedSet;

                objNew.DetectAirDrilling = this.DetectAirDrilling;
                objNew.AirPressure = this.AirPressure;
                objNew.TorqueCycles = this.TorqueCycles;
                objNew.CalibrationTime = this.CalibrationTime;
                objNew.PercentWindow = this.PercentWindow;

                objNew.DoNotPause = this.DoNotPause;
                objNew.__doLogSaveErrors = this.__doLogSaveErrors;
                objNew.__logFileName = this.__logFileName;

                objNew.DetectPipeMovement = this.DetectPipeMovement;
                objNew.PipeMovementThreshold = this.PipeMovementThreshold;

                if (this.rigStates != null)
                {
                    foreach (int objKey in this.rigStates.Keys)
                    {
                        if (this.rigStates[objKey] != null)
                        {
                            objNew.rigStates.Add(objKey, this.rigStates[objKey].GetCopy());
                        }
                    }
                }

                if (this.autoSlideSetupList == null)
                {
                    this.autoSlideSetupList = new Dictionary<int, AutoSlideSettings>();
                }

                foreach (int objKey in this.autoSlideSetupList.Keys)
                {
                    if (this.autoSlideSetupList[objKey] != null)
                    {
                        objNew.autoSlideSetupList.Add(objKey, this.autoSlideSetupList[objKey].GetCopy());
                    }
                }

                return objNew;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static rigState? LoadRigRigStateSetup(IDataServiceDIntel objDataService, string rigName)
        {
            try
            {
                return null;
            }
            catch
            {
                return null;
            }
        }

        public static bool SaveWellRigStateSetup(IDataServiceDIntel objDataService, string wellID, rigState objRigState, string remarks = "")
        {
            try
            {
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static rigState? LoadCommonRigStateSetup(IDataServiceDIntel objDataService)
        {
            try
            {
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}

