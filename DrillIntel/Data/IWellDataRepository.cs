using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using DrillIntel.Models;
using DrillIntel.Data.Objects.DataObjects.Models;

namespace DrillIntel.Data;

/// <summary>
/// Interface for well and log data operations.
/// Enables seamless transition of repository implementation to a separate class library/DLL.
/// </summary>
public interface IWellDataRepository
{
    Task InitializeDictionaryAsync();
    Task<List<VmxCurveDictionary>> GetCurveDictionariesAsync();
    Task LogVmxTimeLogAsync(VmxTimeLog log);
    Task LogVmxDepthLogAsync(VmxDepthLog log);
    Task<List<VmxTimeLog>> GetTimeLogsAsync();
    Task<List<VmxDepthLog>> GetDepthLogsAsync();
    Task<Well?> GetProjectWellAsync();
    Task SaveProjectWellAsync(Well well);
    Task<List<Wellbore>> GetWellboresAsync(string wellId);
    Task SaveProjectWellboreAsync(Wellbore wellbore);
    Task<List<Well>> GetWellsAsync();
    Task EnsureWellAsync(string wellName, string? fieldName = null);
    Task CreateDynamicTimelogTableAsync(string tableName, List<ChannelMapping> mappings);
    Task BulkInsertTimelogAsync(string tableName, DataTable data);
    Task<StreamImportResult> StreamImportDataAsync(
        string tableName,
        string filePath,
        List<ChannelMapping> mappings,
        IProgress<ImportProgressReport>? progress = null,
        CancellationToken cancellationToken = default);
}

