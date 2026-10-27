using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DrillIntel.Models.Broomstick;

namespace DrillIntel.ViewModels.Broomstick;

/// <summary>
/// TreeView node representing a folder, Well group, or Plan group in the Broomstick TreeView hierarchy.
/// </summary>
public partial class BroomstickDocumentTreeGroupNode : ObservableObject
{
    [ObservableProperty]
    private string _header = string.Empty;

    [ObservableProperty]
    private string _groupType = "Group"; // "Well", "Plan", or "Category"

    [ObservableProperty]
    private string _groupId = string.Empty;

    [ObservableProperty]
    private int _documentCount;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isVisibleInFilter = true;

    public ObservableCollection<object> Children { get; } = new();

    public string BadgeDisplay => $"{DocumentCount} {(DocumentCount == 1 ? "document" : "documents")}";
}

/// <summary>
/// TreeView leaf node representing a saved Broomstick Document template.
/// </summary>
public partial class BroomstickDocumentTreeNode : ObservableObject
{
    public BroomstickTemplateItem Model { get; }

    public string TemplateId => Model.TemplateId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string _documentName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WellDisplay))]
    private string _wellId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WellDisplay))]
    private string? _wellName;

    [ObservableProperty]
    private string? _wellboreId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LogDisplay))]
    private string? _logId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanDisplay))]
    private string? _planId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlanDisplay))]
    private string? _planName;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ProfileDisplay))]
    private string? _profileId;

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
    private bool _isSelected;

    [ObservableProperty]
    private bool _isVisibleInFilter = true;

    public string DisplayName => DocumentName;

    public string WellDisplay => !string.IsNullOrWhiteSpace(WellName)
        ? (!string.IsNullOrWhiteSpace(WellId) && !string.Equals(WellId, WellName, StringComparison.OrdinalIgnoreCase) ? $"{WellName} ({WellId})" : WellName)
        : (!string.IsNullOrWhiteSpace(WellId) ? WellId : "(None)");

    public string LogDisplay => !string.IsNullOrWhiteSpace(LogId) ? LogId : "(None)";

    public string PlanDisplay => !string.IsNullOrWhiteSpace(PlanName)
        ? (!string.IsNullOrWhiteSpace(PlanId) && !string.Equals(PlanId, PlanName, StringComparison.OrdinalIgnoreCase) ? $"{PlanName} ({PlanId})" : PlanName)
        : (!string.IsNullOrWhiteSpace(PlanId) ? PlanId : "(None)");

    public string ProfileDisplay => !string.IsNullOrWhiteSpace(ProfileId) ? ProfileId : "(Default Profile)";

    public BroomstickDocumentTreeNode(BroomstickTemplateItem model)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        _documentName = model.DocumentName;
        _wellId = model.WellId;
        _wellboreId = model.WellboreId;
        _logId = model.LogId;
        _planId = model.PlanId;
        _profileId = model.ProfileId;
        _description = model.Description ?? string.Empty;
        _isDefault = model.IsDefault;
        _createdBy = model.CreatedBy ?? "SYSTEM";
        _modifiedBy = model.ModifiedBy ?? "SYSTEM";
        _modifiedDate = model.ModifiedDate;
    }

    public void UpdateFromModel(BroomstickTemplateItem model)
    {
        DocumentName = model.DocumentName;
        WellId = model.WellId;
        WellboreId = model.WellboreId;
        LogId = model.LogId;
        PlanId = model.PlanId;
        ProfileId = model.ProfileId;
        Description = model.Description ?? string.Empty;
        IsDefault = model.IsDefault;
        ModifiedBy = model.ModifiedBy ?? "SYSTEM";
        ModifiedDate = model.ModifiedDate;
    }
}

