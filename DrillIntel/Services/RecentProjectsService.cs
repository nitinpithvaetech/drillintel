using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using DrillIntel.Models;
using DrillIntel.Data;

namespace DrillIntel.Services;

public interface IRecentProjectsService
{
    IReadOnlyList<RecentProject> GetRecentProjects();
    void AddOrUpdate(string filePath, string? wellName = null, string? fieldName = null);
    void Remove(string filePath);
    void Clear();
    void TogglePin(string filePath);
    event EventHandler? RecentProjectsChanged;
}

public class RecentProjectsService : IRecentProjectsService
{
    private const int MaxRecentCount = 25;
    private readonly string _storagePath;
    private readonly List<RecentProject> _recentProjects = new();
    private readonly object _syncLock = new();

    public event EventHandler? RecentProjectsChanged;

    public RecentProjectsService()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dir = Path.Combine(appData, "DrillIntel");
        if (!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        _storagePath = Path.Combine(dir, "recent_projects.json");
        LoadFromDisk();
    }

    public IReadOnlyList<RecentProject> GetRecentProjects()
    {
        lock (_syncLock)
        {
            foreach (var item in _recentProjects)
            {
                item.RefreshState();
            }

            return _recentProjects
                .OrderByDescending(p => p.IsPinned)
                .ThenByDescending(p => p.LastOpened)
                .ToList();
        }
    }

    public void AddOrUpdate(string filePath, string? wellName = null, string? fieldName = null)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        lock (_syncLock)
        {
            try
            {
                var fullPath = Path.GetFullPath(filePath);
                var existing = _recentProjects.FirstOrDefault(p =>
                    string.Equals(p.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));

                // If well metadata is not provided, try querying the database directly
                if (string.IsNullOrWhiteSpace(wellName) && File.Exists(fullPath))
                {
                    (wellName, fieldName) = TryExtractWellInfo(fullPath);
                }

                if (existing != null)
                {
                    existing.LastOpened = DateTime.Now;
                    if (!string.IsNullOrWhiteSpace(wellName)) existing.WellName = wellName;
                    if (!string.IsNullOrWhiteSpace(fieldName)) existing.FieldName = fieldName;
                    existing.RefreshState();
                }
                else
                {
                    var item = new RecentProject
                    {
                        FilePath = fullPath,
                        ProjectName = Path.GetFileNameWithoutExtension(fullPath),
                        WellName = wellName,
                        FieldName = fieldName,
                        LastOpened = DateTime.Now,
                        IsPinned = false
                    };
                    _recentProjects.Add(item);
                }

                TrimExcess();
                SaveToDisk();
            }
            catch
            {
                // Non-fatal, keep service resilient
            }
        }

        RecentProjectsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        bool removed = false;
        lock (_syncLock)
        {
            var fullPath = Path.GetFullPath(filePath);
            var item = _recentProjects.FirstOrDefault(p =>
                string.Equals(p.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                _recentProjects.Remove(item);
                removed = true;
                SaveToDisk();
            }
        }

        if (removed)
        {
            RecentProjectsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Clear()
    {
        lock (_syncLock)
        {
            _recentProjects.Clear();
            SaveToDisk();
        }

        RecentProjectsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void TogglePin(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return;

        bool changed = false;
        lock (_syncLock)
        {
            var fullPath = Path.GetFullPath(filePath);
            var item = _recentProjects.FirstOrDefault(p =>
                string.Equals(p.FilePath, fullPath, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                item.IsPinned = !item.IsPinned;
                changed = true;
                SaveToDisk();
            }
        }

        if (changed)
        {
            RecentProjectsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void TrimExcess()
    {
        if (_recentProjects.Count > MaxRecentCount)
        {
            var unpinned = _recentProjects
                .Where(p => !p.IsPinned)
                .OrderBy(p => p.LastOpened)
                .ToList();

            while (_recentProjects.Count > MaxRecentCount && unpinned.Count > 0)
            {
                var oldest = unpinned[0];
                unpinned.RemoveAt(0);
                _recentProjects.Remove(oldest);
            }
        }
    }

    private void LoadFromDisk()
    {
        lock (_syncLock)
        {
            _recentProjects.Clear();
            if (!File.Exists(_storagePath)) return;

            try
            {
                var json = File.ReadAllText(_storagePath);
                if (string.IsNullOrWhiteSpace(json)) return;

                var items = JsonSerializer.Deserialize<List<RecentProjectDto>>(json);
                if (items != null)
                {
                    foreach (var dto in items)
                    {
                        if (string.IsNullOrWhiteSpace(dto.FilePath)) continue;
                        _recentProjects.Add(new RecentProject
                        {
                            FilePath = dto.FilePath,
                            ProjectName = dto.ProjectName ?? Path.GetFileNameWithoutExtension(dto.FilePath),
                            WellName = dto.WellName,
                            FieldName = dto.FieldName,
                            LastOpened = dto.LastOpened,
                            IsPinned = dto.IsPinned
                        });
                    }
                }
            }
            catch
            {
                // If the json file is corrupted, recover safely
            }
        }
    }

    private void SaveToDisk()
    {
        lock (_syncLock)
        {
            try
            {
                var dtos = _recentProjects.Select(p => new RecentProjectDto
                {
                    FilePath = p.FilePath,
                    ProjectName = p.ProjectName,
                    WellName = p.WellName,
                    FieldName = p.FieldName,
                    LastOpened = p.LastOpened,
                    IsPinned = p.IsPinned
                }).ToList();

                var json = JsonSerializer.Serialize(dtos, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_storagePath, json);
            }
            catch
            {
                // Resilience against write permission issues
            }
        }
    }

    private static (string? WellName, string? FieldName) TryExtractWellInfo(string dintelFilePath)
    {
        try
        {
            using var ds = new DataServiceDIntel(dintelFilePath);
            var table = ds.GetTable("SELECT WELL_NAME, FIELD FROM VMX_WELL LIMIT 1");
            if (table.Rows.Count > 0)
            {
                var row = table.Rows[0];
                string? well = row.IsNull("WELL_NAME") ? null : row["WELL_NAME"]?.ToString();
                string? field = row.IsNull("FIELD") ? null : row["FIELD"]?.ToString();
                return (well, field);
            }
        }
        catch
        {
            // Database might be locked, empty, or uninitialized
        }

        return (null, null);
    }

    private class RecentProjectDto
    {
        public string FilePath { get; set; } = string.Empty;
        public string? ProjectName { get; set; }
        public string? WellName { get; set; }
        public string? FieldName { get; set; }
        public DateTime LastOpened { get; set; }
        public bool IsPinned { get; set; }
    }
}
