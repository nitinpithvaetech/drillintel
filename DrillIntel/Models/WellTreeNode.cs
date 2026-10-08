using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DrillIntel.Data.Objects.DataObjects.Models;

namespace DrillIntel.Models;

public enum WellTreeNodeType
{
    Well,
    Folder,
    TimeLog,
    DepthLog
}

public partial class WellTreeNode : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDepthLogNode))]
    [NotifyPropertyChangedFor(nameof(IsTimeLogNode))]
    [NotifyPropertyChangedFor(nameof(IsWellNode))]
    [NotifyPropertyChangedFor(nameof(IsLogNode))]
    [NotifyPropertyChangedFor(nameof(HasContextMenu))]
    [NotifyPropertyChangedFor(nameof(IsLinkedTimeLog))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDepthLogNode))]
    [NotifyPropertyChangedFor(nameof(IsTimeLogNode))]
    [NotifyPropertyChangedFor(nameof(IsWellNode))]
    [NotifyPropertyChangedFor(nameof(IsLogNode))]
    [NotifyPropertyChangedFor(nameof(HasContextMenu))]
    [NotifyPropertyChangedFor(nameof(IsLinkedTimeLog))]
    private WellTreeNodeType _type;

    public bool IsWellNode => Type == WellTreeNodeType.Well;

    public bool IsDepthLogNode => Type == WellTreeNodeType.DepthLog;

    public bool IsTimeLogNode => Type == WellTreeNodeType.TimeLog;

    public bool IsLogNode => IsDepthLogNode || IsTimeLogNode;

    public bool HasContextMenu => IsWellNode || IsDepthLogNode || IsTimeLogNode;

    public bool IsLinkedTimeLog
    {
        get
        {
            if (Type != WellTreeNodeType.TimeLog) return false;
            if (Tag is TimeLog tl)
            {
                return tl.LinkToParent &&
                       !string.IsNullOrWhiteSpace(tl.LinkWellID) &&
                       !string.IsNullOrWhiteSpace(tl.LinkWellboreID) &&
                       !string.IsNullOrWhiteSpace(tl.LinkLogID) &&
                       (string.IsNullOrWhiteSpace(tl.ObjectID) || !tl.LinkLogID.Equals(tl.ObjectID, StringComparison.OrdinalIgnoreCase));
            }
            return false;
        }
    }

    public void NotifyLinkedStatusChanged()
    {
        OnPropertyChanged(nameof(IsLinkedTimeLog));
    }

    [ObservableProperty]
    private string _iconKind = "File";

    [ObservableProperty]
    private string _iconColor = "#2196F3";

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private string _badge = string.Empty;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLinkedTimeLog))]
    private object? _tag;

    public ObservableCollection<WellTreeNode> Children { get; } = new();
}

