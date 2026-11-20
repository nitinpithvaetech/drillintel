using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.DQI;
using DrillIntel.Projects;

namespace DrillIntel.Services
{
    public class DQIService : IDQIService
    {
        private readonly IDQIRepository _repository;
        private readonly ProjectSession _session;

        public DQIService(IDQIRepository repository, ProjectSession session)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public Task<List<DQIWellLookupItem>> GetLiveWellsAsync() => _repository.GetLiveWellsAsync();

        public Task<List<DQIChannel>> GetChannelsAsync() => _repository.GetChannelsAsync();

        public Task<bool> SaveChannelsAsync(List<DQIChannel> channels) => _repository.SaveChannelsAsync(channels);

        public Task<List<DQIDataBoundary>> GetBoundariesAsync() => _repository.GetBoundariesAsync();

        public Task<bool> SaveBoundariesAsync(List<DQIDataBoundary> boundaries) => _repository.SaveBoundariesAsync(boundaries);

        public Task<string> GetNullValuesAsync() => _repository.GetNullValuesAsync();

        public Task<bool> SaveNullValuesAsync(string nullValues) => _repository.SaveNullValuesAsync(nullValues);

        public Task<WellAverageIndices> GetWellAverageIndicesAsync(string wellId) => _repository.GetWellAverageIndicesAsync(wellId);

        public Task<DQIData> LoadDQIDataAsync(string wellId, string mnemonic) => _repository.LoadDQIDataAsync(wellId, mnemonic);

        public Task<List<string>> GetAvailableChannelMnemonicsFromTimeLogAsync(string wellId) => _repository.GetAvailableChannelMnemonicsFromTimeLogAsync(wellId);

        public async Task<bool> ProcessWellDQIAsync(
            string wellId,
            IProgress<(double ProgressPercent, string StatusMessage)>? progress = null,
            CancellationToken ct = default)
        {
            var ds = _session.GetDataService();
            var processor = new DQIProcessor(ds);
            return await processor.ProcessWellDQIAsync(wellId, progress, ct);
        }
    }
}
