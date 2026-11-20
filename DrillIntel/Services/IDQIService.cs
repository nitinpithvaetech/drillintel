using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DrillIntel.Data.Objects.DataObjects.DQI;

namespace DrillIntel.Services
{
    public interface IDQIService
    {
        Task<List<DQIWellLookupItem>> GetLiveWellsAsync();
        Task<List<DQIChannel>> GetChannelsAsync();
        Task<bool> SaveChannelsAsync(List<DQIChannel> channels);
        Task<List<DQIDataBoundary>> GetBoundariesAsync();
        Task<bool> SaveBoundariesAsync(List<DQIDataBoundary> boundaries);
        Task<string> GetNullValuesAsync();
        Task<bool> SaveNullValuesAsync(string nullValues);
        Task<WellAverageIndices> GetWellAverageIndicesAsync(string wellId);
        Task<DQIData> LoadDQIDataAsync(string wellId, string mnemonic);
        Task<List<string>> GetAvailableChannelMnemonicsFromTimeLogAsync(string wellId);
        Task<bool> ProcessWellDQIAsync(
            string wellId,
            IProgress<(double ProgressPercent, string StatusMessage)>? progress = null,
            CancellationToken ct = default);
    }
}
