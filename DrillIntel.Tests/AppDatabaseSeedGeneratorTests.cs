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

            // 8. Backward Compatibility Views
            cmd.CommandText = @"
                CREATE VIEW IF NOT EXISTS VMX_COMMON_RIGSTATE_SETUP AS SELECT * FROM APP_RIGSTATE_COMMON_SETUP;
                CREATE VIEW IF NOT EXISTS VMX_COMMON_RIGSTATE_ITEMS AS SELECT * FROM APP_RIGSTATE_COMMON_ITEMS;
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
        }
    }
}

