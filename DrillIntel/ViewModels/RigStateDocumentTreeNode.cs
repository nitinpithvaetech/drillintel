using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DrillIntel.Models.TChart;

namespace DrillIntel.ViewModels;

/// <summary>
/// TreeView node representing a saved Rig State document in the workspace navigation pane.
/// </summary>
public partial class RigStateDocumentTreeNode : ObservableObject
{
    public DocTemplateItem Model { get; }

    public string TemplateId => Model.TemplateId;
    public string TemplateType => Model.TemplateType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _documentName;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private bool _isDefault;

    [ObservableProperty]
    private string _createdBy;

    [ObservableProperty]
    private string _modifiedBy;

    [ObservableProperty]
    private DateTime? _modifiedDate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    [NotifyPropertyChangedFor(nameof(TrackCountDisplay))]
    private int _trackCount;

    [ObservableProperty]
    private int _channelCount;

    [ObservableProperty]
    private string _orientationDisplay = "Horizontal";

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isVisibleInFilter = true;

    public ObservableCollection<RigStateDocumentTreeNode> Children { get; } = new();

    public string DisplayName => DocumentName;

    public string TrackCountDisplay => TrackCount == 1 ? "1 track" : $"{TrackCount} tracks";

    public RigStateDocumentTreeNode(DocTemplateItem model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _documentName = model.DocumentName;
        _description = model.Description ?? string.Empty;
        _isDefault = model.IsDefault;
        _createdBy = model.CreatedBy ?? "SYSTEM";
        _modifiedBy = model.ModifiedBy ?? "SYSTEM";
        _modifiedDate = model.ModifiedDate;

        UnpackTemplateMetrics(model);
    }

    public void UpdateFromModel(DocTemplateItem model)
    {
        DocumentName = model.DocumentName;
        Description = model.Description ?? string.Empty;
        IsDefault = model.IsDefault;
        ModifiedBy = model.ModifiedBy ?? "SYSTEM";
        ModifiedDate = model.ModifiedDate;
        UnpackTemplateMetrics(model);
    }

    private void UnpackTemplateMetrics(DocTemplateItem model)
    {
        var docData = RigStateDocumentData.FromBytes(model.TemplateData);
        if (docData?.ConsoleModel != null)
        {
            TrackCount = docData.ConsoleModel.Tracks?.Count ?? 0;
            ChannelCount = docData.ConsoleModel.Tracks?.Sum(t => t.Channels?.Count ?? 0) ?? 0;
            OrientationDisplay = docData.ConsoleModel.TrackOrientation.ToString();
        }
        else
        {
            TrackCount = 0;
            ChannelCount = 0;
            OrientationDisplay = "Horizontal";
        }
    }
}

