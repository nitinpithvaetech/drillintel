using System;
using System.Collections.Generic;
using System.Data;
using DrillIntel.Data;

namespace DrillIntel.Models
{
    /// <summary>
    /// Represents a measurement unit registered in the Application Database (table APP_UNIT_MASTER).
    /// Provides CRUD operations, category filtering, and default unit initialization.
    /// </summary>
    public class Unit
    {
        public const string TableName = "APP_UNIT_MASTER";

        /// <summary>
        /// Optional application-wide default data service for Unit operations when not explicitly passed.
        /// </summary>
        public static IDataServiceDIntel? DefaultDataService { get; set; }

        public int ID { get; set; } = 0;
        public string UnitName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsDefault { get; set; } = false;

        public Unit()
        {
        }

        public Unit(string unitName, string category, string description = "", bool isDefault = false, int id = 0)
        {
            ID = id;
            UnitName = unitName ?? string.Empty;
            Category = category ?? string.Empty;
            Description = description ?? string.Empty;
            IsDefault = isDefault;
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
                IsDefault = this.IsDefault
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
        /// Ensures the APP_UNIT_MASTER table exists in the target database.
        /// </summary>
        public static void EnsureTableExists(IDataServiceDIntel? db = null)
        {
            var dataService = ResolveDataService(db);
            const string sql = @"
                CREATE TABLE IF NOT EXISTS APP_UNIT_MASTER (
                    ID          INTEGER PRIMARY KEY AUTOINCREMENT,
                    UNIT_NAME   TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    CATEGORY    TEXT NOT NULL,
                    DESCRIPTION TEXT,
                    IS_DEFAULT  INTEGER NOT NULL DEFAULT 0
                );";
            dataService.ExecuteNonQuery(sql);
        }

        /// <summary>
        /// Retrieves all registered units, optionally filtered by category.
        /// </summary>
        public static List<Unit> GetList(IDataServiceDIntel? db = null, string? category = null)
        {
            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService);

                string sql;
                Dictionary<string, object?>? parameters = null;

                if (string.IsNullOrWhiteSpace(category))
                {
                    sql = "SELECT ID, UNIT_NAME, CATEGORY, DESCRIPTION, IS_DEFAULT FROM APP_UNIT_MASTER ORDER BY CATEGORY ASC, IS_DEFAULT DESC, UNIT_NAME ASC;";
                }
                else
                {
                    sql = "SELECT ID, UNIT_NAME, CATEGORY, DESCRIPTION, IS_DEFAULT FROM APP_UNIT_MASTER WHERE CATEGORY = @Category ORDER BY IS_DEFAULT DESC, UNIT_NAME ASC;";
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
        public static Unit? GetUnit(IDataServiceDIntel? db, string unitName)
        {
            if (string.IsNullOrWhiteSpace(unitName)) return null;

            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService);

                const string sql = "SELECT ID, UNIT_NAME, CATEGORY, DESCRIPTION, IS_DEFAULT FROM APP_UNIT_MASTER WHERE UNIT_NAME = @UnitName LIMIT 1;";
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

        public static Unit? GetUnit(string unitName) => GetUnit(null, unitName);

        /// <summary>
        /// Retrieves a unit by its primary key ID.
        /// </summary>
        public static Unit? GetById(IDataServiceDIntel? db, int id)
        {
            if (id <= 0) return null;

            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService);

                const string sql = "SELECT ID, UNIT_NAME, CATEGORY, DESCRIPTION, IS_DEFAULT FROM APP_UNIT_MASTER WHERE ID = @ID LIMIT 1;";
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

        public static Unit? GetById(int id) => GetById(null, id);

        /// <summary>
        /// Retrieves the default unit for a given category.
        /// </summary>
        public static Unit? GetDefaultUnitForCategory(IDataServiceDIntel? db, string category)
        {
            if (string.IsNullOrWhiteSpace(category)) return null;

            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService);

                const string sql = "SELECT ID, UNIT_NAME, CATEGORY, DESCRIPTION, IS_DEFAULT FROM APP_UNIT_MASTER WHERE CATEGORY = @Category AND IS_DEFAULT = 1 LIMIT 1;";
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

        public static Unit? GetDefaultUnitForCategory(string category) => GetDefaultUnitForCategory(null, category);

        /// <summary>
        /// Retrieves all distinct unit categories registered in the database.
        /// </summary>
        public static List<string> GetCategories(IDataServiceDIntel? db = null)
        {
            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService);

                const string sql = "SELECT DISTINCT CATEGORY FROM APP_UNIT_MASTER WHERE CATEGORY IS NOT NULL AND CATEGORY <> '' ORDER BY CATEGORY ASC;";
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
        /// Adds a new unit to APP_UNIT_MASTER. If IsDefault is true, unsets any existing default in the same category.
        /// </summary>
        public static bool Add(IDataServiceDIntel? db, Unit unit)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.UnitName) || string.IsNullOrWhiteSpace(unit.Category))
                return false;

            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService);

                if (unit.IsDefault)
                {
                    var clearDefParams = new Dictionary<string, object?>
                    {
                        { "@Category", unit.Category.Trim() }
                    };
                    dataService.ExecuteNonQuery("UPDATE APP_UNIT_MASTER SET IS_DEFAULT = 0 WHERE CATEGORY = @Category;", clearDefParams);
                }

                const string insertSql = @"
                    INSERT INTO APP_UNIT_MASTER (UNIT_NAME, CATEGORY, DESCRIPTION, IS_DEFAULT)
                    VALUES (@UnitName, @Category, @Description, @IsDefault);";

                var parameters = new Dictionary<string, object?>
                {
                    { "@UnitName", unit.UnitName.Trim() },
                    { "@Category", unit.Category.Trim() },
                    { "@Description", unit.Description ?? string.Empty },
                    { "@IsDefault", unit.IsDefault ? 1 : 0 }
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

        public static bool Add(Unit unit) => Add(null, unit);

        /// <summary>
        /// Edits an existing unit in APP_UNIT_MASTER.
        /// </summary>
        public static bool Edit(IDataServiceDIntel? db, Unit unit)
        {
            if (unit == null || string.IsNullOrWhiteSpace(unit.UnitName) || string.IsNullOrWhiteSpace(unit.Category))
                return false;

            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService);

                if (unit.IsDefault)
                {
                    var clearDefParams = new Dictionary<string, object?>
                    {
                        { "@Category", unit.Category.Trim() },
                        { "@ID", unit.ID },
                        { "@UnitName", unit.UnitName.Trim() }
                    };
                    dataService.ExecuteNonQuery("UPDATE APP_UNIT_MASTER SET IS_DEFAULT = 0 WHERE CATEGORY = @Category AND ID <> @ID AND UNIT_NAME <> @UnitName;", clearDefParams);
                }

                string updateSql;
                Dictionary<string, object?> parameters;

                if (unit.ID > 0)
                {
                    updateSql = @"
                        UPDATE APP_UNIT_MASTER 
                        SET UNIT_NAME = @UnitName, CATEGORY = @Category, DESCRIPTION = @Description, IS_DEFAULT = @IsDefault
                        WHERE ID = @ID;";

                    parameters = new Dictionary<string, object?>
                    {
                        { "@ID", unit.ID },
                        { "@UnitName", unit.UnitName.Trim() },
                        { "@Category", unit.Category.Trim() },
                        { "@Description", unit.Description ?? string.Empty },
                        { "@IsDefault", unit.IsDefault ? 1 : 0 }
                    };
                }
                else
                {
                    updateSql = @"
                        UPDATE APP_UNIT_MASTER 
                        SET CATEGORY = @Category, DESCRIPTION = @Description, IS_DEFAULT = @IsDefault
                        WHERE UNIT_NAME = @UnitName;";

                    parameters = new Dictionary<string, object?>
                    {
                        { "@UnitName", unit.UnitName.Trim() },
                        { "@Category", unit.Category.Trim() },
                        { "@Description", unit.Description ?? string.Empty },
                        { "@IsDefault", unit.IsDefault ? 1 : 0 }
                    };
                }

                return dataService.ExecuteNonQuery(updateSql, parameters);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool Edit(Unit unit) => Edit(null, unit);

        /// <summary>
        /// Deletes a unit by its ID.
        /// </summary>
        public static bool Delete(IDataServiceDIntel? db, int id)
        {
            if (id <= 0) return false;

            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService);

                const string sql = "DELETE FROM APP_UNIT_MASTER WHERE ID = @ID;";
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

        public static bool Delete(int id) => Delete(null, id);

        /// <summary>
        /// Deletes a unit by its name (case-insensitive).
        /// </summary>
        public static bool Delete(IDataServiceDIntel? db, string unitName)
        {
            if (string.IsNullOrWhiteSpace(unitName)) return false;

            try
            {
                var dataService = ResolveDataService(db);
                EnsureTableExists(dataService);

                const string sql = "DELETE FROM APP_UNIT_MASTER WHERE UNIT_NAME = @UnitName;";
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

        public static bool Delete(string unitName) => Delete(null, unitName);

        /// <summary>
        /// Seeds default oilfield measurement units into APP_UNIT_MASTER if not already present.
        /// </summary>
        public static void CreateDefaultUnits(IDataServiceDIntel? db = null)
        {
            var dataService = ResolveDataService(db);
            EnsureTableExists(dataService);

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

            const string sql = @"
                INSERT OR IGNORE INTO APP_UNIT_MASTER (UNIT_NAME, CATEGORY, DESCRIPTION, IS_DEFAULT)
                VALUES (@UnitName, @Category, @Description, @IsDefault);";

            foreach (var item in defaultUnits)
            {
                var parameters = new Dictionary<string, object?>
                {
                    { "@UnitName", item.UnitName },
                    { "@Category", item.Category },
                    { "@Description", item.Description },
                    { "@IsDefault", item.IsDefault ? 1 : 0 }
                };

                dataService.ExecuteNonQuery(sql, parameters);
            }
        }

        /// <summary>
        /// Saves this unit instance (calls Add if ID == 0, Edit if ID > 0).
        /// </summary>
        public bool Save(IDataServiceDIntel? db = null)
        {
            return ID <= 0 ? Add(db, this) : Edit(db, this);
        }

        /// <summary>
        /// Deletes this unit instance from the database.
        /// </summary>
        public bool DeleteSelf(IDataServiceDIntel? db = null)
        {
            if (ID > 0) return Delete(db, ID);
            if (!string.IsNullOrWhiteSpace(UnitName)) return Delete(db, UnitName);
            return false;
        }

        private static Unit MapFromRow(DataRow row)
        {
            return new Unit
            {
                ID = CheckNull(row["ID"], 0),
                UnitName = CheckNull(row["UNIT_NAME"], string.Empty),
                Category = CheckNull(row["CATEGORY"], string.Empty),
                Description = CheckNull(row["DESCRIPTION"], string.Empty),
                IsDefault = Convert.ToInt32(CheckNull(row["IS_DEFAULT"], 0)) == 1
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

