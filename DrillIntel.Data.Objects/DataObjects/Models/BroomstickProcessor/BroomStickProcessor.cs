using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor;

namespace DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor
{
    #region Logger Abstraction
    public interface IBroomstickLogger
    {
        void Debug(string message);
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }

    public class DefaultBroomstickLogger : IBroomstickLogger
    {
        private readonly Action<string>? _logAction;

        public DefaultBroomstickLogger(Action<string>? logAction = null)
        {
            _logAction = logAction;
        }

        public void Debug(string message)
        {
            _logAction?.Invoke($"[DEBUG] {message}");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] {message}");
        }

        public void Info(string message)
        {
            _logAction?.Invoke($"[INFO] {message}");
            System.Diagnostics.Debug.WriteLine($"[INFO] {message}");
        }

        public void Warn(string message)
        {
            _logAction?.Invoke($"[WARN] {message}");
            System.Diagnostics.Debug.WriteLine($"[WARN] {message}");
        }

        public void Error(string message)
        {
            _logAction?.Invoke($"[ERROR] {message}");
            System.Diagnostics.Debug.WriteLine($"[ERROR] {message}");
        }
    }
    #endregion

    #region Task Progress Abstraction
    public static class VuMaxProTask
    {
        public static void updateTaskProgress(ref IDataServiceDIntel objDataService, string taskID, double progress)
        {
            updateTaskProgress(objDataService, taskID, progress);
        }

        public static void updateTaskProgress(IDataServiceDIntel objDataService, string taskID, double progress)
        {
            if (objDataService == null || string.IsNullOrWhiteSpace(taskID)) return;
            try
            {
                double lnProgress = Math.Round(progress, 2);
                if (lnProgress > 100) lnProgress = 100;

                string strSQL = "UPDATE eVuMaxTasks SET [Progress] = @Progress WHERE [TaskId] = @TaskId";
                var parameters = new Dictionary<string, object?>
                {
                    ["@Progress"] = lnProgress,
                    ["@TaskId"] = taskID
                };
                objDataService.ExecuteNonQuery(strSQL, parameters);
            }
            catch
            {
            }
        }

        public static bool isTaskCancelled(ref IDataServiceDIntel objDataService, string taskID)
        {
            return isTaskCancelled(objDataService, taskID);
        }

        public static bool isTaskCancelled(IDataServiceDIntel objDataService, string taskID)
        {
            if (objDataService == null || string.IsNullOrWhiteSpace(taskID)) return false;
            try
            {
                string strSQL = "SELECT [Cancelled] FROM eVuMaxTasks WHERE [TaskId] = @TaskId LIMIT 1";
                var parameters = new Dictionary<string, object?> { ["@TaskId"] = taskID };
                object? val = objDataService.GetValue(strSQL, parameters);
                if (val != null && val != DBNull.Value)
                {
                    return Convert.ToInt32(val) == 1;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
    #endregion

    /// <summary>
    /// BroomStickProcessor calculates Hookload and Torque broomstick points from time log connection data,
    /// implementing pick up, slack off, rotate-off-bottom (ROB), and on-bottom torque detection.
    /// Adapted for SQLite database persistence via IDataServiceDIntel.
    /// </summary>
    public class BroomStickProcessor
    {
        // ================= External References ============================= //
        public string TaskID = "";
        public string WellID = "";
        public IDataServiceDIntel objDataService;
        // =================================================================== //

        private Dictionary<int, wellSection> WellSectionList = new Dictionary<int, wellSection>();
        private wellSection objWellSection = new wellSection();
        #pragma warning disable CS0414
        private bool useWellSectionPUSOSettings = true;
#pragma warning restore CS0414

        public Dictionary<int, PUSOPoint> PUPoints = new Dictionary<int, PUSOPoint>();
        public Dictionary<int, PUSOPoint> SOPoints = new Dictionary<int, PUSOPoint>();
        public Dictionary<int, PUSOPoint> RotatePoints = new Dictionary<int, PUSOPoint>();
        public Dictionary<int, PUSOPoint> OnBottomTorquePoints = new Dictionary<int, PUSOPoint>();

        public double MAX_DEPTH_DIFF = 70d;

        public TimeLog _objTimeLog;
        public BroomStickSetup _objSetup = new BroomStickSetup();

        private Dictionary<double, double> pkupRigStates = new Dictionary<double, double>();
        private Dictionary<double, double> slkRigStates = new Dictionary<double, double>();
        private Dictionary<double, double> rotRigStates = new Dictionary<double, double>();
        public bool CombinedMode = false;

        public rigState objRigState;

        #region Algorithm Settings
        public bool RUN_DRLG_CHECK = true;
        public bool RUN_HKLD_CHECK = true;
        #endregion

        public IBroomstickLogger processLogger = new DefaultBroomstickLogger();

        public BroomStickProcessor()
        {
        }

        public BroomStickProcessor(
            IDataServiceDIntel dataService,
            string wellId,
            TimeLog? timeLog = null,
            BroomStickSetup? setup = null,
            IBroomstickLogger? logger = null)
        {
            objDataService = dataService;
            WellID = wellId;
            _objTimeLog = timeLog!;
            if (setup != null) _objSetup = setup;
            if (logger != null) processLogger = logger;
        }

        /// <summary>
        /// Ensures all required SQLite tables and schemas exist for Broomstick processing.
        /// </summary>
        public static void EnsureTables(IDataServiceDIntel db)
        {
            if (db == null) return;
            try
            {
                PUSOPoint.EnsureTableExists(db);
                BroomstickProfile.EnsureTableExists(db);

                const string ddl = @"
CREATE TABLE IF NOT EXISTS VMX_AKPI_DRLG_CONNECTIONS (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    WELL_ID TEXT NOT NULL,
    DEPTH REAL,
    FROM_DATE TEXT,
    TO_DATE TEXT
);
CREATE TABLE IF NOT EXISTS VMX_BROOMSTICK_PRC_INFO (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    WELL_ID TEXT NOT NULL,
    TYPE INTEGER NOT NULL,
    PRC_LAST_DATE TEXT
);
CREATE TABLE IF NOT EXISTS VMX_BS_WELL_PROFILE (
    WELL_ID TEXT NOT NULL PRIMARY KEY,
    BS_PROFILE_ID TEXT,
    HKLD_PROFILE_ID TEXT
);
CREATE TABLE IF NOT EXISTS eVuMaxBroomstickWarnings (
    ID TEXT PRIMARY KEY,
    WellId TEXT NOT NULL,
    Warning TEXT
);
CREATE TABLE IF NOT EXISTS eVuMaxBroomstickProcessLog (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    WellId TEXT NOT NULL,
    HoleDepth REAL,
    ConnStartDate TEXT,
    ConnEndDate TEXT,
    ProcessLog BLOB
);
CREATE TABLE IF NOT EXISTS eVuMaxTasks (
    TaskId TEXT PRIMARY KEY,
    WellId TEXT,
    TaskDescriptor TEXT,
    Progress REAL,
    Progresstext TEXT,
    Cancelled INTEGER DEFAULT 0
);
CREATE INDEX IF NOT EXISTS IDX_VMX_AKPI_DRLG_CONN ON VMX_AKPI_DRLG_CONNECTIONS(WELL_ID, FROM_DATE, DEPTH);
CREATE INDEX IF NOT EXISTS IDX_VMX_BS_PRC_INFO ON VMX_BROOMSTICK_PRC_INFO(WELL_ID, TYPE);
";
                db.ExecuteNonQuery(ddl);
            }
            catch
            {
            }
        }

        public Dictionary<double, CombinedPUSO> getCombinedList()
        {
            try
            {
                var uniqueDepths = new Dictionary<double, double>();

                PUSOPoint[] arrPUPoints = PUPoints.Values.ToArray();
                Array.Sort(arrPUPoints);

                PUSOPoint[] arrSOPoints = SOPoints.Values.ToArray();
                Array.Sort(arrSOPoints);

                PUSOPoint[] arrRotatePoints = RotatePoints.Values.ToArray();
                Array.Sort(arrRotatePoints);

                for (int i = 0; i <= arrPUPoints.Length - 1; i++)
                {
                    double lnDepth = arrPUPoints[i].Depth;
                    if (!uniqueDepths.ContainsKey(lnDepth))
                    {
                        uniqueDepths.Add(lnDepth, lnDepth);
                    }
                }

                for (int i = 0; i <= arrSOPoints.Length - 1; i++)
                {
                    double lnDepth = arrSOPoints[i].Depth;
                    if (!uniqueDepths.ContainsKey(lnDepth))
                    {
                        uniqueDepths.Add(lnDepth, lnDepth);
                    }
                }

                for (int i = 0; i <= arrRotatePoints.Length - 1; i++)
                {
                    double lnDepth = arrRotatePoints[i].Depth;
                    if (!uniqueDepths.ContainsKey(lnDepth))
                    {
                        uniqueDepths.Add(lnDepth, lnDepth);
                    }
                }

                var list = new Dictionary<double, CombinedPUSO>();

                foreach (double lnDepth in uniqueDepths.Values)
                {
                    int MaxCount = 0;
                    int puCount = 0;
                    int soCount = 0;
                    int rotateCount = 0;

                    var subPUPoints = new Dictionary<double, PUSOPoint>();
                    var subSOPoints = new Dictionary<double, PUSOPoint>();
                    var subRotatePoints = new Dictionary<double, PUSOPoint>();

                    for (int i = 0; i <= arrPUPoints.Length - 1; i++)
                    {
                        if (arrPUPoints[i].Depth == lnDepth)
                        {
                            puCount += 1;
                            subPUPoints.Add(subPUPoints.Count + 1, arrPUPoints[i].getCopy());
                        }
                    }

                    for (int i = 0; i <= arrSOPoints.Length - 1; i++)
                    {
                        if (arrSOPoints[i].Depth == lnDepth)
                        {
                            soCount += 1;
                            subSOPoints.Add(subSOPoints.Count + 1, arrSOPoints[i].getCopy());
                        }
                    }

                    for (int i = 0; i <= arrRotatePoints.Length - 1; i++)
                    {
                        if (arrRotatePoints[i].Depth == lnDepth)
                        {
                            rotateCount += 1;
                            subRotatePoints.Add(subRotatePoints.Count + 1, arrRotatePoints[i].getCopy());
                        }
                    }

                    if (puCount > MaxCount) MaxCount = puCount;
                    if (soCount > MaxCount) MaxCount = soCount;
                    if (rotateCount > MaxCount) MaxCount = rotateCount;

                    PUSOPoint[] arrSubPUPoints = subPUPoints.Values.ToArray();
                    PUSOPoint[] arrSubSOPoints = subSOPoints.Values.ToArray();
                    PUSOPoint[] arrSubRotatePoints = subRotatePoints.Values.ToArray();

                    for (int i = 0; i <= MaxCount - 1; i++)
                    {
                        var objCombinedPoint = new CombinedPUSO();

                        // Pick Up ...
                        if (i <= arrSubPUPoints.Length - 1)
                        {
                            objCombinedPoint.Depth = arrSubPUPoints[i].Depth;
                            objCombinedPoint.DynamicPU = arrSubPUPoints[i].DynamicValue;
                            objCombinedPoint.Circulation = arrSubPUPoints[i].Circulation;
                            objCombinedPoint.PumpPressure = arrSubPUPoints[i].PumpPressure;
                            objCombinedPoint.BlockMovement = arrSubPUPoints[i].BlockMovement;
                            objCombinedPoint.RPM = arrSubPUPoints[i].RPM;
                            objCombinedPoint.RigStates = arrSubPUPoints[i].RigStates;

                            if (arrSubPUPoints[i].PumpStatus == PUSOPoint.pusoPumpStatus.PumpOn)
                            {
                                objCombinedPoint.PUPumpStatus = 1;
                            }
                            else
                            {
                                objCombinedPoint.PUPumpStatus = 0;
                            }

                            switch (_objSetup.pickupPointSelectionMethod)
                            {
                                case BroomStickSetup.bmStaticMethod.Min:
                                    objCombinedPoint.StaticPU = arrSubPUPoints[i].Min;
                                    break;
                                case BroomStickSetup.bmStaticMethod.Max:
                                    objCombinedPoint.StaticPU = arrSubPUPoints[i].Max;
                                    break;
                                case BroomStickSetup.bmStaticMethod.Avg:
                                    objCombinedPoint.StaticPU = arrSubPUPoints[i].Avg;
                                    break;
                            }
                        }

                        // Slack Off ...
                        if (i <= arrSubSOPoints.Length - 1)
                        {
                            objCombinedPoint.Depth = arrSubSOPoints[i].Depth;
                            objCombinedPoint.DynamicSO = arrSubSOPoints[i].DynamicValue;
                            objCombinedPoint.Circulation = arrSubSOPoints[i].Circulation;
                            objCombinedPoint.PumpPressure = arrSubSOPoints[i].PumpPressure;
                            objCombinedPoint.BlockMovement = arrSubSOPoints[i].BlockMovement;
                            objCombinedPoint.RPM = arrSubSOPoints[i].RPM;
                            objCombinedPoint.RigStates = arrSubSOPoints[i].RigStates;

                            if (arrSubSOPoints[i].PumpStatus == PUSOPoint.pusoPumpStatus.PumpOn)
                            {
                                objCombinedPoint.SOPumpStatus = 1;
                            }
                            else
                            {
                                objCombinedPoint.SOPumpStatus = 0;
                            }

                            switch (_objSetup.slackOffPointSelectionMethod)
                            {
                                case BroomStickSetup.bmStaticMethod.Min:
                                    objCombinedPoint.StaticSO = arrSubSOPoints[i].Min;
                                    break;
                                case BroomStickSetup.bmStaticMethod.Max:
                                    objCombinedPoint.StaticSO = arrSubSOPoints[i].Max;
                                    break;
                                case BroomStickSetup.bmStaticMethod.Avg:
                                    objCombinedPoint.StaticSO = arrSubSOPoints[i].Avg;
                                    objCombinedPoint.StaticSO2 = arrSubSOPoints[i].Avg2;
                                    break;
                            }
                        }

                        // Rotate ...
                        if (i <= arrSubRotatePoints.Length - 1)
                        {
                            objCombinedPoint.Depth = arrSubRotatePoints[i].Depth;
                            objCombinedPoint.Rotate = arrSubRotatePoints[i].DynamicValue;
                            objCombinedPoint.Circulation = arrSubRotatePoints[i].Circulation;
                            objCombinedPoint.PumpPressure = arrSubRotatePoints[i].PumpPressure;
                            objCombinedPoint.BlockMovement = (int)arrSubRotatePoints[i].BlockMovement;
                            objCombinedPoint.RPM = arrSubRotatePoints[i].RPM;
                            objCombinedPoint.RigStates = arrSubRotatePoints[i].RigStates;

                            if (arrSubRotatePoints[i].PumpStatus == PUSOPoint.pusoPumpStatus.PumpOn)
                            {
                                objCombinedPoint.PUPumpStatus = 1;
                            }
                            else
                            {
                                objCombinedPoint.PUPumpStatus = 0;
                            }
                        }

                        if (objCombinedPoint.DynamicPU == 0 && objCombinedPoint.DynamicSO == 0 && objCombinedPoint.Rotate == 0)
                        {
                        }
                        else
                        {
                            list.Add(list.Count + 1, objCombinedPoint);
                        }
                    }
                }

                return list;
            }
            catch (Exception)
            {
                return new Dictionary<double, CombinedPUSO>();
            }
        }

        private wellSection? getPUSOSettingsfromWellSection(double paramDepth)
        {
            try
            {
                if (WellSectionList != null)
                {
                    foreach (wellSection objWellSection_ in WellSectionList.Values)
                    {
                        if (paramDepth >= objWellSection_.StartDepth && paramDepth <= objWellSection_.EndDepth)
                        {
                            return objWellSection_;
                        }
                    }
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        public void ProcessPoints()
        {
            try
            {
                EnsureTables(objDataService);

                // Clear warnings 
                //clearBroomstickWarnings();
                //clearBroomstickLogs();

                VuMaxProTask.updateTaskProgress(ref objDataService, this.TaskID, 1);

                processLogger?.Debug("Loading Broomstick Setup ");

                string lastError = "";
                var objTimeLog = _objTimeLog;
                var fromDate = DateTime.MinValue;
                var toDate = DateTime.MinValue;

                WellSectionList = new Dictionary<int, wellSection>();
                WellSectionList = wellSection.getList(objDataService, objTimeLog != null ? objTimeLog.WellID : WellID);

                PUPoints.Clear();
                SOPoints.Clear();
                RotatePoints.Clear();
                OnBottomTorquePoints.Clear();

                objRigState = rigState.loadWellRigStateSetup(ref objDataService, WellID);

                // First load PUSO Settings for the well
                WellPUSOSettings objPUSOSettings = null!;

                // First retrieve the BS Profile associated with the well
                BroomstickProfile? objBSProfile = BroomstickProfile.getWellBSProfile(objDataService, WellID);

                if (objBSProfile != null)
                {
                    objPUSOSettings = new WellPUSOSettings();
                    objPUSOSettings.WellID = WellID;
                    objPUSOSettings.RunNo = "";
                    objPUSOSettings.DepthMovementTime = objBSProfile.TimeThreshold;
                    objPUSOSettings.ROTPoints = (int)objBSProfile.RotPoints;
                    objPUSOSettings.ROTTolerance = objBSProfile.RotChange;
                    objPUSOSettings.ROTMaxRPM = objBSProfile.RotMaxRPM;
                    objPUSOSettings.ROTMinRPM = objBSProfile.RotMinRPM;
                    objPUSOSettings.ROTPumpCutOff = objBSProfile.RotPumpCutOff;
                    objPUSOSettings.ROTChannel = objBSProfile.RotPumpChannel;
                    objPUSOSettings.SOFilterMaxLocal = objBSProfile.SlkLocalMax;
                    objPUSOSettings.SODynMethod = objBSProfile.SlkDynamicMethod;
                    objPUSOSettings.SOMinMovement = objBSProfile.SlkMinMovement;
                    objPUSOSettings.SOMaxMovement = objBSProfile.SlkMaxMovment;
                    objPUSOSettings.SORPMCutOff = objBSProfile.SlkRPMCutOff;
                    objPUSOSettings.SOPumpCutOff = objBSProfile.SlkPumpCutOff;
                    objPUSOSettings.SOChannel = objBSProfile.SlkPumpChannel;
                    objPUSOSettings.PUFilterMaxLocal = objBSProfile.PkupLocalMax;
                    objPUSOSettings.PUDynMethod = objBSProfile.PkupDynamicMethod;
                    objPUSOSettings.PUMinMovement = objBSProfile.PkupMinMovement;
                    objPUSOSettings.PUMaxMovement = objBSProfile.PkupMaxMovment;
                    objPUSOSettings.PURPMCutOff = objBSProfile.PkupRPMCutOff;
                    objPUSOSettings.PUPumpCutOff = objBSProfile.PkupPumpCutOff;
                    objPUSOSettings.PUChannel = objBSProfile.PkupPumpChannel;

                    _objSetup.pickupRigStates = objBSProfile.PkupRigStates;
                    _objSetup.slackOffRigStates = objBSProfile.SlkRigStates;
                    _objSetup.rotateRigStates = objBSProfile.RotRigStates;
                }
                else
                {
                    processLogger?.Debug("No profile is attached to this well. Finding default profile ");
                    string defaultProfileID = "";

                    DataTable dataDefaultProfile = objDataService.GetTable("SELECT ID FROM VMX_BS_GLOBAL_PROFILE WHERE IS_DEFAULT=1 LIMIT 1");
                    if (dataDefaultProfile != null && dataDefaultProfile.Rows.Count > 0)
                    {
                        defaultProfileID = dataDefaultProfile.Rows[0]["ID"].ToString() ?? "";
                        dataDefaultProfile.Dispose();
                    }

                    if (!string.IsNullOrWhiteSpace(defaultProfileID))
                    {
                        processLogger?.Debug("Default profile found. Using the default profile settings ...");
                        BroomstickProfile? objDefaultProfile = BroomstickProfile.loadProfile(objDataService, defaultProfileID, ref lastError);

                        if (objDefaultProfile != null)
                        {
                            objPUSOSettings = new WellPUSOSettings();
                            objPUSOSettings.WellID = WellID;
                            objPUSOSettings.RunNo = "";
                            objPUSOSettings.DepthMovementTime = objDefaultProfile.TimeThreshold;
                            objPUSOSettings.ROTPoints = (int)objDefaultProfile.RotPoints;
                            objPUSOSettings.ROTTolerance = objDefaultProfile.RotChange;
                            objPUSOSettings.ROTMaxRPM = objDefaultProfile.RotMaxRPM;
                            objPUSOSettings.ROTMinRPM = objDefaultProfile.RotMinRPM;
                            objPUSOSettings.ROTPumpCutOff = objDefaultProfile.RotPumpCutOff;
                            objPUSOSettings.ROTChannel = objDefaultProfile.RotPumpChannel;
                            objPUSOSettings.SOFilterMaxLocal = objDefaultProfile.SlkLocalMax;
                            objPUSOSettings.SODynMethod = objDefaultProfile.SlkDynamicMethod;
                            objPUSOSettings.SOMinMovement = objDefaultProfile.SlkMinMovement;
                            objPUSOSettings.SOMaxMovement = objDefaultProfile.SlkMaxMovment;
                            objPUSOSettings.SORPMCutOff = objDefaultProfile.SlkRPMCutOff;
                            objPUSOSettings.SOPumpCutOff = objDefaultProfile.SlkPumpCutOff;
                            objPUSOSettings.SOChannel = objDefaultProfile.SlkPumpChannel;
                            objPUSOSettings.PUFilterMaxLocal = objDefaultProfile.PkupLocalMax;
                            objPUSOSettings.PUDynMethod = objDefaultProfile.PkupDynamicMethod;
                            objPUSOSettings.PUMinMovement = objDefaultProfile.PkupMinMovement;
                            objPUSOSettings.PUMaxMovement = objDefaultProfile.PkupMaxMovment;
                            objPUSOSettings.PURPMCutOff = objDefaultProfile.PkupRPMCutOff;
                            objPUSOSettings.PUPumpCutOff = objDefaultProfile.PkupPumpCutOff;
                            objPUSOSettings.PUChannel = objDefaultProfile.PkupPumpChannel;

                            _objSetup.pickupRigStates = objDefaultProfile.PkupRigStates;
                            _objSetup.slackOffRigStates = objDefaultProfile.SlkRigStates;
                            _objSetup.rotateRigStates = objDefaultProfile.RotRigStates;

                            string strloadSQL = "SELECT COUNT(*) FROM VMX_BS_WELL_PROFILE WHERE WELL_ID = @WellID";
                            double rowCount = Convert.ToDouble(objDataService.GetValue(strloadSQL, new Dictionary<string, object?> { ["@WellID"] = WellID }) ?? 0);

                            if (rowCount == 0)
                            {
                                string strInsertSQL = "INSERT INTO VMX_BS_WELL_PROFILE (WELL_ID,BS_PROFILE_ID,HKLD_PROFILE_ID) VALUES(@WELL_ID,@BS_PROFILE_ID,@HKLD_PROFILE_ID)";
                                objDataService.ExecuteNonQuery(strInsertSQL, new Dictionary<string, object?>
                                {
                                    ["@WELL_ID"] = WellID,
                                    ["@BS_PROFILE_ID"] = defaultProfileID,
                                    ["@HKLD_PROFILE_ID"] = defaultProfileID
                                });
                            }
                            else
                            {
                                string strUpdateSQL = "UPDATE VMX_BS_WELL_PROFILE SET BS_PROFILE_ID = @BS_PROFILE_ID, HKLD_PROFILE_ID = @HKLD_PROFILE_ID WHERE WELL_ID = @WELL_ID";
                                objDataService.ExecuteNonQuery(strUpdateSQL, new Dictionary<string, object?>
                                {
                                    ["@WELL_ID"] = WellID,
                                    ["@BS_PROFILE_ID"] = defaultProfileID,
                                    ["@HKLD_PROFILE_ID"] = defaultProfileID
                                });
                            }
                        }
                    }
                    else
                    {
                        processLogger?.Debug("No default profile found, aborting the process ");
                        addBroomstickWarnings("Broomstick Profile is not assigned to the well and no default profile found. Please assign the profile to the well to detect broomstick points");
                        return;
                    }
                }

                if (objPUSOSettings != null)
                {
                    _objSetup.pickupPumpMnemonic = objPUSOSettings.PUChannel;
                    _objSetup.pickupCutOffValue = objPUSOSettings.PUPumpCutOff;
                    _objSetup.pickupRPMCutOff = objPUSOSettings.PURPMCutOff;
                    _objSetup.pickupMaxBlockMovement = (int)objPUSOSettings.PUMaxMovement;
                    _objSetup.pickupMinBlockMovement = (int)objPUSOSettings.PUMinMovement;
                    _objSetup.pickupDynamicMethod = (BroomStickSetup.bmDynamicMethod)objPUSOSettings.PUDynMethod;
                    _objSetup.PULocalMaxFilter = objPUSOSettings.PUFilterMaxLocal;

                    _objSetup.slackOffPumpMnemonic = objPUSOSettings.SOChannel;
                    _objSetup.slackOffCutOffValue = objPUSOSettings.SOPumpCutOff;
                    _objSetup.slackOffRPMCutOff = objPUSOSettings.SORPMCutOff;
                    _objSetup.slackOffMaxBlockMovement = (int)objPUSOSettings.SOMaxMovement;
                    _objSetup.slackOffMinBlockMovement = (int)objPUSOSettings.SOMinMovement;
                    _objSetup.slackOffDynamicMethod = (BroomStickSetup.bmDynamicMethod)objPUSOSettings.SODynMethod;
                    _objSetup.SOLocalMinFilter = objPUSOSettings.SOFilterMaxLocal;

                    _objSetup.rotatePumpMnemonic = objPUSOSettings.ROTChannel;
                    _objSetup.rotateCutOffValue = objPUSOSettings.ROTPumpCutOff;
                    _objSetup.rotateRPMCutOff = objPUSOSettings.ROTMinRPM;
                    _objSetup.rotateMaxRPM = objPUSOSettings.ROTMaxRPM;
                    _objSetup.ROBMaxHkldTolerance = objPUSOSettings.ROTTolerance;
                    _objSetup.TolerancePoints = objPUSOSettings.ROTPoints;

                    _objSetup.MaxPauseTime = objPUSOSettings.DepthMovementTime;

                    processLogger?.Debug("[TORQUE-SETUP] Pump Mnemonic: " + _objSetup.rotatePumpMnemonic);
                    processLogger?.Debug("[TORQUE-SETUP] Pump CutOff: " + _objSetup.rotateCutOffValue);
                    processLogger?.Debug("[TORQUE-SETUP] Min RPM (CutOff): " + _objSetup.rotateRPMCutOff);
                    processLogger?.Debug("[TORQUE-SETUP] Max RPM: " + _objSetup.rotateMaxRPM);
                    processLogger?.Debug("[TORQUE-SETUP] Rig States: " + _objSetup.rotateRigStates);
                    processLogger?.Debug("[TORQUE-SETUP] Tolerance Points: " + _objSetup.TolerancePoints);
                    processLogger?.Debug("[TORQUE-SETUP] Hkld Tolerance: " + _objSetup.ROBMaxHkldTolerance);
                    processLogger?.Debug("[TORQUE-SETUP] CheckPUSOProcedure: " + _objSetup.CheckPUSOProcedure);
                    processLogger?.Debug("[TORQUE-SETUP] DontPlotROBWithNoStability: " + _objSetup.DontPlotROBWithNoStability);
                    processLogger?.Debug("[TORQUE-SETUP] ShowMultiplePoints: " + _objSetup.ShowMultiplePoints);
                    processLogger?.Debug("[TORQUE-SETUP] EnforceRule: " + _objSetup.EnforceRule);
                }

                // Prepare rig states 
                if (_objSetup.pickupRigStates == null) _objSetup.pickupRigStates = "";
                if (_objSetup.slackOffRigStates == null) _objSetup.slackOffRigStates = "";
                if (_objSetup.rotateRigStates == null) _objSetup.rotateRigStates = "";

                if (_objSetup.pickupRigStates.Trim() == "")
                {
                    _objSetup.pickupRigStates = "8,10,28";
                }

                string[] arrRigStates = new string[0];
                if (_objSetup.pickupRigStates.Contains("~"))
                {
                    arrRigStates = _objSetup.pickupRigStates.Split('~');
                }
                else if (_objSetup.pickupRigStates.Contains(","))
                {
                    arrRigStates = _objSetup.pickupRigStates.Split(',');
                }
                else if (!string.IsNullOrWhiteSpace(_objSetup.pickupRigStates))
                {
                    arrRigStates = new[] { _objSetup.pickupRigStates.Trim() };
                }

                pkupRigStates.Clear();
                for (int i = 0; i < arrRigStates.Length; i++)
                {
                    if (double.TryParse(arrRigStates[i].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double rs))
                    {
                        if (!pkupRigStates.ContainsKey(rs)) pkupRigStates.Add(rs, rs);
                    }
                }

                if (_objSetup.slackOffRigStates.Trim() == "")
                {
                    _objSetup.slackOffRigStates = "6,4,27";
                }

                arrRigStates = new string[0];
                if (_objSetup.slackOffRigStates.Contains("~"))
                {
                    arrRigStates = _objSetup.slackOffRigStates.Split('~');
                }
                else if (_objSetup.slackOffRigStates.Contains(","))
                {
                    arrRigStates = _objSetup.slackOffRigStates.Split(',');
                }
                else if (!string.IsNullOrWhiteSpace(_objSetup.slackOffRigStates))
                {
                    arrRigStates = new[] { _objSetup.slackOffRigStates.Trim() };
                }

                slkRigStates.Clear();
                for (int i = 0; i < arrRigStates.Length; i++)
                {
                    if (double.TryParse(arrRigStates[i].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double rs))
                    {
                        if (!slkRigStates.ContainsKey(rs)) slkRigStates.Add(rs, rs);
                    }
                }

                if (_objSetup.rotateRigStates.Trim() == "")
                {
                    _objSetup.rotateRigStates = "11,12,13";
                }

                arrRigStates = new string[0];
                if (_objSetup.rotateRigStates.Contains("~"))
                {
                    arrRigStates = _objSetup.rotateRigStates.Split('~');
                }
                else if (_objSetup.rotateRigStates.Contains(","))
                {
                    arrRigStates = _objSetup.rotateRigStates.Split(',');
                }
                else if (!string.IsNullOrWhiteSpace(_objSetup.rotateRigStates))
                {
                    arrRigStates = new[] { _objSetup.rotateRigStates.Trim() };
                }

                rotRigStates.Clear();
                for (int i = 0; i < arrRigStates.Length; i++)
                {
                    if (double.TryParse(arrRigStates[i].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double rs))
                    {
                        if (!rotRigStates.ContainsKey(rs)) rotRigStates.Add(rs, rs);
                    }
                }

                if (objTimeLog is null)
                {
                    return;
                }

                var maxDate = fromDate;
                var maxDateHkld = DateTime.MinValue;
                var maxDateTorque = DateTime.MinValue;

                loadPointsFromDBHookload();
                loadPointsFromDBTorque();
                loadPointsFromDBOnBottomTorque();

                object? maxHkldVal = objDataService.GetValue("SELECT MAX(END_DATE) FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID = @WellID AND TYPE=0", new Dictionary<string, object?> { ["@WellID"] = WellID });
                if (maxHkldVal != null && maxHkldVal != DBNull.Value && !string.IsNullOrWhiteSpace(maxHkldVal.ToString()))
                {
                    DateTime.TryParse(maxHkldVal.ToString(), out maxDateHkld);
                }

                object? maxTorqueVal = objDataService.GetValue("SELECT MAX(END_DATE) FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID = @WellID AND TYPE=3", new Dictionary<string, object?> { ["@WellID"] = WellID });
                if (maxTorqueVal != null && maxTorqueVal != DBNull.Value && !string.IsNullOrWhiteSpace(maxTorqueVal.ToString()))
                {
                    DateTime.TryParse(maxTorqueVal.ToString(), out maxDateTorque);
                }

                if (maxDateHkld != DateTime.MinValue && maxDateTorque != DateTime.MinValue)
                {
                    if (maxDateHkld < maxDateTorque)
                    {
                        maxDate = maxDateHkld;
                    }
                    else
                    {
                        maxDate = maxDateTorque;
                    }
                }
                else if (maxDateHkld != DateTime.MinValue)
                {
                    maxDate = maxDateHkld;
                }
                else if (maxDateTorque != DateTime.MinValue)
                {
                    maxDate = maxDateTorque;
                }

                string __dataTableName = objTimeLog.getDataTableName(ref objDataService);

                DateTime __startDate = DateTime.MinValue;
                DateTime __enddate = DateTime.MinValue;

                if (maxDate != DateTime.MinValue)
                {
                    __startDate = maxDate;
                    __enddate = toDate;
                }
                else
                {
                    __startDate = fromDate;
                    __enddate = toDate;
                }

                if (fromDate == DateTime.MinValue)
                {
                    double fi = objTimeLog.getFirstIndexOptimized(ref objDataService);
                    if (fi > 0) fromDate = DateTime.FromOADate(fi);
                }

                if (toDate == DateTime.MinValue)
                {
                    double li = objTimeLog.getLastIndexOptimized(ref objDataService);
                    if (li > 0) toDate = DateTime.FromOADate(li);
                }

                if (__startDate == DateTime.MinValue)
                {
                    double fi = objTimeLog.getFirstIndexOptimized(ref objDataService);
                    if (fi > 0) __startDate = DateTime.FromOADate(fi);
                }

                if (__enddate == DateTime.MinValue)
                {
                    double li = objTimeLog.getLastIndexOptimized(ref objDataService);
                    if (li > 0) __enddate = DateTime.FromOADate(li);
                }

                processLogger?.Debug("Processing data from date " + __startDate.ToString("dd-MMM-yyyy HH:mm:ss") + " onwards ");
                processLogger?.Debug("Finding drilling connections in this date range ");

                string strSQL = "SELECT FROM_DATE,TO_DATE,DEPTH FROM VMX_AKPI_DRLG_CONNECTIONS WHERE WELL_ID = @WellID AND FROM_DATE >= @StartDate ORDER BY DEPTH";
                var connParams = new Dictionary<string, object?>
                {
                    ["@WellID"] = WellID,
                    ["@StartDate"] = __startDate.ToString("dd-MMM-yyyy HH:mm:ss")
                };

                DataTable objDrlgConnections = objDataService.GetTable(strSQL, connParams);

                if (objDrlgConnections != null && objDrlgConnections.Rows.Count > 0)
                {
                    processLogger?.Debug("Processing #" + objDrlgConnections.Rows.Count.ToString() + " drilling connections for broomstick points ");

                    double totalConnections = objDrlgConnections.Rows.Count;
                    double connectionsProcessed = 0;
                    DateTime lastProcessedDate = DateTime.MinValue;

                    foreach (DataRow objRow in objDrlgConnections.Rows)
                    {
                        if (!string.IsNullOrEmpty(TaskID) && VuMaxProTask.isTaskCancelled(ref objDataService, TaskID))
                        {
                            processLogger?.Debug("Cancellation requested, stopping early");
                            string __sql1 = "DELETE FROM eVuMaxTasks WHERE [TaskId] = @TaskId";
                            objDataService.ExecuteNonQuery(__sql1, new Dictionary<string, object?> { ["@TaskId"] = TaskID });
                            return;
                        }

                        DateTime connFromDate = DateTime.Parse(DataService.checkNull(objRow["FROM_DATE"], DateTime.MinValue).ToString()!);
                        DateTime connToDate = DateTime.Parse(DataService.checkNull(objRow["TO_DATE"], DateTime.MinValue).ToString()!);
                        double depth = DataService.checkNumericNull(objRow["DEPTH"]);

                        string broomstickLog = "";
                        detectAndAddPointsInConnection(__dataTableName, connFromDate, connToDate, depth, ref broomstickLog);

                        // Add this log for this connection to the database ...
                        addBroomstickLog(depth, connFromDate, connToDate, broomstickLog);

                        lastProcessedDate = connToDate;
                        connectionsProcessed++;

                        double progress = Math.Round((connectionsProcessed * 100) / totalConnections, 2);
                        if (progress > 100) progress = 100;

                        processLogger?.Debug("Progress: " + progress);
                        VuMaxProTask.updateTaskProgress(ref objDataService, this.TaskID, progress);
                    }

                    // Finally group by depths
                    if (!_objSetup.ShowMultiplePoints)
                    {
                        groupByDepth();
                    }

                    if (_objSetup.EnforceRule)
                    {
                        enforceRule();
                    }

                    // First clear the data points
                    clearPointsFromDBHookload(__startDate, __enddate);
                    clearPointsFromDBTorque(__startDate, __enddate);

                    processLogger?.Debug("Processing completed. saving broomstick points to the database ");

                    savePointsToDBHookload();
                    savePointsToDBTorque();

                    processLogger?.Debug("Saved successfully to the database");
                    saveProcessInfo(toDate.AddHours(-1), 0);

                    objDrlgConnections.Dispose();
                    objDrlgConnections = null;
                }
                else
                {
                    processLogger?.Debug("No drilling connections found in the database. No broomstick points will be detected");
                }

                // Process on bottom torque points
                processLogger?.Debug("Processing On Bottom Torque Points ... ");

                objDrlgConnections = objDataService.GetTable(strSQL, connParams);

                if (objDrlgConnections != null && objDrlgConnections.Rows.Count > 0)
                {
                    clearPointsFromDBOnBottomTorque(__startDate, __enddate);

                    for (int i = 1; i < objDrlgConnections.Rows.Count; i++)
                    {
                        DateTime prevConnEndDate = DateTime.Parse(objDrlgConnections.Rows[i - 1]["TO_DATE"].ToString()!);
                        DateTime currentConnStartDate = DateTime.Parse(objDrlgConnections.Rows[i]["FROM_DATE"].ToString()!);

                        processLogger?.Debug("Stand #" + i.ToString() + " Start Date " + prevConnEndDate.ToString("dd-MMM-yyyy HH:mm:ss") + " to " + currentConnStartDate.ToString("dd-MMM-yyyy HH:mm:ss"));

                        double prevConnDepth = DataService.checkNumericNull(objDrlgConnections.Rows[i - 1]["DEPTH"]);
                        double currentConnDepth = DataService.checkNumericNull(objDrlgConnections.Rows[i]["DEPTH"]);

                        string avgSql = "SELECT AVG(STOR) FROM " + __dataTableName + " WHERE DATETIME >= @FromDate AND DATETIME <= @ToDate AND RIG_STATE IN (0,1,19)";
                        var avgParams = new Dictionary<string, object?>
                        {
                            ["@FromDate"] = prevConnEndDate.ToString("dd-MMM-yyyy HH:mm:ss"),
                            ["@ToDate"] = currentConnStartDate.ToString("dd-MMM-yyyy HH:mm:ss")
                        };

                        object? avgObj = objDataService.GetValue(avgSql, avgParams);
                        double avgTorque = 0;
                        if (avgObj != null && avgObj != DBNull.Value && double.TryParse(avgObj.ToString(), out double parsedTorque))
                        {
                            avgTorque = parsedTorque;
                        }

                        processLogger?.Debug("Avg. Torque: " + avgTorque.ToString() + " for depth: " + currentConnDepth.ToString() + " (range: " + prevConnEndDate.ToString("dd-MMM-yyyy HH:mm:ss") + " to " + currentConnStartDate.ToString("dd-MMM-yyyy HH:mm:ss") + ")");
                        if (avgTorque == 0)
                        {
                            processLogger?.Debug("[TORQUE-WARNING] Avg Torque is 0 at depth " + currentConnDepth.ToString() + " - no data in range with RIG_STATE IN (0,1,19). This point will have DynamicValue=0 and may be filtered out.");
                        }

                        var objPUSO = new PUSOPoint();
                        objPUSO.StartIndex = i;
                        objPUSO.StartDate = prevConnEndDate;
                        objPUSO.StartDepth = prevConnDepth;
                        objPUSO.Depth = currentConnDepth;
                        objPUSO.EndDate = currentConnStartDate;
                        objPUSO.EndDepth = currentConnDepth;
                        objPUSO.DynamicValue = avgTorque;
                        objPUSO.DynamicTorqueValue = avgTorque;
                        objPUSO.UserVisible = true;

                        OnBottomTorquePoints.Add(OnBottomTorquePoints.Count + 1, objPUSO);
                    }

                    processLogger?.Debug("Saving (" + OnBottomTorquePoints.Count.ToString() + ") On Bottom Torque Points to the database ... ");
                    savePointsToDBOnBottomTorque();

                    objDrlgConnections.Dispose();
                    objDrlgConnections = null;

                    processLogger?.Debug("Saved on bottom torque points successfully to the database ");
                }
            }
            catch (Exception ex)
            {
                processLogger.Error("Error occured while processing broomstick points " + ex.Message + ex.StackTrace);
            }
        }

        public async Task ProcessPointsAsync(CancellationToken ct = default, IProgress<double>? progress = null)
        {
            await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                ProcessPoints();
                progress?.Report(100.0);
            }, ct);
        }

        private void clearBroomstickWarnings()
        {
            try
            {
                string strSQL = "DELETE FROM eVuMaxBroomstickWarnings WHERE WellId = @WellId";
                objDataService.ExecuteNonQuery(strSQL, new Dictionary<string, object?> { ["@WellId"] = WellID });
            }
            catch (Exception)
            {
            }
        }

        private void addBroomstickWarnings(string strWarning)
        {
            try
            {
                string strSQL = "INSERT INTO eVuMaxBroomstickWarnings ([WellId],[Id],[Warning]) VALUES(@WellId,@Id,@Warning)";
                objDataService.ExecuteNonQuery(strSQL, new Dictionary<string, object?>
                {
                    ["@WellId"] = WellID,
                    ["@Id"] = Guid.NewGuid().ToString(),
                    ["@Warning"] = strWarning
                });
            }
            catch (Exception)
            {
            }
        }

        private void clearBroomstickLogs()
        {
            try
            {
                string strSQL = "DELETE FROM eVuMaxBroomstickProcessLog WHERE WellId = @WellId";
                objDataService.ExecuteNonQuery(strSQL, new Dictionary<string, object?> { ["@WellId"] = WellID });
            }
            catch (Exception)
            {
            }
        }

        private void addBroomstickLog(double paramDepth, DateTime connStartDate, DateTime connEndDate, string strLog)
        {
            try
            {
                string strSQL = "INSERT INTO eVuMaxBroomstickProcessLog ([WellId],[HoleDepth],[ConnStartDate],[ConnEndDate],[ProcessLog]) VALUES(@WellId,@HoleDepth,@ConnStartDate,@ConnEndDate,@ProcessLog)";
                byte[] binaryData = Encoding.UTF8.GetBytes(strLog ?? "");

                objDataService.ExecuteNonQuery(strSQL, new Dictionary<string, object?>
                {
                    ["@WellId"] = WellID,
                    ["@HoleDepth"] = paramDepth,
                    ["@ConnStartDate"] = connStartDate.ToString("dd-MMM-yyyy HH:mm:ss"),
                    ["@ConnEndDate"] = connEndDate.ToString("dd-MMM-yyyy HH:mm:ss"),
                    ["@ProcessLog"] = binaryData
                });
            }
            catch (Exception)
            {
            }
        }

        public DateTime getLastProcessDate(int paramType)
        {
            try
            {
                string strSQL = "SELECT PRC_LAST_DATE FROM VMX_BROOMSTICK_PRC_INFO WHERE WELL_ID = @WellID AND TYPE = @Type LIMIT 1";
                object? val = objDataService.GetValue(strSQL, new Dictionary<string, object?>
                {
                    ["@WellID"] = WellID,
                    ["@Type"] = paramType
                });

                if (val != null && val != DBNull.Value && DateTime.TryParse(val.ToString(), out DateTime dt))
                {
                    return dt;
                }
                return DateTime.MinValue;
            }
            catch (Exception)
            {
                return DateTime.MinValue;
            }
        }

        public void saveProcessInfo(DateTime paramLastDate, int paramType)
        {
            try
            {
                string delSql = "DELETE FROM VMX_BROOMSTICK_PRC_INFO WHERE WELL_ID = @WellID AND TYPE = @Type";
                objDataService.ExecuteNonQuery(delSql, new Dictionary<string, object?>
                {
                    ["@WellID"] = WellID,
                    ["@Type"] = paramType
                });

                string insSql = "INSERT INTO VMX_BROOMSTICK_PRC_INFO (WELL_ID, TYPE, PRC_LAST_DATE) VALUES (@WellID, @Type, @PrcLastDate)";
                objDataService.ExecuteNonQuery(insSql, new Dictionary<string, object?>
                {
                    ["@WellID"] = WellID,
                    ["@Type"] = paramType,
                    ["@PrcLastDate"] = paramLastDate.ToString("dd-MMM-yyyy HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                processLogger.Error("saveProcessInfo - " + ex.Message + ex.StackTrace);
            }
        }

        public void clearAllDBEntries()
        {
            try
            {
                string strSQL1 = "DELETE FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID = @WellID";
                objDataService.ExecuteNonQuery(strSQL1, new Dictionary<string, object?> { ["@WellID"] = WellID });

                string strSQL2 = "DELETE FROM VMX_BROOMSTICK_PRC_INFO WHERE WELL_ID = @WellID";
                objDataService.ExecuteNonQuery(strSQL2, new Dictionary<string, object?> { ["@WellID"] = WellID });
            }
            catch (Exception ex)
            {
                processLogger.Error("clearAllDBEntries - " + ex.Message + ex.StackTrace);
            }
        }

        public void clearPointsFromDBHookload(DateTime paramFromDate, DateTime paramToDate)
        {
            try
            {
                string strSQL = "DELETE FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID = @WellID " +
                                "AND START_DATE >= @FromDate " +
                                "AND END_DATE <= @ToDate " +
                                "AND TYPE IN (0, 1, 2)";

                objDataService.ExecuteNonQuery(strSQL, new Dictionary<string, object?>
                {
                    ["@WellID"] = WellID,
                    ["@FromDate"] = paramFromDate.ToString("dd-MMM-yyyy HH:mm:ss"),
                    ["@ToDate"] = paramToDate.ToString("dd-MMM-yyyy HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                processLogger.Error("clearPointsFromDBHookload - " + ex.Message + ex.StackTrace);
            }
        }

        public void clearPointsFromDBTorque(DateTime paramFromDate, DateTime paramToDate)
        {
            try
            {
                string strSQL = "DELETE FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID = @WellID " +
                                "AND START_DATE >= @FromDate " +
                                "AND END_DATE <= @ToDate " +
                                "AND TYPE IN (3)";

                objDataService.ExecuteNonQuery(strSQL, new Dictionary<string, object?>
                {
                    ["@WellID"] = WellID,
                    ["@FromDate"] = paramFromDate.ToString("dd-MMM-yyyy HH:mm:ss"),
                    ["@ToDate"] = paramToDate.ToString("dd-MMM-yyyy HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                processLogger.Error("clearPointsFromDBTorque - " + ex.Message + ex.StackTrace);
            }
        }

        public void clearPointsFromDBOnBottomTorque(DateTime paramFromDate, DateTime paramToDate)
        {
            try
            {
                string strSQL = "DELETE FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID = @WellID " +
                                "AND START_DATE >= @FromDate " +
                                "AND END_DATE <= @ToDate " +
                                "AND TYPE IN (4)";

                objDataService.ExecuteNonQuery(strSQL, new Dictionary<string, object?>
                {
                    ["@WellID"] = WellID,
                    ["@FromDate"] = paramFromDate.ToString("dd-MMM-yyyy HH:mm:ss"),
                    ["@ToDate"] = paramToDate.ToString("dd-MMM-yyyy HH:mm:ss")
                });
            }
            catch (Exception ex)
            {
                processLogger.Error("clearPointsFromDBOnBottomTorque - " + ex.Message + ex.StackTrace);
            }
        }

        public void savePointsToDBHookload()
        {
            try
            {
                // Save PU Points (Type 0)
                SavePointDictionary(PUPoints.Values, 0);

                // Save SO Points (Type 1)
                SavePointDictionary(SOPoints.Values, 1);

                // Save Rotate Points (Type 2)
                SavePointDictionary(RotatePoints.Values, 2);
            }
            catch (Exception ex)
            {
                processLogger.Error("savePointsToDBHookload - " + ex.Message + ex.StackTrace);
            }
        }

        public void loadPointsFromDBHookload()
        {
            try
            {
                PUPoints.Clear();
                SOPoints.Clear();
                RotatePoints.Clear();

                LoadPointDictionary(PUPoints, 0);
                LoadPointDictionary(SOPoints, 1);
                LoadPointDictionary(RotatePoints, 2);
            }
            catch (Exception ex)
            {
                processLogger.Error("loadPointsFromDBHookload - " + ex.Message + ex.StackTrace);
            }
        }

        public void loadPointsFromDBTorque()
        {
            try
            {
                RotatePoints.Clear();
                string strSQL = "SELECT * FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID = @WellID AND TYPE = 3";
                processLogger?.Debug("[TORQUE-LOAD] Loading torque (ROB) points from DB. SQL: " + strSQL);

                DataTable objData = objDataService.GetTable(strSQL, new Dictionary<string, object?> { ["@WellID"] = WellID });
                processLogger?.Debug("[TORQUE-LOAD] Rows returned from DB (TYPE=3/ROB): " + (objData != null ? objData.Rows.Count.ToString() : "null"));

                if (objData != null)
                {
                    foreach (DataRow objRow in objData.Rows)
                    {
                        var objItem = MapPointFromRow(objRow);
                        RotatePoints.Add(RotatePoints.Count + 1, objItem);
                    }
                }

                processLogger?.Debug("[TORQUE-LOAD] Total torque (ROB) points loaded into memory: " + RotatePoints.Count.ToString());
            }
            catch (Exception ex)
            {
                processLogger?.Error("[TORQUE-LOAD] Exception in loadPointsFromDBTorque: " + ex.Message + ex.StackTrace);
            }
        }

        public void loadPointsFromDBOnBottomTorque()
        {
            try
            {
                OnBottomTorquePoints.Clear();
                string strSQL = "SELECT * FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID = @WellID AND TYPE = 4";
                DataTable objData = objDataService.GetTable(strSQL, new Dictionary<string, object?> { ["@WellID"] = WellID });

                if (objData != null)
                {
                    foreach (DataRow objRow in objData.Rows)
                    {
                        var objItem = MapPointFromRow(objRow);
                        OnBottomTorquePoints.Add(OnBottomTorquePoints.Count + 1, objItem);
                    }
                }
            }
            catch (Exception ex)
            {
                processLogger.Error("loadPointsFromDBOnBottomTorque - " + ex.Message + ex.StackTrace);
            }
        }

        public void savePointsToDBTorque()
        {
            try
            {
                processLogger?.Debug("[TORQUE-SAVE] savePointsToDBTorque called. RotatePoints count: " + RotatePoints.Count.ToString());

                if (RotatePoints.Count == 0)
                {
                    processLogger?.Debug("[TORQUE-SAVE] No ROB/Torque points to save - RotatePoints is empty. Check ROT rig states, RPM cutoffs, and pump settings.");
                }

                SavePointDictionary(RotatePoints.Values, 3);
                processLogger?.Debug("[TORQUE-SAVE] savePointsToDBTorque completed. RotatePoints saved: " + RotatePoints.Count.ToString());
            }
            catch (Exception ex)
            {
                processLogger?.Error("[TORQUE-SAVE] Exception in savePointsToDBTorque: " + ex.Message + ex.StackTrace);
            }
        }

        public void savePointsToDBOnBottomTorque()
        {
            try
            {
                SavePointDictionary(OnBottomTorquePoints.Values, 4);
            }
            catch (Exception ex)
            {
                processLogger.Error("savePointsToDBOnBottomTorque - " + ex.Message + ex.StackTrace);
            }
        }

        private void SavePointDictionary(IEnumerable<PUSOPoint> points, int pointType)
        {
            string userName = objDataService.UserName ?? Environment.UserName;
            string now = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss", CultureInfo.InvariantCulture);

            foreach (PUSOPoint objItem in points)
            {
                try
                {
                    string delSql = "DELETE FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID = @WellID AND CAST(DEPTH AS INT) = @Depth AND TYPE = @Type";
                    objDataService.ExecuteNonQuery(delSql, new Dictionary<string, object?>
                    {
                        ["@WellID"] = WellID,
                        ["@Depth"] = (int)Math.Floor(objItem.Depth),
                        ["@Type"] = pointType
                    });

                    string insSql = @"INSERT INTO VMX_BROOMSTICK_POINTS (
    WELL_ID, DEPTH, TYPE, START_DATE, END_DATE, START_INDEX, END_INDEX,
    MIN, MAX, AVG, AVG2, START_DEPTH, END_DEPTH, BLOCK_MOVEMENT, MAX_INDEX,
    DYNAMIC_VALUE, DYNAMIC_TORQUE_VALUE, CIRCULATION, PUMP_PRESSURE,
    USER_DYNAMIC_VALUE, USER_STATIC_VALUE, USER_VISIBLE, PUMP_STATUS,
    CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE, RPM, RIG_STATES
) VALUES (
    @WellID, @Depth, @Type, @StartDate, @EndDate, @StartIndex, @EndIndex,
    @Min, @Max, @Avg, @Avg2, @StartDepth, @EndDepth, @BlockMovement, @MaxIndex,
    @DynamicValue, @DynamicTorqueValue, @Circulation, @PumpPressure,
    @UserDynamicValue, @UserStaticValue, @UserVisible, @PumpStatus,
    @CreatedBy, @CreatedDate, @ModifiedBy, @ModifiedDate, @RPM, @RigStates
)";

                    objDataService.ExecuteNonQuery(insSql, new Dictionary<string, object?>
                    {
                        ["@WellID"] = WellID,
                        ["@Depth"] = objItem.Depth,
                        ["@Type"] = pointType,
                        ["@StartDate"] = objItem.StartDate != DateTime.MinValue ? objItem.StartDate.ToString("dd-MMM-yyyy HH:mm:ss") : (object)DBNull.Value,
                        ["@EndDate"] = objItem.EndDate != DateTime.MinValue ? objItem.EndDate.ToString("dd-MMM-yyyy HH:mm:ss") : (object)DBNull.Value,
                        ["@StartIndex"] = objItem.StartIndex,
                        ["@EndIndex"] = objItem.EndIndex,
                        ["@Min"] = objItem.Min,
                        ["@Max"] = objItem.Max,
                        ["@Avg"] = objItem.Avg,
                        ["@Avg2"] = objItem.Avg2,
                        ["@StartDepth"] = objItem.StartDepth,
                        ["@EndDepth"] = objItem.EndDepth,
                        ["@BlockMovement"] = objItem.BlockMovement,
                        ["@MaxIndex"] = objItem.MaxIndex,
                        ["@DynamicValue"] = objItem.DynamicValue,
                        ["@DynamicTorqueValue"] = objItem.DynamicTorqueValue,
                        ["@Circulation"] = objItem.Circulation,
                        ["@PumpPressure"] = objItem.PumpPressure,
                        ["@UserDynamicValue"] = objItem.UserDynamicValue,
                        ["@UserStaticValue"] = objItem.UserStaticValue,
                        ["@UserVisible"] = objItem.UserVisible ? 1 : 0,
                        ["@PumpStatus"] = (int)objItem.PumpStatus,
                        ["@CreatedBy"] = userName,
                        ["@CreatedDate"] = now,
                        ["@ModifiedBy"] = userName,
                        ["@ModifiedDate"] = now,
                        ["@RPM"] = objItem.RPM,
                        ["@RigStates"] = objItem.RigStates ?? ""
                    });
                }
                catch (Exception ex)
                {
                    processLogger.Error("SavePointDictionary error: " + ex.Message);
                }
            }
        }

        private void LoadPointDictionary(Dictionary<int, PUSOPoint> dict, int pointType)
        {
            string strSQL = "SELECT * FROM VMX_BROOMSTICK_POINTS WHERE WELL_ID = @WellID AND TYPE = @Type";
            DataTable objData = objDataService.GetTable(strSQL, new Dictionary<string, object?>
            {
                ["@WellID"] = WellID,
                ["@Type"] = pointType
            });

            if (objData != null)
            {
                foreach (DataRow objRow in objData.Rows)
                {
                    var objItem = MapPointFromRow(objRow);
                    dict.Add(dict.Count + 1, objItem);
                }
            }
        }

        private static PUSOPoint MapPointFromRow(DataRow objRow)
        {
            var objItem = new PUSOPoint();
            objItem.Depth = DataService.checkNumericNull(objRow["DEPTH"]);
            objItem.StartDate = DateTime.Parse(DataService.checkNull(objRow["START_DATE"], DateTime.MinValue).ToString()!);
            objItem.EndDate = DateTime.Parse(DataService.checkNull(objRow["END_DATE"], DateTime.MinValue).ToString()!);
            objItem.StartIndex = (int)DataService.checkNumericNull(objRow["START_INDEX"]);
            objItem.EndIndex = (int)DataService.checkNumericNull(objRow["END_INDEX"]);
            objItem.Min = DataService.checkNumericNull(objRow["MIN"]);
            objItem.Max = DataService.checkNumericNull(objRow["MAX"]);
            objItem.Avg = DataService.checkNumericNull(objRow["AVG"]);
            objItem.Avg2 = DataService.checkNumericNull(objRow["AVG2"]);
            objItem.StartDepth = DataService.checkNumericNull(objRow["START_DEPTH"]);
            objItem.EndDepth = DataService.checkNumericNull(objRow["END_DEPTH"]);
            objItem.BlockMovement = DataService.checkNumericNull(objRow["BLOCK_MOVEMENT"]);
            objItem.MaxIndex = (int)DataService.checkNumericNull(objRow["MAX_INDEX"]);
            objItem.DynamicValue = DataService.checkNumericNull(objRow["DYNAMIC_VALUE"]);
            objItem.DynamicTorqueValue = DataService.checkNumericNull(objRow["DYNAMIC_TORQUE_VALUE"]);
            objItem.Circulation = DataService.checkNumericNull(objRow["CIRCULATION"]);
            objItem.PumpPressure = DataService.checkNumericNull(objRow["PUMP_PRESSURE"]);
            objItem.UserDynamicValue = DataService.checkNumericNull(objRow["USER_DYNAMIC_VALUE"]);
            objItem.UserStaticValue = DataService.checkNumericNull(objRow["USER_STATIC_VALUE"]);
            objItem.UserVisible = int.Parse(DataService.checkNull(objRow["USER_VISIBLE"], 0).ToString()!) == 1;
            objItem.PumpStatus = (PUSOPoint.pusoPumpStatus)int.Parse(DataService.checkNull(objRow["PUMP_STATUS"], 0).ToString()!);

            if (objRow.Table.Columns.Contains("RPM"))
            {
                objItem.RPM = double.Parse(DataService.checkNull(objRow["RPM"], 0).ToString()!);
            }
            if (objRow.Table.Columns.Contains("RIG_STATES"))
            {
                objItem.RigStates = DataService.checkNull(objRow["RIG_STATES"], "").ToString()!;
            }

            return objItem;
        }

        public void detectAndAddPointsInConnection(
            string paramDataTableName,
            DateTime connStartDate,
            DateTime connEndDate,
            double paramDepth,
            ref string broomstickLog)
        {
            broomstickLog = "";

            try
            {
                broomstickLog += "\n------------------------------------------------------------ ";
                broomstickLog += "\n Connection Depth: " + paramDepth.ToString();
                broomstickLog += "\n------------------------------------------------------------ ";
                broomstickLog += "\n ";

                double lnHKldCutOff = objRigState != null ? objRigState.HookloadCutOff : 90.0;
                var tmpPUList = new Dictionary<int, PUSOPoint>();
                var tmpSOList = new Dictionary<int, PUSOPoint>();

                double lnHoleDepth = 0d;

                string query = "SELECT DATETIME,TIME_DURATION,DEPTH,HDTH,HKLD,RIG_STATE,STOR,CIRC,SPPA,RPM,BPOS FROM " + paramDataTableName +
                               " WHERE DATETIME >= @ConnStartDate AND DATETIME <= @ConnEndDate ORDER BY DATETIME DESC";

                DataTable objData = objDataService.GetTable(query, new Dictionary<string, object?>
                {
                    ["@ConnStartDate"] = connStartDate.ToString("dd-MMM-yyyy HH:mm:ss"),
                    ["@ConnEndDate"] = connEndDate.ToString("dd-MMM-yyyy HH:mm:ss")
                });

                if (objData == null || objData.Rows.Count == 0) return;

                for (int i = objData.Rows.Count - 1; i >= 0; i--)
                {
                    double lnRigState = double.Parse(DataService.checkNull(objData.Rows[i]["RIG_STATE"], -1).ToString()!);
                    lnHoleDepth = double.Parse(objData.Rows[i]["HDTH"].ToString()!);

                    // ************ PICK UP ***************************************************
                    if (pkupRigStates.ContainsKey(lnRigState))
                    {
                        int returnPointer = i;
                        var objPoint = detectPU(objData, i, ref returnPointer, 0);

                        if (objPoint != null)
                        {
                            string pkupLog = "";
                            if (PUSOPoint.isValidPickUp(objPoint, _objSetup, ref pkupLog))
                            {
                                broomstickLog += "\nValid pickup point found ... ";
                                objPoint.Depth = lnHoleDepth;

                                objWellSection = new wellSection();
                                objWellSection = getPUSOSettingsfromWellSection(objPoint.Depth)!;

                                if (objWellSection != null)
                                {
                                    PUSOPoint.calculateStaticPUValues(objData, ref objPoint, _objSetup, objWellSection.PUSOSettings);
                                    PUSOPoint.calculateDynamicPUValues(objData, ref objPoint, _objSetup, objWellSection.PUSOSettings);
                                }
                                else
                                {
                                    PUSOPoint.calculateStaticPUValues(objData, ref objPoint, _objSetup);
                                    PUSOPoint.calculateDynamicPUValues(objData, ref objPoint, _objSetup);
                                }

                                broomstickLog += "\nPickup Point Dynamic Value: " + objPoint.DynamicValue.ToString();
                                broomstickLog += "\n             Min Value: " + objPoint.Min.ToString();
                                broomstickLog += "\n             Max Value: " + objPoint.Max.ToString();
                                broomstickLog += "\n             Avg Value: " + objPoint.Avg.ToString();

                                tmpPUList.Add(tmpPUList.Count + 1, objPoint.getCopy());
                            }
                            else
                            {
                                if (pkupLog.Trim() != "")
                                {
                                    broomstickLog += "\nInvalid pickup point found ... ";
                                    broomstickLog += "\n" + pkupLog;
                                }
                            }
                        }

                        if (returnPointer > i)
                        {
                            returnPointer = i;
                        }

                        i = returnPointer;
                        lnRigState = double.Parse(DataService.checkNull(objData.Rows[i]["RIG_STATE"], -1).ToString()!);
                    }

                    // ************ SLACK OFF ***************************************************
                    if (slkRigStates.ContainsKey(lnRigState))
                    {
                        int returnPointer = i;
                        var objPoint = detectSO(objData, i, ref returnPointer, 0);

                        if (objPoint != null)
                        {
                            string soLog = "";
                            if (PUSOPoint.isValidSlackOff(objPoint, _objSetup, ref soLog))
                            {
                                broomstickLog += "\nValid slack off point found ... ";
                                objPoint.Depth = lnHoleDepth;

                                objWellSection = new wellSection();
                                objWellSection = getPUSOSettingsfromWellSection(objPoint.Depth)!;

                                if (objWellSection != null)
                                {
                                    PUSOPoint.calculateStaticSOValues(objData, ref objPoint, _objSetup, objWellSection.PUSOSettings);
                                    PUSOPoint.calculateStaticSOValues2(objData, ref objPoint, _objSetup, objWellSection.PUSOSettings);
                                    PUSOPoint.calculateDynamicSOValues(objData, ref objPoint, _objSetup, objWellSection.PUSOSettings);
                                }
                                else
                                {
                                    PUSOPoint.calculateStaticSOValues(objData, ref objPoint, _objSetup);
                                    PUSOPoint.calculateStaticSOValues2(objData, ref objPoint, _objSetup);
                                    PUSOPoint.calculateDynamicSOValues(objData, ref objPoint, _objSetup);
                                }

                                broomstickLog += "\nSlack Off Point Dynamic Value: " + objPoint.DynamicValue.ToString();
                                broomstickLog += "\n                    Min Value: " + objPoint.Min.ToString();
                                broomstickLog += "\n                    Max Value: " + objPoint.Max.ToString();
                                broomstickLog += "\n                    Avg Value: " + objPoint.Avg.ToString();

                                tmpSOList.Add(tmpSOList.Count + 1, objPoint.getCopy());
                            }
                            else
                            {
                                if (soLog.Trim() != "")
                                {
                                    broomstickLog += "\nInvalid slack off point found ... ";
                                    broomstickLog += "\n" + soLog;
                                }
                            }
                        }

                        if (returnPointer > i)
                        {
                            returnPointer = i;
                        }

                        i = returnPointer;
                    }
                }

                // ***** Now Detect Rotate Point ***********************************
                double depthWindowFrom = lnHoleDepth - 0.25d;
                double depthWindowTo = lnHoleDepth + 0.25d;

                int sampleCount = 0;
                double sumHkld = 0d;
                double sumTorque = 0d;
                double AvgValue = 0d;
                double AvgTorqueValue = 0d;

                bool RotateRigStateFound = false;
                var robPoints = new Dictionary<int, ROBPoint>();
                string strRigStates = string.Join(",", rotRigStates.Keys.ToArray());
                Dictionary<double, double> localRotRigStates = new Dictionary<double, double>();

                broomstickLog += "\n Detecting ROB Points ";

                double minRPM = 0;
                double maxRPM = 0;

                for (int i = objData.Rows.Count - 2; i >= 0; i--)
                {
                    double lnRigState = double.Parse(DataService.checkNull(objData.Rows[i]["RIG_STATE"], -1).ToString()!);
                    double lnHkld = double.Parse(objData.Rows[i]["HKLD"].ToString()!);
                    double lnRPM = double.Parse(objData.Rows[i]["RPM"].ToString()!);
                    double lnDepth = double.Parse(objData.Rows[i]["DEPTH"].ToString()!);

                    bool AtInSlipsDepth = false;
                    if (lnDepth >= depthWindowFrom && lnDepth <= depthWindowTo)
                    {
                        AtInSlipsDepth = true;
                    }

                    if (AtInSlipsDepth && lnHkld > lnHKldCutOff)
                    {
                        AtInSlipsDepth = false;
                    }

                    foreach (PUSOPoint objPoint in tmpPUList.Values)
                    {
                        if (i >= objPoint.StartIndex && i <= objPoint.EndIndex)
                        {
                            AtInSlipsDepth = true;
                            break;
                        }
                    }

                    foreach (PUSOPoint objPoint in tmpSOList.Values)
                    {
                        if (i >= objPoint.StartIndex && i <= objPoint.EndIndex)
                        {
                            AtInSlipsDepth = true;
                            break;
                        }
                    }

                    if (rotRigStates.ContainsKey(lnRigState) && (!AtInSlipsDepth))
                    {
                        if (lnRigState == 3 || lnRigState == 7)
                        {
                            double depthDiff = Math.Abs(double.Parse(objData.Rows[i]["DEPTH"].ToString()!) - double.Parse(objData.Rows[i + 1]["DEPTH"].ToString()!));
                            if (depthDiff > 0.3d) continue;
                        }

                        if (rotRigStates.ContainsKey(lnRigState))
                        {
                            if (lnRPM > maxRPM) maxRPM = lnRPM;
                        }
                    }
                }

                minRPM = maxRPM;

                for (int i = objData.Rows.Count - 2; i >= 0; i--)
                {
                    double lnRigState = double.Parse(DataService.checkNull(objData.Rows[i]["RIG_STATE"], -1).ToString()!);
                    double lnHkld = double.Parse(objData.Rows[i]["HKLD"].ToString()!);
                    double lnRPM = double.Parse(objData.Rows[i]["RPM"].ToString()!);
                    double lnDepth = double.Parse(objData.Rows[i]["DEPTH"].ToString()!);

                    bool AtInSlipsDepth = false;
                    if (lnDepth >= depthWindowFrom && lnDepth <= depthWindowTo)
                    {
                        AtInSlipsDepth = true;
                    }

                    if (AtInSlipsDepth && lnHkld > lnHKldCutOff)
                    {
                        AtInSlipsDepth = false;
                    }

                    foreach (PUSOPoint objPoint in tmpPUList.Values)
                    {
                        if (i >= objPoint.StartIndex && i <= objPoint.EndIndex)
                        {
                            AtInSlipsDepth = true;
                            break;
                        }
                    }

                    foreach (PUSOPoint objPoint in tmpSOList.Values)
                    {
                        if (i >= objPoint.StartIndex && i <= objPoint.EndIndex)
                        {
                            AtInSlipsDepth = true;
                            break;
                        }
                    }

                    if (rotRigStates.ContainsKey(lnRigState) && (!AtInSlipsDepth))
                    {
                        if (lnRigState == 3 || lnRigState == 7)
                        {
                            double depthDiff = Math.Abs(double.Parse(objData.Rows[i]["DEPTH"].ToString()!) - double.Parse(objData.Rows[i + 1]["DEPTH"].ToString()!));
                            if (depthDiff > 0.3d) continue;
                        }

                        if (rotRigStates.ContainsKey(lnRigState))
                        {
                            if (lnRPM < minRPM) minRPM = lnRPM;
                        }
                    }
                }

                broomstickLog += "\n Configured RPM Range. Min : " + _objSetup.rotateRPMCutOff.ToString() + " Max: " + _objSetup.rotateMaxRPM.ToString();
                broomstickLog += "\n Detected RPM Range for Rotate Points ";
                broomstickLog += "\n Min RPM: " + minRPM.ToString() + " Max RPM: " + maxRPM.ToString();

                for (int i = objData.Rows.Count - 2; i >= 0; i--)
                {
                    double lnRigState = double.Parse(DataService.checkNull(objData.Rows[i]["RIG_STATE"], -1).ToString()!);
                    double lnHkld = double.Parse(objData.Rows[i]["HKLD"].ToString()!);
                    double lnRPM = double.Parse(objData.Rows[i]["RPM"].ToString()!);
                    string rotPumpChannel = !string.IsNullOrWhiteSpace(_objSetup.rotatePumpMnemonic) && objData.Columns.Contains(_objSetup.rotatePumpMnemonic) ? _objSetup.rotatePumpMnemonic : "SPPA";
                    double lnPumpValue = objData.Columns.Contains(rotPumpChannel) ? double.Parse(objData.Rows[i][rotPumpChannel].ToString()!) : 0;
                    double lnTorque = objData.Columns.Contains("STOR") ? double.Parse(objData.Rows[i]["STOR"].ToString()!) : 0;
                    double lnDepth = double.Parse(objData.Rows[i]["DEPTH"].ToString()!);
                    DateTime lnDate = DateTime.Parse(objData.Rows[i]["DATETIME"].ToString()!);
                    double lnSPPA = objData.Columns.Contains("SPPA") ? double.Parse(objData.Rows[i]["SPPA"].ToString()!) : 0;
                    double lnCirc = objData.Columns.Contains("CIRC") ? double.Parse(objData.Rows[i]["CIRC"].ToString()!) : 0;

                    objWellSection = new wellSection();
                    objWellSection = getPUSOSettingsfromWellSection(lnDepth)!;

                    if (objWellSection != null && objData.Columns.Contains(objWellSection.PUSOSettings.ROTChannel))
                    {
                        lnPumpValue = double.Parse(objData.Rows[i][objWellSection.PUSOSettings.ROTChannel].ToString()!);
                    }

                    bool AtInSlipsDepth = false;
                    if (lnDepth >= depthWindowFrom && lnDepth <= depthWindowTo)
                    {
                        AtInSlipsDepth = true;
                    }

                    if (AtInSlipsDepth && lnHkld > lnHKldCutOff)
                    {
                        AtInSlipsDepth = false;
                    }

                    foreach (PUSOPoint objPoint in tmpPUList.Values)
                    {
                        if (i >= objPoint.StartIndex && i <= objPoint.EndIndex)
                        {
                            AtInSlipsDepth = true;
                            break;
                        }
                    }

                    foreach (PUSOPoint objPoint in tmpSOList.Values)
                    {
                        if (i >= objPoint.StartIndex && i <= objPoint.EndIndex)
                        {
                            AtInSlipsDepth = true;
                            break;
                        }
                    }

                    if (rotRigStates.ContainsKey(lnRigState) && (!AtInSlipsDepth))
                    {
                        if (lnRigState == 3 || lnRigState == 7)
                        {
                            double depthDiff = Math.Abs(double.Parse(objData.Rows[i]["DEPTH"].ToString()!) - double.Parse(objData.Rows[i + 1]["DEPTH"].ToString()!));
                            if (depthDiff > 0.3d) continue;
                        }

                        bool DoContinue = false;

                        if (rotRigStates.ContainsKey(lnRigState))
                        {
                            if (lnRPM > _objSetup.rotateRPMCutOff && lnRPM <= _objSetup.rotateMaxRPM)
                            {
                                if (!localRotRigStates.ContainsKey(lnRigState)) localRotRigStates.Add(lnRigState, lnRigState);
                                DoContinue = true;
                            }
                        }

                        objWellSection = new wellSection();
                        objWellSection = getPUSOSettingsfromWellSection(lnDepth)!;

                        if (lnRigState == 3 || lnRigState == 7)
                        {
                            if (objWellSection != null)
                            {
                                if (lnRPM > _objSetup.rotateRPMCutOff && lnRPM <= objWellSection.PUSOSettings.ROTMaxRPM)
                                {
                                    if (!localRotRigStates.ContainsKey(lnRigState)) localRotRigStates.Add(lnRigState, lnRigState);
                                    DoContinue = true;
                                }
                            }
                            else if (lnRPM > _objSetup.rotateRPMCutOff && lnRPM <= _objSetup.rotateMaxRPM)
                            {
                                if (!localRotRigStates.ContainsKey(lnRigState)) localRotRigStates.Add(lnRigState, lnRigState);
                                DoContinue = true;
                            }
                        }

                        if (lnHkld > lnHKldCutOff && DoContinue)
                        {
                            if (lnRigState == 11 || lnRigState == 12 || lnRigState == 13)
                            {
                                RotateRigStateFound = true;
                            }

                            strRigStates = string.Join(",", localRotRigStates.Keys.ToArray());

                            if (_objSetup.rotatePumpStatus == BroomStickSetup.bmPumpStatus.Both)
                            {
                                sampleCount++;
                                sumHkld += lnHkld;
                                sumTorque += lnTorque;

                                var objNewPoint = new ROBPoint();
                                objNewPoint.RecordDate = lnDate;
                                objNewPoint.Hkld = lnHkld;
                                objNewPoint.Torque = lnTorque;
                                objNewPoint.RPM = lnRPM;
                                objNewPoint.Circulation = lnCirc;
                                objNewPoint.PumpPressure = lnSPPA;
                                objNewPoint.Depth = lnDepth;
                                objNewPoint.RigStates = strRigStates;

                                if (!withinPUSO(objNewPoint.RecordDate, tmpPUList) && !withinPUSO(objNewPoint.RecordDate, tmpSOList))
                                {
                                    robPoints.Add(robPoints.Count + 1, objNewPoint);
                                }
                            }
                            else
                            {
                                if (_objSetup.rotatePumpStatus == BroomStickSetup.bmPumpStatus.PumpOn)
                                {
                                    if (lnPumpValue > _objSetup.rotateCutOffValue)
                                    {
                                        sampleCount++;
                                        sumHkld += lnHkld;
                                        sumTorque += lnTorque;

                                        var objNewPoint = new ROBPoint();
                                        objNewPoint.RecordDate = lnDate;
                                        objNewPoint.Hkld = lnHkld;
                                        objNewPoint.Torque = lnTorque;
                                        objNewPoint.RPM = lnRPM;
                                        objNewPoint.Circulation = lnCirc;
                                        objNewPoint.PumpPressure = lnSPPA;
                                        objNewPoint.Depth = lnDepth;
                                        objNewPoint.RigStates = strRigStates;

                                        if (!withinPUSO(objNewPoint.RecordDate, tmpPUList) && !withinPUSO(objNewPoint.RecordDate, tmpSOList))
                                        {
                                            robPoints.Add(robPoints.Count + 1, objNewPoint);
                                        }
                                    }
                                }

                                if (_objSetup.rotatePumpStatus == BroomStickSetup.bmPumpStatus.PumpOff)
                                {
                                    if (lnPumpValue <= _objSetup.rotateCutOffValue)
                                    {
                                        sampleCount++;
                                        sumHkld += lnHkld;
                                        sumTorque += lnTorque;

                                        var objNewPoint = new ROBPoint();
                                        objNewPoint.RecordDate = lnDate;
                                        objNewPoint.Hkld = lnHkld;
                                        objNewPoint.Torque = lnTorque;
                                        objNewPoint.RPM = lnRPM;
                                        objNewPoint.Circulation = lnCirc;
                                        objNewPoint.PumpPressure = lnSPPA;
                                        objNewPoint.Depth = lnDepth;
                                        objNewPoint.RigStates = strRigStates;

                                        if (!withinPUSO(objNewPoint.RecordDate, tmpPUList) && !withinPUSO(objNewPoint.RecordDate, tmpSOList))
                                        {
                                            robPoints.Add(robPoints.Count + 1, objNewPoint);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (sampleCount > 0 && sumHkld > 0d)
                {
                    AvgValue = Math.Round(sumHkld / sampleCount, 2);
                }

                if (sampleCount > 0 && sumTorque > 0d)
                {
                    AvgTorqueValue = Math.Round(sumTorque / sampleCount, 2);
                }

                processLogger?.Debug("[TORQUE-ROB] Depth=" + lnHoleDepth + " | robPoints.Count=" + robPoints.Count + " | sampleCount=" + sampleCount + " | AvgValue(Hkld)=" + AvgValue + " | AvgTorqueValue=" + AvgTorqueValue);
                processLogger?.Debug("[TORQUE-ROB] RotateRigStateFound=" + RotateRigStateFound + " | CheckPUSOProcedure=" + _objSetup.CheckPUSOProcedure + " | DontPlotROBWithNoStability=" + _objSetup.DontPlotROBWithNoStability);
                if (robPoints.Count == 0)
                {
                    processLogger?.Debug("[TORQUE-ROB] WARNING: robPoints is empty. Check ROT rig states (" + _objSetup.rotateRigStates + "), RPM cutoff (" + _objSetup.rotateRPMCutOff + "-" + _objSetup.rotateMaxRPM + "), and pump channel/cutoff.");
                }

                bool ROBStabilityFound = false;
                double lnSPPAROB = 0d;
                double lnCircROB = 0d;
                double __RPM = 0d;
                string __rotRigStates = strRigStates;

                ROBPoint.calculateROB(robPoints, _objSetup, ref AvgValue, ref AvgTorqueValue, ref ROBStabilityFound, ref lnSPPAROB, ref lnCircROB, ref __RPM);
                processLogger?.Debug("[TORQUE-ROB] After calculateROB: AvgValue=" + AvgValue + " | AvgTorqueValue=" + AvgTorqueValue + " | ROBStabilityFound=" + ROBStabilityFound);

                if (!_objSetup.DontPlotROBWithNoStability)
                {
                    if (!RotateRigStateFound)
                    {
                        AvgValue = 0d;
                        AvgTorqueValue = 0d;
                    }
                }
                else if (!ROBStabilityFound)
                {
                    AvgValue = 0d;
                    AvgTorqueValue = 0d;
                }

                if (_objSetup.CheckPUSOProcedure)
                {
                    broomstickLog += "\n Pickup -> Slack off -> Rotate procedure enforced. Running check if all points are found in this connection  ";

                    if (AvgValue > 0d && RotateRigStateFound)
                    {
                        if (tmpPUList.Count > 0 || tmpSOList.Count > 0)
                        {
                            bool isDynamicValueFound = false;
                            foreach (PUSOPoint objPoint in tmpPUList.Values)
                            {
                                if (objPoint.DynamicValue > 0)
                                {
                                    isDynamicValueFound = true;
                                    break;
                                }
                            }

                            foreach (PUSOPoint objPoint in tmpSOList.Values)
                            {
                                if (objPoint.DynamicValue > 0)
                                {
                                    isDynamicValueFound = true;
                                    break;
                                }
                            }

                            if (isDynamicValueFound)
                            {
                                broomstickLog += "\n Valid Pickup/Slack off points found in this connection. Hence adding Rotate point. ";

                                var objRotatePoint = new PUSOPoint();
                                objRotatePoint.Depth = lnHoleDepth;
                                objRotatePoint.DynamicValue = AvgValue;
                                objRotatePoint.DynamicTorqueValue = AvgTorqueValue;
                                objRotatePoint.PumpPressure = lnSPPAROB;
                                objRotatePoint.Circulation = lnCircROB;
                                objRotatePoint.StartDate = DateTime.Parse(objData.Rows[objData.Rows.Count - 1]["DATETIME"].ToString()!);
                                objRotatePoint.EndDate = DateTime.Parse(objData.Rows[objData.Rows.Count - 1]["DATETIME"].ToString()!);
                                objRotatePoint.RPM = __RPM;
                                objRotatePoint.RigStates = __rotRigStates;

                                PUSOPoint.updatePumpStatusROB(ref objRotatePoint, _objSetup);
                                RotatePoints.Add(RotatePoints.Count + 1, objRotatePoint.getCopy());

                                broomstickLog += "\n \n ** Rotate Point ** ";
                                broomstickLog += "\n   Depth: " + objRotatePoint.Depth.ToString();
                                broomstickLog += "\n   Dynamic Value: " + objRotatePoint.DynamicValue.ToString();
                                broomstickLog += "\n   Min Value: " + objRotatePoint.Min.ToString();
                                broomstickLog += "\n   Max Value: " + objRotatePoint.Max.ToString();
                                broomstickLog += "\n   Avg Value: " + objRotatePoint.Avg.ToString();

                                foreach (int lnKey in tmpPUList.Keys)
                                {
                                    PUSOPoint objPoint = tmpPUList[lnKey].getCopy();
                                    PUSOPoint.updatePumpStatusPU(ref objPoint, _objSetup);
                                    PUPoints.Add(PUPoints.Count + 1, objPoint);

                                    broomstickLog += "\n \n ** Pickup Point ** ";
                                    broomstickLog += "\n   Depth: " + objPoint.Depth.ToString();
                                    broomstickLog += "\n   Dynamic Value: " + objPoint.DynamicValue.ToString();
                                    broomstickLog += "\n   Min Value: " + objPoint.Min.ToString();
                                    broomstickLog += "\n   Max Value: " + objPoint.Max.ToString();
                                    broomstickLog += "\n   Avg Value: " + objPoint.Avg.ToString();
                                }

                                foreach (int lnKey in tmpSOList.Keys)
                                {
                                    PUSOPoint objPoint = tmpSOList[lnKey].getCopy();
                                    PUSOPoint.updatePumpStatusSO(ref objPoint, _objSetup);
                                    SOPoints.Add(SOPoints.Count + 1, objPoint);

                                    broomstickLog += "\n \n ** Slack Off Point ** ";
                                    broomstickLog += "\n   Depth: " + objPoint.Depth.ToString();
                                    broomstickLog += "\n   Dynamic Value: " + objPoint.DynamicValue.ToString();
                                    broomstickLog += "\n   Min Value: " + objPoint.Min.ToString();
                                    broomstickLog += "\n   Max Value: " + objPoint.Max.ToString();
                                    broomstickLog += "\n   Avg Value: " + objPoint.Avg.ToString();
                                }
                            }
                            else
                            {
                                broomstickLog += "\n No valid Pickup/Slack off points found in this connection. Hence not adding Rotate point. ";
                            }
                        }
                    }
                }
                else if (tmpPUList.Count > 0 || tmpSOList.Count > 0)
                {
                    broomstickLog += "\n No Pickup -> Slack off -> Rotate procedure enforced. ";

                    bool isDynamicValueFound = true;

                    if (isDynamicValueFound)
                    {
                        broomstickLog += "\n Valid ROB Point found, adding to the list ... ";

                        var objRotatePoint = new PUSOPoint();
                        objRotatePoint.Depth = lnHoleDepth;
                        objRotatePoint.DynamicValue = AvgValue;
                        objRotatePoint.DynamicTorqueValue = AvgTorqueValue;
                        objRotatePoint.Circulation = lnCircROB;
                        objRotatePoint.PumpPressure = lnSPPAROB;
                        objRotatePoint.StartDate = DateTime.Parse(objData.Rows[objData.Rows.Count - 1]["DATETIME"].ToString()!);
                        objRotatePoint.EndDate = DateTime.Parse(objData.Rows[objData.Rows.Count - 1]["DATETIME"].ToString()!);
                        objRotatePoint.RPM = __RPM;
                        objRotatePoint.RigStates = __rotRigStates;

                        PUSOPoint.updatePumpStatusROB(ref objRotatePoint, _objSetup);
                        RotatePoints.Add(RotatePoints.Count + 1, objRotatePoint.getCopy());

                        foreach (int lnKey in tmpPUList.Keys)
                        {
                            PUSOPoint objPoint = tmpPUList[lnKey].getCopy();
                            PUSOPoint.updatePumpStatusPU(ref objPoint, _objSetup);
                            PUPoints.Add(PUPoints.Count + 1, objPoint);

                            broomstickLog += "\n \n ** Pickup Point ** ";
                            broomstickLog += "\n   Depth: " + objPoint.Depth.ToString();
                            broomstickLog += "\n   Dynamic Value: " + objPoint.DynamicValue.ToString();
                            broomstickLog += "\n   Min Value: " + objPoint.Min.ToString();
                            broomstickLog += "\n   Max Value: " + objPoint.Max.ToString();
                            broomstickLog += "\n   Avg Value: " + objPoint.Avg.ToString();
                        }

                        foreach (int lnKey in tmpSOList.Keys)
                        {
                            PUSOPoint objPoint = tmpSOList[lnKey].getCopy();
                            PUSOPoint.updatePumpStatusSO(ref objPoint, _objSetup);
                            SOPoints.Add(SOPoints.Count + 1, objPoint);

                            broomstickLog += "\n \n ** Slack Off Point ** ";
                            broomstickLog += "\n   Depth: " + objPoint.Depth.ToString();
                            broomstickLog += "\n   Dynamic Value: " + objPoint.DynamicValue.ToString();
                            broomstickLog += "\n   Min Value: " + objPoint.Min.ToString();
                            broomstickLog += "\n   Max Value: " + objPoint.Max.ToString();
                            broomstickLog += "\n   Avg Value: " + objPoint.Avg.ToString();
                        }
                    }
                }

                processLogger?.Debug("detectAndAddPointsInConnection PUPoint Count: " + tmpPUList.Count.ToString());
                processLogger?.Debug("detectAndAddPointsInConnection SOPoint Count: " + tmpSOList.Count.ToString());
                processLogger?.Debug("detectAndAddPointsInConnection ROBPoint Count: " + robPoints.Count.ToString());
            }
            catch (Exception)
            {
            }
        }

        public bool withinPUSO(DateTime paramDateTime, Dictionary<int, PUSOPoint> paramList)
        {
            try
            {
                if (paramList != null)
                {
                    foreach (PUSOPoint objItem in paramList.Values)
                    {
                        if (paramDateTime >= objItem.StartDate && paramDateTime <= objItem.EndDate)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public PUSOPoint? detectPU(DataTable objData, int currentRowIndex, ref int returnPointer, int limitRow)
        {
            try
            {
                if (objData == null || currentRowIndex < 0 || currentRowIndex + 1 >= objData.Rows.Count) return null;

                var objPUSO = new PUSOPoint();
                objPUSO.StartIndex = currentRowIndex;
                objPUSO.StartDate = DateTime.Parse(objData.Rows[currentRowIndex + 1]["DATETIME"].ToString()!);
                objPUSO.StartDepth = double.Parse(objData.Rows[currentRowIndex + 1]["DEPTH"].ToString()!);

                double sumSPPA = 0d;
                double sumCirc = 0d;
                int sampleCount = 0;

                for (int i = currentRowIndex; i >= limitRow; i--)
                {
                    if (i - 1 < 0) break;

                    double lnDepth = double.Parse(objData.Rows[i]["DEPTH"].ToString()!);
                    double lnNextDepth = double.Parse(objData.Rows[i - 1]["DEPTH"].ToString()!);
                    DateTime lnDate = DateTime.Parse(objData.Rows[i]["DATETIME"].ToString()!);
                    double lnRigState = double.Parse(DataService.checkNull(objData.Rows[i]["RIG_STATE"], -1).ToString()!);
                    double lnNextRigState = double.Parse(DataService.checkNull(objData.Rows[i - 1]["RIG_STATE"], -1).ToString()!);
                    double lnSPPA = objData.Columns.Contains("SPPA") ? double.Parse(objData.Rows[i]["SPPA"].ToString()!) : 0;
                    double lnCirc = objData.Columns.Contains("CIRC") ? double.Parse(objData.Rows[i]["CIRC"].ToString()!) : 0;

                    sumSPPA += lnSPPA;
                    sumCirc += lnCirc;
                    sampleCount++;

                    bool DoCloseDataCollection = false;

                    if (lnNextDepth > lnDepth || (!pkupRigStates.ContainsKey(lnNextRigState)))
                    {
                        DoCloseDataCollection = true;
                    }

                    if (lnNextDepth == lnDepth || !pkupRigStates.ContainsKey(lnNextRigState))
                    {
                        DateTime PauseStartTime1 = new DateTime();

                        for (int pp = i; pp < objData.Rows.Count; pp++)
                        {
                            double ppDepth = double.Parse(objData.Rows[pp]["DEPTH"].ToString()!);
                            DateTime ppDate = DateTime.Parse(objData.Rows[pp]["DATETIME"].ToString()!);

                            if (ppDepth != lnDepth)
                            {
                                if (pp - 1 >= 0 && pp - 1 < objData.Rows.Count)
                                {
                                    PauseStartTime1 = DateTime.Parse(objData.Rows[pp]["DATETIME"].ToString()!);
                                }
                                else
                                {
                                    PauseStartTime1 = ppDate;
                                }
                                break;
                            }
                        }

                        int TimeDiff = (int)Math.Abs((lnDate - PauseStartTime1).TotalSeconds);
                        if (TimeDiff > _objSetup.MaxPauseTime)
                        {
                            DoCloseDataCollection = true;
                        }
                    }

                    if (DoCloseDataCollection)
                    {
                        objPUSO.EndIndex = i;
                        objPUSO.EndDate = DateTime.Parse(objData.Rows[i]["DATETIME"].ToString()!);
                        objPUSO.EndDepth = double.Parse(objData.Rows[i]["DEPTH"].ToString()!);

                        if (sampleCount > 0 && sumSPPA > 0d)
                        {
                            objPUSO.PumpPressure = Math.Round(sumSPPA / sampleCount, 2);
                        }

                        if (sampleCount > 0 && sumCirc > 0d)
                        {
                            objPUSO.Circulation = Math.Round(sumCirc / sampleCount, 2);
                        }

                        returnPointer = i - 1;

                        if (_objSetup.PULocalMaxFilter)
                        {
                            for (int j = objPUSO.StartIndex - 1; j >= objPUSO.EndIndex; j--)
                            {
                                if (j + 1 >= objData.Rows.Count || j < 0) break;
                                double lnPrevHKld = double.Parse(objData.Rows[j + 1]["HKLD"].ToString()!);
                                double lnHkld = double.Parse(objData.Rows[j]["HKLD"].ToString()!);

                                if (lnHkld < lnPrevHKld)
                                {
                                    objPUSO.StartIndex = j;
                                    break;
                                }
                            }
                        }

                        return objPUSO;
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public PUSOPoint? detectSO(DataTable objData, int currentRowIndex, ref int returnPointer, int limitRow)
        {
            try
            {
                if (objData == null || currentRowIndex < 0 || currentRowIndex + 1 >= objData.Rows.Count) return null;

                var objPUSO = new PUSOPoint();
                objPUSO.StartIndex = currentRowIndex;
                objPUSO.StartDate = DateTime.Parse(objData.Rows[currentRowIndex + 1]["DATETIME"].ToString()!);
                objPUSO.StartDepth = double.Parse(objData.Rows[currentRowIndex + 1]["DEPTH"].ToString()!);

                double sumSPPA = 0d;
                double sumCirc = 0d;
                int sampleCount = 0;

                for (int i = currentRowIndex; i >= limitRow; i--)
                {
                    if (i - 1 < 0) break;

                    double lnDepth = double.Parse(objData.Rows[i]["DEPTH"].ToString()!);
                    double lnNextDepth = double.Parse(objData.Rows[i - 1]["DEPTH"].ToString()!);
                    DateTime lnDate = DateTime.Parse(objData.Rows[i]["DATETIME"].ToString()!);
                    double lnRigState = double.Parse(DataService.checkNull(objData.Rows[i]["RIG_STATE"], -1).ToString()!);
                    double lnNextRigState = double.Parse(DataService.checkNull(objData.Rows[i - 1]["RIG_STATE"], -1).ToString()!);
                    double lnSPPA = objData.Columns.Contains("SPPA") ? double.Parse(objData.Rows[i]["SPPA"].ToString()!) : 0;
                    double lnCirc = objData.Columns.Contains("CIRC") ? double.Parse(objData.Rows[i]["CIRC"].ToString()!) : 0;

                    sumSPPA += lnSPPA;
                    sumCirc += lnCirc;
                    sampleCount++;

                    bool DoCloseDataCollection = false;

                    if (lnNextDepth < lnDepth || !slkRigStates.ContainsKey(lnNextRigState))
                    {
                        DoCloseDataCollection = true;
                    }

                    if (lnNextDepth == lnDepth || !slkRigStates.ContainsKey(lnNextRigState))
                    {
                        DateTime PauseStartTime = new DateTime();

                        for (int pp = i; pp < objData.Rows.Count; pp++)
                        {
                            double ppDepth = double.Parse(objData.Rows[pp]["DEPTH"].ToString()!);
                            DateTime ppDate = DateTime.Parse(objData.Rows[pp]["DATETIME"].ToString()!);

                            if (ppDepth != lnDepth)
                            {
                                if (pp - 1 >= 0 && pp - 1 < objData.Rows.Count)
                                {
                                    PauseStartTime = DateTime.Parse(objData.Rows[pp]["DATETIME"].ToString()!);
                                }
                                else
                                {
                                    PauseStartTime = ppDate;
                                }
                                break;
                            }
                        }

                        int TimeDiff = (int)Math.Abs((lnDate - PauseStartTime).TotalSeconds);
                        if (TimeDiff > _objSetup.MaxPauseTime)
                        {
                            DoCloseDataCollection = true;
                        }
                    }

                    if (DoCloseDataCollection)
                    {
                        objPUSO.EndIndex = i;
                        objPUSO.EndDate = DateTime.Parse(objData.Rows[i]["DATETIME"].ToString()!);
                        objPUSO.EndDepth = double.Parse(objData.Rows[i]["DEPTH"].ToString()!);

                        if (sampleCount > 0 && sumSPPA > 0d)
                        {
                            objPUSO.PumpPressure = Math.Round(sumSPPA / sampleCount, 2);
                        }

                        if (sampleCount > 0 && sumCirc > 0d)
                        {
                            objPUSO.Circulation = Math.Round(sumCirc / sampleCount, 2);
                        }

                        returnPointer = i - 1;

                        if (_objSetup.SOLocalMinFilter)
                        {
                            for (int j = objPUSO.StartIndex - 1; j >= objPUSO.EndIndex; j--)
                            {
                                if (j + 1 >= objData.Rows.Count || j < 0) break;
                                double lnPrevHKld = double.Parse(objData.Rows[j + 1]["HKLD"].ToString()!);
                                double lnHkld = double.Parse(objData.Rows[j]["HKLD"].ToString()!);

                                if (lnHkld > lnPrevHKld)
                                {
                                    objPUSO.StartIndex = j;
                                    break;
                                }
                            }
                        }

                        return objPUSO;
                    }
                }

                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public object? groupByDepth()
        {
            try
            {
                var tmpPUList = new Dictionary<double, Dictionary<int, PUSOPoint>>();
                var tmpSOList = new Dictionary<double, Dictionary<int, PUSOPoint>>();
                var tmpROBList = new Dictionary<double, Dictionary<int, PUSOPoint>>();

                foreach (PUSOPoint objPoint in PUPoints.Values)
                {
                    if (!tmpPUList.ContainsKey(objPoint.Depth))
                    {
                        tmpPUList.Add(objPoint.Depth, new Dictionary<int, PUSOPoint>());
                    }
                    tmpPUList[objPoint.Depth].Add(tmpPUList[objPoint.Depth].Count + 1, objPoint.getCopy());
                }

                foreach (PUSOPoint objPoint in SOPoints.Values)
                {
                    if (!tmpSOList.ContainsKey(objPoint.Depth))
                    {
                        tmpSOList.Add(objPoint.Depth, new Dictionary<int, PUSOPoint>());
                    }
                    tmpSOList[objPoint.Depth].Add(tmpSOList[objPoint.Depth].Count + 1, objPoint.getCopy());
                }

                foreach (PUSOPoint objPoint in RotatePoints.Values)
                {
                    if (!tmpROBList.ContainsKey(objPoint.Depth))
                    {
                        tmpROBList.Add(objPoint.Depth, new Dictionary<int, PUSOPoint>());
                    }
                    tmpROBList[objPoint.Depth].Add(tmpROBList[objPoint.Depth].Count + 1, objPoint.getCopy());
                }

                var finalPUList = new Dictionary<int, PUSOPoint>();
                var finalSOList = new Dictionary<int, PUSOPoint>();
                var finalROBList = new Dictionary<int, PUSOPoint>();

                foreach (double lnDepth in tmpPUList.Keys)
                {
                    PUSOPoint? maxPoint = null;
                    PUSOPoint? minPoint = null;
                    double minValue = 0d;
                    double maxValue = 0d;

                    foreach (PUSOPoint objPoint in tmpPUList[lnDepth].Values)
                    {
                        if (objPoint.DynamicValue > maxValue)
                        {
                            maxValue = objPoint.DynamicValue;
                            maxPoint = objPoint.getCopy();
                        }
                    }

                    if (maxPoint == null)
                    {
                        maxValue = tmpPUList[lnDepth].Values.First().DynamicValue;
                        maxPoint = tmpPUList[lnDepth].Values.First().getCopy();
                    }

                    minValue = maxValue;
                    minPoint = maxPoint.getCopy();

                    foreach (PUSOPoint objPoint in tmpPUList[lnDepth].Values)
                    {
                        if (objPoint.DynamicValue < minValue)
                        {
                            minValue = objPoint.DynamicValue;
                            minPoint = objPoint.getCopy();
                        }
                    }

                    if (minPoint == null)
                    {
                        minValue = tmpPUList[lnDepth].Values.First().DynamicValue;
                        minPoint = tmpPUList[lnDepth].Values.First().getCopy();
                    }

                    if (_objSetup.pickupMultiPointMethod == BroomStickSetup.bmMultiSelectMethod.Max)
                    {
                        finalPUList.Add(finalPUList.Count + 1, maxPoint.getCopy());
                    }
                    else
                    {
                        finalPUList.Add(finalPUList.Count + 1, minPoint.getCopy());
                    }
                }

                foreach (double lnDepth in tmpSOList.Keys)
                {
                    PUSOPoint? maxPoint = null;
                    PUSOPoint? minPoint = null;
                    double minValue = 0d;
                    double maxValue = 0d;

                    foreach (PUSOPoint objPoint in tmpSOList[lnDepth].Values)
                    {
                        if (objPoint.DynamicValue > maxValue)
                        {
                            maxValue = objPoint.DynamicValue;
                            maxPoint = objPoint.getCopy();
                        }
                    }

                    if (maxPoint == null)
                    {
                        maxValue = tmpSOList[lnDepth].Values.First().DynamicValue;
                        maxPoint = tmpSOList[lnDepth].Values.First().getCopy();
                    }

                    minValue = maxValue;
                    minPoint = maxPoint.getCopy();

                    foreach (PUSOPoint objPoint in tmpSOList[lnDepth].Values)
                    {
                        if (objPoint.DynamicValue < minValue)
                        {
                            minValue = objPoint.DynamicValue;
                            minPoint = objPoint.getCopy();
                        }
                    }

                    if (minPoint == null)
                    {
                        minValue = tmpSOList[lnDepth].Values.First().DynamicValue;
                        minPoint = tmpSOList[lnDepth].Values.First().getCopy();
                    }

                    if (_objSetup.slackOffMultiPointMethod == BroomStickSetup.bmMultiSelectMethod.Max)
                    {
                        finalSOList.Add(finalSOList.Count + 1, maxPoint.getCopy());
                    }
                    else
                    {
                        finalSOList.Add(finalSOList.Count + 1, minPoint.getCopy());
                    }
                }

                foreach (double lnDepth in tmpROBList.Keys)
                {
                    PUSOPoint? maxPoint = null;
                    PUSOPoint? minPoint = null;
                    double minValue = 0d;
                    double maxValue = 0d;

                    foreach (PUSOPoint objPoint in tmpROBList[lnDepth].Values)
                    {
                        if (objPoint.DynamicValue > maxValue)
                        {
                            maxValue = objPoint.DynamicValue;
                            maxPoint = objPoint.getCopy();
                        }
                    }

                    if (maxPoint == null)
                    {
                        maxValue = tmpROBList[lnDepth].Values.First().DynamicValue;
                        maxPoint = tmpROBList[lnDepth].Values.First().getCopy();
                    }

                    minValue = maxValue;
                    minPoint = maxPoint.getCopy();

                    foreach (PUSOPoint objPoint in tmpROBList[lnDepth].Values)
                    {
                        if (objPoint.DynamicValue < minValue)
                        {
                            minValue = objPoint.DynamicValue;
                            minPoint = objPoint.getCopy();
                        }
                    }

                    if (minPoint == null)
                    {
                        minValue = tmpROBList[lnDepth].Values.First().DynamicValue;
                        minPoint = tmpROBList[lnDepth].Values.First().getCopy();
                    }

                    if (_objSetup.ROBMultiPointMethod == BroomStickSetup.bmMultiSelectMethod.Max)
                    {
                        finalROBList.Add(finalROBList.Count + 1, maxPoint.getCopy());
                    }
                    else
                    {
                        finalROBList.Add(finalROBList.Count + 1, minPoint.getCopy());
                    }
                }

                PUPoints.Clear();
                SOPoints.Clear();
                RotatePoints.Clear();

                foreach (PUSOPoint objPoint in finalPUList.Values)
                {
                    PUPoints.Add(PUPoints.Count + 1, objPoint.getCopy());
                }

                foreach (PUSOPoint objPoint in finalSOList.Values)
                {
                    SOPoints.Add(SOPoints.Count + 1, objPoint.getCopy());
                }

                foreach (PUSOPoint objPoint in finalROBList.Values)
                {
                    RotatePoints.Add(RotatePoints.Count + 1, objPoint.getCopy());
                }
            }
            catch (Exception)
            {
            }

            return null;
        }

        public void enforceRule()
        {
            try
            {
                var newPUList = new Dictionary<int, PUSOPoint>();
                var newSOList = new Dictionary<int, PUSOPoint>();
                var newROBList = new Dictionary<int, PUSOPoint>();

                // Pickup Points should be greater than ROB and Slack Off
                foreach (PUSOPoint objPoint in PUPoints.Values)
                {
                    var tmpROB = new Dictionary<int, PUSOPoint>();
                    var tmpSO = new Dictionary<int, PUSOPoint>();

                    foreach (PUSOPoint objItem in RotatePoints.Values)
                    {
                        if (Math.Round(objItem.Depth, 2) == Math.Round(objPoint.Depth, 2))
                        {
                            tmpROB.Add(tmpROB.Count + 1, objItem.getCopy());
                        }
                    }

                    foreach (PUSOPoint objItem in SOPoints.Values)
                    {
                        if (Math.Round(objItem.Depth, 2) == Math.Round(objPoint.Depth, 2))
                        {
                            tmpSO.Add(tmpSO.Count + 1, objItem.getCopy());
                        }
                    }

                    bool isValidPoint = true;

                    foreach (PUSOPoint objItem in tmpROB.Values)
                    {
                        if (objPoint.DynamicValue <= objItem.DynamicValue)
                        {
                            isValidPoint = false;
                            break;
                        }
                    }

                    foreach (PUSOPoint objItem in tmpSO.Values)
                    {
                        if (objPoint.DynamicValue <= objItem.DynamicValue)
                        {
                            isValidPoint = false;
                            break;
                        }
                    }

                    if (isValidPoint)
                    {
                        newPUList.Add(newPUList.Count + 1, objPoint.getCopy());
                    }
                }

                // Slack Off Points should be less than ROB and Pick up
                foreach (PUSOPoint objPoint in SOPoints.Values)
                {
                    var tmpROB = new Dictionary<int, PUSOPoint>();
                    var tmpPU = new Dictionary<int, PUSOPoint>();

                    foreach (PUSOPoint objItem in RotatePoints.Values)
                    {
                        if (Math.Round(objItem.Depth, 2) == Math.Round(objPoint.Depth, 2))
                        {
                            tmpROB.Add(tmpROB.Count + 1, objItem.getCopy());
                        }
                    }

                    foreach (PUSOPoint objItem in PUPoints.Values)
                    {
                        if (Math.Round(objItem.Depth, 2) == Math.Round(objPoint.Depth, 2))
                        {
                            tmpPU.Add(tmpPU.Count + 1, objItem.getCopy());
                        }
                    }

                    bool isValidPoint = true;

                    foreach (PUSOPoint objItem in tmpROB.Values)
                    {
                        if (objPoint.DynamicValue >= objItem.DynamicValue)
                        {
                            isValidPoint = false;
                            break;
                        }
                    }

                    foreach (PUSOPoint objItem in tmpPU.Values)
                    {
                        if (objPoint.DynamicValue >= objItem.DynamicValue)
                        {
                            isValidPoint = false;
                            break;
                        }
                    }

                    if (isValidPoint)
                    {
                        newSOList.Add(newSOList.Count + 1, objPoint.getCopy());
                    }
                }

                // ROB Points should be between Pickup and Slack Off
                foreach (PUSOPoint objPoint in RotatePoints.Values)
                {
                    newROBList.Add(newROBList.Count + 1, objPoint.getCopy());
                }

                PUPoints.Clear();
                SOPoints.Clear();
                RotatePoints.Clear();

                foreach (PUSOPoint objPoint in newPUList.Values)
                {
                    PUPoints.Add(PUPoints.Count + 1, objPoint.getCopy());
                }

                foreach (PUSOPoint objPoint in newSOList.Values)
                {
                    SOPoints.Add(SOPoints.Count + 1, objPoint.getCopy());
                }

                foreach (PUSOPoint objPoint in newROBList.Values)
                {
                    RotatePoints.Add(RotatePoints.Count + 1, objPoint.getCopy());
                }
            }
            catch (Exception)
            {
            }
        }

        public bool IsValidConnection(
            VirtualTable.VirtualTable objVirtualData,
            int currentRowIndex,
            ref int forwardPointer,
            ref int paramDrillingStartRow,
            ref int paramlnInSlipsRowIndex,
            ref double paramlnInSlipsDepth,
            ref int paramDrillingEndRow)
        {
            try
            {
                double lnHKldCutOff = objRigState != null ? objRigState.HookloadCutOff : 90.0;
                double lnInSlipsDepth = objVirtualData.getDoubleValue(currentRowIndex, "DEPTH");
                double lnHoleDepth = objVirtualData.getDoubleValue(currentRowIndex, "HDTH");
                int lnInSlipsRowIndex = currentRowIndex;

                bool isDrillingFound = false;
                bool isNextDrillingFound = false;

                DateTime pointerDate = objVirtualData.getDateTimeValue(currentRowIndex, "DATETIME");

                int timeThreshold = 5 * 60; // 5 hours in minutes
                DateTime lowerLimit = pointerDate.AddMinutes(-1 * timeThreshold);
                DateTime upperLimit = pointerDate.AddMinutes(timeThreshold);

                DateTime drillingEndTime = DateTime.MinValue;
                DateTime drillingStartTime = DateTime.MinValue;
                DateTime slipsStartTime = DateTime.MinValue;
                DateTime slipsEndTime = DateTime.MinValue;
                DateTime slipsToBottomTime = DateTime.MinValue;

                int drillingEndRowIndex = 0;
                int slipsStartRowIndex = 0;
                int slipsEndRowIndex = 0;
                int slipsToBottomRowIndex = 0;
                int drillingStartRowIndex = 0;
                int depthCounter = 0;
                double drillingEndHkld = 0;

                // Go back up to 60 minutes and find out ...
                for (int i = currentRowIndex; i < objVirtualData.RecordCount; i++)
                {
                    double lnRigState = objVirtualData.getDoubleValue(i, "RIG_STATE");
                    drillingEndTime = objVirtualData.getDateTimeValue(i, "DATETIME");
                    drillingEndRowIndex = i;
                    drillingEndHkld = objVirtualData.getDoubleValue(i, "HKLD");

                    if (drillingEndTime <= lowerLimit) break;

                    if (lnRigState == 0d || lnRigState == 1d || lnRigState == 19d)
                    {
                        DateTime currentDate = drillingEndTime;
                        bool isConstantlyDrilling = true;
                        int lnBreakRow = i;

                        for (int j = i; j < objVirtualData.RecordCount; j++)
                        {
                            double lnSubRigState = objVirtualData.getDoubleValue(j, "RIG_STATE");
                            DateTime lnSubDate = objVirtualData.getDateTimeValue(j, "DATETIME");

                            if (Math.Abs((currentDate - lnSubDate).TotalSeconds) >= 60) break;

                            if (lnSubRigState == 0d || lnSubRigState == 1d || lnSubRigState == 19d)
                            {
                            }
                            else
                            {
                                isConstantlyDrilling = false;
                                lnBreakRow = j;
                                break;
                            }
                        }

                        if (!RUN_DRLG_CHECK) isConstantlyDrilling = true;

                        if (!isConstantlyDrilling)
                        {
                            bool subDrillingRowFound = false;

                            for (int j = lnBreakRow; j < objVirtualData.RecordCount; j++)
                            {
                                double lnSubRigState = objVirtualData.getDoubleValue(j, "RIG_STATE");
                                DateTime lnSubDate = objVirtualData.getDateTimeValue(j, "DATETIME");

                                if (lnSubDate <= lowerLimit) break;

                                if (lnSubRigState == 0d || lnSubRigState == 1d || lnSubRigState == 19d)
                                {
                                    if (RUN_DRLG_CHECK)
                                    {
                                        int subSubStartRow = j;
                                        bool innerMatched = false;

                                        while (subSubStartRow < objVirtualData.RecordCount)
                                        {
                                            bool restartSubSearch = false;
                                            for (int ssr = subSubStartRow; ssr < objVirtualData.RecordCount; ssr++)
                                            {
                                                if (ssr == objVirtualData.RecordCount - 1) break;

                                                double lnsSubRigState = objVirtualData.getDoubleValue(ssr, "RIG_STATE");
                                                DateTime lnsSubDate = objVirtualData.getDateTimeValue(ssr, "DATETIME");

                                                if (lnsSubRigState == 0d || lnsSubRigState == 1d || lnsSubRigState == 19d)
                                                {
                                                    if (Math.Abs((lnsSubDate - lnSubDate).TotalSeconds) >= 60)
                                                    {
                                                        subDrillingRowFound = true;
                                                        drillingEndTime = objVirtualData.getDateTimeValue(ssr, "DATETIME");
                                                        drillingEndRowIndex = ssr;
                                                        drillingEndHkld = objVirtualData.getDoubleValue(ssr, "HKLD");
                                                        isDrillingFound = true;
                                                        innerMatched = true;
                                                        break;
                                                    }
                                                }
                                                else
                                                {
                                                    subSubStartRow = ssr + 1;
                                                    restartSubSearch = true;
                                                    break;
                                                }
                                            }

                                            if (innerMatched || !restartSubSearch) break;
                                        }

                                        if (innerMatched) break;
                                    }
                                    else
                                    {
                                        subDrillingRowFound = true;
                                        drillingEndTime = objVirtualData.getDateTimeValue(j, "DATETIME");
                                        drillingEndRowIndex = j;
                                        drillingEndHkld = objVirtualData.getDoubleValue(j, "HKLD");
                                        isDrillingFound = true;
                                        break;
                                    }
                                }
                            }

                            if (!RUN_DRLG_CHECK) subDrillingRowFound = true;

                            if (!subDrillingRowFound)
                            {
                                isDrillingFound = false;
                                break;
                            }
                        }

                        isDrillingFound = true;
                        break;
                    }
                }

                for (int i = currentRowIndex; i >= 0; i--)
                {
                    double lnRigState = objVirtualData.getDoubleValue(i, "RIG_STATE");
                    drillingStartTime = objVirtualData.getDateTimeValue(i, "DATETIME");
                    drillingStartRowIndex = i;

                    if (drillingStartTime >= upperLimit) break;

                    if (lnRigState == 0d || lnRigState == 1d || lnRigState == 19d)
                    {
                        DateTime lnEndLimitNextDrillingTime = drillingStartTime.AddSeconds(60);
                        bool isContinuousDrilling = true;

                        for (int j = i; j >= 0; j--)
                        {
                            DateTime lnCurrentDateTime = objVirtualData.getDateTimeValue(j, "DATETIME");
                            if (lnCurrentDateTime >= upperLimit || lnCurrentDateTime >= lnEndLimitNextDrillingTime) break;

                            double lnSubRigState = objVirtualData.getDoubleValue(j, "RIG_STATE");
                            if (lnSubRigState != 0d && lnSubRigState != 1d && lnSubRigState != 19d)
                            {
                                isContinuousDrilling = false;
                            }
                        }

                        if (!RUN_DRLG_CHECK) isContinuousDrilling = true;

                        if (isContinuousDrilling)
                        {
                            isNextDrillingFound = true;
                            break;
                        }
                    }
                }

                if ((!isDrillingFound) || (!isNextDrillingFound))
                {
                    for (int i = currentRowIndex; i < objVirtualData.RecordCount; i++)
                    {
                        double lnRigState = objVirtualData.getDoubleValue(i, "RIG_STATE");
                        if (lnRigState == 0d || lnRigState == 1d || lnRigState == 19d)
                        {
                            forwardPointer = i;
                            return false;
                        }
                    }

                    forwardPointer = (int)objVirtualData.RecordCount - 1;
                    return false;
                }
                else
                {
                    forwardPointer = drillingEndRowIndex;
                }

                // Go backward and find the slip start time
                if (RUN_HKLD_CHECK)
                {
                    for (int i = drillingEndRowIndex - 1; i >= 0; i--)
                    {
                        double lnHkld = objVirtualData.getDoubleValue(i, "HKLD");
                        double lnRigState = objVirtualData.getDoubleValue(i, "RIG_STATE");

                        if (lnHkld <= lnHKldCutOff || lnRigState == 2d)
                        {
                            slipsStartRowIndex = i;
                            slipsStartTime = objVirtualData.getDateTimeValue(i, "DATETIME");
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = drillingEndRowIndex - 1; i >= 0; i--)
                    {
                        double lnHkld = objVirtualData.getDoubleValue(i, "HKLD");
                        double lnRigState = objVirtualData.getDoubleValue(i, "RIG_STATE");

                        if (lnHkld <= lnHKldCutOff && (lnRigState == 2d || lnRigState == 11d || lnRigState == 12d || lnRigState == 13d || lnRigState == 14d))
                        {
                            slipsStartRowIndex = i;
                            slipsStartTime = objVirtualData.getDateTimeValue(i, "DATETIME");
                            break;
                        }
                    }
                }

                if (slipsStartRowIndex > 0)
                {
                    if (RUN_HKLD_CHECK)
                    {
                        for (int i = slipsStartRowIndex - 1; i >= 0; i--)
                        {
                            double lnHkld = objVirtualData.getDoubleValue(i, "HKLD");
                            if (lnHkld > lnHKldCutOff)
                            {
                                slipsEndRowIndex = i;
                                slipsEndTime = objVirtualData.getDateTimeValue(i, "DATETIME");
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (int i = slipsStartRowIndex - 1; i >= 0; i--)
                        {
                            double lnHkld = objVirtualData.getDoubleValue(i, "HKLD");
                            double lnRigState = objVirtualData.getDoubleValue(i, "RIG_STATE");

                            if (lnHkld > lnHKldCutOff && (lnRigState == 2d || lnRigState == 11d || lnRigState == 12d || lnRigState == 13d || lnRigState == 14d))
                            {
                                slipsEndRowIndex = i;
                                slipsEndTime = objVirtualData.getDateTimeValue(i, "DATETIME");
                                break;
                            }
                        }
                    }

                    int searchLimitNext = 0;
                    DateTime dtSearchLimitNext = objVirtualData.getDateTimeValue(slipsEndRowIndex, "DATETIME").AddHours(5);

                    for (int nd = slipsEndRowIndex; nd >= 0; nd--)
                    {
                        if (objVirtualData.getDateTimeValue(nd, "DATETIME") >= dtSearchLimitNext)
                        {
                            searchLimitNext = nd;
                            break;
                        }
                    }

                    if (searchLimitNext < 0) searchLimitNext = 0;

                    for (int i = slipsEndRowIndex - 1; i >= searchLimitNext; i--)
                    {
                        double lnRigState = objVirtualData.getDoubleValue(i, "RIG_STATE");

                        if (lnRigState == 0d || lnRigState == 1d || lnRigState == 19d)
                        {
                            DateTime lnEndLimitNextDrillingTime = drillingStartTime.AddSeconds(60);
                            bool isContinuousDrilling = true;

                            for (int j = i; j >= 0; j--)
                            {
                                DateTime lnCurrentDateTime = objVirtualData.getDateTimeValue(j, "DATETIME");
                                if (lnCurrentDateTime >= upperLimit || lnCurrentDateTime >= lnEndLimitNextDrillingTime) break;

                                double lnSubRigState = objVirtualData.getDoubleValue(j, "RIG_STATE");
                                if (lnSubRigState != 0d && lnSubRigState != 1d && lnSubRigState != 19d)
                                {
                                    isContinuousDrilling = false;
                                }
                            }

                            if (!RUN_DRLG_CHECK) isContinuousDrilling = true;

                            if (isContinuousDrilling)
                            {
                                slipsToBottomRowIndex = i;
                                slipsToBottomTime = objVirtualData.getDateTimeValue(i, "DATETIME");
                                break;
                            }
                        }
                    }

                    if (slipsToBottomTime == DateTime.MinValue)
                    {
                        slipsToBottomTime = slipsEndTime;
                    }

                    if (RUN_HKLD_CHECK)
                    {
                        for (int i = slipsEndRowIndex; i >= slipsToBottomRowIndex; i--)
                        {
                            double lnHkld = objVirtualData.getDoubleValue(i, "HKLD");
                            if (lnHkld < lnHKldCutOff)
                            {
                                DateTime lnSubDate = objVirtualData.getDateTimeValue(i, "DATETIME");
                                if (lnSubDate > slipsEndTime)
                                {
                                    slipsEndRowIndex = i;
                                    slipsEndTime = objVirtualData.getDateTimeValue(i, "DATETIME");
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int i = slipsEndRowIndex; i >= slipsToBottomRowIndex; i--)
                        {
                            double lnHkld = objVirtualData.getDoubleValue(i, "HKLD");
                            double lnRigState = objVirtualData.getDoubleValue(i, "RIG_STATE");

                            if (lnHkld < lnHKldCutOff && (lnRigState == 2d || lnRigState == 11d || lnRigState == 12d || lnRigState == 13d || lnRigState == 14d))
                            {
                                DateTime lnSubDate = objVirtualData.getDateTimeValue(i, "DATETIME");
                                if (lnSubDate > slipsEndTime)
                                {
                                    slipsEndRowIndex = i;
                                    slipsEndTime = objVirtualData.getDateTimeValue(i, "DATETIME");
                                }
                            }
                        }
                    }

                    double Slot1Time = Math.Abs((drillingEndTime - slipsStartTime).TotalSeconds);
                    double Slot2Time = Math.Abs((slipsStartTime - slipsEndTime).TotalSeconds);

                    if (Slot1Time >= 5d && Slot2Time < 14400d)
                    {
                        paramDrillingStartRow = drillingEndRowIndex;
                        paramlnInSlipsRowIndex = slipsStartRowIndex;
                        paramlnInSlipsDepth = lnInSlipsDepth;
                        paramDrillingEndRow = drillingStartRowIndex;
                        return true;
                    }

                    depthCounter++;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}



