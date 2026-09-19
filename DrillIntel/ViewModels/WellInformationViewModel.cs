using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DrillIntel.ViewModels;

public partial class WellInformationViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _wellName = string.Empty;

    [ObservableProperty]
    private string _fieldName = "General Field";

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool CanSave => !string.IsNullOrWhiteSpace(WellName);

    public event Action<bool>? RequestClose;

    public WellInformationViewModel(string defaultWellName = "", string defaultFieldName = "General Field")
    {
        WellName = defaultWellName;
        FieldName = string.IsNullOrWhiteSpace(defaultFieldName) ? "General Field" : defaultFieldName;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(WellName))
        {
            ErrorMessage = "Well Name is required.";
            return;
        }

        ErrorMessage = string.Empty;
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}

