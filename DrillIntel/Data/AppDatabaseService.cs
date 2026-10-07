using System;
using System.IO;
using DrillIntel.Data;

namespace DrillIntel.Data;

/// <summary>
/// Implementation of IAppDatabaseService managing the Application Database in ProgramData.
/// Enforces the Single Source of Truth (SSOT) principle by copying from the shipped
/// template file on first run and failing fast if the build artifact is missing.
/// </summary>
public class AppDatabaseService : IAppDatabaseService
{
    private readonly object _lock = new();
    private IDataServiceDIntel? _dataService;
    private bool _isInitialized;

    public string DatabasePath { get; }
    public string TemplatePath { get; }

    public bool IsInitialized => _isInitialized && _dataService != null && _dataService.IsConnectionOpen();

    /// <summary>
    /// Creates an instance using standard system paths.
    /// Target: C:\ProgramData\DrillIntel\DrillIntelApp.sqlite
    /// Template: [BaseDirectory]\Data\DrillIntelApp.sqlite
    /// </summary>
    public AppDatabaseService()
        : this(
            databasePath: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DrillIntel", "DrillIntelApp.sqlite"),
            templatePath: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "DrillIntelApp.sqlite"))
    {
    }

    /// <summary>
    /// Creates an instance with explicit database and template paths (primarily for testing and custom configurations).
    /// </summary>
    public AppDatabaseService(string databasePath, string templatePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("Database path cannot be null or empty.", nameof(databasePath));
        if (string.IsNullOrWhiteSpace(templatePath))
            throw new ArgumentException("Template path cannot be null or empty.", nameof(templatePath));

        DatabasePath = Path.GetFullPath(databasePath);
        TemplatePath = Path.GetFullPath(templatePath);
    }

    public void Initialize()
    {
        lock (_lock)
        {
            if (IsInitialized) return;

            var targetDir = Path.GetDirectoryName(DatabasePath);
            if (!string.IsNullOrEmpty(targetDir) && !Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            if (!File.Exists(DatabasePath))
            {
                if (!File.Exists(TemplatePath))
                {
                    throw new FileNotFoundException(
                        $"The master application database template was not found at '{TemplatePath}'. " +
                        "Please rebuild the solution to ensure 'Data/DrillIntelApp.sqlite' is copied to the output directory.",
                        TemplatePath);
                }

                // Copy the authoritative template to ProgramData
                File.Copy(TemplatePath, DatabasePath, overwrite: false);
            }

            var ds = new DataServiceDIntel(DatabasePath);
            if (!ds.IsConnectionOpen())
            {
                throw new InvalidOperationException($"Failed to open Application Database at '{DatabasePath}': {ds.LastError}");
            }

            // Apply SQLite performance PRAGMAs
            ds.ExecuteNonQuery(@"
                PRAGMA foreign_keys = ON;
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = NORMAL;
                PRAGMA busy_timeout = 5000;
            ");

            _dataService = ds;
            _isInitialized = true;

            // Configure BaseDatabaseProvider, Unit and UnitConverter global default service and ensure master units/conversions exist
            BaseDatabaseProvider.BaseDataService = ds;
            DrillIntel.Models.Unit.DefaultDataService = ds;
            DrillIntel.Models.UnitConverter.DefaultDataService = ds;
            DrillIntel.Models.Unit.EnsureTableExists(ds);
            DrillIntel.Models.Unit.CreateDefaultUnits(ds);
            DrillIntel.Models.UnitConverter.EnsureTableExists(ds);
            DrillIntel.Models.UnitConverter.CreateDefaultConversion(ds);
        }
    }

    public IDataServiceDIntel GetDataService()
    {
        if (!IsInitialized)
        {
            Initialize();
        }

        return _dataService ?? throw new InvalidOperationException("Application database service is not initialized.");
    }

    public System.Collections.Generic.List<DrillIntel.Models.AppChannelMapping> GetChannelMappings()
    {
        var ds = GetDataService();
        var dt = ds.GetTable("SELECT ID, MNEMONIC, STANDARD_CHANNEL, DESCRIPTION, DEFAULT_UNIT, SOURCE_VENDOR FROM APP_CHANNEL_MAPPING ORDER BY STANDARD_CHANNEL, MNEMONIC;");
        var list = new System.Collections.Generic.List<DrillIntel.Models.AppChannelMapping>();
        if (dt != null)
        {
            foreach (System.Data.DataRow row in dt.Rows)
            {
                list.Add(new DrillIntel.Models.AppChannelMapping
                {
                    Id = row["ID"] != DBNull.Value ? Convert.ToInt32(row["ID"]) : 0,
                    Mnemonic = row["MNEMONIC"] != DBNull.Value ? Convert.ToString(row["MNEMONIC"]) ?? string.Empty : string.Empty,
                    StandardChannel = row["STANDARD_CHANNEL"] != DBNull.Value ? Convert.ToString(row["STANDARD_CHANNEL"]) ?? string.Empty : string.Empty,
                    Description = row["DESCRIPTION"] != DBNull.Value ? Convert.ToString(row["DESCRIPTION"]) ?? string.Empty : string.Empty,
                    DefaultUnit = row["DEFAULT_UNIT"] != DBNull.Value ? Convert.ToString(row["DEFAULT_UNIT"]) ?? string.Empty : string.Empty,
                    SourceVendor = row["SOURCE_VENDOR"] != DBNull.Value ? Convert.ToString(row["SOURCE_VENDOR"]) ?? string.Empty : "Standard"
                });
            }
        }
        return list;
    }

    public System.Collections.Generic.List<DrillIntel.Models.Unit> GetUnits(string? category = null)
    {
        return DrillIntel.Models.Unit.GetList(GetDataService(), category);
    }

    public System.Collections.Generic.List<DrillIntel.Models.UnitConverter> GetUnitConversions(string? category = null)
    {
        return DrillIntel.Models.UnitConverter.GetList(GetDataService(), category);
    }

    public double ConvertUnit(double value, string fromUnit, string toUnit)
    {
        return DrillIntel.Models.UnitConverter.Convert(value, fromUnit, toUnit, GetDataService());
    }

    public void Close()
    {
        lock (_lock)
        {
            if (_dataService != null)
            {
                _dataService.CloseConnection();
                _dataService.Dispose();
                _dataService = null;
            }
            _isInitialized = false;
        }
    }

    public void Dispose()
    {
        Close();
        GC.SuppressFinalize(this);
    }
}

