using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DrillIntel.Models;

public partial class RecentProject : ObservableObject
{
    private string _filePath = string.Empty;
    public string FilePath
    {
        get => _filePath;
        set
        {
            if (SetProperty(ref _filePath, value))
            {
                OnPropertyChanged(nameof(ProjectName));
                OnPropertyChanged(nameof(DirectoryDisplay));
                OnPropertyChanged(nameof(FileExists));
                OnPropertyChanged(nameof(FileSizeDisplay));
            }
        }
    }

    private string? _projectName;
    public string ProjectName
    {
        get => !string.IsNullOrWhiteSpace(_projectName) ? _projectName : Path.GetFileNameWithoutExtension(FilePath);
        set => SetProperty(ref _projectName, value);
    }

    private string? _wellName;
    public string? WellName
    {
        get => _wellName;
        set => SetProperty(ref _wellName, value);
    }

    private string? _fieldName;
    public string? FieldName
    {
        get => _fieldName;
        set => SetProperty(ref _fieldName, value);
    }

    private DateTime _lastOpened = DateTime.Now;
    public DateTime LastOpened
    {
        get => _lastOpened;
        set
        {
            if (SetProperty(ref _lastOpened, value))
            {
                OnPropertyChanged(nameof(LastOpenedDisplay));
            }
        }
    }

    private bool _isPinned;
    public bool IsPinned
    {
        get => _isPinned;
        set => SetProperty(ref _isPinned, value);
    }

    public string DirectoryDisplay
    {
        get
        {
            try
            {
                return Path.GetDirectoryName(FilePath) ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public bool FileExists
    {
        get
        {
            try
            {
                return !string.IsNullOrWhiteSpace(FilePath) && File.Exists(FilePath);
            }
            catch
            {
                return false;
            }
        }
    }

    public string FileSizeDisplay
    {
        get
        {
            try
            {
                if (!FileExists) return "File missing";
                var fi = new FileInfo(FilePath);
                long bytes = fi.Length;
                if (bytes < 1024) return $"{bytes} B";
                if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
                return $"{bytes / (1024.0 * 1024.0):F1} MB";
            }
            catch
            {
                return "Unknown";
            }
        }
    }

    public string WellAndFieldDisplay
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(WellName) && !string.IsNullOrWhiteSpace(FieldName))
                return $"{WellName} • {FieldName}";
            if (!string.IsNullOrWhiteSpace(WellName))
                return WellName;
            return "General Well";
        }
    }

    public string LastOpenedDisplay
    {
        get
        {
            var diff = DateTime.Now - LastOpened;
            if (diff.TotalMinutes < 2) return "Just now";
            if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
            if (diff.TotalHours < 24 && LastOpened.Date == DateTime.Today) return $"Today at {LastOpened:hh:mm tt}";
            if (diff.TotalDays < 2 && LastOpened.Date == DateTime.Today.AddDays(-1)) return $"Yesterday at {LastOpened:hh:mm tt}";
            if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";
            return LastOpened.ToString("dd MMM yyyy");
        }
    }

    public void RefreshState()
    {
        OnPropertyChanged(nameof(FileExists));
        OnPropertyChanged(nameof(FileSizeDisplay));
        OnPropertyChanged(nameof(LastOpenedDisplay));
    }
}

