using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using DrillIntel.Data;

namespace DrillIntel.Models
{
    /// <summary>
    /// Represents a unit conversion definition in the Application Database (table APP_UNIT_CONVERSIONS)
    /// or Project Database (table VMX_UNIT_CONVERSIONS).
    /// Provides methods to convert values, manage conversion rules (Add, Edit, Remove, GetList, GetUnitConversion),
    /// and seed default oilfield unit conversion multipliers and offsets.
    /// </summary>
    public class UnitConverter
    {
        public const string TableName = "APP_UNIT_CONVERSIONS";
        public const string ProjectTableName = "APP_UNIT_CONVERSIONS";
        public const string LegacyProjectTableName = "VMX_UNIT_CONVERSIONS";

        /// <summary>
        /// Normalizes table name, ensuring any reference to legacy VMX_UNIT_CONVERSIONS redirects to APP_UNIT_CONVERSIONS.
        /// </summary>
        public static string NormalizeTableName(string? tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName) || string.Equals(tableName, "VMX_UNIT_CONVERSIONS", StringComparison.OrdinalIgnoreCase))
            {
                return TableName;
            }
            return tableName;
        }

        /// <summary>
        /// Optional application-wide default data service for UnitConverter operations when not explicitly passed.
        /// </summary>
        public static IDataServiceDIntel? DefaultDataService { get; set; }

        public int ID { get; set; } = 0;
        public string FromUnit { get; set; } = string.Empty;
        public string ToUnit { get; set; } = string.Empty;
        public double Multiplier { get; set; } = 1.0;
        public double Offset { get; set; } = 0.0;
        public string Category { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public string CreatedDate { get; set; } = string.Empty;
        public string ModifiedBy { get; set; } = string.Empty;
        public string ModifiedDate { get; set; } = string.Empty;

        public UnitConverter()
        {
        }

        public UnitConverter(string fromUnit, string toUnit, double multiplier, double offset = 0.0, string category = "", int id = 0,
                             string createdBy = "", string createdDate = "", string modifiedBy = "", string modifiedDate = "")
        {
            ID = id;
            FromUnit = fromUnit ?? string.Empty;
            ToUnit = toUnit ?? string.Empty;
            Multiplier = multiplier;
            Offset = offset;
            Category = category ?? string.Empty;
            CreatedBy = createdBy ?? string.Empty;
            CreatedDate = createdDate ?? string.Empty;
            ModifiedBy = modifiedBy ?? string.Empty;
            ModifiedDate = modifiedDate ?? string.Empty;
        }

        /// <summary>
        /// Creates a deep copy of this UnitConverter instance.
        /// </summary>
        public UnitConverter GetCopy()
        {
            return new UnitConverter
            {
                ID = this.ID,
                FromUnit = this.FromUnit,
                ToUnit = this.ToUnit,
                Multiplier = this.Multiplier,
                Offset = this.Offset,
                Category = this.Category,
                CreatedBy = this.CreatedBy,
                CreatedDate = this.CreatedDate,
                ModifiedBy = this.ModifiedBy,
                ModifiedDate = this.ModifiedDate
            };
        }

        /// <summary>
        /// Converts the given value using this converter's multiplier and offset: (value * Multiplier) + Offset.
        /// </summary>
        public double Convert(double value) => (value * Multiplier) + Offset;

        public override string ToString() => $"{FromUnit} -> {ToUnit} (* {Multiplier} + {Offset})";

        #region Database Operations

        /// <summary>
        /// Determines whether the given data service points to a project-level database (.dintel / .drint).
        /// Project databases contain schema metadata tables such as VMX_SCHEMA_INFO or VMX_WELL_LIST.
        /// </summary>
        public static bool IsProjectDatabase(IDataServiceDIntel? db)
        {
            if (db == null) return false;
            try
            {
                var result = db.GetValue("SELECT 1 FROM sqlite_master WHERE type='table' AND name IN ('VMX_SCHEMA_INFO', 'VMX_WELL_LIST') LIMIT 1;");
                return result != null && result != DBNull.Value;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Removes all project-level Unit Conversion tables and views (APP_UNIT_CONVERSIONS, VMX_UNIT_CONVERSIONS)
        /// and migrates any unique conversions to the Base Database.
        /// </summary>
        public static void RemoveProjectLevelTables(IDataServiceDIntel? projectDb)
        {
            if (projectDb == null) return;

            try
            {
                var exists = projectDb.GetValue("SELECT 1 FROM sqlite_master WHERE name IN ('APP_UNIT_CONVERSIONS', 'VMX_UNIT_CONVERSIONS') LIMIT 1;");
                if (exists == null || exists == DBNull.Value) return;

                // Migrate any existing conversions to Base Database before dropping
                var baseDb = DefaultDataService ?? BaseDatabaseProvider.GetBaseDataService();
                if (baseDb != null && baseDb != projectDb)
                {
                    string[] sourceTables = { "APP_UNIT_CONVERSIONS", "VMX_UNIT_CONVERSIONS" };
                    foreach (var srcTable in sourceTables)
                    {
                        try
                        {
                            var hasTable = projectDb.GetValue($"SELECT 1 FROM sqlite_master WHERE name='{srcTable}' LIMIT 1;");
                            if (hasTable == null || hasTable == DBNull.Value) continue;

                            var dt = projectDb.GetTable($"SELECT FROM_UNIT, TO_UNIT, MULTIPLIER, OFFSET, CATEGORY FROM {srcTable};");
                            if (dt != null)
                            {
                                EnsureTableExists(baseDb, TableName);
                                foreach (DataRow row in dt.Rows)
                                {
                                    string fromUnit = row["FROM_UNIT"]?.ToString() ?? "";
                                    string toUnit = row["TO_UNIT"]?.ToString() ?? "";
                                    if (!string.IsNullOrWhiteSpace(fromUnit) && !string.IsNullOrWhiteSpace(toUnit))
                                    {
                                        var existing = GetUnitConversion(baseDb, fromUnit, toUnit, TableName);
                                        if (existing == null)
                                        {
                                            Add(baseDb, new UnitConverter(
                                                fromUnit: fromUnit,
                                                toUnit: toUnit,
                                                multiplier: row["MULTIPLIER"] != DBNull.Value ? System.Convert.ToDouble(row["MULTIPLIER"]) : 1.0,
                                                offset: row["OFFSET"] != DBNull.Value ? System.Convert.ToDouble(row["OFFSET"]) : 0.0,
                                                category: row["CATEGORY"]?.ToString() ?? ""
                                            ), TableName);
                                        }
                                    }
                                }
                                dt.Dispose();
                            }
                        }
                        catch { }
                    }
                }

                // Drop triggers, view, and table from project database
                try { projectDb.ExecuteNonQuery("DROP TRIGGER IF EXISTS trg_vmx_unit_conversions_insert;"); } catch { }
                try { projectDb.ExecuteNonQuery("DROP TRIGGER IF EXISTS trg_vmx_unit_conversions_update;"); } catch { }
                try { projectDb.ExecuteNonQuery("DROP TRIGGER IF EXISTS trg_vmx_unit_conversions_delete;"); } catch { }

                try
                {
                    var appType = projectDb.GetValue("SELECT type FROM sqlite_master WHERE name='APP_UNIT_CONVERSIONS' LIMIT 1;")?.ToString();
                    if (string.Equals(appType, "view", StringComparison.OrdinalIgnoreCase))
                        projectDb.ExecuteNonQuery("DROP VIEW IF EXISTS APP_UNIT_CONVERSIONS;");
                    else if (string.Equals(appType, "table", StringComparison.OrdinalIgnoreCase))
                        projectDb.ExecuteNonQuery("DROP TABLE IF EXISTS APP_UNIT_CONVERSIONS;");
                }
                catch { }

                try
                {
                    var vmxType = projectDb.GetValue("SELECT type FROM sqlite_master WHERE name='VMX_UNIT_CONVERSIONS' LIMIT 1;")?.ToString();
                    if (string.Equals(vmxType, "view", StringComparison.OrdinalIgnoreCase))
                        projectDb.ExecuteNonQuery("DROP VIEW IF EXISTS VMX_UNIT_CONVERSIONS;");
                    else if (string.Equals(vmxType, "table", StringComparison.OrdinalIgnoreCase))
                        projectDb.ExecuteNonQuery("DROP TABLE IF EXISTS VMX_UNIT_CONVERSIONS;");
                }
                catch { }
            }
            catch
            {
                // Best-effort
            }
        }

        public static IDataServiceDIntel ResolveDataService(IDataServiceDIntel? db)
        {
            // If db is a project-level database, redirect to Base Database (single source of truth)
            if (db == null || IsProjectDatabase(db))
            {
                var baseDb = DefaultDataService ?? BaseDatabaseProvider.GetBaseDataService();
                if (baseDb != null)
                {
                    return baseDb;
                }
            }

            var service = db ?? DefaultDataService ?? BaseDatabaseProvider.GetBaseDataService();
            if (service == null)
            {
                throw new InvalidOperationException("No database service provided or configured for UnitConverter operations.");
            }
            return service;
        }

        /// <summary>
        /// Ensures the unit conversion table exists in the target database and contains audit columns.
        /// Also redirects any legacy references to VMX_UNIT_CONVERSIONS to APP_UNIT_CONVERSIONS and removes the legacy table.
        /// Project-level databases will have any legacy unit tables/views removed.
        /// </summary>
        public static void EnsureTableExists(IDataServiceDIntel? db = null, string tableName = TableName)
        {
            if (db != null && IsProjectDatabase(db))
            {
                // Project-level database should not contain unit conversion tables
                RemoveProjectLevelTables(db);
                return;
            }

            var dataService = ResolveDataService(db);
            tableName = NormalizeTableName(tableName);

            string sql = $@"
                CREATE TABLE IF NOT EXISTS {tableName} (
                    ID               INTEGER PRIMARY KEY AUTOINCREMENT,
                    FROM_UNIT        TEXT NOT NULL COLLATE NOCASE,
                    TO_UNIT          TEXT NOT NULL COLLATE NOCASE,
                    MULTIPLIER       REAL NOT NULL,
                    OFFSET           REAL NOT NULL DEFAULT 0.0,
                    CATEGORY         TEXT NOT NULL,
                    CREATED_BY       TEXT,
                    CREATED_DATE     TEXT,
                    MODIFIED_BY      TEXT,
                    MODIFIED_DATE    TEXT,
                    UNIQUE(FROM_UNIT, TO_UNIT)
                );";
            dataService.ExecuteNonQuery(sql);

            // Add missing columns if upgrading an older database
            try
            {
                var tableInfo = dataService.GetTable($"PRAGMA table_info({tableName});");
                if (tableInfo != null)
                {
                    var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (DataRow row in tableInfo.Rows)
                    {
                        cols.Add(row["name"]?.ToString() ?? string.Empty);
                    }

                    if (!cols.Contains("CREATED_BY"))
                        dataService.ExecuteNonQuery($"ALTER TABLE {tableName} ADD COLUMN CREATED_BY TEXT;");
                    if (!cols.Contains("CREATED_DATE"))
                        dataService.ExecuteNonQuery($"ALTER TABLE {tableName} ADD COLUMN CREATED_DATE TEXT;");
                    if (!cols.Contains("MODIFIED_BY"))
                        dataService.ExecuteNonQuery($"ALTER TABLE {tableName} ADD COLUMN MODIFIED_BY TEXT;");
                    if (!cols.Contains("MODIFIED_DATE"))
                        dataService.ExecuteNonQuery($"ALTER TABLE {tableName} ADD COLUMN MODIFIED_DATE TEXT;");

                    dataService.ExecuteNonQuery($"UPDATE {tableName} SET CREATED_BY = 'System' WHERE CREATED_BY IS NULL OR CREATED_BY = '';");
                    dataService.ExecuteNonQuery($"UPDATE {tableName} SET CREATED_DATE = datetime('now','localtime') WHERE CREATED_DATE IS NULL OR CREATED_DATE = '';");
                }
            }
            catch
            {
                // Best-effort
            }

            // Ensure backward compatibility: redirect VMX_UNIT_CONVERSIONS to APP_UNIT_CONVERSIONS and remove the legacy table
            try
            {
                var vmxCheck = dataService.GetValue("SELECT type FROM sqlite_master WHERE name = 'VMX_UNIT_CONVERSIONS';");
                if (vmxCheck != null && string.Equals(vmxCheck.ToString(), "table", StringComparison.OrdinalIgnoreCase))
                {
                    dataService.ExecuteNonQuery($@"
                        INSERT OR IGNORE INTO {tableName} (FROM_UNIT, TO_UNIT, MULTIPLIER, OFFSET, CATEGORY, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE)
                        SELECT FROM_UNIT, TO_UNIT, MULTIPLIER, OFFSET, CATEGORY, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE
                        FROM VMX_UNIT_CONVERSIONS;
                    ");
                    dataService.ExecuteNonQuery("DROP TABLE IF EXISTS VMX_UNIT_CONVERSIONS;");
                }

                dataService.ExecuteNonQuery($@"
                    CREATE VIEW IF NOT EXISTS VMX_UNIT_CONVERSIONS AS 
                    SELECT ID, FROM_UNIT, TO_UNIT, MULTIPLIER, OFFSET, CATEGORY, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE 
                    FROM {tableName};

                    CREATE TRIGGER IF NOT EXISTS trg_vmx_unit_conversions_insert
                    INSTEAD OF INSERT ON VMX_UNIT_CONVERSIONS
                    BEGIN
                        INSERT INTO {tableName} (FROM_UNIT, TO_UNIT, MULTIPLIER, OFFSET, CATEGORY, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE)
                        VALUES (NEW.FROM_UNIT, NEW.TO_UNIT, NEW.MULTIPLIER, NEW.OFFSET, NEW.CATEGORY, NEW.CREATED_BY, NEW.CREATED_DATE, NEW.MODIFIED_BY, NEW.MODIFIED_DATE);
                    END;

                    CREATE TRIGGER IF NOT EXISTS trg_vmx_unit_conversions_update
                    INSTEAD OF UPDATE ON VMX_UNIT_CONVERSIONS
                    BEGIN
                        UPDATE {tableName} SET
                            FROM_UNIT = NEW.FROM_UNIT,
                            TO_UNIT = NEW.TO_UNIT,
                            MULTIPLIER = NEW.MULTIPLIER,
                            OFFSET = NEW.OFFSET,
                            CATEGORY = NEW.CATEGORY,
                            MODIFIED_BY = NEW.MODIFIED_BY,
                            MODIFIED_DATE = NEW.MODIFIED_DATE
                        WHERE ID = OLD.ID OR (OLD.ID IS NULL AND FROM_UNIT = OLD.FROM_UNIT AND TO_UNIT = OLD.TO_UNIT);
                    END;

                    CREATE TRIGGER IF NOT EXISTS trg_vmx_unit_conversions_delete
                    INSTEAD OF DELETE ON VMX_UNIT_CONVERSIONS
                    BEGIN
                        DELETE FROM {tableName} WHERE ID = OLD.ID OR (FROM_UNIT = OLD.FROM_UNIT AND TO_UNIT = OLD.TO_UNIT);
                    END;
                ");
            }
            catch
            {
                // Best-effort
            }
        }

        /// <summary>
        /// Retrieves all registered unit conversions, optionally filtered by category.
        /// </summary>
        public static List<UnitConverter> GetList(IDataServiceDIntel? db = null, string? category = null, string tableName = TableName)
        {
            try
            {
                var dataService = ResolveDataService(db);
                tableName = NormalizeTableName(tableName);
                EnsureTableExists(dataService, tableName);

                string sql;
                Dictionary<string, object?>? parameters = null;

                if (string.IsNullOrWhiteSpace(category) || category == "All Categories")
                {
                    sql = $"SELECT ID, FROM_UNIT, TO_UNIT, MULTIPLIER, OFFSET, CATEGORY, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE FROM {tableName} ORDER BY CATEGORY ASC, FROM_UNIT ASC, TO_UNIT ASC;";
                }
                else
                {
                    sql = $"SELECT ID, FROM_UNIT, TO_UNIT, MULTIPLIER, OFFSET, CATEGORY, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE FROM {tableName} WHERE CATEGORY = @Category ORDER BY FROM_UNIT ASC, TO_UNIT ASC;";
                    parameters = new Dictionary<string, object?>
                    {
                        { "@Category", category.Trim() }
                    };
                }

                DataTable dt = dataService.GetTable(sql, parameters);
                var list = new List<UnitConverter>();

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        list.Add(MapFromRow(row));
                    }
                }

                return list;
            }
            catch (Exception)
            {
                return new List<UnitConverter>();
            }
        }

        /// <summary>
        /// Looks up a unit conversion rule by ID.
        /// </summary>
        public static UnitConverter? GetById(IDataServiceDIntel? db, int id, string tableName = TableName)
        {
            if (id <= 0) return null;

            try
            {
                var dataService = ResolveDataService(db);
                tableName = NormalizeTableName(tableName);
                EnsureTableExists(dataService, tableName);

                string sql = $"SELECT ID, FROM_UNIT, TO_UNIT, MULTIPLIER, OFFSET, CATEGORY, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE FROM {tableName} WHERE ID = @ID LIMIT 1;";
                var parameters = new Dictionary<string, object?>
                {
                    { "@ID", id }
                };

                DataTable dt = dataService.GetTable(sql, parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapFromRow(dt.Rows[0]);
                }
            }
            catch
            {
                // Fallback
            }

            return null;
        }

        /// <summary>
        /// Looks up a unit conversion rule between fromUnit and toUnit.
        /// Supports direct lookup, mathematical inversion (if reverse is stored), and 2-step bridging via common intermediate units.
        /// </summary>
        public static UnitConverter? GetUnitConversion(IDataServiceDIntel? db, string fromUnit, string toUnit, string tableName = TableName)
        {
            if (string.IsNullOrWhiteSpace(fromUnit) || string.IsNullOrWhiteSpace(toUnit))
                return null;

            string from = fromUnit.Trim();
            string to = toUnit.Trim();

            // 1. Same unit -> Identity conversion
            if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            {
                return new UnitConverter
                {
                    FromUnit = from,
                    ToUnit = to,
                    Multiplier = 1.0,
                    Offset = 0.0,
                    Category = string.Empty
                };
            }

            try
            {
                var dataService = ResolveDataService(db);
                tableName = NormalizeTableName(tableName);
                EnsureTableExists(dataService, tableName);

                // 2. Direct lookup: FROM_UNIT -> TO_UNIT
                string directSql = $"SELECT ID, FROM_UNIT, TO_UNIT, MULTIPLIER, OFFSET, CATEGORY, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE FROM {tableName} WHERE FROM_UNIT = @FromUnit AND TO_UNIT = @ToUnit LIMIT 1;";
                var directParams = new Dictionary<string, object?>
                {
                    { "@FromUnit", from },
                    { "@ToUnit", to }
                };

                DataTable directDt = dataService.GetTable(directSql, directParams);
                if (directDt != null && directDt.Rows.Count > 0)
                {
                    return MapFromRow(directDt.Rows[0]);
                }

                // 3. Inverse lookup: if TO_UNIT -> FROM_UNIT exists: B = (A * M) + O => A = (B - O) / M = B * (1/M) - (O/M)
                string inverseSql = $"SELECT ID, FROM_UNIT, TO_UNIT, MULTIPLIER, OFFSET, CATEGORY, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE FROM {tableName} WHERE FROM_UNIT = @ToUnit AND TO_UNIT = @FromUnit LIMIT 1;";
                var inverseParams = new Dictionary<string, object?>
                {
                    { "@ToUnit", to },
                    { "@FromUnit", from }
                };

                DataTable inverseDt = dataService.GetTable(inverseSql, inverseParams);
                if (inverseDt != null && inverseDt.Rows.Count > 0)
                {
                    var rev = MapFromRow(inverseDt.Rows[0]);
                    if (Math.Abs(rev.Multiplier) > 1e-12)
                    {
                        return new UnitConverter
                        {
                            ID = 0,
                            FromUnit = from,
                            ToUnit = to,
                            Multiplier = 1.0 / rev.Multiplier,
                            Offset = -rev.Offset / rev.Multiplier,
                            Category = rev.Category
                        };
                    }
                }

                // 4. Two-step bridge lookup (e.g. from -> intermediate, intermediate -> to)
                string bridgeSql = $@"
                    SELECT c1.MULTIPLIER as M1, c1.OFFSET as O1, c1.CATEGORY as CAT,
                           c2.MULTIPLIER as M2, c2.OFFSET as O2
                    FROM {tableName} c1
                    JOIN {tableName} c2 ON c1.TO_UNIT = c2.FROM_UNIT
                    WHERE c1.FROM_UNIT = @FromUnit AND c2.TO_UNIT = @ToUnit
                    LIMIT 1;";

                DataTable bridgeDt = dataService.GetTable(bridgeSql, directParams);
                if (bridgeDt != null && bridgeDt.Rows.Count > 0)
                {
                    DataRow brow = bridgeDt.Rows[0];
                    double m1 = CheckNull(brow["M1"], 1.0);
                    double o1 = CheckNull(brow["O1"], 0.0);
                    double m2 = CheckNull(brow["M2"], 1.0);
                    double o2 = CheckNull(brow["O2"], 0.0);
                    string cat = CheckNull(brow["CAT"], string.Empty);

                    // Combined: To = (From * M1 + O1) * M2 + O2 = From * (M1 * M2) + (O1 * M2 + O2)
                    return new UnitConverter
                    {
                        ID = 0,
                        FromUnit = from,
                        ToUnit = to,
                        Multiplier = m1 * m2,
                        Offset = (o1 * m2) + o2,
                        Category = cat
                    };
                }
            }
            catch (Exception)
            {
                // Fallback
            }

            return null;
        }

        public static UnitConverter? GetUnitConversion(string fromUnit, string toUnit) => GetUnitConversion(null, fromUnit, toUnit, TableName);

        /// <summary>
        /// Adds a unit conversion rule. Replaces if from/to combination already exists.
        /// </summary>
        public static bool Add(IDataServiceDIntel? db, UnitConverter conversion, string tableName = TableName)
        {
            if (conversion == null || string.IsNullOrWhiteSpace(conversion.FromUnit) || string.IsNullOrWhiteSpace(conversion.ToUnit))
                return false;

            try
            {
                var dataService = ResolveDataService(db);
                tableName = NormalizeTableName(tableName);
                EnsureTableExists(dataService, tableName);

                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string user = Environment.UserName;

                string sql = $@"
                    INSERT OR REPLACE INTO {tableName} (FROM_UNIT, TO_UNIT, MULTIPLIER, OFFSET, CATEGORY, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE)
                    VALUES (@FromUnit, @ToUnit, @Multiplier, @Offset, @Category, @CreatedBy, @CreatedDate, @ModifiedBy, @ModifiedDate);";

                var parameters = new Dictionary<string, object?>
                {
                    { "@FromUnit", conversion.FromUnit.Trim() },
                    { "@ToUnit", conversion.ToUnit.Trim() },
                    { "@Multiplier", conversion.Multiplier },
                    { "@Offset", conversion.Offset },
                    { "@Category", conversion.Category?.Trim() ?? string.Empty },
                    { "@CreatedBy", !string.IsNullOrWhiteSpace(conversion.CreatedBy) ? conversion.CreatedBy : user },
                    { "@CreatedDate", !string.IsNullOrWhiteSpace(conversion.CreatedDate) ? conversion.CreatedDate : now },
                    { "@ModifiedBy", !string.IsNullOrWhiteSpace(conversion.ModifiedBy) ? conversion.ModifiedBy : user },
                    { "@ModifiedDate", !string.IsNullOrWhiteSpace(conversion.ModifiedDate) ? conversion.ModifiedDate : now }
                };

                bool success = dataService.ExecuteNonQuery(sql, parameters);
                if (success)
                {
                    object? rowId = dataService.GetValue("SELECT last_insert_rowid();");
                    if (rowId != null && int.TryParse(rowId.ToString(), out int id))
                    {
                        conversion.ID = id;
                    }
                }

                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool Add(UnitConverter conversion) => Add(null, conversion, TableName);

        /// <summary>
        /// Edits an existing unit conversion rule.
        /// </summary>
        public static bool Edit(IDataServiceDIntel? db, UnitConverter conversion, string tableName = TableName)
        {
            if (conversion == null || string.IsNullOrWhiteSpace(conversion.FromUnit) || string.IsNullOrWhiteSpace(conversion.ToUnit))
                return false;

            try
            {
                var dataService = ResolveDataService(db);
                tableName = NormalizeTableName(tableName);
                EnsureTableExists(dataService, tableName);

                string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string user = Environment.UserName;

                string sql;
                Dictionary<string, object?> parameters;

                if (conversion.ID > 0)
                {
                    sql = $@"
                        UPDATE {tableName}
                        SET FROM_UNIT = @FromUnit, TO_UNIT = @ToUnit, MULTIPLIER = @Multiplier, OFFSET = @Offset, CATEGORY = @Category,
                            MODIFIED_BY = @ModifiedBy, MODIFIED_DATE = @ModifiedDate
                        WHERE ID = @ID;";

                    parameters = new Dictionary<string, object?>
                    {
                        { "@ID", conversion.ID },
                        { "@FromUnit", conversion.FromUnit.Trim() },
                        { "@ToUnit", conversion.ToUnit.Trim() },
                        { "@Multiplier", conversion.Multiplier },
                        { "@Offset", conversion.Offset },
                        { "@Category", conversion.Category?.Trim() ?? string.Empty },
                        { "@ModifiedBy", !string.IsNullOrWhiteSpace(conversion.ModifiedBy) ? conversion.ModifiedBy : user },
                        { "@ModifiedDate", now }
                    };
                }
                else
                {
                    sql = $@"
                        UPDATE {tableName}
                        SET MULTIPLIER = @Multiplier, OFFSET = @Offset, CATEGORY = @Category,
                            MODIFIED_BY = @ModifiedBy, MODIFIED_DATE = @ModifiedDate
                        WHERE FROM_UNIT = @FromUnit AND TO_UNIT = @ToUnit;";

                    parameters = new Dictionary<string, object?>
                    {
                        { "@FromUnit", conversion.FromUnit.Trim() },
                        { "@ToUnit", conversion.ToUnit.Trim() },
                        { "@Multiplier", conversion.Multiplier },
                        { "@Offset", conversion.Offset },
                        { "@Category", conversion.Category?.Trim() ?? string.Empty },
                        { "@ModifiedBy", !string.IsNullOrWhiteSpace(conversion.ModifiedBy) ? conversion.ModifiedBy : user },
                        { "@ModifiedDate", now }
                    };
                }

                return dataService.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool Edit(UnitConverter conversion) => Edit(null, conversion, TableName);

        /// <summary>
        /// Removes a conversion rule by its ID.
        /// </summary>
        public static bool Remove(IDataServiceDIntel? db, int id, string tableName = TableName)
        {
            if (id <= 0) return false;

            try
            {
                var dataService = ResolveDataService(db);
                tableName = NormalizeTableName(tableName);
                EnsureTableExists(dataService, tableName);

                string sql = $"DELETE FROM {tableName} WHERE ID = @ID;";
                var parameters = new Dictionary<string, object?>
                {
                    { "@ID", id }
                };

                return dataService.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool Remove(int id) => Remove(null, id, TableName);

        /// <summary>
        /// Removes a conversion rule by its (fromUnit, toUnit) pair.
        /// </summary>
        public static bool Remove(IDataServiceDIntel? db, string fromUnit, string toUnit, string tableName = TableName)
        {
            if (string.IsNullOrWhiteSpace(fromUnit) || string.IsNullOrWhiteSpace(toUnit))
                return false;

            try
            {
                var dataService = ResolveDataService(db);
                tableName = NormalizeTableName(tableName);
                EnsureTableExists(dataService, tableName);

                string sql = $"DELETE FROM {tableName} WHERE FROM_UNIT = @FromUnit AND TO_UNIT = @ToUnit;";
                var parameters = new Dictionary<string, object?>
                {
                    { "@FromUnit", fromUnit.Trim() },
                    { "@ToUnit", toUnit.Trim() }
                };

                return dataService.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool Remove(string fromUnit, string toUnit) => Remove(null, fromUnit, toUnit, TableName);

        public static bool Delete(IDataServiceDIntel? db, int id, string tableName = TableName) => Remove(db, id, tableName);
        public static bool Delete(int id) => Remove(null, id, TableName);
        public static bool Delete(IDataServiceDIntel? db, string fromUnit, string toUnit, string tableName = TableName) => Remove(db, fromUnit, toUnit, tableName);
        public static bool Delete(string fromUnit, string toUnit) => Remove(null, fromUnit, toUnit, TableName);

        /// <summary>
        /// Converts a numerical value from one unit to another using registered conversion rules.
        /// If fromUnit and toUnit match or no conversion rule is found, the original value is returned.
        /// </summary>
        public static double Convert(double value, string fromUnit, string toUnit, IDataServiceDIntel? db = null, string tableName = TableName)
        {
            if (string.IsNullOrWhiteSpace(fromUnit) || string.IsNullOrWhiteSpace(toUnit) ||
                string.Equals(fromUnit.Trim(), toUnit.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }

            tableName = NormalizeTableName(tableName);
            var conv = GetUnitConversion(db, fromUnit, toUnit, tableName);
            if (conv != null)
            {
                return (value * conv.Multiplier) + conv.Offset;
            }

            return value;
        }

        /// <summary>
        /// Attempts to convert a numerical value from one unit to another.
        /// Returns true if converted successfully (or if units are identical), false otherwise.
        /// </summary>
        public static bool TryConvert(double value, string fromUnit, string toUnit, out double result, IDataServiceDIntel? db = null, string tableName = TableName)
        {
            if (string.IsNullOrWhiteSpace(fromUnit) || string.IsNullOrWhiteSpace(toUnit) ||
                string.Equals(fromUnit.Trim(), toUnit.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                result = value;
                return true;
            }

            tableName = NormalizeTableName(tableName);
            var conv = GetUnitConversion(db, fromUnit, toUnit, tableName);
            if (conv != null)
            {
                result = (value * conv.Multiplier) + conv.Offset;
                return true;
            }

            result = value;
            return false;
        }

        /// <summary>
        /// Seeds standard oilfield unit conversion multipliers and offsets into the specified table.
        /// </summary>
        public static void CreateDefaulConversion(IDataServiceDIntel? db = null) => CreateDefaultConversion(db, TableName);

        /// <summary>
        /// Seeds standard oilfield unit conversion multipliers and offsets into the specified table.
        /// </summary>
        public static void CreateDefaultConversion(IDataServiceDIntel? db = null, string tableName = TableName)
        {
            var dataService = ResolveDataService(db);
            tableName = NormalizeTableName(tableName);
            EnsureTableExists(dataService, tableName);

            var defaultConversions = new (string From, string To, double Multiplier, double Offset, string Category)[]
            {
                // Length
                ("ft", "m", 0.3048, 0.0, "Length"),
                ("m", "ft", 3.280839895, 0.0, "Length"),
                ("ft", "in", 12.0, 0.0, "Length"),
                ("in", "ft", 0.0833333333, 0.0, "Length"),

                // Weight / Force
                ("klb", "daN", 444.8221615, 0.0, "Weight"),
                ("daN", "klb", 0.002248089, 0.0, "Weight"),
                ("klb", "lb", 1000.0, 0.0, "Weight"),
                ("lb", "klb", 0.001, 0.0, "Weight"),
                ("lb", "kg", 0.45359237, 0.0, "Weight"),
                ("kg", "lb", 2.20462262, 0.0, "Weight"),
                ("klb", "kg", 453.59237, 0.0, "Weight"),
                ("kg", "klb", 0.00220462, 0.0, "Weight"),

                // Pressure
                ("psi", "kPa", 6.89475729, 0.0, "Pressure"),
                ("kPa", "psi", 0.145037737, 0.0, "Pressure"),
                ("psi", "bar", 0.068947573, 0.0, "Pressure"),
                ("bar", "psi", 14.50377377, 0.0, "Pressure"),
                ("psi", "MPa", 0.006894757, 0.0, "Pressure"),
                ("MPa", "psi", 145.037738, 0.0, "Pressure"),

                // Temperature
                // C = (F - 32) * 5/9 = F * (5/9) - (160/9)
                ("degF", "degC", 5.0 / 9.0, -160.0 / 9.0, "Temperature"),
                // F = C * 1.8 + 32
                ("degC", "degF", 1.8, 32.0, "Temperature"),
                // K = C + 273.15
                ("degC", "K", 1.0, 273.15, "Temperature"),
                ("K", "degC", 1.0, -273.15, "Temperature"),

                // Flow Rate
                ("gpm", "lpm", 3.78541178, 0.0, "Flow Rate"),
                ("lpm", "gpm", 0.26417205, 0.0, "Flow Rate"),
                ("gpm", "m3/hr", 0.227124707, 0.0, "Flow Rate"),
                ("m3/hr", "gpm", 4.40286754, 0.0, "Flow Rate"),

                // Torque
                ("ft-lbf", "kN-m", 0.001355818, 0.0, "Torque"),
                ("kN-m", "ft-lbf", 737.562149, 0.0, "Torque"),
                ("ft-lbf", "N-m", 1.355818, 0.0, "Torque"),
                ("N-m", "ft-lbf", 0.737562, 0.0, "Torque"),

                // Rate of Penetration
                ("ft/hr", "m/hr", 0.3048, 0.0, "Rate of Penetration"),
                ("m/hr", "ft/hr", 3.280839895, 0.0, "Rate of Penetration")
            };

            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string user = "System";

            string sql = $@"
                INSERT OR IGNORE INTO {tableName} (FROM_UNIT, TO_UNIT, MULTIPLIER, OFFSET, CATEGORY, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE)
                VALUES (@FromUnit, @ToUnit, @Multiplier, @Offset, @Category, @CreatedBy, @CreatedDate, @ModifiedBy, @ModifiedDate);";

            foreach (var item in defaultConversions)
            {
                var parameters = new Dictionary<string, object?>
                {
                    { "@FromUnit", item.From },
                    { "@ToUnit", item.To },
                    { "@Multiplier", item.Multiplier },
                    { "@Offset", item.Offset },
                    { "@Category", item.Category },
                    { "@CreatedBy", user },
                    { "@CreatedDate", now },
                    { "@ModifiedBy", user },
                    { "@ModifiedDate", now }
                };

                dataService.ExecuteNonQuery(sql, parameters);
            }
        }

        /// <summary>
        /// Saves this conversion rule (calls Add if ID == 0, Edit if ID > 0).
        /// </summary>
        public bool Save(IDataServiceDIntel? db = null, string tableName = TableName)
        {
            tableName = NormalizeTableName(tableName);
            return ID <= 0 ? Add(db, this, tableName) : Edit(db, this, tableName);
        }

        /// <summary>
        /// Removes this conversion rule from the database.
        /// </summary>
        public bool RemoveSelf(IDataServiceDIntel? db = null, string tableName = TableName)
        {
            tableName = NormalizeTableName(tableName);
            if (ID > 0) return Remove(db, ID, tableName);
            if (!string.IsNullOrWhiteSpace(FromUnit) && !string.IsNullOrWhiteSpace(ToUnit))
                return Remove(db, FromUnit, ToUnit, tableName);
            return false;
        }

        private static UnitConverter MapFromRow(DataRow row)
        {
            return new UnitConverter
            {
                ID = CheckNull(row["ID"], 0),
                FromUnit = CheckNull(row["FROM_UNIT"], string.Empty),
                ToUnit = CheckNull(row["TO_UNIT"], string.Empty),
                Multiplier = CheckNull(row["MULTIPLIER"], 1.0),
                Offset = CheckNull(row["OFFSET"], 0.0),
                Category = CheckNull(row["CATEGORY"], string.Empty),
                CreatedBy = row.Table.Columns.Contains("CREATED_BY") ? CheckNull(row["CREATED_BY"], string.Empty) : string.Empty,
                CreatedDate = row.Table.Columns.Contains("CREATED_DATE") ? CheckNull(row["CREATED_DATE"], string.Empty) : string.Empty,
                ModifiedBy = row.Table.Columns.Contains("MODIFIED_BY") ? CheckNull(row["MODIFIED_BY"], string.Empty) : string.Empty,
                ModifiedDate = row.Table.Columns.Contains("MODIFIED_DATE") ? CheckNull(row["MODIFIED_DATE"], string.Empty) : string.Empty
            };
        }

        private static string CheckNull(object? value, string defaultValue)
        {
            if (value == null || value == DBNull.Value) return defaultValue;
            return value.ToString() ?? defaultValue;
        }

        private static double CheckNull(object? value, double defaultValue)
        {
            if (value == null || value == DBNull.Value) return defaultValue;
            if (double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                return d;
            return defaultValue;
        }

        private static int CheckNull(object? value, int defaultValue)
        {
            if (value == null || value == DBNull.Value) return defaultValue;
            if (int.TryParse(value.ToString(), out int i)) return i;
            return defaultValue;
        }

        #endregion
    }
}

