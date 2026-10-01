using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DrillIntel.Models;

/// <summary>
/// Represents a channel item displayed in the Channels grid of the Timelog Edit Dialog.
/// </summary>
public partial class TimelogChannelItem : ObservableObject
{
    [ObservableProperty]
    private bool _upload = true;

    [ObservableProperty]
    private string _mnemonic = string.Empty;

    [ObservableProperty]
    private string _unit = string.Empty;

    [ObservableProperty]
    private string _vuMaxUnitId = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _uploadMnemonic = string.Empty;

    [ObservableProperty]
    private string _valueType = "0";

    [ObservableProperty]
    private string _expression = string.Empty;

    [ObservableProperty]
    private bool _doNotInterpol;

    public string DataType { get; set; } = "Double";
    public int ColumnOrder { get; set; }
    public string? OriginalMnemonic { get; set; }

    public TimelogChannelItem Clone()
    {
        return new TimelogChannelItem
        {
            Upload = this.Upload,
            Mnemonic = this.Mnemonic,
            Unit = this.Unit,
            VuMaxUnitId = this.VuMaxUnitId,
            Description = this.Description,
            UploadMnemonic = this.UploadMnemonic,
            ValueType = this.ValueType,
            Expression = this.Expression,
            DoNotInterpol = this.DoNotInterpol,
            DataType = this.DataType,
            ColumnOrder = this.ColumnOrder,
            OriginalMnemonic = this.OriginalMnemonic
        };
    }
}

/// <summary>
/// Strongly-typed metadata loaded from vmx_time_log.
/// </summary>
public class TimeLogEditMetadata
{
    public string LogId { get; set; } = string.Empty;
    public string WellId { get; set; } = string.Empty;
    public string WellboreId { get; set; } = string.Empty;
    public string LogName { get; set; } = string.Empty;
    public string ServiceCompany { get; set; } = string.Empty;
    public string EdrProvider { get; set; } = string.Empty;
    public string RunNo { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool PrimaryLog { get; set; }
    public bool RemarksLog { get; set; }
    public bool NoAutoCalc { get; set; }
    public double StartingHoleDepth { get; set; }
    public string DataTableName { get; set; } = string.Empty;
    public bool LinkToParent { get; set; }
    public string LinkWellId { get; set; } = string.Empty;
    public string LinkWellboreId { get; set; } = string.Empty;
    public string LinkLogId { get; set; } = string.Empty;
    public bool DontMoveAhead { get; set; }
    public string DuplicateAction { get; set; } = "Merge Columns";
}

/// <summary>
/// Lookup options for linking timelogs.
/// </summary>
public class WellOption
{
    public string WellId { get; set; } = string.Empty;
    public string WellName { get; set; } = string.Empty;
    public override string ToString() => WellName;
}

public class WellboreOption
{
    public string WellboreId { get; set; } = string.Empty;
    public string WellId { get; set; } = string.Empty;
    public string WellboreName { get; set; } = string.Empty;
    public override string ToString() => WellboreName;
}

public class TimeLogOption
{
    public string LogId { get; set; } = string.Empty;
    public string WellId { get; set; } = string.Empty;
    public string WellboreId { get; set; } = string.Empty;
    public string LogName { get; set; } = string.Empty;
    public override string ToString() => LogName;
}

