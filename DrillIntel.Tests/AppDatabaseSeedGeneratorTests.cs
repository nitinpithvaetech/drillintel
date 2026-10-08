using System;
using System.IO;
using Microsoft.Data.Sqlite;
using DrillIntel.Data.Objects.DataObjects.Services;
using Xunit;

namespace DrillIntel.Tests;

[Collection("AppDatabaseCollection")]
public class AppDatabaseSeedGeneratorTests
{
    private static readonly string SolutionDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\.."));
    private static readonly string TargetSeedPath = Path.Combine(SolutionDir, "DrillIntel", "Data", "DrillIntelApp.sqlite");

    [Fact]
    public void GenerateAndVerifyAppDatabaseSeed()
    {
        var targetDir = Path.GetDirectoryName(TargetSeedPath)!;
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // Delete existing file if present to guarantee clean creation
        if (File.Exists(TargetSeedPath))
        {
            SqliteConnection.ClearAllPools();
            File.Delete(TargetSeedPath);
        }

        using (var connection = new SqliteConnection($"Data Source={TargetSeedPath};Mode=ReadWriteCreate;"))
        {
            connection.Open();

            using var cmd = connection.CreateCommand();

            // 1. Pragmas
            cmd.CommandText = @"
                PRAGMA journal_mode = DELETE;
                PRAGMA foreign_keys = ON;
            ";
            cmd.ExecuteNonQuery();

            // 2. APP_SCHEMA_INFO
            cmd.CommandText = @"
                CREATE TABLE APP_SCHEMA_INFO (
                    SCHEMA_VERSION INTEGER NOT NULL,
                    MIGRATED_DATE  TEXT NOT NULL
                );
                INSERT INTO APP_SCHEMA_INFO (SCHEMA_VERSION, MIGRATED_DATE) VALUES (1, datetime('now'));
            ";
            cmd.ExecuteNonQuery();

            // 3. APP_RIGSTATE_COMMON_SETUP
            cmd.CommandText = @"
                CREATE TABLE APP_RIGSTATE_COMMON_SETUP (
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
                    PIPE_MOVE_THRESHOLD                                            REAL,
                    MIST_CUTOFF                                                    REAL
                );

                INSERT INTO APP_RIGSTATE_COMMON_SETUP (
                    UNKNOWN_NAME, UNKNOWN_NUMBER, UNKNOWN_COLOR,
                    HOOKLOAD_CUTOFF, RPM_CUTOFF, CIRC_CUTOFF, SENSITIVITY,
                    PUMP_PRESSURE_CUTOFF, DEPTH_COMP_SENSITIVITY, DETECT_AUTO_SLIDE,
                    MIN_TORQUE, MAX_TORQUE, CALIBRATION_ROWS, MIN_TORQUE_DIFF, MIN_RPM, MAX_RPM,
                    SELECTED_SET,
                    MIN_TORQUE2, MAX_TORQUE2, CALIBRATION_ROWS2, MIN_TORQUE_DIFF2, MIN_RPM2, MAX_RPM2,
                    MIN_TORQUE3, MAX_TORQUE3, CALIBRATION_ROWS3, MIN_TORQUE_DIFF3, MIN_RPM3, MAX_RPM3,
                    DETECT_AIR_DRILLING, AIR_PRESSURE, TORQUE_CUTOFF, TORQUE_CYCLES,
                    CALB_TIME, PERCENT_WINDOW, DETECT_PIPE_MOVE, PIPE_MOVE_THRESHOLD, MIST_CUTOFF,
                    CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE
                ) VALUES (
                    'Unknown', 15, -2818048,
                    90.0, 1.0, 1.0, 1.0,
                    0.0, 0.3, 0,
                    1.0, 12000.0, 20, 500.0, 0.0, 50.0,
                    1,
                    0.0, 15000.0, 50, 1.0, 0.0, 44.0,
                    0.0, 0.0, 0, 0.0, 0.0, 0.0,
                    0, 0.0, 0.0, 0,
                    0, 0, 0, 0.0, 0.0,
                    'SYSTEM', datetime('now'), 'SYSTEM', datetime('now')
                );
            ";
            cmd.ExecuteNonQuery();

            // 4. APP_RIGSTATE_COMMON_ITEMS
            cmd.CommandText = @"
                CREATE TABLE APP_RIGSTATE_COMMON_ITEMS (
                    RIG_STATE_NUMBER       INTEGER NOT NULL PRIMARY KEY,
                    RIG_STATE_NAME          TEXT NOT NULL,
                    RIG_STATE_COLOR          INTEGER,
                    CREATED_BY                TEXT,
                    CREATED_DATE              TEXT,
                    MODIFIED_BY                TEXT,
                    MODIFIED_DATE              TEXT
                );
            ";
            cmd.ExecuteNonQuery();

            // Seed all 28 default items
            foreach (var item in RigStateService.DefaultRigStateItems)
            {
                cmd.CommandText = $@"
                    INSERT INTO APP_RIGSTATE_COMMON_ITEMS (
                        RIG_STATE_NUMBER, RIG_STATE_NAME, RIG_STATE_COLOR, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE
                    ) VALUES (
                        {item.Number}, '{item.Name.Replace("'", "''")}', {item.Color}, 'SYSTEM', datetime('now'), 'SYSTEM', datetime('now')
                    );
                ";
                cmd.ExecuteNonQuery();
            }

            // 5. APP_CHANNEL_MAPPING
            cmd.CommandText = @"
                CREATE TABLE APP_CHANNEL_MAPPING (
                    ID               INTEGER PRIMARY KEY AUTOINCREMENT,
                    MNEMONIC         TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    STANDARD_CHANNEL TEXT NOT NULL,
                    DESCRIPTION      TEXT,
                    DEFAULT_UNIT     TEXT,
                    SOURCE_VENDOR    TEXT DEFAULT 'Standard'
                );

                INSERT INTO APP_CHANNEL_MAPPING (MNEMONIC, STANDARD_CHANNEL, DESCRIPTION, DEFAULT_UNIT, SOURCE_VENDOR) VALUES
                ('DEPTH', 'Depth', 'Measured Depth', 'ft', 'Standard'),
                ('DMEA', 'Depth', 'Measured Depth', 'ft', 'Standard'),
                ('DEPT', 'Depth', 'Depth', 'ft', 'Standard'),
                ('HKLD', 'Hookload', 'Hookload', 'klb', 'Standard'),
                ('WOB', 'Hookload', 'Weight on Bit', 'klb', 'Standard'),
                ('WEIGHT', 'Hookload', 'Bit Weight', 'klb', 'Standard'),
                ('RPM', 'RPM', 'Rotary Speed', 'rpm', 'Standard'),
                ('SRPM', 'RPM', 'Surface RPM', 'rpm', 'Standard'),
                ('TRPM', 'RPM', 'Total RPM', 'rpm', 'Standard'),
                ('SPPA', 'Pump Pressure', 'Standpipe Pressure', 'psi', 'Standard'),
                ('PRESS', 'Pump Pressure', 'Pump Pressure', 'psi', 'Standard'),
                ('PUMP', 'Pump Pressure', 'Pump Pressure', 'psi', 'Standard'),
                ('TQA', 'Torque', 'Torque Average', 'ft-lbf', 'Standard'),
                ('TORQ', 'Torque', 'Surface Torque', 'ft-lbf', 'Standard'),
                ('FLOW', 'Flow Rate', 'Flow Rate In', 'gpm', 'Standard'),
                ('FLOWIN', 'Flow Rate', 'Flow Rate In', 'gpm', 'Standard'),
                ('ROPA', 'Rate of Penetration', 'ROP Average', 'ft/hr', 'Standard');
            ";
            cmd.ExecuteNonQuery();

            // 6. APP_UNIT_MASTER
            cmd.CommandText = @"
                CREATE TABLE APP_UNIT_MASTER (
                    ID          INTEGER PRIMARY KEY AUTOINCREMENT,
                    UNIT_NAME   TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    CATEGORY    TEXT NOT NULL,
                    DESCRIPTION TEXT,
                    IS_DEFAULT  INTEGER NOT NULL DEFAULT 0
                );

                INSERT INTO APP_UNIT_MASTER (UNIT_NAME, CATEGORY, DESCRIPTION, IS_DEFAULT) VALUES
                -- Length
                ('ft', 'Length', 'Feet', 1),
                ('m', 'Length', 'Meters', 0),
                ('in', 'Length', 'Inches', 0),
                -- Weight / Force
                ('klb', 'Weight', 'Kilopounds force', 1),
                ('daN', 'Weight', 'Decanewtons', 0),
                ('lb', 'Weight', 'Pounds force', 0),
                ('kg', 'Weight', 'Kilograms', 0),
                -- Pressure
                ('psi', 'Pressure', 'Pounds per square inch', 1),
                ('kPa', 'Pressure', 'Kilopascals', 0),
                ('bar', 'Pressure', 'Bars', 0),
                ('MPa', 'Pressure', 'Megapascals', 0),
                -- Temperature
                ('degF', 'Temperature', 'Degrees Fahrenheit', 1),
                ('degC', 'Temperature', 'Degrees Celsius', 0),
                ('K', 'Temperature', 'Kelvin', 0),
                -- Flow Rate
                ('gpm', 'Flow Rate', 'Gallons per minute', 1),
                ('lpm', 'Flow Rate', 'Liters per minute', 0),
                ('m3/hr', 'Flow Rate', 'Cubic meters per hour', 0),
                -- Torque
                ('ft-lbf', 'Torque', 'Foot-pounds force', 1),
                ('kN-m', 'Torque', 'Kilonewton meters', 0),
                ('N-m', 'Torque', 'Newton meters', 0),
                -- Rotary Speed
                ('rpm', 'Rotary Speed', 'Revolutions per minute', 1);

                CREATE VIEW IF NOT EXISTS VMX_UNIT_MASTER AS 
                SELECT * FROM APP_UNIT_MASTER;
            ";
            cmd.ExecuteNonQuery();

            // 7. APP_UNIT_CONVERSIONS
            cmd.CommandText = @"
                CREATE TABLE APP_UNIT_CONVERSIONS (
                    ID               INTEGER PRIMARY KEY AUTOINCREMENT,
                    FROM_UNIT        TEXT NOT NULL COLLATE NOCASE,
                    TO_UNIT          TEXT NOT NULL COLLATE NOCASE,
                    MULTIPLIER       REAL NOT NULL,
                    OFFSET           REAL NOT NULL DEFAULT 0.0,
                    CATEGORY         TEXT NOT NULL,
                    UNIQUE(FROM_UNIT, TO_UNIT)
                );

                INSERT INTO APP_UNIT_CONVERSIONS (FROM_UNIT, TO_UNIT, MULTIPLIER, OFFSET, CATEGORY) VALUES
                -- Length
                ('ft', 'm', 0.3048, 0.0, 'Length'),
                ('m', 'ft', 3.280839895, 0.0, 'Length'),
                -- Weight
                ('klb', 'daN', 444.8221615, 0.0, 'Weight'),
                ('daN', 'klb', 0.002248089, 0.0, 'Weight'),
                ('lb', 'kg', 0.45359237, 0.0, 'Weight'),
                ('kg', 'lb', 2.20462262, 0.0, 'Weight'),
                -- Pressure
                ('psi', 'kPa', 6.89475729, 0.0, 'Pressure'),
                ('kPa', 'psi', 0.145037737, 0.0, 'Pressure'),
                ('psi', 'bar', 0.068947573, 0.0, 'Pressure'),
                ('bar', 'psi', 14.50377377, 0.0, 'Pressure'),
                -- Temperature
                ('degF', 'degC', 0.5555555556, -17.77777778, 'Temperature'),
                ('degC', 'degF', 1.8, 32.0, 'Temperature'),
                -- Flow Rate
                ('gpm', 'lpm', 3.78541178, 0.0, 'Flow Rate'),
                ('lpm', 'gpm', 0.26417205, 0.0, 'Flow Rate'),
                -- Torque
                ('ft-lbf', 'kN-m', 0.001355818, 0.0, 'Torque'),
                ('kN-m', 'ft-lbf', 737.562149, 0.0, 'Torque');
            ";
            cmd.ExecuteNonQuery();

            // 8. APP_BS_GLOBAL_PROFILE
            cmd.CommandText = @"
                CREATE TABLE APP_BS_GLOBAL_PROFILE (
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

                INSERT INTO APP_BS_GLOBAL_PROFILE (
                    ID, NAME, TYPE, NOTES, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE,
                    DOWNSAMPLE_ON, DATA_POINTS, TIME_PERIOD, GROUP_FUNC, SHOW_DEPTH_TRACK, TRACK_WIDTH,
                    FILTER_BY_RANGE, MIN_HKLD, MAX_HKLD, FILTER_BY_INTERVAL, DEPTH_INTERVAL, INTERVAL_WINDOW,
                    POINTS_TO_PLOT, PKUP_PUMP_CHANNEL, PKUP_PUMP_CUTOFF, PKUP_RPM_CUTOFF, PKUP_MAX_MOVEMENT,
                    PKUP_MIN_MOVEMENT, PKUP_PLOT_POINTS, PKUP_STATIC_METHOD, PKUP_DYNAMIC_METHOD, PKUP_LOCAL_MAX,
                    SLK_PUMP_CHANNEL, SLK_PUMP_CUTOFF, SLK_RPM_CUTOFF, SLK_MAX_MOVEMENT, SLK_MIN_MOVEMENT,
                    SLK_PLOT_POINTS, SLK_STATIC_METHOD, SLK_DYNAMIC_METHOD, SLK_LOCAL_MAX,
                    ROT_PUMP_CHANNEL, ROT_PUMP_CUTOFF, ROT_MIN_RPM, ROT_MAX_RPM, ROT_PLOT_POINTS,
                    ROT_CHANGE, ROT_POINTS, ROT_CHECK_PUSO, TIME_THRESHOLD, ENFORCE_PUSO,
                    PKUP_MULTI_METHOD, SLK_MULTI_METHOD, ROB_MULTI_METHOD, SHOW_MULTI,
                    PKUP_RIGSTATES, SLK_RIGSTATES, ROT_RIGSTATES, ENFORCE_RULE, PLOT_ONBOTTOM,
                    IS_DEFAULT, CASING_PKUP_MIN_MOVEMENT, CASING_PKUP_MAX_MOVEMENT,
                    CASING_SLK_MIN_MOVEMENT, CASING_SLK_MAX_MOVEMENT, CASING_PKUP_RIGSTATES, CASING_SLK_RIGSTATES
                ) VALUES (
                    'DEFAULT-BS-PROFILE-001', 'Default Profile', 0, 'Default Broomstick Global Profile',
                    'SYSTEM', datetime('now'), 'SYSTEM', datetime('now'),
                    0, 6, 0, '', 0, 100,
                    0, 0.0, 0.0, 0, 100.0, 10.0,
                    0, 'SPPA', 99.0, 12.0, 70.0,
                    5.0, 0, 0, 0, 0,
                    'SPPA', 99.0, 12.0, 70.0, 5.0,
                    0, 0, 0, 0,
                    'SPPA', 99.0, 12.0, 30.0, 0,
                    1.0, 1.0, 0, 1.0, 0,
                    0, 0, 0, 0,
                    '', '', '', 0, 0,
                    1, 0.0, 0.0,
                    0.0, 0.0, '', ''
                );
            ";
            cmd.ExecuteNonQuery();

            // 9. Backward Compatibility Views
            cmd.CommandText = @"
                CREATE VIEW IF NOT EXISTS VMX_COMMON_RIGSTATE_SETUP AS SELECT * FROM APP_RIGSTATE_COMMON_SETUP;
                CREATE VIEW IF NOT EXISTS VMX_COMMON_RIGSTATE_ITEMS AS SELECT * FROM APP_RIGSTATE_COMMON_ITEMS;
                CREATE VIEW IF NOT EXISTS VMX_BS_GLOBAL_PROFILE AS SELECT * FROM APP_BS_GLOBAL_PROFILE;
            ";
            cmd.ExecuteNonQuery();

            // 9. Pragma optimize & VACUUM
            cmd.CommandText = "VACUUM;";
            cmd.ExecuteNonQuery();
        }

        // Verify file exists on disk and is non-empty
        Assert.True(File.Exists(TargetSeedPath));
        var fileInfo = new FileInfo(TargetSeedPath);
        Assert.True(fileInfo.Length > 0);

        // Verification queries
        using (var verifyConn = new SqliteConnection($"Data Source={TargetSeedPath};Mode=ReadOnly;"))
        {
            verifyConn.Open();
            using var vCmd = verifyConn.CreateCommand();

            vCmd.CommandText = "SELECT COUNT(*) FROM APP_RIGSTATE_COMMON_ITEMS;";
            long itemsCount = (long)vCmd.ExecuteScalar()!;
            Assert.Equal(28, itemsCount);

            vCmd.CommandText = "SELECT COUNT(*) FROM APP_RIGSTATE_COMMON_SETUP;";
            long setupCount = (long)vCmd.ExecuteScalar()!;
            Assert.Equal(1, setupCount);

            vCmd.CommandText = "SELECT COUNT(*) FROM APP_CHANNEL_MAPPING;";
            long channelsCount = (long)vCmd.ExecuteScalar()!;
            Assert.True(channelsCount >= 14);

            vCmd.CommandText = "SELECT COUNT(*) FROM APP_UNIT_MASTER;";
            long unitsCount = (long)vCmd.ExecuteScalar()!;
            Assert.True(unitsCount >= 15);

            vCmd.CommandText = "SELECT COUNT(*) FROM APP_UNIT_CONVERSIONS;";
            long convCount = (long)vCmd.ExecuteScalar()!;
            Assert.True(convCount >= 10);

            vCmd.CommandText = "SELECT COUNT(*) FROM APP_BS_GLOBAL_PROFILE;";
            long bsCount = (long)vCmd.ExecuteScalar()!;
            Assert.True(bsCount >= 1);

            vCmd.CommandText = "SELECT COUNT(*) FROM VMX_BS_GLOBAL_PROFILE;";
            long bsViewCount = (long)vCmd.ExecuteScalar()!;
            Assert.True(bsViewCount >= 1);
        }
    }
}

