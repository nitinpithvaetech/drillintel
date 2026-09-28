using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

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
    [NotifyPropertyChangedFor(nameof(HasContextMenu))]
    private string _name = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDepthLogNode))]
    [NotifyPropertyChangedFor(nameof(IsTimeLogNode))]
    [NotifyPropertyChangedFor(nameof(HasContextMenu))]
    private WellTreeNodeType _type;

    public bool IsDepthLogNode =>
        (Type == WellTreeNodeType.Folder && Name.Equals("Depthlogs", System.StringComparison.OrdinalIgnoreCase)) ||
        Type == WellTreeNodeType.DepthLog;

    public bool IsTimeLogNode =>
        (Type == WellTreeNodeType.Folder && Name.Equals("Timelogs", System.StringComparison.OrdinalIgnoreCase)) ||
        Type == WellTreeNodeType.TimeLog;

    public bool HasContextMenu => IsDepthLogNode || IsTimeLogNode;

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
    private object? _tag;

    public ObservableCollection<WellTreeNode> Children { get; } = new();
}

