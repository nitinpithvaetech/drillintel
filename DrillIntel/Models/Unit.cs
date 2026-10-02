using System;
using System.Collections.Generic;
using System.Data;
using DrillIntel.Data;

namespace DrillIntel.Models
{
    /// <summary>
    /// Represents a measurement unit registered in the Application Database (table APP_UNIT_MASTER)
    /// or Project Database (table VMX_UNIT_MASTER).
    /// Provides CRUD operations, category filtering, and default unit initialization.
    /// </summary>
    public class Unit
    {
        public const string TableName = "APP_UNIT_MASTER";
        public const string ProjectTableName = "VMX_UNIT_MASTER";

        /// <summary>
        /// Optional application-wide default data service for Unit operations when not explicitly passed.
        /// </summary>
        public static IDataServiceDIntel? DefaultDataService { get; set; }

        public int ID { get; set; } = 0;
        public string UnitName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsDefault { get; set; } = false;
        public string CreatedBy { get; set; } = string.Empty;
        public string CreatedDate { get; set; } = string.Empty;
        public string ModifiedBy { get; set; } = string.Empty;
        public string ModifiedDate { get; set; } = string.Empty;

        public Unit()
        {
        }

        public Unit(string unitName, string category, string description = "", bool isDefault = false, int id = 0,
                    string createdBy = "", string createdDate = "", string modifiedBy = "", string modifiedDate = "")
        {
            ID = id;
            UnitName = unitName ?? string.Empty;
            Category = category ?? string.Empty;
            Description = description ?? string.Empty;
            IsDefault = isDefault;
            CreatedBy = createdBy ?? string.Empty;
            CreatedDate = createdDate ?? string.Empty;
            ModifiedBy = modifiedBy ?? string.Empty;
            ModifiedDate = modifiedDate ?? string.Empty;
        }

        /// <summary>
        /// Creates a deep copy of this Unit instance.
        /// </summary>
        public Unit GetCopy()
        {
            return new Unit
            {
                ID = this.ID,
                UnitName = this.UnitName,
                Category = this.Category,
                Description = this.Description,
                IsDefault = this.IsDefault,
                CreatedBy = this.CreatedBy,
                CreatedDate = this.CreatedDate,
                ModifiedBy = this.ModifiedBy,
                ModifiedDate = this.ModifiedDate
            };
        }

        public override string ToString() => UnitName;

        #region Database Operations

        private static IDataServiceDIntel ResolveDataService(IDataServiceDIntel? db)
        {
            var service = db ?? DefaultDataService;
            if (service == null)
            {
                throw new InvalidOperationException("No database service provided or configured for Unit operations.");
            }
            return service;
        }

        /// <summary>
        /// Ensures the unit master table exists in the target database and contains audit columns.
        /// </summary>
        public static void EnsureTableExists(IDataServiceDIntel? db = null, string tableName = TableName)
        {
            var dataService = ResolveDataService(db);
            string sql = $@"
                CREATE TABLE IF NOT EXISTS {tableName} (
                    ID            INTEGER PRIMARY KEY AUTOINCREMENT,
                    UNIT_NAME     TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    CATEGORY      TEXT NOT NULL,
                    DESCRIPTION   TEXT,
                    IS_DEFAULT    INTEGER NOT NULL DEFAULT 0,
                    CREATED_BY    TEXT,
                    CREATED_DATE  TEXT,
                    MODIFIED_BY   TEXT,
                    MODIFIED_DATE TEXT
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
        }

        /// <summary>
        /// Retrieves all registered units, optionally filtered by category.
        /// </summary>
        public static List<Unit> GetList(IDataServiceDIntel? db = null, string? category = null, string tableName = TableName)
        {
            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService, tableName);

                string sql;
                Dictionary<string, object?>? parameters = null;

                if (string.IsNullOrWhiteSpace(category))
                {
                    sql = $"SELECT ID, UNIT_NAME, CATEGORY, DESCRIPTION, IS_DEFAULT, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE FROM {tableName} ORDER BY CATEGORY ASC, IS_DEFAULT DESC, UNIT_NAME ASC;";
                }
                else
                {
                    sql = $"SELECT ID, UNIT_NAME, CATEGORY, DESCRIPTION, IS_DEFAULT, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE FROM {tableName} WHERE CATEGORY = @Category ORDER BY IS_DEFAULT DESC, UNIT_NAME ASC;";
                    parameters = new Dictionary<string, object?>
                    {
                        { "@Category", category.Trim() }
                    };
                }

                DataTable dt = dataService.GetTable(sql, parameters);
                var list = new List<Unit>();

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
                return new List<Unit>();
            }
        }

        /// <summary>
        /// Retrieves a unit by its unique unit name (case-insensitive).
        /// </summary>
        public static Unit? GetUnit(IDataServiceDIntel? db, string unitName, string tableName = TableName)
        {
            if (string.IsNullOrWhiteSpace(unitName)) return null;

            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService, tableName);

                string sql = $"SELECT ID, UNIT_NAME, CATEGORY, DESCRIPTION, IS_DEFAULT, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE FROM {tableName} WHERE UNIT_NAME = @UnitName LIMIT 1;";
                var parameters = new Dictionary<string, object?>
                {
                    { "@UnitName", unitName.Trim() }
                };

                DataTable dt = dataService.GetTable(sql, parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapFromRow(dt.Rows[0]);
                }
            }
            catch (Exception)
            {
                // Fallback
            }

            return null;
        }

        public static Unit? GetUnit(string unitName) => GetUnit(null, unitName, TableName);

        /// <summary>
        /// Retrieves a unit by its primary key ID.
        /// </summary>
        public static Unit? GetById(IDataServiceDIntel? db, int id, string tableName = TableName)
        {
            if (id <= 0) return null;

            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService, tableName);

                string sql = $"SELECT ID, UNIT_NAME, CATEGORY, DESCRIPTION, IS_DEFAULT, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE FROM {tableName} WHERE ID = @ID LIMIT 1;";
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
            catch (Exception)
            {
                // Fallback
            }

            return null;
        }

        public static Unit? GetById(int id) => GetById(null, id, TableName);

        /// <summary>
        /// Retrieves the default unit for a given category.
        /// </summary>
        public static Unit? GetDefaultUnitForCategory(IDataServiceDIntel? db, string category, string tableName = TableName)
        {
            if (string.IsNullOrWhiteSpace(category)) return null;

            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService, tableName);

                string sql = $"SELECT ID, UNIT_NAME, CATEGORY, DESCRIPTION, IS_DEFAULT, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE FROM {tableName} WHERE CATEGORY = @Category AND IS_DEFAULT = 1 LIMIT 1;";
                var parameters = new Dictionary<string, object?>
                {
                    { "@Category", category.Trim() }
                };

                DataTable dt = dataService.GetTable(sql, parameters);
                if (dt != null && dt.Rows.Count > 0)
                {
                    return MapFromRow(dt.Rows[0]);
                }
            }
            catch (Exception)
            {
                // Fallback
            }

            return null;
        }

        public static Unit? GetDefaultUnitForCategory(string category) => GetDefaultUnitForCategory(null, category, TableName);

        /// <summary>
        /// Retrieves all distinct unit categories registered in the database.
        /// </summary>
        public static List<string> GetCategories(IDataServiceDIntel? db = null, string tableName = TableName)
        {
            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService, tableName);

                string sql = $"SELECT DISTINCT CATEGORY FROM {tableName} WHERE CATEGORY IS NOT NULL AND CATEGORY <> '' ORDER BY CATEGORY ASC;";
                DataTable dt = dataService.GetTable(sql);
                var list = new List<string>();

                if (dt != null)
                {
                    foreach (DataRow row in dt.Rows)
                    {
                        string cat = CheckNull(row["CATEGORY"], string.Empty);
                        if (!string.IsNullOrWhiteSpace(cat))
                        {
                            list.Add(cat);
                        }
                    }
                }

                return list;
            }
            catch (Exception)
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Adds a new unit to the target table. If IsDefault is true, unsets any existing default in the same category.
        /// </summary>
        public static bool Add(IDataServiceDIntel? db, Unit unit, string tableName = TableName)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.UnitName) || string.IsNullOrWhiteSpace(unit.Category))
                return false;

            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService, tableName);

                if (unit.IsDefault)
                {
                    var clearDefParams = new Dictionary<string, object?>
                    {
                        { "@Category", unit.Category.Trim() }
                    };
                    dataService.ExecuteNonQuery($"UPDATE {tableName} SET IS_DEFAULT = 0 WHERE CATEGORY = @Category;", clearDefParams);
                }

                if (string.IsNullOrWhiteSpace(unit.CreatedBy))
                    unit.CreatedBy = Environment.UserName;
                if (string.IsNullOrWhiteSpace(unit.CreatedDate))
                    unit.CreatedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                if (string.IsNullOrWhiteSpace(unit.ModifiedBy))
                    unit.ModifiedBy = unit.CreatedBy;
                if (string.IsNullOrWhiteSpace(unit.ModifiedDate))
                    unit.ModifiedDate = unit.CreatedDate;

                string insertSql = $@"
                    INSERT INTO {tableName} (UNIT_NAME, CATEGORY, DESCRIPTION, IS_DEFAULT, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE)
                    VALUES (@UnitName, @Category, @Description, @IsDefault, @CreatedBy, @CreatedDate, @ModifiedBy, @ModifiedDate);";

                var parameters = new Dictionary<string, object?>
                {
                    { "@UnitName", unit.UnitName.Trim() },
                    { "@Category", unit.Category.Trim() },
                    { "@Description", unit.Description ?? string.Empty },
                    { "@IsDefault", unit.IsDefault ? 1 : 0 },
                    { "@CreatedBy", unit.CreatedBy },
                    { "@CreatedDate", unit.CreatedDate },
                    { "@ModifiedBy", unit.ModifiedBy },
                    { "@ModifiedDate", unit.ModifiedDate }
                };

                bool success = dataService.ExecuteNonQuery(insertSql, parameters);
                if (success)
                {
                    object? rowId = dataService.GetValue("SELECT last_insert_rowid();");
                    if (rowId != null && int.TryParse(rowId.ToString(), out int id))
                    {
                        unit.ID = id;
                    }
                }

                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool Add(Unit unit) => Add(null, unit, TableName);

        /// <summary>
        /// Edits an existing unit in the target table.
        /// </summary>
        public static bool Edit(IDataServiceDIntel? db, Unit unit, string tableName = TableName)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.UnitName) || string.IsNullOrWhiteSpace(unit.Category))
                return false;

            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService, tableName);

                if (unit.IsDefault)
                {
                    var clearDefParams = new Dictionary<string, object?>
                    {
                        { "@Category", unit.Category.Trim() },
                        { "@ID", unit.ID },
                        { "@UnitName", unit.UnitName.Trim() }
                    };
                    dataService.ExecuteNonQuery($"UPDATE {tableName} SET IS_DEFAULT = 0 WHERE CATEGORY = @Category AND ID <> @ID AND UNIT_NAME <> @UnitName;", clearDefParams);
                }

                unit.ModifiedBy = Environment.UserName;
                unit.ModifiedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

                string updateSql;
                Dictionary<string, object?> parameters;

                if (unit.ID > 0)
                {
                    updateSql = $@"
                        UPDATE {tableName} 
                        SET UNIT_NAME = @UnitName, CATEGORY = @Category, DESCRIPTION = @Description, IS_DEFAULT = @IsDefault,
                            MODIFIED_BY = @ModifiedBy, MODIFIED_DATE = @ModifiedDate
                        WHERE ID = @ID;";

                    parameters = new Dictionary<string, object?>
                    {
                        { "@ID", unit.ID },
                        { "@UnitName", unit.UnitName.Trim() },
                        { "@Category", unit.Category.Trim() },
                        { "@Description", unit.Description ?? string.Empty },
                        { "@IsDefault", unit.IsDefault ? 1 : 0 },
                        { "@ModifiedBy", unit.ModifiedBy },
                        { "@ModifiedDate", unit.ModifiedDate }
                    };
                }
                else
                {
                    updateSql = $@"
                        UPDATE {tableName} 
                        SET CATEGORY = @Category, DESCRIPTION = @Description, IS_DEFAULT = @IsDefault,
                            MODIFIED_BY = @ModifiedBy, MODIFIED_DATE = @ModifiedDate
                        WHERE UNIT_NAME = @UnitName;";

                    parameters = new Dictionary<string, object?>
                    {
                        { "@UnitName", unit.UnitName.Trim() },
                        { "@Category", unit.Category.Trim() },
                        { "@Description", unit.Description ?? string.Empty },
                        { "@IsDefault", unit.IsDefault ? 1 : 0 },
                        { "@ModifiedBy", unit.ModifiedBy },
                        { "@ModifiedDate", unit.ModifiedDate }
                    };
                }

                return dataService.ExecuteNonQuery(updateSql, parameters);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool Edit(Unit unit) => Edit(null, unit, TableName);

        /// <summary>
        /// Deletes a unit by its ID from the target table.
        /// </summary>
        public static bool Delete(IDataServiceDIntel? db, int id, string tableName = TableName)
        {
            if (id <= 0) return false;

            try
            {
                var dataService = ResolveDataService(db);
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

        public static bool Delete(int id) => Delete(null, id, TableName);

        /// <summary>
        /// Deletes a unit by its name (case-insensitive) from the target table.
        /// </summary>
        public static bool Delete(IDataServiceDIntel? db, string unitName, string tableName = TableName)
        {
            if (string.IsNullOrWhiteSpace(unitName)) return false;

            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService, tableName);

                string sql = $"DELETE FROM {tableName} WHERE UNIT_NAME = @UnitName;";
                var parameters = new Dictionary<string, object?>
                {
                    { "@UnitName", unitName.Trim() }
                };

                return dataService.ExecuteNonQuery(sql, parameters);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool Delete(string unitName) => Delete(null, unitName, TableName);

        /// <summary>
        /// Seeds default oilfield measurement units into the target table if not already present.
        /// </summary>
        public static void CreateDefaultUnits(IDataServiceDIntel? db = null, string tableName = TableName)
        {
            var dataService = ResolveDataService(db);
            EnsureTableExists(dataService, tableName);

            var defaultUnits = new (string UnitName, string Category, string Description, bool IsDefault)[]
            {
                // Length
                ("ft", "Length", "Feet", true),
                ("m", "Length", "Meters", false),
                ("in", "Length", "Inches", false),

                // Weight / Force
                ("klb", "Weight", "Kilopounds force", true),
                ("daN", "Weight", "Decanewtons", false),
                ("lb", "Weight", "Pounds force", false),
                ("kg", "Weight", "Kilograms", false),

                // Pressure
                ("psi", "Pressure", "Pounds per square inch", true),
                ("kPa", "Pressure", "Kilopascals", false),
                ("bar", "Pressure", "Bars", false),
                ("MPa", "Pressure", "Megapascals", false),

                // Temperature
                ("degF", "Temperature", "Degrees Fahrenheit", true),
                ("degC", "Temperature", "Degrees Celsius", false),
                ("K", "Temperature", "Kelvin", false),

                // Flow Rate
                ("gpm", "Flow Rate", "Gallons per minute", true),
                ("lpm", "Flow Rate", "Liters per minute", false),
                ("m3/hr", "Flow Rate", "Cubic meters per hour", false),

                // Torque
                ("ft-lbf", "Torque", "Foot-pounds force", true),
                ("kN-m", "Torque", "Kilonewton meters", false),
                ("N-m", "Torque", "Newton meters", false),

                // Rotary Speed
                ("rpm", "Rotary Speed", "Revolutions per minute", true),

                // Rate of Penetration
                ("ft/hr", "Rate of Penetration", "Feet per hour", true),
                ("m/hr", "Rate of Penetration", "Meters per hour", false)
            };

            string sql = $@"
                INSERT OR IGNORE INTO {tableName} (UNIT_NAME, CATEGORY, DESCRIPTION, IS_DEFAULT, CREATED_BY, CREATED_DATE, MODIFIED_BY, MODIFIED_DATE)
                VALUES (@UnitName, @Category, @Description, @IsDefault, @CreatedBy, @CreatedDate, @ModifiedBy, @ModifiedDate);";

            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            foreach (var item in defaultUnits)
            {
                var parameters = new Dictionary<string, object?>
                {
                    { "@UnitName", item.UnitName },
                    { "@Category", item.Category },
                    { "@Description", item.Description },
                    { "@IsDefault", item.IsDefault ? 1 : 0 },
                    { "@CreatedBy", "System" },
                    { "@CreatedDate", now },
                    { "@ModifiedBy", "System" },
                    { "@ModifiedDate", now }
                };

                dataService.ExecuteNonQuery(sql, parameters);
            }
        }

        /// <summary>
        /// Saves this unit instance (calls Add if ID == 0, Edit if ID > 0).
        /// </summary>
        public bool Save(IDataServiceDIntel? db = null, string tableName = TableName)
        {
            return ID <= 0 ? Add(db, this, tableName) : Edit(db, this, tableName);
        }

        /// <summary>
        /// Deletes this unit instance from the target database table.
        /// </summary>
        public bool DeleteSelf(IDataServiceDIntel? db = null, string tableName = TableName)
        {
            if (ID > 0) return Delete(db, ID, tableName);
            if (!string.IsNullOrWhiteSpace(UnitName)) return Delete(db, UnitName, tableName);
            return false;
        }

        private static Unit MapFromRow(DataRow row)
        {
            var table = row.Table;
            return new Unit
            {
                ID = CheckNull(row["ID"], 0),
                UnitName = CheckNull(row["UNIT_NAME"], string.Empty),
                Category = CheckNull(row["CATEGORY"], string.Empty),
                Description = CheckNull(row["DESCRIPTION"], string.Empty),
                IsDefault = Convert.ToInt32(CheckNull(row["IS_DEFAULT"], 0)) == 1,
                CreatedBy = table != null && table.Columns.Contains("CREATED_BY") ? CheckNull(row["CREATED_BY"], string.Empty) : string.Empty,
                CreatedDate = table != null && table.Columns.Contains("CREATED_DATE") ? CheckNull(row["CREATED_DATE"], string.Empty) : string.Empty,
                ModifiedBy = table != null && table.Columns.Contains("MODIFIED_BY") ? CheckNull(row["MODIFIED_BY"], string.Empty) : string.Empty,
                ModifiedDate = table != null && table.Columns.Contains("MODIFIED_DATE") ? CheckNull(row["MODIFIED_DATE"], string.Empty) : string.Empty
            };
        }

        private static string CheckNull(object? value, string defaultValue)
        {
            if (value == null || value == DBNull.Value) return defaultValue;
            return value.ToString() ?? defaultValue;
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

