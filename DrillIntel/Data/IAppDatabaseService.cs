using System;
using DrillIntel.Data;

namespace DrillIntel.Data;

/// <summary>
/// Service managing the application-level SQLite database located in ProgramData.
/// Provides master configurations (Rig State Master, Channel Mappings, Unit Registry)
/// that are independent of individual projects.
/// </summary>
public interface IAppDatabaseService : IDisposable
{
    /// <summary>
    /// Gets the absolute path to the active application database in ProgramData.
    /// </summary>
    string DatabasePath { get; }

    /// <summary>
    /// Gets the absolute path to the shipped seed template database in the application output directory.
    /// </summary>
    string TemplatePath { get; }

    /// <summary>
    /// Gets whether the application database has been initialized and its connection is open.
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Initializes the database connection, copying the authoritative seed template from the output folder
    /// to ProgramData if the database does not already exist.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Returns the open database service instance connected to the application database.
    /// </summary>
    IDataServiceDIntel GetDataService();

    /// <summary>
    /// Loads all channel mnemonic mappings from table APP_CHANNEL_MAPPING.
    /// </summary>
    System.Collections.Generic.List<DrillIntel.Models.AppChannelMapping> GetChannelMappings();

    /// <summary>
    /// Loads all units from APP_UNIT_MASTER, optionally filtered by category.
    /// </summary>
    System.Collections.Generic.List<DrillIntel.Models.Unit> GetUnits(string? category = null);

    /// <summary>
    /// Loads all unit conversion rules from APP_UNIT_CONVERSIONS, optionally filtered by category.
    /// </summary>
    System.Collections.Generic.List<DrillIntel.Models.UnitConverter> GetUnitConversions(string? category = null);

    /// <summary>
    /// Converts a value between units using registered conversions in the application database.
    /// </summary>
    double ConvertUnit(double value, string fromUnit, string toUnit);

    /// <summary>
    /// Closes the application database connection.
    /// </summary>
    void Close();
}

