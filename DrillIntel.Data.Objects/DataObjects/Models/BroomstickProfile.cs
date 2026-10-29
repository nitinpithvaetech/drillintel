using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DrillIntel.Data;

namespace DrillIntel.Data.Objects.DataObjects.Models
{
    public class BroomstickProfile
    {
        public string ID { get; set; } = "";
        public string Name { get; set; } = "";
        public int Type { get; set; } = 0;
        public string Notes { get; set; } = "";

        public int DownSampleOn { get; set; } = 0;
        public int DataPoints { get; set; } = 6;
        public int TimePeriod { get; set; } = 0;
        public string GroupFunctions { get; set; } = "";
        public bool ShowDepthTrack { get; set; } = false;
        public int TrackWidth { get; set; } = 100;
        public bool FilterByRange { get; set; } = false;
        public double MinHooklaod { get; set; } = 0;
        public double MaxHookload { get; set; } = 0;
        public bool FilterByInterval { get; set; } = false;
        public double DepthInterval { get; set; } = 100;
        public double IntervalWindow { get; set; } = 10;

        public int PointsToPlot { get; set; } = 0;
        public string PkupPumpChannel { get; set; } = "SPPA";
        public double PkupPumpCutOff { get; set; } = 99;
        public double PkupRPMCutOff { get; set; } = 12;
        public double PkupMaxMovment { get; set; } = 70;
        public double PkupMinMovement { get; set; } = 5;
        public int PkupPlotPoints { get; set; } = 0;
        public int PkupStaticMethod { get; set; } = 0;
        public int PkupDynamicMethod { get; set; } = 0;
        public bool PkupLocalMax { get; set; } = false;

        public string SlkPumpChannel { get; set; } = "SPPA";
        public double SlkPumpCutOff { get; set; } = 99;
        public double SlkRPMCutOff { get; set; } = 12;
        public double SlkMaxMovment { get; set; } = 70;
        public double SlkMinMovement { get; set; } = 5;
        public int SlkPlotPoints { get; set; } = 0;
        public int SlkStaticMethod { get; set; } = 0;
        public int SlkDynamicMethod { get; set; } = 0;
        public bool SlkLocalMax { get; set; } = false;

        public string RotPumpChannel { get; set; } = "SPPA";
        public double RotPumpCutOff { get; set; } = 99;
        public double RotMinRPM { get; set; } = 12;
        public double RotMaxRPM { get; set; } = 30;
        public int RotPlotPoints { get; set; } = 0;
        public double RotChange { get; set; } = 1;
        public double RotPoints { get; set; } = 1;
        public bool RotCheckPUSO { get; set; } = false;
        public int TimeThreshold { get; set; } = 1;
        public bool EnforcePUSO { get; set; } = false;

        public int PkupMultiMethod { get; set; } = 0;
        public int SlkMultiMethod { get; set; } = 0;
        public int RotMultiMethod { get; set; } = 0;
        public bool ShowMultiple { get; set; } = false;

        public string PkupRigStates { get; set; } = "";
        public string SlkRigStates { get; set; } = "";
        public string RotRigStates { get; set; } = "";
        public bool EnforceRule { get; set; } = false;

        public bool PlotOnBottomTorque { get; set; } = false;

        // Properties matching Broomstick Profile schema
        public bool IsDefault { get; set; } = false;
        public string CreatedBy { get; set; } = "";
        public string CreatedDate { get; set; } = "";
        public string ModifiedBy { get; set; } = "";
        public string ModifiedDate { get; set; } = "";

        public double CasingPkupMinMovement { get; set; } = 0;
        public double CasingPkupMaxMovement { get; set; } = 0;
        public double CasingSlkMinMovement { get; set; } = 0;
        public double CasingSlkMaxMovement { get; set; } = 0;
        public string CasingPkupRigStates { get; set; } = "";
        public string CasingSlkRigStates { get; set; } = "";

        // =====================================================================
        // Target Table Names: Global vs Project Database Contexts
        // =====================================================================
        public const string AppGlobalProfileTable = "APP_BS_GLOBAL_PROFILE";
        public const string ProjectProfileTable = "VMX_BS_GLOBAL_PROFILE";

        /// <summary>
        /// Determines whether the specified data service represents the Global Application Database (DrillIntelApp.sqlite).
        /// </summary>
        public static bool IsGlobalDatabase(IDataServiceDIntel? db)
        {
            if (db == null) return false;

            if (BaseDatabaseProvider.BaseDataService != null && ReferenceEquals(db, BaseDatabaseProvider.BaseDataService))
                return true;

            string path = db.DatabaseFilePath;
            if (!string.IsNullOrWhiteSpace(path))
            {
                string fileName = Path.GetFileName(path);
                string ext = Path.GetExtension(path);

                if (fileName.Equals("DrillIntelApp.sqlite", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (fileName.IndexOf("DrillIntelApp", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (fileName.IndexOf("AppDatabase", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (fileName.EndsWith("App.sqlite", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (ext.Equals(".dintel", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".drint", StringComparison.OrdinalIgnoreCase) ||
                    fileName.IndexOf("Project", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fileName.IndexOf("Proj", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return false;
                }
            }

            try
            {
                // In a project database, VMX_BS_GLOBAL_PROFILE is created as an actual table.
                if (db.TableExists(ProjectProfileTable))
                    return false;
                if (db.TableExists(AppGlobalProfileTable))
                    return true;
            }
            catch
            {
            }

            return false;
        }

        /// <summary>
        /// Gets the target table name for Broomstick Profile operations based on active database context.
        /// DrillIntelApp.sqlite uses APP_BS_GLOBAL_PROFILE; Project databases use VMX_BS_GLOBAL_PROFILE.
        /// </summary>
        public static string GetTargetTableName(IDataServiceDIntel? db, bool? isProjectDatabase = null)
        {
            if (isProjectDatabase.HasValue)
                return isProjectDatabase.Value ? ProjectProfileTable : AppGlobalProfileTable;

            return IsGlobalDatabase(db) ? AppGlobalProfileTable : ProjectProfileTable;
        }

        private static Dictionary<string, object?> BuildParameterMap(BroomstickProfile profile, string currentUserName)
        {
            return new Dictionary<string, object?>
            {
                ["@ID"] = profile.ID,
                ["@NAME"] = profile.Name,
                ["@TYPE"] = profile.Type,
                ["@NOTES"] = profile.Notes,
                ["@CREATED_BY"] = string.IsNullOrEmpty(profile.CreatedBy) ? currentUserName : profile.CreatedBy,
                ["@CREATED_DATE"] = string.IsNullOrEmpty(profile.CreatedDate) ? DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss") : profile.CreatedDate,
                ["@MODIFIED_BY"] = currentUserName,
                ["@MODIFIED_DATE"] = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss"),
                ["@DOWNSAMPLE_ON"] = profile.DownSampleOn,
                ["@DATA_POINTS"] = profile.DataPoints,
                ["@TIME_PERIOD"] = profile.TimePeriod,
                ["@GROUP_FUNC"] = profile.GroupFunctions,
                ["@SHOW_DEPTH_TRACK"] = profile.ShowDepthTrack ? 1 : 0,
                ["@TRACK_WIDTH"] = profile.TrackWidth,
                ["@FILTER_BY_RANGE"] = profile.FilterByRange ? 1 : 0,
                ["@MIN_HKLD"] = profile.MinHooklaod,
                ["@MAX_HKLD"] = profile.MaxHookload,
                ["@FILTER_BY_INTERVAL"] = profile.FilterByInterval ? 1 : 0,
                ["@DEPTH_INTERVAL"] = profile.DepthInterval,
                ["@INTERVAL_WINDOW"] = profile.IntervalWindow,
                ["@POINTS_TO_PLOT"] = profile.PointsToPlot,

                ["@PKUP_PUMP_CHANNEL"] = profile.PkupPumpChannel,
                ["@PKUP_PUMP_CUTOFF"] = profile.PkupPumpCutOff,
                ["@PKUP_RPM_CUTOFF"] = profile.PkupRPMCutOff,
                ["@PKUP_MAX_MOVEMENT"] = profile.PkupMaxMovment,
                ["@PKUP_MIN_MOVEMENT"] = profile.PkupMinMovement,
                ["@PKUP_PLOT_POINTS"] = profile.PkupPlotPoints,
                ["@PKUP_STATIC_METHOD"] = profile.PkupStaticMethod,
                ["@PKUP_DYNAMIC_METHOD"] = profile.PkupDynamicMethod,
                ["@PKUP_LOCAL_MAX"] = profile.PkupLocalMax ? 1 : 0,

                ["@SLK_PUMP_CHANNEL"] = profile.SlkPumpChannel,
                ["@SLK_PUMP_CUTOFF"] = profile.SlkPumpCutOff,
                ["@SLK_RPM_CUTOFF"] = profile.SlkRPMCutOff,
                ["@SLK_MAX_MOVEMENT"] = profile.SlkMaxMovment,
                ["@SLK_MIN_MOVEMENT"] = profile.SlkMinMovement,
                ["@SLK_PLOT_POINTS"] = profile.SlkPlotPoints,
                ["@SLK_STATIC_METHOD"] = profile.SlkStaticMethod,
                ["@SLK_DYNAMIC_METHOD"] = profile.SlkDynamicMethod,
                ["@SLK_LOCAL_MAX"] = profile.SlkLocalMax ? 1 : 0,

                ["@ROT_PUMP_CHANNEL"] = profile.RotPumpChannel,
                ["@ROT_PUMP_CUTOFF"] = profile.RotPumpCutOff,
                ["@ROT_MIN_RPM"] = profile.RotMinRPM,
                ["@ROT_MAX_RPM"] = profile.RotMaxRPM,
                ["@ROT_PLOT_POINTS"] = profile.RotPlotPoints,
                ["@ROT_CHANGE"] = profile.RotChange,
                ["@ROT_POINTS"] = profile.RotPoints,
                ["@ROT_CHECK_PUSO"] = profile.RotCheckPUSO ? 1 : 0,
                ["@TIME_THRESHOLD"] = profile.TimeThreshold,
                ["@ENFORCE_PUSO"] = profile.EnforcePUSO ? 1 : 0,

                ["@PKUP_MULTI_METHOD"] = profile.PkupMultiMethod,
                ["@SLK_MULTI_METHOD"] = profile.SlkMultiMethod,
                ["@ROB_MULTI_METHOD"] = profile.RotMultiMethod,
                ["@SHOW_MULTI"] = profile.ShowMultiple ? 1 : 0,
                ["@PKUP_RIGSTATES"] = profile.PkupRigStates,
                ["@SLK_RIGSTATES"] = profile.SlkRigStates,
                ["@ROT_RIGSTATES"] = profile.RotRigStates,
                ["@ENFORCE_RULE"] = profile.EnforceRule ? 1 : 0,
                ["@PLOT_ONBOTTOM"] = profile.PlotOnBottomTorque ? 1 : 0,

                ["@IS_DEFAULT"] = profile.IsDefault ? 1 : 0,
                ["@CASING_PKUP_MIN_MOVEMENT"] = profile.CasingPkupMinMovement,
                ["@CASING_PKUP_MAX_MOVEMENT"] = profile.CasingPkupMaxMovement,
                ["@CASING_SLK_MIN_MOVEMENT"] = profile.CasingSlkMinMovement,
                ["@CASING_SLK_MAX_MOVEMENT"] = profile.CasingSlkMaxMovement,
                ["@CASING_PKUP_RIGSTATES"] = profile.CasingPkupRigStates,
                ["@CASING_SLK_RIGSTATES"] = profile.CasingSlkRigStates,
            };
        }

        // =====================================================================
        // CRUD Operations: Save, Insert, Update, Delete, Retrieve
        // =====================================================================

        /// <summary>
        /// Saves (replaces) a profile in the active database context.
        /// DrillIntelApp.sqlite targets APP_BS_GLOBAL_PROFILE; Project databases target VMX_BS_GLOBAL_PROFILE.
        /// </summary>
        public static bool SaveProfile(IDataServiceDIntel db, BroomstickProfile profile, string currentUserName, out string lastError, bool? isProjectDatabase = null)
        {
            lastError = "";
            try
            {
                EnsureTableExists(db, isProjectDatabase);
                string tableName = GetTargetTableName(db, isProjectDatabase);

                db.ExecuteNonQuery($"DELETE FROM {tableName};");

                string insertSql = $@"
INSERT INTO {tableName} (
    ID, NAME, TYPE, NOTES, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE, DOWNSAMPLE_ON, DATA_POINTS,
    TIME_PERIOD, GROUP_FUNC, SHOW_DEPTH_TRACK, TRACK_WIDTH, FILTER_BY_RANGE, MIN_HKLD, MAX_HKLD, FILTER_BY_INTERVAL, DEPTH_INTERVAL, INTERVAL_WINDOW,
    POINTS_TO_PLOT, PKUP_PUMP_CHANNEL, PKUP_PUMP_CUTOFF, PKUP_RPM_CUTOFF, PKUP_MAX_MOVEMENT, PKUP_MIN_MOVEMENT, PKUP_PLOT_POINTS, PKUP_STATIC_METHOD,
    PKUP_DYNAMIC_METHOD, PKUP_LOCAL_MAX, SLK_PUMP_CHANNEL, SLK_PUMP_CUTOFF, SLK_RPM_CUTOFF, SLK_MAX_MOVEMENT, SLK_MIN_MOVEMENT, SLK_PLOT_POINTS, SLK_STATIC_METHOD,
    SLK_DYNAMIC_METHOD, SLK_LOCAL_MAX, ROT_PUMP_CHANNEL, ROT_PUMP_CUTOFF, ROT_MIN_RPM, ROT_MAX_RPM, ROT_PLOT_POINTS, ROT_CHANGE, ROT_POINTS, ROT_CHECK_PUSO,
    TIME_THRESHOLD, ENFORCE_PUSO, PKUP_MULTI_METHOD, SLK_MULTI_METHOD, ROB_MULTI_METHOD, SHOW_MULTI, PKUP_RIGSTATES, SLK_RIGSTATES, ROT_RIGSTATES,
    ENFORCE_RULE, PLOT_ONBOTTOM, IS_DEFAULT, CASING_PKUP_MIN_MOVEMENT, CASING_PKUP_MAX_MOVEMENT, CASING_SLK_MIN_MOVEMENT, CASING_SLK_MAX_MOVEMENT,
    CASING_PKUP_RIGSTATES, CASING_SLK_RIGSTATES
) VALUES (
    @ID, @NAME, @TYPE, @NOTES, @CREATED_BY, @CREATED_DATE, @MODIFIED_BY, @MODIFIED_DATE, @DOWNSAMPLE_ON, @DATA_POINTS,
    @TIME_PERIOD, @GROUP_FUNC, @SHOW_DEPTH_TRACK, @TRACK_WIDTH, @FILTER_BY_RANGE, @MIN_HKLD, @MAX_HKLD, @FILTER_BY_INTERVAL, @DEPTH_INTERVAL, @INTERVAL_WINDOW,
    @POINTS_TO_PLOT, @PKUP_PUMP_CHANNEL, @PKUP_PUMP_CUTOFF, @PKUP_RPM_CUTOFF, @PKUP_MAX_MOVEMENT, @PKUP_MIN_MOVEMENT, @PKUP_PLOT_POINTS, @PKUP_STATIC_METHOD,
    @PKUP_DYNAMIC_METHOD, @PKUP_LOCAL_MAX, @SLK_PUMP_CHANNEL, @SLK_PUMP_CUTOFF, @SLK_RPM_CUTOFF, @SLK_MAX_MOVEMENT, @SLK_MIN_MOVEMENT, @SLK_PLOT_POINTS, @SLK_STATIC_METHOD,
    @SLK_DYNAMIC_METHOD, @SLK_LOCAL_MAX, @ROT_PUMP_CHANNEL, @ROT_PUMP_CUTOFF, @ROT_MIN_RPM, @ROT_MAX_RPM, @ROT_PLOT_POINTS, @ROT_CHANGE, @ROT_POINTS, @ROT_CHECK_PUSO,
    @TIME_THRESHOLD, @ENFORCE_PUSO, @PKUP_MULTI_METHOD, @SLK_MULTI_METHOD, @ROB_MULTI_METHOD, @SHOW_MULTI, @PKUP_RIGSTATES, @SLK_RIGSTATES, @ROT_RIGSTATES,
    @ENFORCE_RULE, @PLOT_ONBOTTOM, @IS_DEFAULT, @CASING_PKUP_MIN_MOVEMENT, @CASING_PKUP_MAX_MOVEMENT, @CASING_SLK_MIN_MOVEMENT, @CASING_SLK_MAX_MOVEMENT,
    @CASING_PKUP_RIGSTATES, @CASING_SLK_RIGSTATES
)";

                var p = BuildParameterMap(profile, currentUserName);
                if (db.ExecuteNonQuery(insertSql, p))
                {
                    return true;
                }

                lastError = db.LastError;
                return false;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ex.StackTrace;
                return false;
            }
        }

        /// <summary>
        /// Inserts a new profile record into the target table for the active database context.
        /// </summary>
        public static bool InsertProfile(IDataServiceDIntel db, BroomstickProfile profile, string currentUserName, out string lastError, bool? isProjectDatabase = null)
        {
            lastError = "";
            try
            {
                EnsureTableExists(db, isProjectDatabase);
                string tableName = GetTargetTableName(db, isProjectDatabase);

                string insertSql = $@"
INSERT INTO {tableName} (
    ID, NAME, TYPE, NOTES, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE, DOWNSAMPLE_ON, DATA_POINTS,
    TIME_PERIOD, GROUP_FUNC, SHOW_DEPTH_TRACK, TRACK_WIDTH, FILTER_BY_RANGE, MIN_HKLD, MAX_HKLD, FILTER_BY_INTERVAL, DEPTH_INTERVAL, INTERVAL_WINDOW,
    POINTS_TO_PLOT, PKUP_PUMP_CHANNEL, PKUP_PUMP_CUTOFF, PKUP_RPM_CUTOFF, PKUP_MAX_MOVEMENT, PKUP_MIN_MOVEMENT, PKUP_PLOT_POINTS, PKUP_STATIC_METHOD,
    PKUP_DYNAMIC_METHOD, PKUP_LOCAL_MAX, SLK_PUMP_CHANNEL, SLK_PUMP_CUTOFF, SLK_RPM_CUTOFF, SLK_MAX_MOVEMENT, SLK_MIN_MOVEMENT, SLK_PLOT_POINTS, SLK_STATIC_METHOD,
    SLK_DYNAMIC_METHOD, SLK_LOCAL_MAX, ROT_PUMP_CHANNEL, ROT_PUMP_CUTOFF, ROT_MIN_RPM, ROT_MAX_RPM, ROT_PLOT_POINTS, ROT_CHANGE, ROT_POINTS, ROT_CHECK_PUSO,
    TIME_THRESHOLD, ENFORCE_PUSO, PKUP_MULTI_METHOD, SLK_MULTI_METHOD, ROB_MULTI_METHOD, SHOW_MULTI, PKUP_RIGSTATES, SLK_RIGSTATES, ROT_RIGSTATES,
    ENFORCE_RULE, PLOT_ONBOTTOM, IS_DEFAULT, CASING_PKUP_MIN_MOVEMENT, CASING_PKUP_MAX_MOVEMENT, CASING_SLK_MIN_MOVEMENT, CASING_SLK_MAX_MOVEMENT,
    CASING_PKUP_RIGSTATES, CASING_SLK_RIGSTATES
) VALUES (
    @ID, @NAME, @TYPE, @NOTES, @CREATED_BY, @CREATED_DATE, @MODIFIED_BY, @MODIFIED_DATE, @DOWNSAMPLE_ON, @DATA_POINTS,
    @TIME_PERIOD, @GROUP_FUNC, @SHOW_DEPTH_TRACK, @TRACK_WIDTH, @FILTER_BY_RANGE, @MIN_HKLD, @MAX_HKLD, @FILTER_BY_INTERVAL, @DEPTH_INTERVAL, @INTERVAL_WINDOW,
    @POINTS_TO_PLOT, @PKUP_PUMP_CHANNEL, @PKUP_PUMP_CUTOFF, @PKUP_RPM_CUTOFF, @PKUP_MAX_MOVEMENT, @PKUP_MIN_MOVEMENT, @PKUP_PLOT_POINTS, @PKUP_STATIC_METHOD,
    @PKUP_DYNAMIC_METHOD, @PKUP_LOCAL_MAX, @SLK_PUMP_CHANNEL, @SLK_PUMP_CUTOFF, @SLK_RPM_CUTOFF, @SLK_MAX_MOVEMENT, @SLK_MIN_MOVEMENT, @SLK_PLOT_POINTS, @SLK_STATIC_METHOD,
    @SLK_DYNAMIC_METHOD, @SLK_LOCAL_MAX, @ROT_PUMP_CHANNEL, @ROT_PUMP_CUTOFF, @ROT_MIN_RPM, @ROT_MAX_RPM, @ROT_PLOT_POINTS, @ROT_CHANGE, @ROT_POINTS, @ROT_CHECK_PUSO,
    @TIME_THRESHOLD, @ENFORCE_PUSO, @PKUP_MULTI_METHOD, @SLK_MULTI_METHOD, @ROB_MULTI_METHOD, @SHOW_MULTI, @PKUP_RIGSTATES, @SLK_RIGSTATES, @ROT_RIGSTATES,
    @ENFORCE_RULE, @PLOT_ONBOTTOM, @IS_DEFAULT, @CASING_PKUP_MIN_MOVEMENT, @CASING_PKUP_MAX_MOVEMENT, @CASING_SLK_MIN_MOVEMENT, @CASING_SLK_MAX_MOVEMENT,
    @CASING_PKUP_RIGSTATES, @CASING_SLK_RIGSTATES
)";

                var p = BuildParameterMap(profile, currentUserName);
                if (db.ExecuteNonQuery(insertSql, p))
                {
                    return true;
                }

                lastError = db.LastError;
                return false;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ex.StackTrace;
                return false;
            }
        }

        /// <summary>
        /// Updates an existing profile record in the target table for the active database context.
        /// </summary>
        public static bool UpdateProfile(IDataServiceDIntel db, BroomstickProfile profile, string currentUserName, out string lastError, bool? isProjectDatabase = null)
        {
            lastError = "";
            try
            {
                EnsureTableExists(db, isProjectDatabase);
                string tableName = GetTargetTableName(db, isProjectDatabase);

                string updateSql = $@"
UPDATE {tableName} SET
    NAME = @NAME,
    TYPE = @TYPE,
    NOTES = @NOTES,
    MODIFIED_BY = @MODIFIED_BY,
    MODIFIED_DATE = @MODIFIED_DATE,
    DOWNSAMPLE_ON = @DOWNSAMPLE_ON,
    DATA_POINTS = @DATA_POINTS,
    TIME_PERIOD = @TIME_PERIOD,
    GROUP_FUNC = @GROUP_FUNC,
    SHOW_DEPTH_TRACK = @SHOW_DEPTH_TRACK,
    TRACK_WIDTH = @TRACK_WIDTH,
    FILTER_BY_RANGE = @FILTER_BY_RANGE,
    MIN_HKLD = @MIN_HKLD,
    MAX_HKLD = @MAX_HKLD,
    FILTER_BY_INTERVAL = @FILTER_BY_INTERVAL,
    DEPTH_INTERVAL = @DEPTH_INTERVAL,
    INTERVAL_WINDOW = @INTERVAL_WINDOW,
    POINTS_TO_PLOT = @POINTS_TO_PLOT,
    PKUP_PUMP_CHANNEL = @PKUP_PUMP_CHANNEL,
    PKUP_PUMP_CUTOFF = @PKUP_PUMP_CUTOFF,
    PKUP_RPM_CUTOFF = @PKUP_RPM_CUTOFF,
    PKUP_MAX_MOVEMENT = @PKUP_MAX_MOVEMENT,
    PKUP_MIN_MOVEMENT = @PKUP_MIN_MOVEMENT,
    PKUP_PLOT_POINTS = @PKUP_PLOT_POINTS,
    PKUP_STATIC_METHOD = @PKUP_STATIC_METHOD,
    PKUP_DYNAMIC_METHOD = @PKUP_DYNAMIC_METHOD,
    PKUP_LOCAL_MAX = @PKUP_LOCAL_MAX,
    SLK_PUMP_CHANNEL = @SLK_PUMP_CHANNEL,
    SLK_PUMP_CUTOFF = @SLK_PUMP_CUTOFF,
    SLK_RPM_CUTOFF = @SLK_RPM_CUTOFF,
    SLK_MAX_MOVEMENT = @SLK_MAX_MOVEMENT,
    SLK_MIN_MOVEMENT = @SLK_MIN_MOVEMENT,
    SLK_PLOT_POINTS = @SLK_PLOT_POINTS,
    SLK_STATIC_METHOD = @SLK_STATIC_METHOD,
    SLK_DYNAMIC_METHOD = @SLK_DYNAMIC_METHOD,
    SLK_LOCAL_MAX = @SLK_LOCAL_MAX,
    ROT_PUMP_CHANNEL = @ROT_PUMP_CHANNEL,
    ROT_PUMP_CUTOFF = @ROT_PUMP_CUTOFF,
    ROT_MIN_RPM = @ROT_MIN_RPM,
    ROT_MAX_RPM = @ROT_MAX_RPM,
    ROT_PLOT_POINTS = @ROT_PLOT_POINTS,
    ROT_CHANGE = @ROT_CHANGE,
    ROT_POINTS = @ROT_POINTS,
    ROT_CHECK_PUSO = @ROT_CHECK_PUSO,
    TIME_THRESHOLD = @TIME_THRESHOLD,
    ENFORCE_PUSO = @ENFORCE_PUSO,
    PKUP_MULTI_METHOD = @PKUP_MULTI_METHOD,
    SLK_MULTI_METHOD = @SLK_MULTI_METHOD,
    ROB_MULTI_METHOD = @ROB_MULTI_METHOD,
    SHOW_MULTI = @SHOW_MULTI,
    PKUP_RIGSTATES = @PKUP_RIGSTATES,
    SLK_RIGSTATES = @SLK_RIGSTATES,
    ROT_RIGSTATES = @ROT_RIGSTATES,
    ENFORCE_RULE = @ENFORCE_RULE,
    PLOT_ONBOTTOM = @PLOT_ONBOTTOM,
    IS_DEFAULT = @IS_DEFAULT,
    CASING_PKUP_MIN_MOVEMENT = @CASING_PKUP_MIN_MOVEMENT,
    CASING_PKUP_MAX_MOVEMENT = @CASING_PKUP_MAX_MOVEMENT,
    CASING_SLK_MIN_MOVEMENT = @CASING_SLK_MIN_MOVEMENT,
    CASING_SLK_MAX_MOVEMENT = @CASING_SLK_MAX_MOVEMENT,
    CASING_PKUP_RIGSTATES = @CASING_PKUP_RIGSTATES,
    CASING_SLK_RIGSTATES = @CASING_SLK_RIGSTATES
WHERE ID = @ID;";

                var p = BuildParameterMap(profile, currentUserName);
                if (db.ExecuteNonQuery(updateSql, p))
                {
                    return true;
                }

                lastError = db.LastError;
                return false;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ex.StackTrace;
                return false;
            }
        }

        /// <summary>
        /// Deletes a specific profile from the target table for the active database context.
        /// </summary>
        public static bool DeleteProfile(IDataServiceDIntel db, string profileID, out string lastError, bool? isProjectDatabase = null)
        {
            lastError = "";
            try
            {
                EnsureTableExists(db, isProjectDatabase);
                string tableName = GetTargetTableName(db, isProjectDatabase);
                return db.ExecuteNonQuery(
                    $"DELETE FROM {tableName} WHERE ID = @ID;",
                    new Dictionary<string, object?> { ["@ID"] = profileID });
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ex.StackTrace;
                return false;
            }
        }

        /// <summary>
        /// Deletes all profile entries from the target table for the active database context.
        /// </summary>
        public static bool DeleteAllProfiles(IDataServiceDIntel db, out string lastError, bool? isProjectDatabase = null)
        {
            lastError = "";
            try
            {
                EnsureTableExists(db, isProjectDatabase);
                string tableName = GetTargetTableName(db, isProjectDatabase);
                return db.ExecuteNonQuery($"DELETE FROM {tableName};");
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ex.StackTrace;
                return false;
            }
        }

        /// <summary>
        /// Loads a profile by ID from the active database context.
        /// DrillIntelApp.sqlite loads from APP_BS_GLOBAL_PROFILE; Project databases load from VMX_BS_GLOBAL_PROFILE.
        /// </summary>
        public static BroomstickProfile? LoadProfile(IDataServiceDIntel db, string profileID, out string lastError, bool? isProjectDatabase = null)
        {
            lastError = "";
            try
            {
                EnsureTableExists(db, isProjectDatabase);
                string tableName = GetTargetTableName(db, isProjectDatabase);
                DataTable data = db.GetTable(
                    $"SELECT * FROM {tableName} WHERE ID = @ID",
                    new Dictionary<string, object?> { ["@ID"] = profileID });

                if (data.Rows.Count > 0)
                {
                    return MapFromRow(data.Rows[0]);
                }

                lastError = "Profile not found in the database";
                return null;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ex.StackTrace;
                return null;
            }
        }

        public static BroomstickProfile? loadProfile(IDataServiceDIntel db, string profileID, ref string lastError, bool? isProjectDatabase = null)
        {
            return LoadProfile(db, profileID, out lastError, isProjectDatabase);
        }

        /// <summary>
        /// Retrieves the BroomstickProfile associated with the project from VMX_BS_GLOBAL_PROFILE.
        /// (Note: VMX_BS_WELL_PROFILE is not used; project settings are stored in VMX_BS_GLOBAL_PROFILE).
        /// </summary>
        public static BroomstickProfile? getWellBSProfile(IDataServiceDIntel db, string wellID)
        {
            try
            {
                if (db == null) return null;
                return LoadSingleProfile(db, out _, isProjectDatabase: true);
            }
            catch
            {
                return null;
            }
        }

        public static BroomstickProfile? GetWellBSProfile(IDataServiceDIntel db, string wellID) => getWellBSProfile(db, wellID);

        public static BroomstickProfile MapFromRow(DataRow row)
        {
            return new BroomstickProfile
            {
                ID = (string)DataServiceDIntel.CheckNull(row["ID"], ""),
                Name = (string)DataServiceDIntel.CheckNull(row["NAME"], ""),
                Type = Convert.ToInt32(DataServiceDIntel.CheckNull(row["TYPE"], 0)),
                Notes = (string)DataServiceDIntel.CheckNull(row["NOTES"], ""),
                DownSampleOn = Convert.ToInt32(DataServiceDIntel.CheckNull(row["DOWNSAMPLE_ON"], 0)),
                DataPoints = Convert.ToInt32(DataServiceDIntel.CheckNull(row["DATA_POINTS"], 0)),
                TimePeriod = Convert.ToInt32(DataServiceDIntel.CheckNull(row["TIME_PERIOD"], 0)),
                GroupFunctions = (string)DataServiceDIntel.CheckNull(row["GROUP_FUNC"], ""),
                ShowDepthTrack = Convert.ToInt32(DataServiceDIntel.CheckNull(row["SHOW_DEPTH_TRACK"], 0)) == 1,
                TrackWidth = Convert.ToInt32(DataServiceDIntel.CheckNull(row["TRACK_WIDTH"], 0)),
                FilterByRange = Convert.ToInt32(DataServiceDIntel.CheckNull(row["FILTER_BY_RANGE"], 0)) == 1,
                MinHooklaod = Convert.ToDouble(DataServiceDIntel.CheckNull(row["MIN_HKLD"], 0)),
                MaxHookload = Convert.ToDouble(DataServiceDIntel.CheckNull(row["MAX_HKLD"], 0)),
                FilterByInterval = Convert.ToInt32(DataServiceDIntel.CheckNull(row["FILTER_BY_INTERVAL"], 0)) == 1,
                DepthInterval = Convert.ToDouble(DataServiceDIntel.CheckNull(row["DEPTH_INTERVAL"], 0)),
                IntervalWindow = Convert.ToDouble(DataServiceDIntel.CheckNull(row["INTERVAL_WINDOW"], 0)),
                PointsToPlot = Convert.ToInt32(DataServiceDIntel.CheckNull(row["POINTS_TO_PLOT"], 0)),

                PkupPumpChannel = (string)DataServiceDIntel.CheckNull(row["PKUP_PUMP_CHANNEL"], ""),
                PkupPumpCutOff = Convert.ToDouble(DataServiceDIntel.CheckNull(row["PKUP_PUMP_CUTOFF"], 0)),
                PkupRPMCutOff = Convert.ToDouble(DataServiceDIntel.CheckNull(row["PKUP_RPM_CUTOFF"], 0)),
                PkupMaxMovment = Convert.ToDouble(DataServiceDIntel.CheckNull(row["PKUP_MAX_MOVEMENT"], 0)),
                PkupMinMovement = Convert.ToDouble(DataServiceDIntel.CheckNull(row["PKUP_MIN_MOVEMENT"], 0)),
                PkupPlotPoints = Convert.ToInt32(DataServiceDIntel.CheckNull(row["PKUP_PLOT_POINTS"], 0)),
                PkupStaticMethod = Convert.ToInt32(DataServiceDIntel.CheckNull(row["PKUP_STATIC_METHOD"], 0)),
                PkupDynamicMethod = Convert.ToInt32(DataServiceDIntel.CheckNull(row["PKUP_DYNAMIC_METHOD"], 0)),
                PkupLocalMax = Convert.ToInt32(DataServiceDIntel.CheckNull(row["PKUP_LOCAL_MAX"], 0)) == 1,

                SlkPumpChannel = (string)DataServiceDIntel.CheckNull(row["SLK_PUMP_CHANNEL"], ""),
                SlkPumpCutOff = Convert.ToDouble(DataServiceDIntel.CheckNull(row["SLK_PUMP_CUTOFF"], 0)),
                SlkRPMCutOff = Convert.ToDouble(DataServiceDIntel.CheckNull(row["SLK_RPM_CUTOFF"], 0)),
                SlkMaxMovment = Convert.ToDouble(DataServiceDIntel.CheckNull(row["SLK_MAX_MOVEMENT"], 0)),
                SlkMinMovement = Convert.ToDouble(DataServiceDIntel.CheckNull(row["SLK_MIN_MOVEMENT"], 0)),
                SlkPlotPoints = Convert.ToInt32(DataServiceDIntel.CheckNull(row["SLK_PLOT_POINTS"], 0)),
                SlkStaticMethod = Convert.ToInt32(DataServiceDIntel.CheckNull(row["SLK_STATIC_METHOD"], 0)),
                SlkDynamicMethod = Convert.ToInt32(DataServiceDIntel.CheckNull(row["SLK_DYNAMIC_METHOD"], 0)),
                SlkLocalMax = Convert.ToInt32(DataServiceDIntel.CheckNull(row["SLK_LOCAL_MAX"], 0)) == 1,

                RotPumpChannel = (string)DataServiceDIntel.CheckNull(row["ROT_PUMP_CHANNEL"], ""),
                RotPumpCutOff = Convert.ToDouble(DataServiceDIntel.CheckNull(row["ROT_PUMP_CUTOFF"], 0)),
                RotMinRPM = Convert.ToDouble(DataServiceDIntel.CheckNull(row["ROT_MIN_RPM"], 0)),
                RotMaxRPM = Convert.ToDouble(DataServiceDIntel.CheckNull(row["ROT_MAX_RPM"], 0)),
                RotPlotPoints = Convert.ToInt32(DataServiceDIntel.CheckNull(row["ROT_PLOT_POINTS"], 0)),
                RotChange = Convert.ToDouble(DataServiceDIntel.CheckNull(row["ROT_CHANGE"], 0)),
                RotPoints = Convert.ToDouble(DataServiceDIntel.CheckNull(row["ROT_POINTS"], 0)),
                RotCheckPUSO = Convert.ToInt32(DataServiceDIntel.CheckNull(row["ROT_CHECK_PUSO"], 0)) == 1,
                TimeThreshold = Convert.ToInt32(DataServiceDIntel.CheckNull(row["TIME_THRESHOLD"], 0)),
                EnforcePUSO = Convert.ToInt32(DataServiceDIntel.CheckNull(row["ENFORCE_PUSO"], 0)) == 1,

                PkupMultiMethod = Convert.ToInt32(DataServiceDIntel.CheckNull(row["PKUP_MULTI_METHOD"], 0)),
                SlkMultiMethod = Convert.ToInt32(DataServiceDIntel.CheckNull(row["SLK_MULTI_METHOD"], 0)),
                RotMultiMethod = Convert.ToInt32(DataServiceDIntel.CheckNull(row["ROB_MULTI_METHOD"], 0)),

                ShowMultiple = Convert.ToInt32(DataServiceDIntel.CheckNull(row["SHOW_MULTI"], 0)) == 1,

                PkupRigStates = (string)DataServiceDIntel.CheckNull(row["PKUP_RIGSTATES"], ""),
                SlkRigStates = (string)DataServiceDIntel.CheckNull(row["SLK_RIGSTATES"], ""),
                RotRigStates = (string)DataServiceDIntel.CheckNull(row["ROT_RIGSTATES"], ""),
                EnforceRule = Convert.ToInt32(DataServiceDIntel.CheckNull(row["ENFORCE_RULE"], 0)) == 1,
                PlotOnBottomTorque = Convert.ToInt32(DataServiceDIntel.CheckNull(row["PLOT_ONBOTTOM"], 0)) == 1,

                IsDefault = row.Table.Columns.Contains("IS_DEFAULT") && Convert.ToInt32(DataServiceDIntel.CheckNull(row["IS_DEFAULT"], 0)) == 1,
                CreatedBy = row.Table.Columns.Contains("CREATED_BY") ? (string)DataServiceDIntel.CheckNull(row["CREATED_BY"], "") : "",
                CreatedDate = row.Table.Columns.Contains("CREATED_DATE") ? (string)DataServiceDIntel.CheckNull(row["CREATED_DATE"], "") : "",
                ModifiedBy = row.Table.Columns.Contains("MODIFIED_BY") ? (string)DataServiceDIntel.CheckNull(row["MODIFIED_BY"], "") : "",
                ModifiedDate = row.Table.Columns.Contains("MODIFIED_DATE") ? (string)DataServiceDIntel.CheckNull(row["MODIFIED_DATE"], "") : "",
                CasingPkupMinMovement = row.Table.Columns.Contains("CASING_PKUP_MIN_MOVEMENT") ? Convert.ToDouble(DataServiceDIntel.CheckNull(row["CASING_PKUP_MIN_MOVEMENT"], 0)) : 0,
                CasingPkupMaxMovement = row.Table.Columns.Contains("CASING_PKUP_MAX_MOVEMENT") ? Convert.ToDouble(DataServiceDIntel.CheckNull(row["CASING_PKUP_MAX_MOVEMENT"], 0)) : 0,
                CasingSlkMinMovement = row.Table.Columns.Contains("CASING_SLK_MIN_MOVEMENT") ? Convert.ToDouble(DataServiceDIntel.CheckNull(row["CASING_SLK_MIN_MOVEMENT"], 0)) : 0,
                CasingSlkMaxMovement = row.Table.Columns.Contains("CASING_SLK_MAX_MOVEMENT") ? Convert.ToDouble(DataServiceDIntel.CheckNull(row["CASING_SLK_MAX_MOVEMENT"], 0)) : 0,
                CasingPkupRigStates = row.Table.Columns.Contains("CASING_PKUP_RIGSTATES") ? (string)DataServiceDIntel.CheckNull(row["CASING_PKUP_RIGSTATES"], "") : "",
                CasingSlkRigStates = row.Table.Columns.Contains("CASING_SLK_RIGSTATES") ? (string)DataServiceDIntel.CheckNull(row["CASING_SLK_RIGSTATES"], "") : "",
            };
        }

        /// <summary>
        /// In DrillIntel, BroomstickProfile is a single entry configuration per database.
        /// Loads the single profile from the active database context (APP_BS_GLOBAL_PROFILE for App DB; VMX_BS_GLOBAL_PROFILE for Project DB).
        /// If no row exists, creates and persists default profile.
        /// </summary>
        public static BroomstickProfile LoadSingleProfile(IDataServiceDIntel db, out string lastError, bool? isProjectDatabase = null)
        {
            lastError = "";
            try
            {
                EnsureTableExists(db, isProjectDatabase);
                string tableName = GetTargetTableName(db, isProjectDatabase);
                DataTable data = db.GetTable($"SELECT * FROM {tableName} LIMIT 1");
                if (data != null && data.Rows.Count > 0)
                {
                    return MapFromRow(data.Rows[0]);
                }

                // If no profile exists, create and persist default single entry
                var defaultProfile = CreateDefault("Default Profile");
                SaveProfile(db, defaultProfile, "SYSTEM", out lastError, isProjectDatabase);
                return defaultProfile;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ex.StackTrace;
                return CreateDefault();
            }
        }

        /// <summary>
        /// Loads all profiles from the active database context.
        /// </summary>
        public static List<BroomstickProfile> LoadAllProfiles(IDataServiceDIntel db, out string lastError, bool? isProjectDatabase = null)
        {
            lastError = "";
            var list = new List<BroomstickProfile>();
            try
            {
                EnsureTableExists(db, isProjectDatabase);
                string tableName = GetTargetTableName(db, isProjectDatabase);
                DataTable data = db.GetTable($"SELECT * FROM {tableName} ORDER BY IS_DEFAULT DESC, NAME ASC");
                if (data != null && data.Rows.Count > 0)
                {
                    foreach (DataRow row in data.Rows)
                    {
                        list.Add(MapFromRow(row));
                    }
                }
                return list;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ex.StackTrace;
                return list;
            }
        }

        /// <summary>
        /// Gets the default profile from the active database context.
        /// </summary>
        public static BroomstickProfile? GetDefaultProfile(IDataServiceDIntel db, out string lastError, bool? isProjectDatabase = null)
        {
            lastError = "";
            try
            {
                EnsureTableExists(db, isProjectDatabase);
                string tableName = GetTargetTableName(db, isProjectDatabase);
                DataTable data = db.GetTable($"SELECT * FROM {tableName} WHERE IS_DEFAULT = 1 LIMIT 1");
                if (data != null && data.Rows.Count > 0)
                {
                    return MapFromRow(data.Rows[0]);
                }

                // Fallback to first profile if no default explicitly marked
                DataTable allData = db.GetTable($"SELECT * FROM {tableName} LIMIT 1");
                if (allData != null && allData.Rows.Count > 0)
                {
                    return MapFromRow(allData.Rows[0]);
                }

                return null;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ex.StackTrace;
                return null;
            }
        }

        public static BroomstickProfile CreateDefault(string name = "Default Profile")
        {
            return new BroomstickProfile
            {
                ID = Guid.NewGuid().ToString(),
                Name = name,
                Type = 0,
                Notes = "eVumaxPro Calibrated Broomstick Global Profile",
                DownSampleOn = 0,
                DataPoints = 100,
                TimePeriod = 0,
                GroupFunctions = "MIN,MAX,AVG",
                ShowDepthTrack = false,
                TrackWidth = 100,
                FilterByRange = true,
                MinHooklaod = 1.0,
                MaxHookload = 300.0,
                FilterByInterval = false,
                DepthInterval = 500.0,
                IntervalWindow = 10.0,

                PointsToPlot = 0,
                PkupPumpChannel = "SPPA",
                PkupPumpCutOff = 99,
                PkupRPMCutOff = 12,
                PkupMaxMovment = 70,
                PkupMinMovement = 5,
                PkupPlotPoints = 0,
                PkupStaticMethod = 0,
                PkupDynamicMethod = 0,
                PkupLocalMax = false,

                SlkPumpChannel = "SPPA",
                SlkPumpCutOff = 99,
                SlkRPMCutOff = 12,
                SlkMaxMovment = 70,
                SlkMinMovement = 5,
                SlkPlotPoints = 0,
                SlkStaticMethod = 0,
                SlkDynamicMethod = 0,
                SlkLocalMax = false,

                RotPumpChannel = "SPPA",
                RotPumpCutOff = 99,
                RotMinRPM = 12,
                RotMaxRPM = 30,
                RotPlotPoints = 0,
                RotChange = 1,
                RotPoints = 1,
                RotCheckPUSO = false,
                TimeThreshold = 1,
                EnforcePUSO = false,

                PkupMultiMethod = 0,
                SlkMultiMethod = 0,
                RotMultiMethod = 0,
                ShowMultiple = false,

                PkupRigStates = "",
                SlkRigStates = "",
                RotRigStates = "",
                EnforceRule = false,
                PlotOnBottomTorque = false,

                IsDefault = true,
                CreatedBy = "SYSTEM",
                CreatedDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss"),
                ModifiedBy = "SYSTEM",
                ModifiedDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss")
            };
        }

        // =====================================================================
        // DDL Schema Definitions: Global vs Project Level
        // =====================================================================

        /// <summary>
        /// DDL for Global Application Database (DrillIntelApp.sqlite).
        /// Physical table is APP_BS_GLOBAL_PROFILE; view is VMX_BS_GLOBAL_PROFILE.
        /// </summary>
        public const string CreateAppTableSql = @"
CREATE TABLE IF NOT EXISTS APP_BS_GLOBAL_PROFILE (
    ID                          TEXT NOT NULL PRIMARY KEY,
    NAME                        TEXT NOT NULL,
    TYPE                        INTEGER NOT NULL,
    NOTES                       TEXT,
    CREATED_BY                  TEXT,
    CREATED_DATE                TEXT,
    MODIFIED_BY                 TEXT,
    MODIFIED_DATE               TEXT,
    DOWNSAMPLE_ON               INTEGER,
    DATA_POINTS                 INTEGER,
    TIME_PERIOD                 INTEGER,
    GROUP_FUNC                  TEXT,
    SHOW_DEPTH_TRACK            INTEGER,
    TRACK_WIDTH                 INTEGER,
    FILTER_BY_RANGE             INTEGER,
    MIN_HKLD                    REAL,
    MAX_HKLD                    REAL,
    FILTER_BY_INTERVAL          INTEGER,
    DEPTH_INTERVAL              REAL,
    INTERVAL_WINDOW             REAL,
    POINTS_TO_PLOT              INTEGER,
    PKUP_PUMP_CHANNEL           TEXT,
    PKUP_PUMP_CUTOFF            REAL,
    PKUP_RPM_CUTOFF             REAL,
    PKUP_MAX_MOVEMENT           REAL,
    PKUP_MIN_MOVEMENT           REAL,
    PKUP_PLOT_POINTS            INTEGER,
    PKUP_STATIC_METHOD          INTEGER,
    PKUP_DYNAMIC_METHOD         INTEGER,
    PKUP_LOCAL_MAX              INTEGER,
    SLK_PUMP_CHANNEL            TEXT,
    SLK_PUMP_CUTOFF             REAL,
    SLK_RPM_CUTOFF              REAL,
    SLK_MAX_MOVEMENT            REAL,
    SLK_MIN_MOVEMENT            REAL,
    SLK_PLOT_POINTS             INTEGER,
    SLK_STATIC_METHOD           INTEGER,
    SLK_DYNAMIC_METHOD          INTEGER,
    SLK_LOCAL_MAX               INTEGER,
    ROT_PUMP_CHANNEL            TEXT,
    ROT_PUMP_CUTOFF             REAL,
    ROT_MIN_RPM                 REAL,
    ROT_MAX_RPM                 REAL,
    ROT_PLOT_POINTS             INTEGER,
    ROT_CHANGE                  REAL,
    ROT_POINTS                  REAL,
    ROT_CHECK_PUSO              INTEGER,
    TIME_THRESHOLD              REAL,
    ENFORCE_PUSO                INTEGER,
    PKUP_MULTI_METHOD           INTEGER,
    SLK_MULTI_METHOD            INTEGER,
    ROB_MULTI_METHOD            INTEGER,
    SHOW_MULTI                  INTEGER,
    PKUP_RIGSTATES              TEXT,
    SLK_RIGSTATES               TEXT,
    ROT_RIGSTATES               TEXT,
    ENFORCE_RULE                INTEGER,
    PLOT_ONBOTTOM               INTEGER,
    IS_DEFAULT                  INTEGER,
    CASING_PKUP_MIN_MOVEMENT    REAL,
    CASING_PKUP_MAX_MOVEMENT    REAL,
    CASING_SLK_MIN_MOVEMENT     REAL,
    CASING_SLK_MAX_MOVEMENT     REAL,
    CASING_PKUP_RIGSTATES       TEXT,
    CASING_SLK_RIGSTATES        TEXT
);
CREATE VIEW IF NOT EXISTS VMX_BS_GLOBAL_PROFILE AS SELECT * FROM APP_BS_GLOBAL_PROFILE;
";

        /// <summary>
        /// DDL for Project-level database (.dintel / .sqlite).
        /// Physical table is VMX_BS_GLOBAL_PROFILE; view is APP_BS_GLOBAL_PROFILE.
        /// </summary>
        public const string CreateProjectTableSql = @"
CREATE TABLE IF NOT EXISTS VMX_BS_GLOBAL_PROFILE (
    ID                          TEXT NOT NULL PRIMARY KEY,
    NAME                        TEXT NOT NULL,
    TYPE                        INTEGER NOT NULL,
    NOTES                       TEXT,
    CREATED_BY                  TEXT,
    CREATED_DATE                TEXT,
    MODIFIED_BY                 TEXT,
    MODIFIED_DATE               TEXT,
    DOWNSAMPLE_ON               INTEGER,
    DATA_POINTS                 INTEGER,
    TIME_PERIOD                 INTEGER,
    GROUP_FUNC                  TEXT,
    SHOW_DEPTH_TRACK            INTEGER,
    TRACK_WIDTH                 INTEGER,
    FILTER_BY_RANGE             INTEGER,
    MIN_HKLD                    REAL,
    MAX_HKLD                    REAL,
    FILTER_BY_INTERVAL          INTEGER,
    DEPTH_INTERVAL              REAL,
    INTERVAL_WINDOW             REAL,
    POINTS_TO_PLOT              INTEGER,
    PKUP_PUMP_CHANNEL           TEXT,
    PKUP_PUMP_CUTOFF            REAL,
    PKUP_RPM_CUTOFF             REAL,
    PKUP_MAX_MOVEMENT           REAL,
    PKUP_MIN_MOVEMENT           REAL,
    PKUP_PLOT_POINTS            INTEGER,
    PKUP_STATIC_METHOD          INTEGER,
    PKUP_DYNAMIC_METHOD         INTEGER,
    PKUP_LOCAL_MAX              INTEGER,
    SLK_PUMP_CHANNEL            TEXT,
    SLK_PUMP_CUTOFF             REAL,
    SLK_RPM_CUTOFF              REAL,
    SLK_MAX_MOVEMENT            REAL,
    SLK_MIN_MOVEMENT            REAL,
    SLK_PLOT_POINTS             INTEGER,
    SLK_STATIC_METHOD           INTEGER,
    SLK_DYNAMIC_METHOD          INTEGER,
    SLK_LOCAL_MAX               INTEGER,
    ROT_PUMP_CHANNEL            TEXT,
    ROT_PUMP_CUTOFF             REAL,
    ROT_MIN_RPM                 REAL,
    ROT_MAX_RPM                 REAL,
    ROT_PLOT_POINTS             INTEGER,
    ROT_CHANGE                  REAL,
    ROT_POINTS                  REAL,
    ROT_CHECK_PUSO              INTEGER,
    TIME_THRESHOLD              REAL,
    ENFORCE_PUSO                INTEGER,
    PKUP_MULTI_METHOD           INTEGER,
    SLK_MULTI_METHOD            INTEGER,
    ROB_MULTI_METHOD            INTEGER,
    SHOW_MULTI                  INTEGER,
    PKUP_RIGSTATES              TEXT,
    SLK_RIGSTATES               TEXT,
    ROT_RIGSTATES               TEXT,
    ENFORCE_RULE                INTEGER,
    PLOT_ONBOTTOM               INTEGER,
    IS_DEFAULT                  INTEGER,
    CASING_PKUP_MIN_MOVEMENT    REAL,
    CASING_PKUP_MAX_MOVEMENT    REAL,
    CASING_SLK_MIN_MOVEMENT     REAL,
    CASING_SLK_MAX_MOVEMENT     REAL,
    CASING_PKUP_RIGSTATES       TEXT,
    CASING_SLK_RIGSTATES        TEXT
);
CREATE VIEW IF NOT EXISTS APP_BS_GLOBAL_PROFILE AS SELECT * FROM VMX_BS_GLOBAL_PROFILE;
";

        /// <summary>
        /// Preserved for backward compatibility; points to the default template schema DDL.
        /// </summary>
        public const string CreateTableSql = CreateAppTableSql;

        /// <summary>
        /// Ensures the appropriate Broomstick Profile table exists for the given database context.
        /// For DrillIntelApp.sqlite, creates APP_BS_GLOBAL_PROFILE table.
        /// For Project databases, creates VMX_BS_GLOBAL_PROFILE table.
        /// </summary>
        public static void EnsureTableExists(IDataServiceDIntel db, bool? isProjectDatabase = null)
        {
            if (db == null) return;
            bool isGlobal = isProjectDatabase.HasValue ? !isProjectDatabase.Value : IsGlobalDatabase(db);
            if (isGlobal)
            {
                db.ExecuteNonQuery(CreateAppTableSql);
            }
            else
            {
                try
                {
                    // If an older project database has APP_BS_GLOBAL_PROFILE as a physical table and no VMX table, migrate it
                    if (db.TableExists(AppGlobalProfileTable) && !db.TableExists(ProjectProfileTable))
                    {
                        db.ExecuteNonQuery($"ALTER TABLE {AppGlobalProfileTable} RENAME TO {ProjectProfileTable};");
                        db.ExecuteNonQuery($"CREATE VIEW IF NOT EXISTS {AppGlobalProfileTable} AS SELECT * FROM {ProjectProfileTable};");
                        return;
                    }
                }
                catch
                {
                }

                db.ExecuteNonQuery(CreateProjectTableSql);
            }
        }

        /// <summary>
        /// Copies all existing records from APP_BS_GLOBAL_PROFILE in DrillIntelApp.sqlite
        /// into VMX_BS_GLOBAL_PROFILE in the Project-level database.
        /// </summary>
        public static bool CopyMasterProfilesToProject(IDataServiceDIntel appDb, IDataServiceDIntel projectDb, out string lastError)
        {
            lastError = "";
            try
            {
                if (appDb == null || projectDb == null)
                {
                    lastError = "Database service is null";
                    return false;
                }

                EnsureTableExists(appDb, isProjectDatabase: false);
                EnsureTableExists(projectDb, isProjectDatabase: true);

                // Load all existing records from APP_BS_GLOBAL_PROFILE in DrillIntelApp.sqlite
                var masterProfiles = LoadAllProfiles(appDb, out lastError, isProjectDatabase: false);
                if (masterProfiles.Count == 0)
                {
                    var defaultProfile = CreateDefault();
                    SaveProfile(appDb, defaultProfile, "SYSTEM", out _, isProjectDatabase: false);
                    masterProfiles.Add(defaultProfile);
                }

                // Clear Project-level VMX_BS_GLOBAL_PROFILE table before copying
                DeleteAllProfiles(projectDb, out _, isProjectDatabase: true);

                // Copy all records into VMX_BS_GLOBAL_PROFILE in Project Database
                foreach (var profile in masterProfiles)
                {
                    string user = string.IsNullOrEmpty(profile.ModifiedBy) ? "SYSTEM" : profile.ModifiedBy;
                    if (!InsertProfile(projectDb, profile, user, out lastError, isProjectDatabase: true))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ex.StackTrace;
                return false;
            }
        }
        /// <summary>
        /// Ensures that the target Broomstick profile table exists and contains an authentic
        /// eVumaxPro calibrated default profile. If forceUpdate is true and existing profiles
        /// have uncalibrated legacy defaults (e.g. DATA_POINTS <= 6 or Hookload range 0),
        /// it upgrades them to the calibrated values.
        /// </summary>
        public static void EnsureMasterProfileSeeded(IDataServiceDIntel db, bool? isProjectDatabase = null, bool forceUpdate = true)
        {
            if (db == null) return;

            try
            {
                EnsureTableExists(db, isProjectDatabase);

                var profiles = LoadAllProfiles(db, out string loadErr, isProjectDatabase);
                if (profiles == null || profiles.Count == 0)
                {
                    var defaultProfile = CreateDefault("eVumaxPro Default Profile");
                    SaveProfile(db, defaultProfile, "SYSTEM", out _, isProjectDatabase);
                    return;
                }

                if (forceUpdate)
                {
                    foreach (var p in profiles)
                    {
                        bool needsUpdate = false;
                        if (p.DataPoints <= 6 || p.DataPoints == 0)
                        {
                            p.DataPoints = 100;
                            needsUpdate = true;
                        }
                        if (string.IsNullOrWhiteSpace(p.GroupFunctions))
                        {
                            p.GroupFunctions = "MIN,MAX,AVG";
                            needsUpdate = true;
                        }
                        if (!p.FilterByRange || (p.MinHooklaod == 0 && p.MaxHookload == 0))
                        {
                            p.FilterByRange = true;
                            p.MinHooklaod = 1.0;
                            p.MaxHookload = 300.0;
                            needsUpdate = true;
                        }
                        if (p.DepthInterval <= 0)
                        {
                            p.DepthInterval = 500.0;
                            needsUpdate = true;
                        }

                        if (needsUpdate)
                        {
                            p.ModifiedBy = "SYSTEM";
                            p.ModifiedDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss");
                            UpdateProfile(db, p, "SYSTEM", out _, isProjectDatabase);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EnsureMasterProfileSeeded failed: {ex.Message}");
            }
        }

    }//class
}//namespace