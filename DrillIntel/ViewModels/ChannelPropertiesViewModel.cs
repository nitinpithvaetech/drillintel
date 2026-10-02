using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Models;

namespace DrillIntel.ViewModels;

/// <summary>
/// ViewModel for Channel Properties dialog in Timelog Editor.
/// Supports adding and editing expression and calculated log channels.
/// Populates available units from Project Unit Master (VMX_UNIT_MASTER).
/// Locks Mnemonic during editing as it serves as the primary key.
/// </summary>
public partial class ChannelPropertiesViewModel : ObservableObject
{
    private readonly IDataServiceDIntel? _dataService;
    private readonly HashSet<string> _existingMnemonics = new(StringComparer.OrdinalIgnoreCase);
    private readonly LogChannel? _originalChannel;
    private readonly List<LogChannel> _availableChannels = new();

    public event Action<bool?>? RequestClose;

    // Test hook / UI handler for Expression Editor dialog
    public Func<ExpressionEditorViewModel, bool?>? OpenExpressionEditorHandler { get; set; }

    [ObservableProperty]
    private string _title = "Channel Properties";

    [ObservableProperty]
    private string _subtitle = "Configure channel identification, unit, and expression calculation parameters.";

    [ObservableProperty]
    private string _mnemonic = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _selectedUnit = string.Empty;

    [ObservableProperty]
    private string _valueType = "Expression";

    [ObservableProperty]
    private int _columnOrder = 0;

    [ObservableProperty]
    private string _sensorOffset = "0";

    [ObservableProperty]
    private string _expression = string.Empty;

    [ObservableProperty]
    private bool _isStoredProcedure = false;

    [ObservableProperty]
    private string _parameters = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEditMnemonic))]
    [NotifyPropertyChangedFor(nameof(MnemonicToolTip))]
    private bool _isEditMode;

    /// <summary>
    /// When editing an existing channel, Mnemonic cannot be modified as it is the Primary Key.
    /// </summary>
    public bool CanEditMnemonic => !IsEditMode;

    public string MnemonicToolTip => IsEditMode
        ? "Mnemonic is the primary key and cannot be modified after creation."
        : "Enter a unique mnemonic name for this channel.";

    public ObservableCollection<string> AvailableUnits { get; } = new();

    public ObservableCollection<string> ValueTypes { get; } = new()
    {
        "Expression",
        "Static Value"
    };

    public ChannelPropertiesViewModel() : this(null, null, null, 0, null)
    {
    }

    public ChannelPropertiesViewModel(
        IDataServiceDIntel? dataService = null,
        IEnumerable<string>? existingMnemonics = null,
        LogChannel? existingChannel = null,
        int nextColumnOrder = 0,
        IEnumerable<LogChannel>? availableChannels = null)
    {
        _dataService = dataService;
        _originalChannel = existingChannel;
        _isEditMode = existingChannel != null;

        if (availableChannels != null)
        {
            _availableChannels.AddRange(availableChannels);
        }

        if (existingMnemonics != null)
        {
            foreach (var m in existingMnemonics)
            {
                if (!string.IsNullOrWhiteSpace(m))
                {
                    _existingMnemonics.Add(m.Trim());
                }
            }
        }

        // If editing existing channel, do not consider its own original mnemonic as a collision
        if (_originalChannel != null && !string.IsNullOrWhiteSpace(_originalChannel.Mnemonic))
        {
            _existingMnemonics.Remove(_originalChannel.Mnemonic.Trim());
        }

        // Populate units from Project Unit Master (VMX_UNIT_MASTER)
        LoadUnits(_dataService);

        if (_originalChannel != null)
        {
            Title = $"Edit Channel — {_originalChannel.Mnemonic}";
            Subtitle = "Modify channel details and expression. Note: Mnemonic is the primary key and cannot be changed.";
            Mnemonic = _originalChannel.Mnemonic;
            Description = _originalChannel.Description;
            SelectedUnit = !string.IsNullOrWhiteSpace(_originalChannel.Unit) ? _originalChannel.Unit : (_originalChannel.VuMaxUnitId ?? "");
            ValueType = _originalChannel.valueType == 1 ? "Expression" : "Static Value";
            ColumnOrder = _originalChannel.ColumnOrder;
            SensorOffset = string.IsNullOrWhiteSpace(_originalChannel.sensorOffset) ? "0" : _originalChannel.sensorOffset;
            Expression = _originalChannel.Expression;
            IsStoredProcedure = _originalChannel.isStoredProc;
            Parameters = _originalChannel.StoredProcParams;
        }
        else
        {
            Title = "Add Channel";
            Subtitle = "Configure new expression channel metadata and formula definition.";
            ValueType = "Expression";
            ColumnOrder = nextColumnOrder;
            SensorOffset = "0";
            Expression = string.Empty;
            IsStoredProcedure = false;
            Parameters = string.Empty;

            if (AvailableUnits.Count > 0 && string.IsNullOrWhiteSpace(SelectedUnit))
            {
                SelectedUnit = AvailableUnits[0];
            }
        }
    }

    /// <summary>
    /// Loads units from Project Unit Master (VMX_UNIT_MASTER) with fallback to Application Unit Master.
    /// </summary>
    public void LoadUnits(IDataServiceDIntel? dataService)
    {
        AvailableUnits.Clear();
        var loadedUnits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (dataService != null)
        {
            try
            {
                // Populate from Project Unit Master (table: VMX_UNIT_MASTER)
                var projectUnits = Unit.GetList(dataService, tableName: Unit.ProjectTableName);
                if (projectUnits != null && projectUnits.Count > 0)
                {
                    foreach (var u in projectUnits)
                    {
                        if (!string.IsNullOrWhiteSpace(u.UnitName))
                        {
                            loadedUnits.Add(u.UnitName.Trim());
                        }
                    }
                }

                // If project table had no records, load from APP_UNIT_MASTER
                if (loadedUnits.Count == 0)
                {
                    var appUnits = Unit.GetList(dataService, tableName: Unit.TableName);
                    if (appUnits != null && appUnits.Count > 0)
                    {
                        foreach (var u in appUnits)
                        {
                            if (!string.IsNullOrWhiteSpace(u.UnitName))
                            {
                                loadedUnits.Add(u.UnitName.Trim());
                            }
                        }
                    }
                }
            }
            catch
            {
                // Graceful fallback if database tables are uninitialized
            }
        }

        // Standard oilfield default units fallback if no units exist in database
        if (loadedUnits.Count == 0)
        {
            string[] defaults = ["m", "ft", "in", "psi", "kPa", "bar", "MPa", "degC", "degF", "klb", "daN", "kN", "kg", "lb", "rpm", "ft/hr", "m/hr", "gpm", "lpm", "ft-lbf", "kN-m", "g/cm3", "ppg"];
            foreach (var d in defaults)
            {
                loadedUnits.Add(d);
            }
        }

        foreach (var unitName in loadedUnits.OrderBy(u => u))
        {
            AvailableUnits.Add(unitName);
        }
    }

    [RelayCommand]
    public void Ok()
    {
        string trimmedMnemonic = (Mnemonic ?? string.Empty).Trim();

        if (IsEditMode && _originalChannel != null)
        {
            // Lock mnemonic to the original PK
            trimmedMnemonic = _originalChannel.Mnemonic.Trim();
            Mnemonic = trimmedMnemonic;
        }

        if (string.IsNullOrWhiteSpace(trimmedMnemonic))
        {
            ErrorMessage = "Mnemonic is required and cannot be blank.";
            HasError = true;
            return;
        }

        if (!IsEditMode && _existingMnemonics.Contains(trimmedMnemonic))
        {
            ErrorMessage = $"A channel with mnemonic '{trimmedMnemonic}' already exists in this timelog.";
            HasError = true;
            return;
        }

        ErrorMessage = string.Empty;
        HasError = false;
        RequestClose?.Invoke(true);
    }

    [RelayCommand]
    public void Cancel()
    {
        ErrorMessage = string.Empty;
        HasError = false;
        RequestClose?.Invoke(false);
    }

    [RelayCommand]
    public void OpenEditor()
    {
        var exprVm = new ExpressionEditorViewModel(Expression, _availableChannels);

        bool? result;
        if (OpenExpressionEditorHandler != null)
        {
            result = OpenExpressionEditorHandler(exprVm);
        }
        else
        {
            result = ShowExpressionEditorDialog(exprVm);
        }

        if (result == true)
        {
            Expression = exprVm.Expression;
        }
    }

    private bool? ShowExpressionEditorDialog(ExpressionEditorViewModel exprVm)
    {
        if (System.Windows.Application.Current == null)
        {
            exprVm.Ok();
            return !exprVm.IsStatusError;
        }

        try
        {
            var dialog = new DrillIntel.Views.ExpressionEditorWindow
            {
                DataContext = exprVm,
                Owner = System.Windows.Application.Current?.Windows.OfType<System.Windows.Window>().FirstOrDefault(w => w.IsActive)
                        ?? System.Windows.Application.Current?.MainWindow
            };
            return dialog.ShowDialog();
        }
        catch
        {
            exprVm.Ok();
            return !exprVm.IsStatusError;
        }
    }

    [RelayCommand]
    public void ChannelLibrary()
    {
        // Placeholder for quick library selection
    }

    [RelayCommand]
    public void Help()
    {
        // Tooltip or helper info
    }

    /// <summary>
    /// Creates a newly initialized LogChannel instance with valueType=1 (Expression).
    /// </summary>
    public LogChannel ToLogChannel()
    {
        string mnem = (Mnemonic ?? string.Empty).Trim();
        string unitVal = (SelectedUnit ?? string.Empty).Trim();

        var channel = new LogChannel
        {
            Mnemonic = mnem,
            mnemonic = mnem,
            OriginalMnemonic = mnem,
            Description = (Description ?? string.Empty).Trim(),
            curveDescription = (Description ?? string.Empty).Trim(),
            Unit = unitVal,
            unit = unitVal,
            UnitID = unitVal,
            VuMaxUnitId = unitVal,
            VuMaxUnitID = unitVal,
            // valueType = 1 denotes Expression (queryValue)
            valueType = 1,
            ValueType = "1",
            ColumnOrder = ColumnOrder,
            sensorOffset = (SensorOffset ?? "0").Trim(),
            Expression = Expression ?? string.Empty,
            valueQuery = Expression ?? string.Empty,
            isStoredProc = IsStoredProcedure,
            StoredProcParams = Parameters ?? string.Empty,
            UploadMnemonic = mnem,
            witsmlMnemonic = mnem,
            typeLogData = "Double",
            DataType = "Double",
            WriteBack = true,
            processChannel = true,
            Upload = true
        };

        return channel;
    }

    /// <summary>
    /// Updates the properties of an existing LogChannel with values from this dialog.
    /// Preserves original Mnemonic as it is the primary key.
    /// </summary>
    public void ApplyTo(LogChannel target)
    {
        if (target == null) return;

        string unitVal = (SelectedUnit ?? string.Empty).Trim();

        // Target Mnemonic is the PK and preserved
        target.Description = (Description ?? string.Empty).Trim();
        target.Unit = unitVal;
        target.UnitID = unitVal;
        target.VuMaxUnitId = unitVal;
        target.valueType = 1; // Expression
        target.ValueType = "1";
        target.ColumnOrder = ColumnOrder;
        target.sensorOffset = (SensorOffset ?? "0").Trim();
        target.Expression = Expression ?? string.Empty;
        target.valueQuery = Expression ?? string.Empty;
        target.isStoredProc = IsStoredProcedure;
        target.StoredProcParams = Parameters ?? string.Empty;
        target.Upload = true;
        target.WriteBack = true;
        target.processChannel = true;
    }
}

