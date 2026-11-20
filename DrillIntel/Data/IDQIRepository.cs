using System.Collections.Generic;
using System.Threading.Tasks;
using DrillIntel.Data.Objects.DataObjects.DQI;

namespace DrillIntel.Data
{
    public interface IDQIRepository
    {
        Task EnsureTablesExistAsync();
        Task<List<DQIWellLookupItem>> GetLiveWellsAsync();
        Task<List<DQIChannel>> GetChannelsAsync();
        Task<bool> SaveChannelsAsync(List<DQIChannel> channels);
        Task<List<DQIDataBoundary>> GetBoundariesAsync();
        Task<bool> SaveBoundariesAsync(List<DQIDataBoundary> boundaries);
        Task<string> GetNullValuesAsync();
        Task<bool> SaveNullValuesAsync(string nullValues);
        Task<WellAverageIndices> GetWellAverageIndicesAsync(string wellId);
        Task<DQIData> LoadDQIDataAsync(string wellId, string mnemonic, int offsetMinutes = 0);
        Task<List<string>> GetAvailableChannelMnemonicsFromTimeLogAsync(string wellId);
    }
}
