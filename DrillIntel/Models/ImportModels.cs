using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DrillIntel.Models;

public enum OperationType
{
    NewData = 0,
    UpdateData = 1,
    Batch = 2
}

public enum ImportDataType
{
    TimeLogData = 0,
    DepthLogData = 1,
    TrajectoryData = 2,
    MudLogData = 3
}

public enum ImportFileType
{
    TextFile = 0,
    ExcelFile = 1,
    LASFile = 2,
    WITSMLFile = 3
}

public enum DelimiterChar
{
    Comma = 0,
    Tab = 1,
    Other = 2
}

public enum MappingType
{
    Manual = 0,
    MappingProfile = 1
}

public enum DateFormatType
{
    ISOFormat = 0,
    DDMMYYYYFormat = 1,
    MMDDYYYYFormat = 2
}

public enum TimeZoneType
{
    LocalTimeZone = 0,
    UTCGMT = 1,
    Other = 2
}

public enum DataAssociationType
{
    CreateNewWell = 0,
    UseExistingWell = 1
}

public enum UpdateMethodType
{
    DepthComparision = 0,
    DateTimeComaparision = 1
}

public enum WizardTab
{
    FileSelection,
    Mapping,
    Finalize
}

public class ComboData
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class WellboreInfo
{
    public string WELLBORE_ID { get; set; } = string.Empty;
    public string WELLBORE_NAME { get; set; } = string.Empty;
}

public class TimeLogInfo
{
    public string LOG_ID { get; set; } = string.Empty;
    public string LOG_NAME { get; set; } = string.Empty;
}

public class UpdateMappingInfo
{
    public bool Selected { get; set; }
    public string SourceColumnID { get; set; } = string.Empty;
    public string VuMaxColumnID { get; set; } = string.Empty;
}

public class PreviewRow
{
    public int Id { get; set; }
    public ObservableCollection<string> Cells { get; set; } = new();
}

public partial class RowMappingUpdate : ObservableObject
{
    [ObservableProperty]
    private bool _checked;

    [ObservableProperty]
    private string _selectedTimelogChannel = string.Empty;

    public string SourceChannel { get; set; } = string.Empty;

    public ObservableCollection<string> AvailableTimelogChannels { get; set; } = new();
}

public class MappingChannels
{
    public string Mnemonic { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string MappedMnemonic { get; set; } = string.Empty;
}

public partial class ColumnMappingRow : ObservableObject
{
    [ObservableProperty]
    private string? _sourceColumnName;

    public string VuMaxColumnID { get; set; } = string.Empty;

    public ObservableCollection<string> AvailableSourceColumns { get; set; } = new();
}

public class LogChannelRow
{
    public int Id { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public string MappedTo { get; set; } = string.Empty;
}

public class ImportSettings
{
    // ----- DataType tab -----
    public OperationType OperationType { get; set; } = OperationType.NewData;
    public ImportDataType TypeOfDataInput { get; set; } = ImportDataType.TimeLogData;

    // ----- FileType tab -----
    public ImportFileType TypeOfFile { get; set; } = ImportFileType.TextFile;
    public DelimiterChar ColumnDelimiter { get; set; } = DelimiterChar.Comma;
    public string OtherColumnDelimiter { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;

    // ----- ColumnMapping tab -----
    public MappingType MethodOfColumnMapping { get; set; } = MappingType.Manual;
    public string MappingFileName { get; set; } = string.Empty;

    // ----- MapColumns tab -----
    public int? ImportFromRow { get; set; } = 2;
    public int? ColumnHeadingRow { get; set; } = 1;
    public bool IsDatetimeInSeperatorColumn { get; set; }
    public string DatetimeSeparator { get; set; } = string.Empty;
    public int DateColNo { get; set; }
    public int TimeColNo { get; set; }
    public DateFormatType DateFormat { get; set; } = DateFormatType.ISOFormat;
    public List<UpdateMappingInfo> UpdateMappingList { get; set; } = new();

    // ----- DataAssociation tab (new data) -----
    public DataAssociationType DataAssociation { get; set; } = DataAssociationType.CreateNewWell;
    public string? NewWellName { get; set; }
    public string LogName { get; set; } = string.Empty;
    public string? ExistingWellID { get; set; }
    public string? ExistingWellboreID { get; set; }
    public bool ChkAdjustWellDateTime { get; set; }
    public string WellOffset { get; set; } = "+";
    public int TxtWellHH { get; set; }
    public int TxtWellMM { get; set; }
    public int TxtWellSS { get; set; }

    // ----- DataAssociationUpdate tab -----
    public string? UpdateWellId { get; set; }
    public string? UpdatedTimelogId { get; set; }

    // ----- DateFormat tab -----
    public TimeZoneType TimeZoneType { get; set; } = TimeZoneType.LocalTimeZone;
    public string TimeOffset { get; set; } = "+";
    public int TxtHH { get; set; }
    public int TxtMM { get; set; }

    // ----- DataComparision tab -----
    public UpdateMethodType UpdateMethod { get; set; } = UpdateMethodType.DepthComparision;
    public ComboData? SideTrack { get; set; }
    public bool ChkAdjustDateTime { get; set; }
    public string AdjustTimeOffset { get; set; } = "+";
    public int TxtAdjMM { get; set; }
    public int TxtAdjSS { get; set; }
    public string SourceHoleDepth { get; set; } = string.Empty;
    public string SourceDateTime { get; set; } = string.Empty;
    public string TimeLogHoleDepth { get; set; } = string.Empty;
    public string TimeLogDateTime { get; set; } = string.Empty;

    // ----- Object tab (Trajectory / MudLog) -----
    public string TxtWellID2 { get; set; } = string.Empty;
    public string TxtWellboreID2 { get; set; } = string.Empty;
    public string TxtLogID2 { get; set; } = string.Empty;
    public string TxtLogName2 { get; set; } = string.Empty;

    // ----- Log tab (WITSML time/depth log) -----
    public string TxtW_WellID { get; set; } = string.Empty;
    public string TxtW_WellboreID { get; set; } = string.Empty;
    public string TxtW_LogID { get; set; } = string.Empty;
    public string TxtW_LogName { get; set; } = string.Empty;
}
