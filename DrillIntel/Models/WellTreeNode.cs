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
    private string _name = string.Empty;

    [ObservableProperty]
    private WellTreeNodeType _type;

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

