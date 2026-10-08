using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using DrillIntel.Data;
using System.Data.Common;
using Microsoft.Win32;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Models;

namespace DrillIntel.Projects
{
    // =====================================================================
    // 1) SchemaInitializer
    //    Creates a new .dintel SQLite file and runs the VMX schema against it.
    //    Added PRAGMA optimizations and Schema Versioning.
    // =====================================================================
    public static class SchemaInitializer
    {
        public const int CurrentSchemaVersion = 3;

        public static void CreateDatabase(string dintelFilePath)
        {
            if (string.IsNullOrWhiteSpace(dintelFilePath))
                throw new ArgumentException("A file path is required.", nameof(dintelFilePath));

            var directory = Path.GetDirectoryName(dintelFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(dintelFilePath))
                File.Delete(dintelFilePath);

            var statements = SplitSqlStatements(SchemaScript);

            using var dataService = new DataServiceDIntel(dintelFilePath);
            if (!dataService.IsConnectionOpen())
                throw new InvalidOperationException($"Failed to open database '{dintelFilePath}': {dataService.LastError}");

            dataService.BeginTransaction();
            try
            {
                foreach (var statement in statements)
                {
                    if (string.IsNullOrWhiteSpace(statement)) continue;
                    dataService.ExecuteNonQuery(statement);
                }

                // Insert schema version
                dataService.ExecuteNonQuery(
                    "INSERT INTO VMX_SCHEMA_INFO (SCHEMA_VERSION) VALUES (@v)",
                    new Dictionary<string, object?> { ["@v"] = CurrentSchemaVersion });

                dataService.Commit();
            }
            catch (Exception ex)
            {
                dataService.RollBack();
                throw new InvalidOperationException(
                    $"Failed to initialize schema for '{dintelFilePath}'. See inner exception for details.",
                    ex);
            }
        }

        public static void ApplyPragmas(IDataServiceDIntel dataService)
        {
            // Pragmas are automatically configured in DataServiceDIntel.OpenConnection.
            dataService.ExecuteNonQuery(@"
                PRAGMA foreign_keys = ON;
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA busy_timeout = 5000;
                PRAGMA temp_store = MEMORY;
            ");
        }

        internal static List<string> SplitSqlStatements(string sql)
        {
            var statements = new List<string>();
            var current = new StringBuilder();

            bool inLineComment = false, inBlockComment = false, inSingleQuote = false, inDoubleQuote = false;
            int beginEndDepth = 0;

            for (int i = 0; i < sql.Length; i++)
            {
                char c = sql[i];
                char next = i + 1 < sql.Length ? sql[i + 1] : '\0';

                if (inLineComment)
                {
                    current.Append(c);
                    if (c == '\n') inLineComment = false;
                    continue;
                }

                if (inBlockComment)
                {
                    current.Append(c);
                    if (c == '*' && next == '/')
                    {
                        current.Append(next);
                        i++;
                        inBlockComment = false;
                    }
                    continue;
                }

                if (inSingleQuote)
                {
                    current.Append(c);
                    if (c == '\'' && next == '\'') { current.Append(next); i++; continue; }
                    if (c == '\'') inSingleQuote = false;
                    continue;
                }

                if (inDoubleQuote)
                {
                    current.Append(c);
                    if (c == '"') inDoubleQuote = false;
                    continue;
                }

                if (c == '-' && next == '-') { inLineComment = true; current.Append(c); continue; }
                if (c == '/' && next == '*') { inBlockComment = true; current.Append(c); continue; }
                if (c == '\'') { inSingleQuote = true; current.Append(c); continue; }
                if (c == '"') { inDoubleQuote = true; current.Append(c); continue; }

                if (IsWordBoundaryMatch(sql, i, "BEGIN")) beginEndDepth++;
                else if (IsWordBoundaryMatch(sql, i, "END")) beginEndDepth = Math.Max(0, beginEndDepth - 1);

                if (c == ';' && beginEndDepth == 0)
                {
                    statements.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(c);
            }

            if (!string.IsNullOrWhiteSpace(current.ToString()))
                statements.Add(current.ToString());

            return statements;
        }

        private static bool IsWordBoundaryMatch(string text, int index, string word)
        {
            if (index + word.Length > text.Length) return false;
            if (string.Compare(text, index, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) != 0) return false;

            bool leftOk = index == 0 || !char.IsLetterOrDigit(text[index - 1]);
            int rightIndex = index + word.Length;
            bool rightOk = rightIndex >= text.Length || !char.IsLetterOrDigit(text[rightIndex]);
            return leftOk && rightOk;
        }

        private const string SchemaScript = @"
-- ============================================================================
-- VMX Schema — optimized for SQLite and DrillIntel Time-Series requirements
-- ============================================================================

CREATE TABLE IF NOT EXISTS VMX_SCHEMA_INFO (
    SCHEMA_VERSION INTEGER NOT NULL
);

-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS VMX_WELL (
    WELL_ID                TEXT NOT NULL,
    UWI                    TEXT,
    WELL_NAME               TEXT,
    LEGAL_NAME              TEXT,
    BLOCK                   TEXT,
    FIELD                   TEXT,
    COUNTY                  TEXT,
    DISTRICT                TEXT,
    REGION                  TEXT,
    STATE                   TEXT,
    COUNTRY                 TEXT,
    OPERATOR                TEXT,
    OPERATOR_DIV             TEXT,
    LICENSE_NO               TEXT,
    LICENSE_DATE             TEXT,
    PURPOSE                  TEXT,
    STATUS                   TEXT,
    SPUD_DATE                TEXT,
    PA_DATE                  TEXT,
    TIME_ZONE                TEXT,
    LONGITUDE                REAL,
    LATITUDE                 REAL,
    X_COORDINATE             REAL,
    Y_COORDINATE             REAL,
    PERM_DATUM               TEXT,
    WELL_HEAD_ELEVATION      REAL,
    GROUND_ELEVATION         REAL,
    WATER_DEPTH              REAL,
    WMLS_URL                 TEXT,
    WMLP_URL                 TEXT,
    ALARM_HISTORY_TABLE      TEXT,
    CREATED_BY               TEXT,
    CREATED_DATE             TEXT,
    MODIFIED_BY              TEXT,
    MODIFIED_DATE            TEXT,
    RIG_NAME                 TEXT,
    WELL_LAST_CHECKED        TEXT,
    WELL_FLAGGED             INTEGER,
    DATA_SOURCE              TEXT,
    ALARM_PROFILE_ID         TEXT,
    DATE_FORMAT              TEXT,
    EDR_PROVIDER             TEXT,
    DRILLING_SUP             TEXT,
    DRILLING_ENG             TEXT,
    WITSML_ID                TEXT,
    CONN_CHECK_TIME          TEXT,
    TRIP_CHECK_TIME          TEXT,
    LAST_CHECK_TIME          TEXT,
    LAST_ACK_TIME            TEXT,
    HISTORICAL               INTEGER,
    SEC                      TEXT,
    TWP                      TEXT,
    RGE                      TEXT,
    LEGAL_DESC               TEXT,
    CONT_TYPE                TEXT,
    RIG_TYPE                 TEXT,
    PUMP1_MODEL              TEXT,
    PUMP1_STROKE             TEXT,
    PUMP1_LINER              TEXT,
    PUMP2_MODEL              TEXT,
    PUMP2_STROKE             TEXT,
    PUMP2_LINER              TEXT,
    PUMP3_MODEL              TEXT,
    PUMP3_STROKE             TEXT,
    PUMP3_LINER              TEXT,
    REP                      TEXT,
    TOOL_PUSHER               TEXT,
    TIGHT_HOLE_NO             TEXT,
    REENTRY_NO                TEXT,
    COMMENTS                 TEXT,
    CONTRACTOR                TEXT,
    OBJECTIVE                 TEXT,
    TD_DATE                   TEXT,
    TD_FORMATION               TEXT,
    PUMP1                     TEXT,
    PUMP2                     TEXT,
    PUMP3                     TEXT,
    RIG_COST                  REAL,
    DRLG_CONN_TIME             REAL,
    TRIP_CONN_TIME             REAL,
    BTS_TIME                   REAL,
    STS_TIME                   REAL,
    STB_TIME                   REAL,
    TRIP_IN_SPEED               REAL,
    TRIP_OUT_SPEED              REAL,
    PLANNED_DAYS                REAL,
    LAST_DATE_TIME               TEXT,
    LAST_DEPTH                   REAL,
    LAST_HDTH                    REAL,
    LAST_RIG_STATE                REAL,
    LAST_RIG_STATE_COLOR          REAL,
    LAST_ALARM_DATE               TEXT,
    LAST_ALARM_STATUS             REAL,
    DOWNLOAD_STATUS               INTEGER,
    PIPE_LENGTH                   INTEGER,
    STAND_LENGTH                  INTEGER,
    PLANNED_DEPTH                 INTEGER,
    DRLG_ENG_DEPT                  TEXT,
    DRLG_OP_DEPT                   TEXT,
    WELL_OP_TYPE                   TEXT,
    BI                              TEXT,
    WELL_LOCATION                   TEXT,
    ROP_BENCHMARK                   REAL,
    PIPE_MOVE_BENCHMARK             REAL,
    TRIP_STAND_BENCHMARK            REAL,
    DRLG_CONN_DEVIATION             REAL,
    DRLG_STS_DEVIATION              REAL,
    ROP_DEVIATION                   REAL,
    TRIP_CONN_DEVIATION             REAL,
    PIPE_MOVE_DEVIATION             REAL,
    TRIP_STAND_DEVIATION            REAL,
    TRIP_OH_CONN_TIME                REAL,
    TRIP_CH_CONN_TIME                REAL,
    TRIP_IN_OH_SPEED                 REAL,
    TRIP_IN_CH_SPEED                 REAL,
    TRIP_OUT_OH_SPEED                REAL,
    TRIP_OUT_CH_SPEED                REAL,
    TRIP_STND_OH                     REAL,
    TRIP_STND_CH                     REAL,
    TRIP_JNT_OH                      REAL,
    TRIP_JNT_CH                      REAL,
    TRIP_GRS_OH                      REAL,
    TRIP_GRS_CH                      REAL,
    INFO_TABLE                       TEXT,
    CASING_PIPE_LENGTH                REAL,
    SPUD_DATE_TIME                    TEXT,
    DQI                                REAL,
    FREQUENCY                          REAL,
    DQI_TABLENAME                      TEXT,
    DISABLE_AUTO_TAG                   INTEGER,
    PRIMARY KEY (WELL_ID)
);

-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS VMX_WELLBORE (
    WELL_ID                TEXT NOT NULL,
    WELLBORE_ID             TEXT NOT NULL,
    WELLBORE_NAME           TEXT,
    WELLBORE_TYPE           TEXT,
    WELLBORE_NO             TEXT,
    GOVT_NO                 TEXT,
    SHAPE                   TEXT,
    STATUS                  TEXT,
    PURPOSE                 TEXT,
    KICKOFF_DATE            TEXT,
    DAY_TARGET              INTEGER,
    MD_CURRENT              REAL,
    TVD_CURRENT             REAL,
    MD_KICKOFF              REAL,
    TVD_KICKOFF             REAL,
    MD_PLANNED              REAL,
    TVD_PLANNED             REAL,
    MD_SS_PLANNED           REAL,
    TVD_SS_PLANNED          REAL,
    WMLS_URL                TEXT,
    WMLP_URL                TEXT,
    CREATED_BY              TEXT,
    CREATED_DATE            TEXT,
    MODIFIED_BY             TEXT,
    MODIFIED_DATE           TEXT,
    PRIMARY KEY (WELLBORE_ID, WELL_ID)
);

-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS VMX_TIME_LOG (
    WELL_ID                    TEXT NOT NULL,
    WELLBORE_ID                 TEXT NOT NULL,
    LOG_ID                      TEXT NOT NULL,
    LOG_NAME                    TEXT,
    RUN_NO                      TEXT,
    SERVICE_COMPANY             TEXT,
    COMMENTS                    TEXT,
    DATA_TABLE_NAME              TEXT,
    DESCRIPTION                 TEXT,
    MAX_DATE                    TEXT,
    MAX_DEPTH                   REAL,
    MIN_DATE                    TEXT,
    MIN_DEPTH                   REAL,
    WMLS_URL                    TEXT,
    WMLP_URL                    TEXT,
    LAST_DATA_RECEIVED_ON       TEXT,
    CREATED_BY                  TEXT,
    CREATED_DATE                TEXT,
    MODIFIED_BY                 TEXT,
    MODIFIED_DATE               TEXT,
    EDR_PROVIDER                 TEXT,
    PRIMARY_LOG                  INTEGER,
    REMARKS_LOG                  INTEGER,
    LAST_WRITE_DATE               TEXT,
    PI_WELL_ID                    TEXT,
    PI_WELLBORE_ID                TEXT,
    PI_LOG_ID                     TEXT,
    LINK_TO_PARENT                 INTEGER,
    LINK_WELL_ID                   TEXT,
    LINK_WELLBORE_ID               TEXT,
    LINK_LOG_ID                    TEXT,
    DUPLICATE_ACTION                INTEGER,
    DONT_CALC_HDTH                  INTEGER,
    STARTING_HDTH                   REAL,
    DETECT_SPIKES                   INTEGER,
    TL_PERCENT                      REAL,
    TIME_PERIOD                     INTEGER,
    NR_BTM_DISTANCE                 REAL,
    CMP_WINDOW                      REAL,
    OPEN_SPIKE                      INTEGER,
    ACTION_TYPE                     INTEGER,
    LAST_CHECK_DATE                  TEXT,
    MAX_CLOSE_TIME                   INTEGER,
    CREA_REP_ON_FORMATION            INTEGER,
    SNAP_JOB_ID                      TEXT,
    FORMATION_TOP                    TEXT,
    DEPTH_THRESHOLD                  REAL,
    FREQUENCY                        REAL,
    LAST_SNAP_SENT                    TEXT,
    DONT_MOVE_AHEAD                   INTEGER,
    PRIMARY KEY (LOG_ID, WELLBORE_ID, WELL_ID)
);

-- INDEX: Fast lookup of time logs by their ID (critical for time-series charts)
CREATE INDEX IF NOT EXISTS IX_VMX_TIME_LOG_LOGID ON VMX_TIME_LOG(LOG_ID);

-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS VMX_TIME_LOG_COLUMNS (
    WELL_ID                TEXT NOT NULL,
    WELLBORE_ID             TEXT NOT NULL,
    LOG_ID                  TEXT NOT NULL,
    MNEMONIC                TEXT NOT NULL,
    CHANNEL_NAME             TEXT,
    DATA_TYPE                TEXT,
    UNIT                      TEXT,
    UNIT_ID                   TEXT,
    VUMAX_UNIT_ID              TEXT,
    VALUE_TYPE                 INTEGER,
    VALUE_QUERY                 TEXT,
    WITSML_MNEMONIC              TEXT,
    CREATED_BY                   TEXT,
    CREATED_DATE                 TEXT,
    MODIFIED_BY                  TEXT,
    MODIFIED_DATE                TEXT,
    COLUMN_ORDER                  INTEGER,
    WRITE_BACK                    INTEGER,
    OFFSET                         REAL,
    NO_INTERPOLATE                 INTEGER,
    PI_MNEMONIC                     TEXT,
    IS_STOR_PROC                    INTEGER,
    STOR_PROC_PARAM                  TEXT,
    CURVE_ID                          INTEGER,
    PARENT_MNEMONIC                   TEXT,
    ORIG_UNIT_ID                      TEXT,
    PRIMARY KEY (LOG_ID, MNEMONIC, WELLBORE_ID, WELL_ID)
);

-- INDEX: Fast lookup of columns/mnemonics belonging to a specific time log
CREATE INDEX IF NOT EXISTS IX_VMX_TIME_LOG_COLS ON VMX_TIME_LOG_COLUMNS(LOG_ID, MNEMONIC);

-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS VMX_DEPTH_LOG (
    WELL_ID                TEXT NOT NULL,
    WELLBORE_ID             TEXT NOT NULL,
    LOG_ID                  TEXT NOT NULL,
    LOG_NAME                TEXT,
    RUN_NO                  TEXT,
    SERVICE_COMPANY         TEXT,
    COMMENTS                TEXT,
    DATA_TABLE_NAME          TEXT,
    DESCRIPTION              TEXT,
    MAX_DEPTH                REAL,
    MIN_DEPTH                REAL,
    WMLS_URL                 TEXT,
    WMLP_URL                 TEXT,
    LAST_DATA_RECEIVED_ON     TEXT,
    CREATED_BY                TEXT,
    CREATED_DATE              TEXT,
    MODIFIED_BY                TEXT,
    MODIFIED_DATE              TEXT,
    M_DEPTH                     REAL,
    EDR_PROVIDER                 TEXT,
    LAST_WRITE_DEPTH              REAL,
    PI_WELL_ID                     TEXT,
    PI_WELLBORE_ID                 TEXT,
    PI_LOG_ID                      TEXT,
    LINK_TO_PARENT                  INTEGER,
    LINK_WELL_ID                    TEXT,
    LINK_WELLBORE_ID                TEXT,
    LINK_LOG_ID                     TEXT,
    DUPLICATE_ACTION                 INTEGER,
    PRIMARY_LOG                      INTEGER,
    PRIMARY KEY (LOG_ID, WELLBORE_ID, WELL_ID)
);

-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS VMX_DEPTH_LOG_COLUMNS (
    WELL_ID                TEXT NOT NULL,
    WELLBORE_ID             TEXT NOT NULL,
    LOG_ID                  TEXT NOT NULL,
    MNEMONIC                 TEXT NOT NULL,
    CHANNEL_NAME              TEXT,
    DATA_TYPE                 TEXT,
    UNIT                       TEXT,
    UNIT_ID                    TEXT,
    VUMAX_UNIT_ID               TEXT,
    VALUE_TYPE                  INTEGER,
    VALUE_QUERY                  TEXT,
    WITSML_MNEMONIC               TEXT,
    CREATED_BY                    TEXT,
    CREATED_DATE                  TEXT,
    MODIFIED_BY                   TEXT,
    MODIFIED_DATE                 TEXT,
    COLUMN_ORDER                   INTEGER,
    WRITE_BACK                     INTEGER,
    PI_MNEMONIC                     TEXT,
    AXIS_DATA_COUNT                  INTEGER,
    PARENT_MNEMONIC                   TEXT,
    ORIG_UNIT_ID                      TEXT,
    PRIMARY KEY (LOG_ID, MNEMONIC, WELLBORE_ID, WELL_ID)
);

-- INDEX: Fast lookup of columns/mnemonics belonging to a specific depth log
CREATE INDEX IF NOT EXISTS IX_VMX_DEPTH_LOG_COLS ON VMX_DEPTH_LOG_COLUMNS(LOG_ID, MNEMONIC);

-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS VMX_COMMON_RIGSTATE_SETUP (
    UNKNOWN_NAME              TEXT,
    UNKNOWN_NUMBER             INTEGER,
    UNKNOWN_COLOR               INTEGER,
    HOOKLOAD_CUTOFF              REAL,
    RPM_CUTOFF                    REAL,
    CIRC_CUTOFF                    REAL,
    SENSITIVITY                     REAL,
    PUMP_PRESSURE_CUTOFF             REAL,
    DEPTH_COMP_SENSITIVITY            REAL,
    DETECT_AUTO_SLIDE                  INTEGER,
    MIN_TORQUE                          REAL,
    MAX_TORQUE                           REAL,
    CALIBRATION_ROWS                      INTEGER,
    MIN_TORQUE_DIFF                        REAL,
    MIN_RPM                                 REAL,
    MAX_RPM                                  REAL,
    SELECTED_SET                              INTEGER,
    MIN_TORQUE2                                REAL,
    MAX_TORQUE2                                 REAL,
    CALIBRATION_ROWS2                            INTEGER,
    MIN_TORQUE_DIFF2                              REAL,
    MIN_RPM2                                       REAL,
    MAX_RPM2                                        REAL,
    MIN_TORQUE3                                      REAL,
    MAX_TORQUE3                                       REAL,
    CALIBRATION_ROWS3                                  INTEGER,
    MIN_TORQUE_DIFF3                                    REAL,
    MIN_RPM3                                             REAL,
    MAX_RPM3                                              REAL,
    CREATED_BY                                             TEXT,
    CREATED_DATE                                           TEXT,
    MODIFIED_BY                                            TEXT,
    MODIFIED_DATE                                          TEXT,
    DETECT_AIR_DRILLING                                     INTEGER,
    AIR_PRESSURE                                             REAL,
    TORQUE_CUTOFF                                             REAL,
    TORQUE_CYCLES                                              INTEGER,
    CALB_TIME                                                   INTEGER,
    PERCENT_WINDOW                                               INTEGER,
    DETECT_PIPE_MOVE                                              INTEGER,
    PIPE_MOVE_THRESHOLD                                            REAL
);

-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS VMX_COMMON_RIGSTATE_ITEMS (
    RIG_STATE_NUMBER       INTEGER NOT NULL,
    RIG_STATE_NAME          TEXT NOT NULL,
    RIG_STATE_COLOR          INTEGER,
    CREATED_BY                TEXT,
    CREATED_DATE              TEXT,
    MODIFIED_BY                TEXT,
    MODIFIED_DATE              TEXT,
    PRIMARY KEY (RIG_STATE_NUMBER)
);

-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS APP_BS_GLOBAL_PROFILE (
    ID                          TEXT NOT NULL,
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
    CASING_SLK_RIGSTATES        TEXT,
    PRIMARY KEY (ID)
);

CREATE VIEW IF NOT EXISTS VMX_BS_GLOBAL_PROFILE AS SELECT * FROM APP_BS_GLOBAL_PROFILE;

-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS VMX_AKPI_DRLG_CONNECTIONS (
    WELL_ID                TEXT NOT NULL,
    ENTRY_ID                 INTEGER NOT NULL,
    DEPTH                     REAL,
    BOTTOM_TO_SLIPS            INTEGER,
    SLIPS_TO_SLIPS              INTEGER,
    SLIPS_TO_BOTTOM              INTEGER,
    FROM_DATE                     TEXT,
    TO_DATE                        TEXT,
    TIME                            TEXT,
    USER_COMMENT                     TEXT,
    CREATED_BY                        TEXT,
    CREATED_DATE                      TEXT,
    MODIFIED_BY                       TEXT,
    MODIFIED_DATE                     TEXT,
    LOCAL_FROM_DATE                    TEXT,
    LOCAL_TO_DATE                      TEXT,
    PRIMARY KEY (WELL_ID, ENTRY_ID)
);

-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS VMX_AKPI_DRLG_STAND (
    WELL_ID                TEXT NOT NULL,
    ENTRY_ID                 INTEGER NOT NULL,
    DEPTH                     REAL,
    ROP                        REAL,
    ROTARY_ROP                  REAL,
    SLIDE_ROP                    REAL,
    FROM_DATE                     TEXT,
    TO_DATE                        TEXT,
    TIME                            TEXT,
    USER_COMMENT                     TEXT,
    CREATED_BY                        TEXT,
    CREATED_DATE                      TEXT,
    MODIFIED_BY                       TEXT,
    MODIFIED_DATE                     TEXT,
    LOCAL_FROM_DATE                    TEXT,
    LOCAL_TO_DATE                      TEXT,
    PRIMARY KEY (WELL_ID, ENTRY_ID)
);

-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS VMX_AKPI_TRIP_CONNECTIONS (
    WELL_ID                TEXT NOT NULL,
    ENTRY_ID                 INTEGER NOT NULL,
    DEPTH                     REAL,
    SLIPS_TO_SLIPS             INTEGER,
    FROM_DATE                    TEXT,
    TO_DATE                       TEXT,
    TIME                           TEXT,
    USER_COMMENT                    TEXT,
    CREATED_BY                       TEXT,
    CREATED_DATE                     TEXT,
    MODIFIED_BY                      TEXT,
    MODIFIED_DATE                    TEXT,
    PIPE_MOVE_TIME                    REAL,
    LOCAL_FROM_DATE                    TEXT,
    LOCAL_TO_DATE                       TEXT,
    PRIMARY KEY (WELL_ID, ENTRY_ID)
);

-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS VMX_CONN_INFO (
    WELL_ID                TEXT NOT NULL,
    DEPTH                    REAL NOT NULL,
    CREATED_BY                TEXT,
    CREATED_DATE              TEXT,
    MODIFIED_BY                TEXT,
    MODIFIED_DATE              TEXT,
    PRIMARY KEY (WELL_ID, DEPTH)
);

-- INDEX: Extremely fast range queries and lookups by depth
CREATE INDEX IF NOT EXISTS IX_VMX_CONN_INFO_DEPTH ON VMX_CONN_INFO(DEPTH);

-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS VMX_CON_ANNOTATIONS (
    WELL_ID                TEXT NOT NULL,
    DEPTH                    REAL NOT NULL,
    COMMENT                   TEXT,
    CREATED_BY                 TEXT,
    CREATED_DATE               TEXT,
    MODIFIED_BY                 TEXT,
    MODIFIED_DATE               TEXT,
    PRIMARY KEY (WELL_ID, DEPTH)
);

-- ----------------------------------------------------------------------------
-- VMX_DOC_TEMPLATES — Document & Track Console Templates (VuMaxDR compatible)
CREATE TABLE IF NOT EXISTS VMX_DOC_TEMPLATES (
    TEMPLATE_TYPE   TEXT NOT NULL,
    TEMPLATE_ID     TEXT NOT NULL,
    DOCUMENT_NAME   TEXT NOT NULL DEFAULT '',
    WELL_ID         TEXT,
    WELLBORE_ID     TEXT,
    LOG_ID          TEXT,
    DESCRIPTION     TEXT,
    TEMPLATE_DATA   BLOB,
    IS_DEFAULT      INTEGER NOT NULL DEFAULT 0,
    CREATED_BY      TEXT,
    CREATED_DATE    TEXT,
    MODIFIED_BY     TEXT,
    MODIFIED_DATE   TEXT,
    PRIMARY KEY (TEMPLATE_TYPE, TEMPLATE_ID)
);

CREATE INDEX IF NOT EXISTS IX_VMX_DOC_TEMPLATES_TYPE ON VMX_DOC_TEMPLATES(TEMPLATE_TYPE);
CREATE INDEX IF NOT EXISTS IX_VMX_DOC_TEMPLATES_NAME ON VMX_DOC_TEMPLATES(DOCUMENT_NAME);
";
    }

    // =====================================================================
    // 2) ProjectSession
    //    Holds the currently loaded project connection. Implements migration
    //    checking on load to ensure schema version matches.
    // =====================================================================
    public class ProjectSession : INotifyPropertyChanged, IDisposable
    {
        private IDataServiceDIntel? _dataService;
        private string? _projectName;
        private string? _projectFilePath;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? ProjectChanged;
        public event EventHandler? DataChanged;

        public void NotifyDataChanged() => DataChanged?.Invoke(this, EventArgs.Empty);

        public bool IsProjectOpen => _dataService != null && _dataService.IsConnectionOpen();

        public string? ProjectName
        {
            get => _projectName;
            private set { _projectName = value; OnPropertyChanged(); OnPropertyChanged(nameof(WindowTitleSuffix)); OnPropertyChanged(nameof(IsProjectOpen)); }
        }

        public string? ProjectFilePath
        {
            get => _projectFilePath;
            private set { _projectFilePath = value; OnPropertyChanged(); }
        }

        public string WindowTitleSuffix => IsProjectOpen ? $" — {ProjectName}" : string.Empty;

        public void Load(string dintelFilePath)
        {
            if (!File.Exists(dintelFilePath))
                throw new FileNotFoundException("Project file not found.", dintelFilePath);

            Close();

            var dataService = new DataServiceDIntel(dintelFilePath);
            if (!dataService.IsConnectionOpen())
                throw new InvalidOperationException($"Failed to open project database '{dintelFilePath}': {dataService.LastError}");

            // Check Schema Version & Migrate if necessary
            EnsureSchemaVersion(dataService);

            // Centralized Base database: Remove any project-level unit tables/views and migrate legacy data if present
            DrillIntel.Models.Unit.RemoveProjectLevelTables(dataService);
            DrillIntel.Models.UnitConverter.RemoveProjectLevelTables(dataService);

            _dataService = dataService;
            ProjectFilePath = dintelFilePath;
            ProjectName = Path.GetFileNameWithoutExtension(dintelFilePath);

            ProjectChanged?.Invoke(this, EventArgs.Empty);
        }

        private void EnsureSchemaVersion(IDataServiceDIntel dataService)
        {
            try
            {
                EnsureDocTemplatesTable(dataService);
            }
            catch
            {
                // Non-fatal if database is read-only
            }

            int version = 0;
            try 
            {
                var result = dataService.GetValue("SELECT SCHEMA_VERSION FROM VMX_SCHEMA_INFO LIMIT 1");
                if (result != null)
                    version = Convert.ToInt32(result);
            }
            catch 
            {
                // Table might not exist in older un-versioned databases
            }

            if (version < SchemaInitializer.CurrentSchemaVersion)
            {
                try
                {
                    dataService.ExecuteNonQuery(@"
CREATE TABLE IF NOT EXISTS VMX_SCHEMA_INFO (
    SCHEMA_VERSION INTEGER NOT NULL
);");
                    var count = Convert.ToInt32(dataService.GetValue("SELECT COUNT(1) FROM VMX_SCHEMA_INFO") ?? 0);
                    if (count > 0)
                    {
                        dataService.ExecuteNonQuery("UPDATE VMX_SCHEMA_INFO SET SCHEMA_VERSION = @v;",
                            new Dictionary<string, object?> { ["@v"] = SchemaInitializer.CurrentSchemaVersion });
                    }
                    else
                    {
                        dataService.ExecuteNonQuery("INSERT INTO VMX_SCHEMA_INFO (SCHEMA_VERSION) VALUES (@v);",
                            new Dictionary<string, object?> { ["@v"] = SchemaInitializer.CurrentSchemaVersion });
                    }
                }
                catch
                {
                    // Non-fatal if database is read-only or in locked state
                }
            }
        }

        public static void EnsureDocTemplatesTable(IDataServiceDIntel dataService)
        {
            // Ensure VMX_DOC_TEMPLATES exists and has all required columns
            dataService.ExecuteNonQuery(@"
CREATE TABLE IF NOT EXISTS VMX_DOC_TEMPLATES (
    TEMPLATE_TYPE   TEXT NOT NULL,
    TEMPLATE_ID     TEXT NOT NULL,
    DOCUMENT_NAME   TEXT NOT NULL DEFAULT '',
    WELL_ID         TEXT,
    WELLBORE_ID     TEXT,
    LOG_ID          TEXT,
    DESCRIPTION     TEXT,
    TEMPLATE_DATA   BLOB,
    IS_DEFAULT      INTEGER NOT NULL DEFAULT 0,
    CREATED_BY      TEXT,
    CREATED_DATE    TEXT,
    MODIFIED_BY     TEXT,
    MODIFIED_DATE   TEXT,
    PRIMARY KEY (TEMPLATE_TYPE, TEMPLATE_ID)
);");
            dataService.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS IX_VMX_DOC_TEMPLATES_TYPE ON VMX_DOC_TEMPLATES(TEMPLATE_TYPE);");
            dataService.ExecuteNonQuery("CREATE INDEX IF NOT EXISTS IX_VMX_DOC_TEMPLATES_NAME ON VMX_DOC_TEMPLATES(DOCUMENT_NAME);");

            // Check for missing columns if table pre-existed (e.g. from VuMaxDR)
            var tableInfo = dataService.GetTable("PRAGMA table_info(VMX_DOC_TEMPLATES);");
            var existingCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (tableInfo != null)
            {
                foreach (System.Data.DataRow row in tableInfo.Rows)
                {
                    existingCols.Add(row["name"]?.ToString() ?? "");
                }
            }

            if (!existingCols.Contains("DOCUMENT_NAME"))
            {
                dataService.ExecuteNonQuery("ALTER TABLE VMX_DOC_TEMPLATES ADD COLUMN DOCUMENT_NAME TEXT NOT NULL DEFAULT '';");
                dataService.ExecuteNonQuery("UPDATE VMX_DOC_TEMPLATES SET DOCUMENT_NAME = TEMPLATE_ID WHERE DOCUMENT_NAME IS NULL OR DOCUMENT_NAME = '';");
            }
            if (!existingCols.Contains("WELL_ID"))
                dataService.ExecuteNonQuery("ALTER TABLE VMX_DOC_TEMPLATES ADD COLUMN WELL_ID TEXT;");
            if (!existingCols.Contains("WELLBORE_ID"))
                dataService.ExecuteNonQuery("ALTER TABLE VMX_DOC_TEMPLATES ADD COLUMN WELLBORE_ID TEXT;");
            if (!existingCols.Contains("LOG_ID"))
                dataService.ExecuteNonQuery("ALTER TABLE VMX_DOC_TEMPLATES ADD COLUMN LOG_ID TEXT;");
            if (!existingCols.Contains("DESCRIPTION"))
                dataService.ExecuteNonQuery("ALTER TABLE VMX_DOC_TEMPLATES ADD COLUMN DESCRIPTION TEXT;");
            if (!existingCols.Contains("IS_DEFAULT"))
                dataService.ExecuteNonQuery("ALTER TABLE VMX_DOC_TEMPLATES ADD COLUMN IS_DEFAULT INTEGER NOT NULL DEFAULT 0;");
        }

        public IDataServiceDIntel GetDataService()
        {
            if (_dataService == null || !_dataService.IsConnectionOpen())
                throw new InvalidOperationException("No project is currently loaded.");
            return _dataService;
        }

        public DbConnection GetConnection()
        {
            if (_dataService == null || !_dataService.IsConnectionOpen())
                throw new InvalidOperationException("No project is currently loaded.");
            return _dataService.GetDbConnection();
        }

        public void Close()
        {
            if (_dataService == null) return;
            
            _dataService.CloseConnection();
            _dataService.Dispose();
            _dataService = null;
            
            ProjectFilePath = null;
            ProjectName = null;
            ProjectChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => Close();

        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // =====================================================================
    // 3) ProjectService
    //    Orchestrates the New Project flow: Save dialog -> create -> load.
    // =====================================================================
    public interface IProjectService
    {
        bool CreateNewProject();
        Task<bool> CreateNewProjectAsync();
        bool OpenProject();
        bool OpenProject(string filePath);
    }

    public class ProjectService : IProjectService
    {
        private readonly ProjectSession _session;
        private readonly DrillIntel.Services.IRecentProjectsService? _recentProjectsService;
        private readonly DrillIntel.Data.IAppDatabaseService? _appDatabaseService;

        public ProjectService(
            ProjectSession session,
            DrillIntel.Services.IRecentProjectsService? recentProjectsService = null,
            DrillIntel.Data.IAppDatabaseService? appDatabaseService = null)
        {
            _session = session;
            _recentProjectsService = recentProjectsService;
            _appDatabaseService = appDatabaseService;
        }

        public bool CreateNewProject() => CreateNewProjectAsync().GetAwaiter().GetResult();

        public async Task<bool> CreateNewProjectAsync()
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save Project",
                Filter = "DrillIntel Project (*.dintel)|*.dintel",
                DefaultExt = ".dintel",
                AddExtension = true,
                FileName = "New Project.dintel",
                OverwritePrompt = true
            };

            if (dialog.ShowDialog() != true)
                return false;

            string dintelFilePath = dialog.FileName;

            try
            {
                // Prompt user for initial Well Information first
                var defaultWellName = Path.GetFileNameWithoutExtension(dintelFilePath);
                var vm = new DrillIntel.ViewModels.WellInformationViewModel(defaultWellName, "General Field");
                var win = new DrillIntel.Views.WellInformationWindow
                {
                    DataContext = vm,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                if (Application.Current?.MainWindow != null && Application.Current.MainWindow.IsVisible)
                {
                    win.Owner = Application.Current.MainWindow;
                }

                bool? dialogResult = win.ShowDialog();
                if (dialogResult != true)
                {
                    // User cancelled well info dialog; cleanly abort project creation without creating file
                    return false;
                }

                SchemaInitializer.CreateDatabase(dintelFilePath);

                string chosenWellName = !string.IsNullOrWhiteSpace(vm.WellName) ? vm.WellName.Trim() : defaultWellName;
                string chosenField = !string.IsNullOrWhiteSpace(vm.FieldName) ? vm.FieldName.Trim() : "General Field";

                // Load project into session first (opens database connection)
                _session.Load(dintelFilePath);
                _recentProjectsService?.AddOrUpdate(dintelFilePath, chosenWellName, chosenField);

                // Copy master RigState setup and items from Application Database into the new project
                CopyMasterSetupToProject(_session.GetDataService());

                // Copy master Broomstick profiles from Application Database into the new project
                CopyMasterBsGlobalProfilesToProject(_session.GetDataService());

                // Save well and linked wellbore information through the repository
                var repo = new DrillIntel.Data.WellDataRepository(_session);
                var wellId = Guid.NewGuid().ToString();
                var wellboreId = Guid.NewGuid().ToString();

                var well = new Well
                {
                    ObjectID = wellId,
                    name = chosenWellName,
                    field = chosenField,
                    dTimSpud = DateTime.Now.ToString("o")
                };

                var wellbore = new Wellbore
                {
                    ObjectID = wellboreId,
                    WellID = wellId,
                    nameWell = chosenWellName,
                    name = chosenWellName
                };
                well.wellbores[wellboreId] = wellbore;
                well.__timeLogWellboreID = wellboreId;

                await repo.SaveProjectWellAsync(well);

                // Flush WAL changes so external tools (DB Browser, etc.) immediately see all tables and rows on disk
                _session.GetDataService().ExecuteNonQuery("PRAGMA wal_checkpoint(FULL);");

                // Ensure Dashboard reflects the newly created well
                if (Application.Current?.MainWindow?.DataContext is DrillIntel.ViewModels.MainViewModel mainVm &&
                    mainVm.CurrentViewModel is DrillIntel.ViewModels.DashboardViewModel dash)
                {
                    await dash.RefreshAsync();
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to create the project:\n\n{ex.Message}",
                    "New Project",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);

                _session.Close();
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                TryDeleteFile(dintelFilePath);
                return false;
            }
        }

        public bool OpenProject()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open Project",
                Filter = "DrillIntel Project (*.dintel)|*.dintel"
            };

            if (dialog.ShowDialog() != true)
                return false;

            return OpenProject(dialog.FileName);
        }

        public bool OpenProject(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                System.Windows.MessageBox.Show(
                    $"The project file could not be found or has been moved:\n\n{filePath}",
                    "Open Project",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);

                return false;
            }

            try
            {
                _session.Load(filePath);
                _recentProjectsService?.AddOrUpdate(filePath);

                // Ensure project database schema has APP_BS_GLOBAL_PROFILE and default seed
                EnsureProjectSchemaUpToDate(_session.GetDataService());

                return true;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to load project:\n\n{ex.Message}",
                    "Load Project",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);

                return false;
            }
        }

        private void EnsureProjectSchemaUpToDate(IDataServiceDIntel projectDataService)
        {
            try
            {
                if (projectDataService == null) return;

                BroomstickProfile.EnsureTableExists(projectDataService);

                var dt = projectDataService.GetTable("SELECT COUNT(*) FROM APP_BS_GLOBAL_PROFILE;");
                if (dt != null && dt.Rows.Count > 0 && Convert.ToInt32(dt.Rows[0][0]) == 0)
                {
                    CopyMasterBsGlobalProfilesToProject(projectDataService);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to ensure project schema up to date: {ex.Message}");
            }
        }

        private void CopyMasterSetupToProject(IDataServiceDIntel projectDataService)
        {
            try
            {
                var appDbService = _appDatabaseService ?? (Application.Current != null ? App.AppDatabaseService : null);
                if (appDbService != null)
                {
                    var appDataService = appDbService.GetDataService();
                    if (appDataService != null)
                    {
                        var masterSetup = RigStateService.LoadCommonRigStateSetup(appDataService);
                        if (masterSetup != null)
                        {
                            RigStateService.SaveCommonRigStateSetup(projectDataService, masterSetup);
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                // Non-fatal: log for diagnostics so well creation proceeds even if master DB access fails
                System.Diagnostics.Debug.WriteLine($"Failed to copy master setup to new project: {ex.Message}");
            }
        }

        private void CopyMasterBsGlobalProfilesToProject(IDataServiceDIntel projectDataService)
        {
            try
            {
                var appDbService = _appDatabaseService ?? (Application.Current != null ? App.AppDatabaseService : null);
                if (appDbService != null)
                {
                    var appDataService = appDbService.GetDataService();
                    if (appDataService != null)
                    {
                        BroomstickProfile.CopyMasterProfilesToProject(appDataService, projectDataService, out string error);
                        if (!string.IsNullOrEmpty(error))
                        {
                            System.Diagnostics.Debug.WriteLine($"Warning copying master Broomstick profiles: {error}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Non-fatal: log for diagnostics so well creation proceeds even if master DB access fails
                System.Diagnostics.Debug.WriteLine($"Failed to copy master Broomstick profiles to new project: {ex.Message}");
            }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best-effort */ }
        }
    }

}
