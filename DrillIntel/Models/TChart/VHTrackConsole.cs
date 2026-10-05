using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DrillIntel.Models.TChart;

/// <summary>
/// Root model representing the entire chart track console (vertical or horizontal strip charts).
/// Manages tracks, shared axes, layouts, synchronization, and runtime scales.
/// </summary>
public class VHTrackConsole
{
    public string ID { get; set; } = string.Empty;
    public string WidgetID { get; set; } = string.Empty;
    public enumTrackOrientation TrackOrientation { get; set; } = enumTrackOrientation.Horizontal;
    public enumIndexType IndexType { get; set; } = enumIndexType.TimeLog;
    public string Name { get; set; } = string.Empty;

    public List<VHTrack> Tracks { get; set; } = new List<VHTrack>();
    public string WellID { get; set; } = string.Empty;
    public RTYAxis objYAxis { get; set; } = new RTYAxis();

    public double displayResolution { get; set; } = 100;
    public string LastError { get; set; } = string.Empty;
    public RTDataSource DataSource { get; set; } = new RTDataSource();
    public string DepthUnit { get; set; } = string.Empty;

    public bool realTime { get; set; } = true;
    public bool crossHair { get; set; } = true;
    public double TrackHeaderHeight { get; set; } = 1;

    //public List<VHColorDepthRange> ColorDepthRanges { get; set; } = new List<VHColorDepthRange>();

    // Common scales for runtime rendering
    public object? __yScale { get; set; } = null;
    public object? __depthYScale { get; set; } = null;
    public object? __dateTimeYScale { get; set; } = null;
    public object? __xScale { get; set; } = null;

    //public string operationMode { get; set; } = string.Empty;
    public double currentMinDepth { get; set; } = -999.25;
    public double currentMaxDepth { get; set; } = -999.25;
    public DateTime currentMinDate { get; set; } = DateTime.UnixEpoch;
    public DateTime currentMaxDate { get; set; } = DateTime.UnixEpoch;

    public bool syncWithWidgets { get; set; } = false;
    public string __templateID { get; set; } = string.Empty;
    public string __templateUpdatedOn { get; set; } = string.Empty;

    public bool showFormationTops { get; set; } = true;
    public bool showCasingDepth { get; set; } = true;

    // Multi-well collections
    public Dictionary<string, MultiWellInfoEx> WellList { get; set; } = new Dictionary<string, MultiWellInfoEx>();
    public Dictionary<string, List<object>> data { get; set; } = new Dictionary<string, List<object>>();
    public Dictionary<string, Dictionary<string, string>> channelUnits { get; set; } = new Dictionary<string, Dictionary<string, string>>();
    public Dictionary<string, RMEx> RoadmapEntry { get; set; } = new Dictionary<string, RMEx>();

    public VHTrackConsole()
    {
    }

    /// <summary>
    /// Factory method to instantiate a VHTrackConsole configured for a given index type.
    /// </summary>
    public static VHTrackConsole CreateConsoleFromIndexType(enumIndexType pIndexType)
    {
        return new VHTrackConsole
        {
            IndexType = pIndexType
        };
    }

    /// <summary>
    /// Factory method accepting an integer index type.
    /// </summary>
    public static VHTrackConsole CreateConsoleFromIndexType(int pIndexType)
    {
        return CreateConsoleFromIndexType((enumIndexType)pIndexType);
    }

    /// <summary>
    /// Updates a property by string name with type conversion, matching TypeScript UpdatePropertyByName.
    /// </summary>
    public static void UpdatePropertyByName(object? propertyValue, string propertyName, string valueType, VHTrackConsole target)
    {
        if (target == null || string.IsNullOrWhiteSpace(propertyName)) return;

        try
        {
            var prop = typeof(VHTrackConsole).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop == null || !prop.CanWrite) return;

            object? convertedVal = propertyValue;

            if (propertyValue != null)
            {
                if (string.Equals(valueType, "Number", StringComparison.OrdinalIgnoreCase))
                {
                    convertedVal = System.Convert.ChangeType(propertyValue, prop.PropertyType);
                }
                else if (string.Equals(valueType, "Boolean", StringComparison.OrdinalIgnoreCase))
                {
                    convertedVal = System.Convert.ToBoolean(propertyValue);
                }
                else if (string.Equals(valueType, "Date", StringComparison.OrdinalIgnoreCase))
                {
                    convertedVal = System.Convert.ToDateTime(propertyValue);
                }
                else
                {
                    convertedVal = System.Convert.ChangeType(propertyValue, prop.PropertyType);
                }
            }

            prop.SetValue(target, convertedVal);
        }
        catch (Exception ex)
        {
            target.LastError = ex.Message;
        }
    }

    public VHTrackConsole GetCopy()
    {
        return GetCopy(this);
    }

    public static VHTrackConsole GetCopy(VHTrackConsole? obj)
    {
        if (obj == null) return new VHTrackConsole();

        try
        {
            var objNew = new VHTrackConsole
            {
                ID = obj.ID,
                IndexType = obj.IndexType,
                WellID = obj.WellID,
                TrackOrientation = obj.TrackOrientation,
                WidgetID = obj.WidgetID,
                DataSource = RTDataSource.GetCopy(obj.DataSource),
                Name = obj.Name,
                Tracks = obj.Tracks.Select(t => t.GetCopy()).ToList(),
                objYAxis = RTYAxis.GetCopy(obj.objYAxis),
                displayResolution = obj.displayResolution,
                DepthUnit = obj.DepthUnit,
                realTime = obj.realTime,
                crossHair = obj.crossHair,
                //ColorDepthRanges = obj.ColorDepthRanges.Select(c => c.GetCopy()).ToList(),
                TrackHeaderHeight = obj.TrackHeaderHeight,
                __yScale = obj.__yScale,
                __depthYScale = obj.__depthYScale,
                __dateTimeYScale = obj.__dateTimeYScale,
                __xScale = obj.__xScale,
                //operationMode = obj.operationMode,
                currentMinDepth = obj.currentMinDepth,
                currentMaxDepth = obj.currentMaxDepth,
                currentMinDate = obj.currentMinDate,
                currentMaxDate = obj.currentMaxDate,
                syncWithWidgets = obj.syncWithWidgets,
                __templateID = obj.__templateID,
                __templateUpdatedOn = obj.__templateUpdatedOn,
                showFormationTops = obj.showFormationTops,
                showCasingDepth = obj.showCasingDepth,
                LastError = obj.LastError
            };

            // Copy WellList
            foreach (var kvp in obj.WellList)
            {
                objNew.WellList[kvp.Key] = kvp.Value.GetCopy();
            }

            // Copy data
            foreach (var kvp in obj.data)
            {
                objNew.data[kvp.Key] = new List<object>(kvp.Value);
            }

            // Copy channelUnits
            foreach (var kvp in obj.channelUnits)
            {
                objNew.channelUnits[kvp.Key] = new Dictionary<string, string>(kvp.Value);
            }

            // Copy RoadmapEntry
            foreach (var kvp in obj.RoadmapEntry)
            {
                objNew.RoadmapEntry[kvp.Key] = kvp.Value.GetCopy();
            }

            return objNew;
        }
        catch
        {
            return new VHTrackConsole();
        }
    }

    /// <summary>
    /// Calculates the date difference in days between two dates, matching calculateDateDifference.
    /// Returns -999.25 if either date is invalid.
    /// </summary>
    public static double CalculateDateDifference(DateTime startDate, DateTime endDate)
    {
        try
        {
            if (startDate == DateTime.MinValue || endDate == DateTime.MinValue)
            {
                return -999.25;
            }

            var diff = (endDate - startDate).TotalDays;
            return Math.Round(diff, 2);
        }
        catch
        {
            return -999.25;
        }
    }
}

