using System;
using System.Collections.Generic;
using System.Data;
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

        // Added properties matching APP_BS_GLOBAL_PROFILE schema
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

        /// <summary>
        /// Saves (replaces) a profile — mirrors the original's DELETE-then-INSERT pattern
        /// rather than an UPSERT, so behavior matches the legacy VB exactly. DataServiceDIntel's
        /// LastError/non-throwing contract is preserved: failures return false with LastError set,
        /// exceptions are caught and reported the same way.
        ///
        /// NOTE: the VB original pulled CREATED_BY/MODIFIED_BY from objDataService.UserName —
        /// but DataServiceDIntel is the domain-agnostic SQLite infrastructure class and
        /// deliberately has no concept of a logged-in user, so that value is now passed in
        /// explicitly by the caller (e.g. the current Windows user, or whatever identity
        /// concept DrillIntel's own app layer uses) rather than fabricated here.
        /// </summary>
        // public static bool SaveProfile(DataServiceDIntel db, BroomstickProfile profile, string currentUserName, out string lastError)
        public static bool SaveProfile(IDataServiceDIntel db, BroomstickProfile profile, string currentUserName, out string lastError)
        {
            lastError = "";
            try
            {
                // Ensure table and view exist
                EnsureTableExists(db);
                // db.ExecuteNonQuery(
                //     "DELETE FROM APP_BS_GLOBAL_PROFILE WHERE ID = @ID",
                //     new System.Collections.Generic.Dictionary<string, object?> { ["@ID"] = profile.ID });
                // In DrillIntel, BroomstickProfile is a single entry per database. Ensure all prior entries are cleared.
                db.ExecuteNonQuery("DELETE FROM APP_BS_GLOBAL_PROFILE;");

                /*
                const string insertSql = @"
INSERT INTO APP_BS_GLOBAL_PROFILE (
    ID, NAME, TYPE, NOTES, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE, DOWNSAMPLE_ON, DATA_POINTS,
    TIME_PERIOD, GROUP_FUNC, SHOW_DEPTH_TRACK, TRACK_WIDTH, FILTER_BY_RANGE, MIN_HKLD, MAX_HKLD, FILTER_BY_INTERVAL, DEPTH_INTERVAL, INTERVAL_WINDOW,
    POINTS_TO_PLOT, PKUP_PUMP_CHANNEL, PKUP_PUMP_CUTOFF, PKUP_RPM_CUTOFF, PKUP_MAX_MOVEMENT, PKUP_MIN_MOVEMENT, PKUP_PLOT_POINTS, PKUP_STATIC_METHOD,
    PKUP_DYNAMIC_METHOD, PKUP_LOCAL_MAX, SLK_PUMP_CHANNEL, SLK_PUMP_CUTOFF, SLK_RPM_CUTOFF, SLK_MAX_MOVEMENT, SLK_MIN_MOVEMENT, SLK_PLOT_POINTS, SLK_STATIC_METHOD,
    SLK_DYNAMIC_METHOD, SLK_LOCAL_MAX, ROT_PUMP_CHANNEL, ROT_PUMP_CUTOFF, ROT_MIN_RPM, ROT_MAX_RPM, ROT_PLOT_POINTS, ROT_CHANGE, ROT_POINTS, ROT_CHECK_PUSO,
    TIME_THRESHOLD, ENFORCE_PUSO, PKUP_MULTI_METHOD, SLK_MULTI_METHOD, ROB_MULTI_METHOD, SHOW_MULTI, PKUP_RIGSTATES, SLK_RIGSTATES, ROT_RIGSTATES,
    ENFORCE_RULE, PLOT_ONBOTTOM
) VALUES (
    @ID, @NAME, @TYPE, @NOTES, @CREATED_BY, @CREATED_DATE, @MODIFIED_BY, @MODIFIED_DATE, @DOWNSAMPLE_ON, @DATA_POINTS,
    @TIME_PERIOD, @GROUP_FUNC, @SHOW_DEPTH_TRACK, @TRACK_WIDTH, @FILTER_BY_RANGE, @MIN_HKLD, @MAX_HKLD, @FILTER_BY_INTERVAL, @DEPTH_INTERVAL, @INTERVAL_WINDOW,
    @POINTS_TO_PLOT, @PKUP_PUMP_CHANNEL, @PKUP_PUMP_CUTOFF, @PKUP_RPM_CUTOFF, @PKUP_MAX_MOVEMENT, @PKUP_MIN_MOVEMENT, @PKUP_PLOT_POINTS, @PKUP_STATIC_METHOD,
    @PKUP_DYNAMIC_METHOD, @PKUP_LOCAL_MAX, @SLK_PUMP_CHANNEL, @SLK_PUMP_CUTOFF, @SLK_RPM_CUTOFF, @SLK_MAX_MOVEMENT, @SLK_MIN_MOVEMENT, @SLK_PLOT_POINTS, @SLK_STATIC_METHOD,
    @SLK_DYNAMIC_METHOD, @SLK_LOCAL_MAX, @ROT_PUMP_CHANNEL, @ROT_PUMP_CUTOFF, @ROT_MIN_RPM, @ROT_MAX_RPM, @ROT_PLOT_POINTS, @ROT_CHANGE, @ROT_POINTS, @ROT_CHECK_PUSO,
    @TIME_THRESHOLD, @ENFORCE_PUSO, @PKUP_MULTI_METHOD, @SLK_MULTI_METHOD, @ROB_MULTI_METHOD, @SHOW_MULTI, @PKUP_RIGSTATES, @SLK_RIGSTATES, @ROT_RIGSTATES,
    @ENFORCE_RULE, @PLOT_ONBOTTOM
)";
                */

                const string insertSql = @"
INSERT INTO APP_BS_GLOBAL_PROFILE (
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

                var p = new System.Collections.Generic.Dictionary<string, object?>
                {
                    ["@ID"] = profile.ID,
                    ["@NAME"] = profile.Name,
                    ["@TYPE"] = profile.Type,
                    ["@NOTES"] = profile.Notes,
                    // ["@CREATED_BY"] = currentUserName,
                    // ["@CREATED_DATE"] = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss"),
                    // ["@MODIFIED_BY"] = currentUserName,
                    // ["@MODIFIED_DATE"] = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss"),
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

                    // Added parameter mappings for new columns
                    ["@IS_DEFAULT"] = profile.IsDefault ? 1 : 0,
                    ["@CASING_PKUP_MIN_MOVEMENT"] = profile.CasingPkupMinMovement,
                    ["@CASING_PKUP_MAX_MOVEMENT"] = profile.CasingPkupMaxMovement,
                    ["@CASING_SLK_MIN_MOVEMENT"] = profile.CasingSlkMinMovement,
                    ["@CASING_SLK_MAX_MOVEMENT"] = profile.CasingSlkMaxMovement,
                    ["@CASING_PKUP_RIGSTATES"] = profile.CasingPkupRigStates,
                    ["@CASING_SLK_RIGSTATES"] = profile.CasingSlkRigStates,
                };

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
        /// Loads a profile by ID. Mirrors the original's null-handling via
        /// DataServiceDIntel.CheckNull and returns null (with lastError set) if not found
        /// or on error, matching the VB original's contract exactly.
        /// </summary>
        // public static BroomstickProfile? LoadProfile(DataServiceDIntel db, string profileID, out string lastError)
        public static BroomstickProfile? LoadProfile(IDataServiceDIntel db, string profileID, out string lastError)
        {
            lastError = "";
            try
            {
                EnsureTableExists(db);
                DataTable data = db.GetTable(
                    "SELECT * FROM APP_BS_GLOBAL_PROFILE WHERE ID = @ID",
                    new System.Collections.Generic.Dictionary<string, object?> { ["@ID"] = profileID });

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

                // Added columns mapping
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
        /// Loads the single profile from APP_BS_GLOBAL_PROFILE. If no row exists, creates and persists default profile.
        /// </summary>
        public static BroomstickProfile LoadSingleProfile(IDataServiceDIntel db, out string lastError)
        {
            lastError = "";
            try
            {
                EnsureTableExists(db);
                DataTable data = db.GetTable("SELECT * FROM APP_BS_GLOBAL_PROFILE LIMIT 1");
                if (data != null && data.Rows.Count > 0)
                {
                    return MapFromRow(data.Rows[0]);
                }

                // If no profile exists, create and persist default single entry
                var defaultProfile = CreateDefault("Default Profile");
                SaveProfile(db, defaultProfile, "SYSTEM", out lastError);
                return defaultProfile;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ex.StackTrace;
                return CreateDefault();
            }
        }

        public static List<BroomstickProfile> LoadAllProfiles(IDataServiceDIntel db, out string lastError)
        {
            lastError = "";
            var list = new List<BroomstickProfile>();
            try
            {
                EnsureTableExists(db);
                DataTable data = db.GetTable("SELECT * FROM APP_BS_GLOBAL_PROFILE ORDER BY IS_DEFAULT DESC, NAME ASC");
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

        public static BroomstickProfile? GetDefaultProfile(IDataServiceDIntel db, out string lastError)
        {
            lastError = "";
            try
            {
                EnsureTableExists(db);
                DataTable data = db.GetTable("SELECT * FROM APP_BS_GLOBAL_PROFILE WHERE IS_DEFAULT = 1 LIMIT 1");
                if (data != null && data.Rows.Count > 0)
                {
                    return MapFromRow(data.Rows[0]);
                }

                // Fallback to first profile if no default explicitly marked
                DataTable allData = db.GetTable("SELECT * FROM APP_BS_GLOBAL_PROFILE LIMIT 1");
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
                Notes = "Default Broomstick Global Profile",
                IsDefault = true,
                DownSampleOn = 0,
                DataPoints = 6,
                TimePeriod = 0,
                GroupFunctions = "",
                ShowDepthTrack = false,
                TrackWidth = 100,
                FilterByRange = false,
                MinHooklaod = 0,
                MaxHookload = 0,
                FilterByInterval = false,
                DepthInterval = 100,
                IntervalWindow = 10,
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
                CasingPkupMinMovement = 0,
                CasingPkupMaxMovement = 0,
                CasingSlkMinMovement = 0,
                CasingSlkMaxMovement = 0,
                CasingPkupRigStates = "",
                CasingSlkRigStates = "",
                CreatedBy = "SYSTEM",
                CreatedDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss"),
                ModifiedBy = "SYSTEM",
                ModifiedDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss")
            };
        }

        public const string CreateTableSql = @"
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

        public static void EnsureTableExists(IDataServiceDIntel db)
        {
            if (db == null) return;
            db.ExecuteNonQuery(CreateTableSql);
        }

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

                EnsureTableExists(projectDb);

                /*
                var profiles = LoadAllProfiles(appDb, out lastError);
                if (profiles.Count == 0)
                {
                    var defaultProfile = CreateDefault();
                    SaveProfile(appDb, defaultProfile, "SYSTEM", out _);
                    profiles.Add(defaultProfile);
                }

                foreach (var profile in profiles)
                {
                    string user = string.IsNullOrEmpty(profile.ModifiedBy) ? "SYSTEM" : profile.ModifiedBy;
                    if (!SaveProfile(projectDb, profile, user, out lastError))
                    {
                        return false;
                    }
                }
                */

                // In DrillIntel, BroomstickProfile is single-entry. Copy the single master profile from App DB to Project DB.
                var masterProfile = LoadSingleProfile(appDb, out lastError);
                if (masterProfile == null)
                {
                    masterProfile = CreateDefault();
                    SaveProfile(appDb, masterProfile, "SYSTEM", out _);
                }

                string user = string.IsNullOrEmpty(masterProfile.ModifiedBy) ? "SYSTEM" : masterProfile.ModifiedBy;
                return SaveProfile(projectDb, masterProfile, user, out lastError);

                return true;
            }
            catch (Exception ex)
            {
                lastError = ex.Message + ex.StackTrace;
                return false;
            }
        }
    }//class
}//namespace
