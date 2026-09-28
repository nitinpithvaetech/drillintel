using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data.Objects.DataObjects.Models;

namespace DrillIntel.ViewModels;

public partial class WellInformationViewModel : ObservableObject
{
    [ObservableProperty]
    private string _wellId = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    private string _wellName = string.Empty;

    [ObservableProperty]
    private string _legalName = string.Empty;

    [ObservableProperty]
    private string _fieldName = "General Field";

    [ObservableProperty]
    private string _operatorName = string.Empty;

    [ObservableProperty]
    private string _operatorDiv = string.Empty;

    [ObservableProperty]
    private string _status = "Active";

    [ObservableProperty]
    private string _purpose = "Development";

    [ObservableProperty]
    private string _uwi = string.Empty;

    [ObservableProperty]
    private string _licenseNo = string.Empty;

    [ObservableProperty]
    private string _rigName = string.Empty;

    [ObservableProperty]
    private string _spudDate = string.Empty;

    [ObservableProperty]
    private string _country = string.Empty;

    [ObservableProperty]
    private string _state = string.Empty;

    [ObservableProperty]
    private string _county = string.Empty;

    [ObservableProperty]
    private string _block = string.Empty;

    [ObservableProperty]
    private string _district = string.Empty;

    [ObservableProperty]
    private string _region = string.Empty;

    [ObservableProperty]
    private string _timeZone = string.Empty;

    [ObservableProperty]
    private double _wellheadElevation;

    [ObservableProperty]
    private double _groundElevation;

    [ObservableProperty]
    private double _waterDepth;

    [ObservableProperty]
    private string _comments = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    public bool CanSave => !string.IsNullOrWhiteSpace(WellName);

    public event Action<bool>? RequestClose;

    public WellInformationViewModel(string defaultWellName = "", string defaultFieldName = "General Field")
    {
        WellName = defaultWellName;
        FieldName = string.IsNullOrWhiteSpace(defaultFieldName) ? "General Field" : defaultFieldName;
    }

    public WellInformationViewModel(Well well) : this(well.name, well.field)
    {
        LoadFromWell(well);
    }

    public void LoadFromWell(Well well)
    {
        if (well == null) return;
        WellId = well.ObjectID ?? string.Empty;
        WellName = well.name ?? string.Empty;
        LegalName = well.nameLegal ?? string.Empty;
        FieldName = !string.IsNullOrWhiteSpace(well.field) ? well.field : "General Field";
        OperatorName = well.operatorName ?? string.Empty;
        OperatorDiv = well.operatorDiv ?? string.Empty;
        Status = !string.IsNullOrWhiteSpace(well.statusWell) ? well.statusWell : "Active";
        Purpose = !string.IsNullOrWhiteSpace(well.purposeWell) ? well.purposeWell : "Development";
        Uwi = well.numAPI ?? string.Empty;
        LicenseNo = well.numLicense ?? string.Empty;
        RigName = well.RigName ?? string.Empty;
        SpudDate = well.dTimSpud ?? string.Empty;
        Country = well.country ?? string.Empty;
        State = well.state ?? string.Empty;
        County = well.county ?? string.Empty;
        Block = well.block ?? string.Empty;
        District = well.district ?? string.Empty;
        Region = well.region ?? string.Empty;
        TimeZone = well.timeZone ?? string.Empty;
        WellheadElevation = well.wellheadElevation;
        GroundElevation = well.groundElevation;
        WaterDepth = well.waterDepth;
        Comments = well.Comments ?? string.Empty;
    }

    public void ApplyToWell(Well well)
    {
        if (well == null) return;
        well.name = WellName.Trim();
        well.nameLegal = LegalName?.Trim() ?? string.Empty;
        well.field = !string.IsNullOrWhiteSpace(FieldName) ? FieldName.Trim() : "General Field";
        well.operatorName = OperatorName?.Trim() ?? string.Empty;
        well.operatorDiv = OperatorDiv?.Trim() ?? string.Empty;
        well.statusWell = Status?.Trim() ?? string.Empty;
        well.purposeWell = Purpose?.Trim() ?? string.Empty;
        well.numAPI = Uwi?.Trim() ?? string.Empty;
        well.numLicense = LicenseNo?.Trim() ?? string.Empty;
        well.RigName = RigName?.Trim() ?? string.Empty;
        well.dTimSpud = SpudDate?.Trim() ?? string.Empty;
        well.country = Country?.Trim() ?? string.Empty;
        well.state = State?.Trim() ?? string.Empty;
        well.county = County?.Trim() ?? string.Empty;
        well.block = Block?.Trim() ?? string.Empty;
        well.district = District?.Trim() ?? string.Empty;
        well.region = Region?.Trim() ?? string.Empty;
        well.timeZone = TimeZone?.Trim() ?? string.Empty;
        well.wellheadElevation = WellheadElevation;
        well.groundElevation = GroundElevation;
        well.waterDepth = WaterDepth;
        well.Comments = Comments?.Trim() ?? string.Empty;
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

