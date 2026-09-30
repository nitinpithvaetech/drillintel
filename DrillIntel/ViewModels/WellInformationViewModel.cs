using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data.Objects.DataObjects.Models;

namespace DrillIntel.ViewModels;

public partial class WellInformationViewModel : ObservableObject
{
    private Well? _backupWell;
    private string _initialWellName = string.Empty;
    private string _initialFieldName = "General Field";

    // Window Header & Feedback
    [ObservableProperty]
    private string _title = "Well Information";

    [ObservableProperty]
    private string _subtitle = "Configure well identification, geographic coordinates, elevations, and operational parameters.";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isStatusError;

    [ObservableProperty]
    private bool _isStatusVisible;

    [ObservableProperty]
    private int _selectedTabIndex;

    // Backward compatibility error message
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    // General Identification
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
    private string _contractor = string.Empty;

    [ObservableProperty]
    private string _pcInterest = string.Empty;

    [ObservableProperty]
    private string _status = "Active";

    [ObservableProperty]
    private string _purpose = "Development";

    [ObservableProperty]
    private string _uwi = string.Empty;

    // Key Personnel
    [ObservableProperty]
    private string _drillingSupr = string.Empty;

    [ObservableProperty]
    private string _drillingEng = string.Empty;

    [ObservableProperty]
    private string _rep = string.Empty;

    [ObservableProperty]
    private string _toolPusher = string.Empty;

    [ObservableProperty]
    private string _drlgEngDept = string.Empty;

    [ObservableProperty]
    private string _drlgOpDept = string.Empty;

    // Permits, Milestones & Objectives
    [ObservableProperty]
    private string _licenseNo = string.Empty;

    [ObservableProperty]
    private string _licenseDate = string.Empty;

    [ObservableProperty]
    private string _spudDate = string.Empty;

    [ObservableProperty]
    private string _paDate = string.Empty;

    [ObservableProperty]
    private string _tightHoleNo = string.Empty;

    [ObservableProperty]
    private string _reEntryNo = string.Empty;

    [ObservableProperty]
    private string _objective = string.Empty;

    [ObservableProperty]
    private string _tdDate = string.Empty;

    [ObservableProperty]
    private string _tdFormation = string.Empty;

    [ObservableProperty]
    private double _plannedDepth;

    [ObservableProperty]
    private double _plannedDays;

    // Geographic & Location
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
    private string _wellLocation = string.Empty;

    [ObservableProperty]
    private double _latitude;

    [ObservableProperty]
    private double _longitude;

    [ObservableProperty]
    private double _xCoOrd;

    [ObservableProperty]
    private double _yCoOrd;

    [ObservableProperty]
    private string _permDatum = string.Empty;

    [ObservableProperty]
    private string _sec = string.Empty;

    [ObservableProperty]
    private string _twp = string.Empty;

    [ObservableProperty]
    private string _rge = string.Empty;

    [ObservableProperty]
    private string _legalDesc = string.Empty;

    // Elevations & Depths
    [ObservableProperty]
    private double _wellheadElevation;

    [ObservableProperty]
    private double _groundElevation;

    [ObservableProperty]
    private double _waterDepth;

    // Rig & Operations
    [ObservableProperty]
    private string _rigName = string.Empty;

    [ObservableProperty]
    private string _rigType = string.Empty;

    [ObservableProperty]
    private string _contType = string.Empty;

    [ObservableProperty]
    private string _edrProvider = string.Empty;

    [ObservableProperty]
    private string _dataSource = string.Empty;

    [ObservableProperty]
    private double _rigCost;

    [ObservableProperty]
    private bool _historical;

    [ObservableProperty]
    private double _pipeLength = 30;

    [ObservableProperty]
    private double _standLength = 90;

    // Benchmarks
    [ObservableProperty]
    private double _drlgConnTime;

    [ObservableProperty]
    private double _tripConnTime;

    [ObservableProperty]
    private double _btsTime;

    [ObservableProperty]
    private double _stsTime;

    [ObservableProperty]
    private double _stbTime;

    [ObservableProperty]
    private double _tripInSpeed;

    [ObservableProperty]
    private double _tripOutSpeed;

    // Pumps
    [ObservableProperty]
    private string _pump1Model = string.Empty;

    [ObservableProperty]
    private string _pump1Stroke = string.Empty;

    [ObservableProperty]
    private string _pump1Liner = string.Empty;

    [ObservableProperty]
    private string _pump2Model = string.Empty;

    [ObservableProperty]
    private string _pump2Stroke = string.Empty;

    [ObservableProperty]
    private string _pump2Liner = string.Empty;

    [ObservableProperty]
    private string _pump3Model = string.Empty;

    [ObservableProperty]
    private string _pump3Stroke = string.Empty;

    [ObservableProperty]
    private string _pump3Liner = string.Empty;

    // Remarks
    [ObservableProperty]
    private string _comments = string.Empty;

    public bool CanSave => !string.IsNullOrWhiteSpace(WellName);

    public event Action<bool>? RequestClose;

    public WellInformationViewModel(string defaultWellName = "", string defaultFieldName = "General Field")
    {
        WellName = defaultWellName;
        FieldName = string.IsNullOrWhiteSpace(defaultFieldName) ? "General Field" : defaultFieldName;
        _initialWellName = WellName;
        _initialFieldName = FieldName;
    }

    public WellInformationViewModel(Well well) : this(well?.name ?? "", well?.field ?? "General Field")
    {
        if (well != null)
        {
            LoadFromWell(well);
        }
    }

    partial void OnWellNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && IsStatusError)
        {
            IsStatusVisible = false;
            ErrorMessage = string.Empty;
        }
    }

    public void LoadFromWell(Well well)
    {
        if (well == null) return;
        _backupWell = well.GetCopy();
        _initialWellName = well.name ?? string.Empty;
        _initialFieldName = !string.IsNullOrWhiteSpace(well.field) ? well.field : "General Field";

        WellId = well.ObjectID ?? string.Empty;
        WellName = well.name ?? string.Empty;
        LegalName = well.nameLegal ?? string.Empty;
        FieldName = !string.IsNullOrWhiteSpace(well.field) ? well.field : "General Field";
        OperatorName = well.operatorName ?? string.Empty;
        OperatorDiv = well.operatorDiv ?? string.Empty;
        Contractor = well.Contractor ?? string.Empty;
        PcInterest = well.pcInterest ?? string.Empty;
        Status = !string.IsNullOrWhiteSpace(well.statusWell) ? well.statusWell : "Active";
        Purpose = !string.IsNullOrWhiteSpace(well.purposeWell) ? well.purposeWell : "Development";
        Uwi = well.numAPI ?? string.Empty;

        DrillingSupr = well.DrillingSupr ?? string.Empty;
        DrillingEng = well.DrillingEng ?? string.Empty;
        Rep = well.Rep ?? string.Empty;
        ToolPusher = well.ToolPusher ?? string.Empty;
        DrlgEngDept = well.DrlgEngDept ?? string.Empty;
        DrlgOpDept = well.DrlgOpDept ?? string.Empty;

        LicenseNo = well.numLicense ?? string.Empty;
        LicenseDate = well.dTimeLicense ?? string.Empty;
        SpudDate = well.dTimSpud ?? string.Empty;
        PaDate = well.dTimPa ?? string.Empty;
        TightHoleNo = well.TightHoleNo ?? string.Empty;
        ReEntryNo = well.ReEntryNo ?? string.Empty;
        Objective = well.Objective ?? string.Empty;
        TdDate = well.TDDate ?? string.Empty;
        TdFormation = well.TDFormation ?? string.Empty;
        PlannedDepth = well.PlannedDepth;
        PlannedDays = well.PlannedDays;

        Country = well.country ?? string.Empty;
        State = well.state ?? string.Empty;
        County = well.county ?? string.Empty;
        Block = well.block ?? string.Empty;
        District = well.district ?? string.Empty;
        Region = well.region ?? string.Empty;
        TimeZone = well.timeZone ?? string.Empty;
        WellLocation = well.WellLocation ?? string.Empty;

        Latitude = well.latitude;
        Longitude = well.longitude;
        XCoOrd = well.xCoOrd;
        YCoOrd = well.yCoOrd;
        PermDatum = well.dtmPermanent ?? string.Empty;
        Sec = well.SEC ?? string.Empty;
        Twp = well.TWP ?? string.Empty;
        Rge = well.RGE ?? string.Empty;
        LegalDesc = well.LegalDesc ?? string.Empty;

        WellheadElevation = well.wellheadElevation;
        GroundElevation = well.groundElevation;
        WaterDepth = well.waterDepth;

        RigName = well.RigName ?? string.Empty;
        RigType = well.RigType ?? string.Empty;
        ContType = well.ContType ?? string.Empty;
        EdrProvider = well.EDRProvider ?? string.Empty;
        DataSource = well.DataSource ?? string.Empty;
        RigCost = well.RigCost;
        Historical = well.Historical;
        PipeLength = well.PipeLength > 0 ? well.PipeLength : 30;
        StandLength = well.StandLength > 0 ? well.StandLength : 90;

        DrlgConnTime = well.DrlgConnTime;
        TripConnTime = well.TripConnTime;
        BtsTime = well.BTSTime;
        StsTime = well.STSTime;
        StbTime = well.STBTime;
        TripInSpeed = well.TripInSpeed;
        TripOutSpeed = well.TripOutSpeed;

        Pump1Model = well.Pump1Model ?? string.Empty;
        Pump1Stroke = well.Pump1Stroke ?? string.Empty;
        Pump1Liner = well.Pump1Liner ?? string.Empty;
        Pump2Model = well.Pump2Model ?? string.Empty;
        Pump2Stroke = well.Pump2Stroke ?? string.Empty;
        Pump2Liner = well.Pump2Liner ?? string.Empty;
        Pump3Model = well.Pump3Model ?? string.Empty;
        Pump3Stroke = well.Pump3Stroke ?? string.Empty;
        Pump3Liner = well.Pump3Liner ?? string.Empty;

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
        well.Contractor = Contractor?.Trim() ?? string.Empty;
        well.pcInterest = PcInterest?.Trim() ?? string.Empty;
        well.statusWell = Status?.Trim() ?? string.Empty;
        well.purposeWell = Purpose?.Trim() ?? string.Empty;
        well.numAPI = Uwi?.Trim() ?? string.Empty;

        well.DrillingSupr = DrillingSupr?.Trim() ?? string.Empty;
        well.DrillingEng = DrillingEng?.Trim() ?? string.Empty;
        well.Rep = Rep?.Trim() ?? string.Empty;
        well.ToolPusher = ToolPusher?.Trim() ?? string.Empty;
        well.DrlgEngDept = DrlgEngDept?.Trim() ?? string.Empty;
        well.DrlgOpDept = DrlgOpDept?.Trim() ?? string.Empty;

        well.numLicense = LicenseNo?.Trim() ?? string.Empty;
        well.dTimeLicense = LicenseDate?.Trim() ?? string.Empty;
        well.dTimSpud = SpudDate?.Trim() ?? string.Empty;
        well.dTimPa = PaDate?.Trim() ?? string.Empty;
        well.TightHoleNo = TightHoleNo?.Trim() ?? string.Empty;
        well.ReEntryNo = ReEntryNo?.Trim() ?? string.Empty;
        well.Objective = Objective?.Trim() ?? string.Empty;
        well.TDDate = TdDate?.Trim() ?? string.Empty;
        well.TDFormation = TdFormation?.Trim() ?? string.Empty;
        well.PlannedDepth = PlannedDepth;
        well.PlannedDays = PlannedDays;

        well.country = Country?.Trim() ?? string.Empty;
        well.state = State?.Trim() ?? string.Empty;
        well.county = County?.Trim() ?? string.Empty;
        well.block = Block?.Trim() ?? string.Empty;
        well.district = District?.Trim() ?? string.Empty;
        well.region = Region?.Trim() ?? string.Empty;
        well.timeZone = TimeZone?.Trim() ?? string.Empty;
        well.WellLocation = WellLocation?.Trim() ?? string.Empty;

        well.latitude = Latitude;
        well.longitude = Longitude;
        well.xCoOrd = XCoOrd;
        well.yCoOrd = YCoOrd;
        well.dtmPermanent = PermDatum?.Trim() ?? string.Empty;
        well.SEC = Sec?.Trim() ?? string.Empty;
        well.TWP = Twp?.Trim() ?? string.Empty;
        well.RGE = Rge?.Trim() ?? string.Empty;
        well.LegalDesc = LegalDesc?.Trim() ?? string.Empty;

        well.wellheadElevation = WellheadElevation;
        well.groundElevation = GroundElevation;
        well.waterDepth = WaterDepth;

        well.RigName = RigName?.Trim() ?? string.Empty;
        well.RigType = RigType?.Trim() ?? string.Empty;
        well.ContType = ContType?.Trim() ?? string.Empty;
        well.EDRProvider = EdrProvider?.Trim() ?? string.Empty;
        well.DataSource = DataSource?.Trim() ?? string.Empty;
        well.RigCost = RigCost;
        well.Historical = Historical;
        well.PipeLength = PipeLength;
        well.StandLength = StandLength;

        well.DrlgConnTime = DrlgConnTime;
        well.TripConnTime = TripConnTime;
        well.BTSTime = BtsTime;
        well.STSTime = StsTime;
        well.STBTime = StbTime;
        well.TripInSpeed = TripInSpeed;
        well.TripOutSpeed = TripOutSpeed;

        well.Pump1Model = Pump1Model?.Trim() ?? string.Empty;
        well.Pump1Stroke = Pump1Stroke?.Trim() ?? string.Empty;
        well.Pump1Liner = Pump1Liner?.Trim() ?? string.Empty;
        well.Pump2Model = Pump2Model?.Trim() ?? string.Empty;
        well.Pump2Stroke = Pump2Stroke?.Trim() ?? string.Empty;
        well.Pump2Liner = Pump2Liner?.Trim() ?? string.Empty;
        well.Pump3Model = Pump3Model?.Trim() ?? string.Empty;
        well.Pump3Stroke = Pump3Stroke?.Trim() ?? string.Empty;
        well.Pump3Liner = Pump3Liner?.Trim() ?? string.Empty;

        well.Comments = Comments?.Trim() ?? string.Empty;
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(WellName))
        {
            ErrorMessage = "Well Name is required.";
            StatusMessage = "Well Name is required. Please specify a well name.";
            IsStatusError = true;
            IsStatusVisible = true;
            SelectedTabIndex = 0;
            return;
        }

        ErrorMessage = string.Empty;
        StatusMessage = "Well configuration updated successfully.";
        IsStatusError = false;
        IsStatusVisible = false;
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }

    [RelayCommand]
    private void Reset()
    {
        if (_backupWell != null)
        {
            LoadFromWell(_backupWell.GetCopy());
        }
        else
        {
            WellName = _initialWellName;
            FieldName = _initialFieldName;
            LegalName = string.Empty;
            OperatorName = string.Empty;
            OperatorDiv = string.Empty;
            Contractor = string.Empty;
            PcInterest = string.Empty;
            Status = "Active";
            Purpose = "Development";
            Uwi = string.Empty;
            DrillingSupr = string.Empty;
            DrillingEng = string.Empty;
            Rep = string.Empty;
            ToolPusher = string.Empty;
            DrlgEngDept = string.Empty;
            DrlgOpDept = string.Empty;
            LicenseNo = string.Empty;
            LicenseDate = string.Empty;
            SpudDate = string.Empty;
            PaDate = string.Empty;
            TightHoleNo = string.Empty;
            ReEntryNo = string.Empty;
            Objective = string.Empty;
            TdDate = string.Empty;
            TdFormation = string.Empty;
            PlannedDepth = 0;
            PlannedDays = 0;
            Country = string.Empty;
            State = string.Empty;
            County = string.Empty;
            Block = string.Empty;
            District = string.Empty;
            Region = string.Empty;
            TimeZone = string.Empty;
            WellLocation = string.Empty;
            Latitude = 0;
            Longitude = 0;
            XCoOrd = 0;
            YCoOrd = 0;
            PermDatum = string.Empty;
            Sec = string.Empty;
            Twp = string.Empty;
            Rge = string.Empty;
            LegalDesc = string.Empty;
            WellheadElevation = 0;
            GroundElevation = 0;
            WaterDepth = 0;
            RigName = string.Empty;
            RigType = string.Empty;
            ContType = string.Empty;
            EdrProvider = string.Empty;
            DataSource = string.Empty;
            RigCost = 0;
            Historical = false;
            PipeLength = 30;
            StandLength = 90;
            DrlgConnTime = 0;
            TripConnTime = 0;
            BtsTime = 0;
            StsTime = 0;
            StbTime = 0;
            TripInSpeed = 0;
            TripOutSpeed = 0;
            Pump1Model = string.Empty;
            Pump1Stroke = string.Empty;
            Pump1Liner = string.Empty;
            Pump2Model = string.Empty;
            Pump2Stroke = string.Empty;
            Pump2Liner = string.Empty;
            Pump3Model = string.Empty;
            Pump3Stroke = string.Empty;
            Pump3Liner = string.Empty;
            Comments = string.Empty;
        }

        StatusMessage = "Well information restored to saved values.";
        IsStatusError = false;
        IsStatusVisible = true;
        ErrorMessage = string.Empty;
    }
}

