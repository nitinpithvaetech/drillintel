using System;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Point marker styles for TeeChart series rendering.
/// </summary>
public enum enumRTPointStyle
{
    Square = 0,
    Circle = 1,
    Triangle = 2,
    LeftTriangle = 3,
    RightTriangle = 4,
    DownTriangle = 5,
    Diamond = 6,
    Star = 7
}

/// <summary>
/// Series display style (Line, Point, Area, ColorFill).
/// </summary>
public enum enumRTSeriesStyle
{
    Line = 0,
    Point = 1,
    Area = 2,
    ColorFill = 3
}

/// <summary>
/// Track console creation type.
/// </summary>
public enum enumConsoleType
{
    New = 0,
    Template = 1
}

/// <summary>
/// Pen / line stroke styles.
/// </summary>
public enum enumRTLineStyle
{
    Solid = 0,
    Dash = 1,
    DashDot = 2,
    Dot = 3
}

/// <summary>
/// UI form operational mode.
/// </summary>
public enum enumFormMode
{
    Create = 0,
    Edit = 1
}

/// <summary>
/// Chart axis docking / alignment location.
/// </summary>
public enum enumRTAxisLocation
{
    Left = 0,
    Top = 1,
    Right = 2,
    Bottom = 3
}

/// <summary>
/// Track types supported by VHTrack.
/// </summary>
public enum enumRTTrackType
{
    Regular = 0,
    Index = 1,
    Trajectory = 2,
    ILTAlert = 6,
    IndexDateTime = 7,
    DepthIndex = 8,
    RTOCComment = 9,
    BHA = 10,
    VSHAL = 11
}

/// <summary>
/// Source data domain: DepthLog or TimeLog.
/// </summary>
public enum enumRTDataSourceType
{
    DepthLog = 0,
    TimeLog = 1
}

/// <summary>
/// Line style used for geological formation tops.
/// </summary>
public enum enumFormationLineStyle
{
    Solid = 0,
    Dash = 1,
    DashDot = 2,
    Dot = 3
}

/// <summary>
/// Orientation of the tracks (Vertical or Horizontal).
/// </summary>
public enum enumTrackOrientation
{
    Vertical = 0,
    Horizontal = 1
}

/// <summary>
/// Primary index mode (TimeLog or DepthLog).
/// </summary>
public enum enumIndexType
{
    TimeLog = 0,
    Deptlog = 1
}

/// <summary>
/// Time window filtering options.
/// </summary>
public enum enumFilterByData
{
    Twelve_Hour,
    TwentyFour_Hour,
    One_Week,
    One_Month
}

