using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;

namespace DrillIntel.Data.Objects.DataObjects.Models.Common
{
    /// <summary>
    /// Manages system-wide and application-level settings stored in the VMX_SETTINGS SQLite table.
    /// Supports scalar strings, numbers, booleans, and binary file attachments (BLOBs).
    /// </summary>
    public class SystemSettings
    {
        public Dictionary<string, SettingValue> settings = new Dictionary<string, SettingValue>(StringComparer.OrdinalIgnoreCase);
        public string LastError = "";

        #region Setting Key Constants
        public const string LogWITSMLTransactions = "LogWITSMLTransactions";
        public const string LogFolder = "LogFolder";
        public const string DigitSignificance = "DigitSignificance";
        public const string IgnoreNegative = "IgnoreNegative";
        public const string DepthLogFrequency = "DepthLogFrequency";
        public const string TimeLogFrequency = "TimeLogFrequency";
        public const string TrajectoryFrequency = "TrajectoryFrequency";
        public const string MudLogFrequency = "MudLogFrequency";
        public const string DepthOffset = "DepthOffset";
        public const string TimeDataAlarmTime = "TimeDataAlarmTime";
        public const string DepthDataAlarmTime = "DepthDataAlarmTime";
        public const string DataPageSize = "DataPageSize";
        public const string OverlapSize = "OverlapSize";
        public const string TimeZone = "TimeZone";
        public const string PageSize = "PageSize";
        public const string Orientation = "Orientation";
        public const string LeftMargin = "LeftMargin";
        public const string TopMargin = "TopMargin";
        public const string RightMargin = "RightMargin";
        public const string BottomMargin = "BottomMargin";
        public const string OptimizeDisplay = "OptimizeDisplay";
        public const string UseDataPaging = "UseDataPaging";
        public const string DServerIP = "DServerIP";
        public const string DownloadTimeOut = "DownloadTimeOut";
        public const string DownloadChunkSize = "DownloadChunkSize";
        public const string SoundFileRed = "SoundFileRed";
        public const string SoundFileYellow = "SoundFileYellow";
        public const string SoundFileRemarks = "SoundFileRemarks";
        public const string WellCheckEnabled = "WellCheckEnabled";
        public const string AlarmAcknowledgement = "AlarmAcknowledgement";
        public const string DefaultTemplate = "DefaultTemplate";
        public const string ConnValidationTemplate = "ConnValidationTemplate";
        public const string TripConnValidationTemplate = "TripConnValidationTemplate";
        public const string DrlgStandValidationTemplate = "DrlgStandValidationTemplate";
        public const string ChatServerDomain = "ChatServerDomain";
        public const string SupportEmail = "SupportEmail";

        public const string chatResolveSrvRecords = "chatResolveSrvRecords";
        public const string chatHostName = "chatHostName";
        public const string chatPort = "chatPort";
        public const string chatUseSSL = "chatUseSSL";
        public const string ShareBroomstickDouments = "ShareBroomstickDouments";
        public const string WaitTime = "WaitTime";
        public const string CompanyLogo = "CompanyLogo";
        public const string CompanyLogo2 = "CompanyLogo2";
        public const string DayTimeFrom = "DayTimeFrom";
        public const string DayTimeTo = "DayTimeTo";
        public const string DoNotPerformSlideCleanup = "DoNotPerformSlideCleanup";
        #endregion

        #region Default Settings Definitions
        public static readonly (string Id, string Name, string Value, string Type)[] DefaultSettings = new[]
        {
            ("DigitSignificance", "DigitSignificance", "2", "G"),
            ("DepthOffset", "DepthOffset", "0", "G"),
            ("IgnoreNegative", "IgnoreNegative", "False", "G"),
            ("DoNotPerformSlideCleanup", "DoNotPerformSlideCleanup", "False", "G"),
            ("OptimizeDisplay", "OptimizeDisplay", "False", "G"),
            ("UseDataPaging", "UseDataPaging", "False", "G"),
            ("DataPageSize", "DataPageSize", "5000", "G"),
            ("OverlapSize", "OverlapSize", "100", "G"),
            ("WaitTime", "WaitTime", "30", "G"),
            ("TimeLogFrequency", "TimeLogFrequency", "5", "G"),
            ("DepthLogFrequency", "DepthLogFrequency", "0.5", "G"),
            ("TrajectoryFrequency", "TrajectoryFrequency", "30", "G"),
            ("MudLogFrequency", "MudLogFrequency", "10", "G"),
            ("TimeDataAlarmTime", "TimeDataAlarmTime", "15", "G"),
            ("DepthDataAlarmTime", "DepthDataAlarmTime", "30", "G"),
            ("WellCheckEnabled", "WellCheckEnabled", "False", "G"),
            ("AlarmAcknowledgement", "AlarmAcknowledgement", "False", "G"),
            ("LogFolder", "LogFolder", @"C:\ProgramData\DrillIntel\Logs", "G"),
            ("LogWITSMLTransactions", "LogWITSMLTransactions", "False", "G"),
            ("TimeZone", "TimeZone", "UTC", "G"),
            ("DayTimeFrom", "DayTimeFrom", "06:00:00", "G"),
            ("DayTimeTo", "DayTimeTo", "18:00:00", "G"),
            ("DServerIP", "DServerIP", "127.0.0.1", "G"),
            ("DownloadTimeOut", "DownloadTimeOut", "30", "G"),
            ("DownloadChunkSize", "DownloadChunkSize", "1024", "G"),
            ("SupportEmail", "SupportEmail", "support@drillintel.com", "G"),
            ("ChatServerDomain", "ChatServerDomain", "chat.drillintel.net", "G"),
            ("chatHostName", "chatHostName", "xmpp.drillintel.net", "G"),
            ("chatPort", "chatPort", "5222", "G"),
            ("chatUseSSL", "chatUseSSL", "False", "G"),
            ("chatResolveSrvRecords", "chatResolveSrvRecords", "False", "G"),
            ("SoundFileRed", "SoundFileRed", "", "G"),
            ("SoundFileYellow", "SoundFileYellow", "", "G"),
            ("SoundFileRemarks", "SoundFileRemarks", "", "G"),
            ("PageSize", "PageSize", "A4", "G"),
            ("Orientation", "Orientation", "Landscape", "G"),
            ("LeftMargin", "LeftMargin", "15", "G"),
            ("TopMargin", "TopMargin", "15", "G"),
            ("RightMargin", "RightMargin", "15", "G"),
            ("BottomMargin", "BottomMargin", "15", "G"),
            ("ShareBroomstickDouments", "ShareBroomstickDouments", "False", "G")
        };

        /// <summary>
        /// Inserts default system settings into VMX_SETTINGS if the table contains 0 records.
        /// </summary>
        public static void EnsureDefaultSettings(IDataServiceDIntel objDataService)
        {
            try
            {
                if (objDataService == null) return;

                if (objDataService.IsRecordExist("SELECT 1 FROM VMX_SETTINGS LIMIT 1;"))
                {
                    return;
                }

                foreach (var item in DefaultSettings)
                {
                    var parameters = new Dictionary<string, object?>
                    {
                        ["@id"] = item.Id,
                        ["@name"] = item.Name,
                        ["@val"] = item.Value,
                        ["@type"] = item.Type
                    };

                    objDataService.ExecuteNonQuery(@"
                        INSERT INTO VMX_SETTINGS (SETTING_ID, SETTING_NAME, SETTING_VALUE, SETTING_TYPE)
                        VALUES (@id, @name, @val, @type);", parameters);
                }
            }
            catch { }
        }

        /// <summary>
        /// Inserts default system settings into VMX_SETTINGS using an IDbConnection instance if the table contains 0 records.
        /// </summary>
        public static void EnsureDefaultSettings(IDbConnection conn)
        {
            try
            {
                if (conn == null) return;

                using var checkCmd = conn.CreateCommand();
                checkCmd.CommandText = "SELECT COUNT(*) FROM VMX_SETTINGS;";
                var res = checkCmd.ExecuteScalar();
                long count = 0;
                if (res != null && res != DBNull.Value)
                {
                    count = Convert.ToInt64(res);
                }

                if (count > 0) return;

                using var insertCmd = conn.CreateCommand();
                var pId = insertCmd.CreateParameter();
                pId.ParameterName = "@id";
                insertCmd.Parameters.Add(pId);

                var pName = insertCmd.CreateParameter();
                pName.ParameterName = "@name";
                insertCmd.Parameters.Add(pName);

                var pVal = insertCmd.CreateParameter();
                pVal.ParameterName = "@val";
                insertCmd.Parameters.Add(pVal);

                var pType = insertCmd.CreateParameter();
                pType.ParameterName = "@type";
                insertCmd.Parameters.Add(pType);

                insertCmd.CommandText = @"
                    INSERT INTO VMX_SETTINGS (SETTING_ID, SETTING_NAME, SETTING_VALUE, SETTING_TYPE)
                    VALUES (@id, @name, @val, @type);";

                foreach (var item in DefaultSettings)
                {
                    pId.Value = item.Id;
                    pName.Value = item.Name;
                    pVal.Value = item.Value ?? (object)DBNull.Value;
                    pType.Value = item.Type;
                    insertCmd.ExecuteNonQuery();
                }
            }
            catch { }
        }
        #endregion

        /// <summary>
        /// Ensures that the VMX_SETTINGS table (and compatibility views) exist in the target SQLite database.
        /// </summary>
        public static void EnsureTableExists(IDataServiceDIntel objDataService)
        {
            try
            {
                if (objDataService == null) return;

                objDataService.ExecuteNonQuery(@"
                    CREATE TABLE IF NOT EXISTS VMX_SETTINGS (
                        SETTING_ID TEXT PRIMARY KEY,
                        SETTING_NAME TEXT,
                        SETTING_VALUE TEXT,
                        SETTING_TYPE TEXT,
                        BINARY_VALUE BLOB
                    );
                    CREATE VIEW IF NOT EXISTS SystemSettings AS SELECT * FROM VMX_SETTINGS;
                    CREATE VIEW IF NOT EXISTS APP_SETTINGS AS SELECT * FROM VMX_SETTINGS;");

                EnsureDefaultSettings(objDataService);
            }
            catch { }
        }

        /// <summary>
        /// Ensures that the VMX_SETTINGS table (and compatibility views) exist using an IDbConnection instance.
        /// </summary>
        public static void EnsureTableExists(IDbConnection conn)
        {
            try
            {
                if (conn == null) return;

                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS VMX_SETTINGS (
                        SETTING_ID TEXT PRIMARY KEY,
                        SETTING_NAME TEXT,
                        SETTING_VALUE TEXT,
                        SETTING_TYPE TEXT,
                        BINARY_VALUE BLOB
                    );
                    CREATE VIEW IF NOT EXISTS SystemSettings AS SELECT * FROM VMX_SETTINGS;
                    CREATE VIEW IF NOT EXISTS APP_SETTINGS AS SELECT * FROM VMX_SETTINGS;";
                cmd.ExecuteNonQuery();

                EnsureDefaultSettings(conn);
            }
            catch { }
        }

        /// <summary>
        /// Loads all settings from the VMX_SETTINGS SQLite table into the in-memory settings dictionary.
        /// </summary>
        /// <param name="objDataService">The SQLite database service instance.</param>
        /// <returns>True if settings loaded successfully, false otherwise.</returns>
        public bool LoadSettings(IDataServiceDIntel objDataService)
        {
            try
            {
                if (objDataService == null)
                {
                    LastError = "DataService instance is null.";
                    return false;
                }

                EnsureTableExists(objDataService);

                settings = new Dictionary<string, SettingValue>(StringComparer.OrdinalIgnoreCase);

                DataTable objData = objDataService.GetTable("SELECT * FROM VMX_SETTINGS;");

                if (objData != null && objData.Rows.Count > 0)
                {
                    foreach (DataRow objRow in objData.Rows)
                    {
                        var objSettingValue = new SettingValue
                        {
                            SettingID = DataService.checkNull(objRow["SETTING_ID"], ""),
                            SettingName = DataService.checkNull(objRow["SETTING_NAME"], ""),
                            Value = DataService.checkNull(objRow["SETTING_VALUE"], ""),
                            SettingType = DataService.checkNull(objRow["SETTING_TYPE"], ""),
                            NeedToSave = false
                        };

                        if (!string.IsNullOrEmpty(objSettingValue.SettingID))
                        {
                            settings[objSettingValue.SettingID] = objSettingValue;
                        }
                    }
                }

                LastError = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message + Environment.NewLine + ex.StackTrace;
                return false;
            }
        }

        /// <summary>
        /// Reads binary BLOB data for the specified setting from VMX_SETTINGS,
        /// writes the content to a file in the LogFolder directory, and returns the file path.
        /// </summary>
        /// <param name="objDataService">The SQLite database service instance.</param>
        /// <param name="paramSettingID">The setting ID containing binary data.</param>
        /// <returns>The path to the extracted file, or empty string if not found or on error.</returns>
        public string loadBinaryValue(IDataServiceDIntel objDataService, string paramSettingID)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(paramSettingID))
                    return "";

                EnsureTableExists(objDataService);

                string logFolder = getSetting(LogFolder);
                if (string.IsNullOrWhiteSpace(logFolder))
                {
                    logFolder = Path.Combine(Path.GetTempPath(), "DrillIntel", "Settings");
                }

                if (!Directory.Exists(logFolder))
                {
                    Directory.CreateDirectory(logFolder);
                }

                string fileName = Path.Combine(logFolder, "file" + paramSettingID);

                var parameters = new Dictionary<string, object?> { ["@id"] = paramSettingID };
                DataTable objData = objDataService.GetTable("SELECT BINARY_VALUE FROM VMX_SETTINGS WHERE SETTING_ID = @id;", parameters);

                if (objData != null && objData.Rows.Count > 0)
                {
                    object cellVal = objData.Rows[0]["BINARY_VALUE"];
                    if (cellVal is byte[] fileData && fileData.Length > 0)
                    {
                        File.WriteAllBytes(fileName, fileData);
                        return fileName;
                    }
                }

                return "";
            }
            catch (Exception ex)
            {
                LastError = ex.Message + Environment.NewLine + ex.StackTrace;
                return "";
            }
        }

        /// <summary>
        /// Retrieves the string value of a loaded setting by SettingID.
        /// </summary>
        /// <param name="SettingID">The setting identifier.</param>
        /// <returns>The setting value, or empty string if not found.</returns>
        public string getSetting(string SettingID)
        {
            try
            {
                if (settings != null && !string.IsNullOrEmpty(SettingID) && settings.TryGetValue(SettingID, out var item) && item != null)
                {
                    return item.Value ?? "";
                }
                return "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// Directly persists a single setting into the VMX_SETTINGS SQLite table.
        /// </summary>
        /// <param name="objDataService">The SQLite database service instance.</param>
        /// <param name="paramSettingID">The setting ID and name.</param>
        /// <param name="paramSettingValue">The setting string value.</param>
        public static void saveASetting(IDataServiceDIntel objDataService, string paramSettingID, string paramSettingValue)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(paramSettingID))
                    return;

                EnsureTableExists(objDataService);

                var parameters = new Dictionary<string, object?>
                {
                    ["@id"] = paramSettingID,
                    ["@val"] = paramSettingValue ?? "",
                    ["@type"] = "G"
                };

                // SQLite UPSERT: Insert or update setting value cleanly without SQL injection
                objDataService.ExecuteNonQuery(@"
                    INSERT INTO VMX_SETTINGS (SETTING_ID, SETTING_NAME, SETTING_VALUE, SETTING_TYPE)
                    VALUES (@id, @id, @val, @type)
                    ON CONFLICT(SETTING_ID) DO UPDATE SET
                        SETTING_VALUE = excluded.SETTING_VALUE,
                        SETTING_NAME = excluded.SETTING_NAME,
                        SETTING_TYPE = excluded.SETTING_TYPE;", parameters);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Directly reads a single setting value from the VMX_SETTINGS table.
        /// </summary>
        /// <param name="objDataService">The SQLite database service instance.</param>
        /// <param name="paramSettingID">The setting ID to query.</param>
        /// <returns>The setting value, or empty string if not found.</returns>
        public static string getASetting(IDataServiceDIntel objDataService, string paramSettingID)
        {
            try
            {
                if (objDataService == null || string.IsNullOrWhiteSpace(paramSettingID))
                    return "";

                EnsureTableExists(objDataService);

                var parameters = new Dictionary<string, object?> { ["@id"] = paramSettingID };
                DataTable objData = objDataService.GetTable("SELECT SETTING_VALUE FROM VMX_SETTINGS WHERE SETTING_ID = @id LIMIT 1;", parameters);

                if (objData != null && objData.Rows.Count > 0)
                {
                    return DataService.checkNull(objData.Rows[0]["SETTING_VALUE"], "");
                }
                return "";
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// Backward-compatibility alias preserving the legacy VB.NET spelling 'getAStting'.
        /// </summary>
        public static string getAStting(IDataServiceDIntel objDataService, string paramSettingID) =>
            getASetting(objDataService, paramSettingID);

        /// <summary>
        /// Persists all in-memory settings to the VMX_SETTINGS table in the SQLite database,
        /// including text values and binary files (BLOBs) for settings marked with SettingType = "B".
        /// </summary>
        /// <param name="objDataService">The SQLite database service instance.</param>
        /// <returns>True if settings were saved successfully, false otherwise.</returns>
        public bool SaveSettings(IDataServiceDIntel objDataService)
        {
            try
            {
                if (objDataService == null)
                {
                    LastError = "DataService instance is null.";
                    return false;
                }

                EnsureTableExists(objDataService);

                if (settings == null || settings.Count == 0)
                {
                    return true;
                }

                // 1. Save / Update setting text values
                foreach (var objItem in settings.Values)
                {
                    if (string.IsNullOrEmpty(objItem.SettingID)) continue;

                    var parameters = new Dictionary<string, object?>
                    {
                        ["@id"] = objItem.SettingID,
                        ["@name"] = string.IsNullOrEmpty(objItem.SettingName) ? objItem.SettingID : objItem.SettingName,
                        ["@val"] = objItem.Value ?? "",
                        ["@type"] = string.IsNullOrEmpty(objItem.SettingType) ? "G" : objItem.SettingType
                    };

                    objDataService.ExecuteNonQuery(@"
                        INSERT INTO VMX_SETTINGS (SETTING_ID, SETTING_NAME, SETTING_VALUE, SETTING_TYPE)
                        VALUES (@id, @name, @val, @type)
                        ON CONFLICT(SETTING_ID) DO UPDATE SET
                            SETTING_VALUE = excluded.SETTING_VALUE,
                            SETTING_NAME = excluded.SETTING_NAME,
                            SETTING_TYPE = excluded.SETTING_TYPE;", parameters);
                }

                // 2. Save binary files (SettingType == "B" or "BINARY")
                foreach (var objItem in settings.Values)
                {
                    if (string.Equals(objItem.SettingType, "B", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(objItem.SettingType, "BINARY", StringComparison.OrdinalIgnoreCase))
                    {
                        if (objItem.NeedToSave && !string.IsNullOrWhiteSpace(objItem.Value) && File.Exists(objItem.Value))
                        {
                            byte[] fileBytes = File.ReadAllBytes(objItem.Value);
                            var binParams = new Dictionary<string, object?>
                            {
                                ["@id"] = objItem.SettingID,
                                ["@binary"] = fileBytes
                            };

                            objDataService.ExecuteNonQuery(
                                "UPDATE VMX_SETTINGS SET BINARY_VALUE = @binary WHERE SETTING_ID = @id;",
                                binParams);

                            objItem.NeedToSave = false;
                        }
                    }
                }

                LastError = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message + Environment.NewLine + ex.StackTrace;
                return false;
            }
        }

        /// <summary>
        /// Retrieves a setting value parsed as a Double.
        /// </summary>
        /// <param name="SettingID">The setting identifier.</param>
        /// <returns>The parsed double, or 0.0 if not found or invalid.</returns>
        public double getNumericSetting(string SettingID)
        {
            try
            {
                if (settings != null && !string.IsNullOrEmpty(SettingID) && settings.TryGetValue(SettingID, out var item) && item != null)
                {
                    string strVal = item.Value?.Trim() ?? "";
                    if (double.TryParse(strVal, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                    {
                        return result;
                    }
                    if (double.TryParse(strVal, NumberStyles.Any, CultureInfo.CurrentCulture, out result))
                    {
                        return result;
                    }
                }
                return 0.0;
            }
            catch (Exception)
            {
                return 0.0;
            }
        }

        /// <summary>
        /// Retrieves a setting value parsed as a Boolean.
        /// Evaluates to true if value is "true", "1", or "yes" (case-insensitive).
        /// </summary>
        /// <param name="SettingID">The setting identifier.</param>
        /// <returns>True if setting evaluates to true; false otherwise.</returns>
        public bool getBooleanSetting(string SettingID)
        {
            try
            {
                if (settings != null && !string.IsNullOrEmpty(SettingID) && settings.TryGetValue(SettingID, out var item) && item != null)
                {
                    string val = item.Value?.Trim().ToLowerInvariant() ?? "";
                    return val == "true" || val == "1" || val == "yes";
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Sets or adds an in-memory setting value.
        /// </summary>
        /// <param name="SettingID">The setting identifier.</param>
        /// <param name="SettingValue">The setting string value.</param>
        public void setSetting(string SettingID, string SettingValue)
        {
            try
            {
                if (string.IsNullOrEmpty(SettingID)) return;

                if (settings.ContainsKey(SettingID))
                {
                    settings[SettingID].Value = SettingValue;
                    settings[SettingID].NeedToSave = true;
                }
                else
                {
                    var objNewSetting = new SettingValue
                    {
                        SettingID = SettingID,
                        SettingName = SettingID,
                        Value = SettingValue,
                        SettingType = "G",
                        NeedToSave = true
                    };

                    settings.Add(SettingID, objNewSetting);
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Sets a binary setting whose value is the path to a local file.
        /// Marks the setting as SettingType = "B" and NeedToSave = true so SaveSettings will persist it as a BLOB.
        /// </summary>
        /// <param name="SettingID">The setting identifier.</param>
        /// <param name="filePath">The path to the local file to persist.</param>
        public void setBinarySetting(string SettingID, string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(SettingID)) return;

                if (settings.ContainsKey(SettingID))
                {
                    settings[SettingID].Value = filePath;
                    settings[SettingID].SettingType = "B";
                    settings[SettingID].NeedToSave = true;
                }
                else
                {
                    var objNewSetting = new SettingValue
                    {
                        SettingID = SettingID,
                        SettingName = SettingID,
                        Value = filePath,
                        SettingType = "B",
                        NeedToSave = true
                    };

                    settings.Add(SettingID, objNewSetting);
                }
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Creates a deep copy of this SystemSettings instance and its loaded settings.
        /// </summary>
        public virtual SystemSettings GetCopy()
        {
            var copy = new SystemSettings
            {
                LastError = this.LastError
            };

            if (this.settings != null)
            {
                foreach (var kvp in this.settings)
                {
                    copy.settings[kvp.Key] = new SettingValue
                    {
                        SettingID = kvp.Value.SettingID,
                        SettingName = kvp.Value.SettingName,
                        Value = kvp.Value.Value,
                        SettingType = kvp.Value.SettingType,
                        NeedToSave = kvp.Value.NeedToSave
                    };
                }
            }

            return copy;
        }
    }
}

