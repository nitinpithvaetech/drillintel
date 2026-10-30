using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Models.BroomstickProcessor;

namespace DrillIntel.Models.Broomstick;

/// <summary>
/// Display and feature toggle settings for a Broomstick Plot.
/// </summary>
public class BroomstickPlotSettings
{
    public bool InvertDepthAxis { get; set; } = true;
    public bool HighlightFormationTop { get; set; } = false;
    public bool ShowSection { get; set; } = false;
    public bool ViewCasing { get; set; } = false;
    public string UnitSystem { get; set; } = "klbs";
    public Dictionary<string, bool> SeriesVisibility { get; set; } = new();

    public bool PlotTimeData { get; set; } = true;
    public bool ShowLegend { get; set; } = true;
    public bool PlotMakeupTorque { get; set; } = true;
    public bool PlotTorqueLimit { get; set; } = true;
    public bool ShowCasingPoints { get; set; } = true;
    public bool ShowCasingMax { get; set; } = true;
    public bool ShowCasingMin { get; set; } = false;

    public string PointsStyle { get; set; } = "Rectangle";
    public int PointSize { get; set; } = 5;
    public int PointTransparency { get; set; } = 0;
    public int LineThickness { get; set; } = 3;

    public string FilterMethod { get; set; } = "Moving Avg.";

    public string CasingPointStyle { get; set; } = "Rectangle";
    public int CasingPointSize { get; set; } = 10;
    public int CasingPointTransparency { get; set; } = 10;

    public BroomstickPlotSettings()
    {
        // Default all series to visible
        SeriesVisibility["plan02"] = true;
        SeriesVisibility["plan025"] = true;
        SeriesVisibility["plan03"] = true;
        SeriesVisibility["plan035"] = true;
        SeriesVisibility["plan04"] = true;
        SeriesVisibility["casingPu"] = true;
        SeriesVisibility["rotTheory"] = true;
        SeriesVisibility["casingSo"] = true;
        SeriesVisibility["so02"] = true;
        SeriesVisibility["so025"] = true;
        SeriesVisibility["so03"] = true;
        SeriesVisibility["so035"] = true;
        SeriesVisibility["so04"] = true;
        SeriesVisibility["actualPu"] = true;
        SeriesVisibility["actualSo"] = true;
        SeriesVisibility["actualRot"] = true;
    }
}

/// <summary>
/// Master document domain class encapsulating all configuration, theoretical curves,
/// actual calculated points, and profile filtering rules for a Broomstick Document.
/// Stored in VMX_BROOMSTICK_TEMPLATES.
/// </summary>
public class BroomstickDocument
{
    // Identification & Template metadata
    public string TemplateId { get; set; } = Guid.NewGuid().ToString();
    public string DocumentName { get; set; } = "Default Broomstick Document";
    public string Description { get; set; } = string.Empty;
    public bool IsDefault { get; set; }

    // Well & Plan relational references
    public string WellId { get; set; } = string.Empty;
    public string WellboreId { get; set; } = string.Empty;
    public string LogId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;

    // Filter, styling & calculation profile
    public BroomstickProfile Profile { get; set; } = new BroomstickProfile();

    // Plot view & visual settings
    public BroomstickPlotSettings PlotSettings { get; set; } = new BroomstickPlotSettings();

    // Theoretical Plan Data (loaded from VMX_ADNL_HKLD_PLAN & VMX_ADNL_HKLD_PLAN_DATA)
    [JsonIgnore]
    public AdnlHookloadPlan? BroomstickPlanData { get; set; }

    // Associated child plans (when a Main Plan / RunNo is selected)
    [JsonIgnore]
    public List<AdnlHookloadPlan> AssociatedPlans { get; set; } = new();

    // Actual Calculated Broomstick Points (loaded from VMX_BROOMSTICK_POINTS)
    [JsonIgnore]
    public List<PUSOPoint> BroomstickPointData { get; set; } = new();

    /// <summary>
    /// Serializes the document configuration, profile, and plot settings into a JSON byte array
    /// for storage in VMX_BROOMSTICK_TEMPLATES.TEMPLATE_DATA.
    /// </summary>
    public byte[] SerializeToBlob()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        return JsonSerializer.SerializeToUtf8Bytes(this, options);
    }

    /// <summary>
    /// Deserializes a BroomstickDocument instance from a stored template byte array.
    /// </summary>
    public static BroomstickDocument? DeserializeFromBlob(byte[]? data)
    {
        if (data == null || data.Length == 0) return null;
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            return JsonSerializer.Deserialize<BroomstickDocument>(data, options);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Filters and downsamples actual broomstick points according to the active BroomstickProfile
    /// rules (hookload range cutoff, rig states, downsampling buckets, outlier filtering, and depth/time window).
    /// </summary>
    public List<PUSOPoint> GetFilteredPoints(
        double minDepth = 0, 
        double maxDepth = double.MaxValue,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        if (BroomstickPointData == null || BroomstickPointData.Count == 0)
            return new List<PUSOPoint>();

        var query = BroomstickPointData.AsEnumerable();

        // 1. Depth window bounds
        if (minDepth > 0 || maxDepth < double.MaxValue)
        {
            query = query.Where(p => p.Depth >= minDepth && p.Depth <= maxDepth);
        }

        // 1b. Date window bounds (when filtering by time)
        if (fromDate.HasValue && fromDate.Value > DateTime.MinValue)
        {
            query = query.Where(p => p.StartDate == DateTime.MinValue || p.StartDate >= fromDate.Value || (p.EndDate != DateTime.MinValue && p.EndDate >= fromDate.Value));
        }
        if (toDate.HasValue && toDate.Value > DateTime.MinValue)
        {
            query = query.Where(p => p.StartDate == DateTime.MinValue || p.StartDate <= toDate.Value || (p.EndDate != DateTime.MinValue && p.EndDate <= toDate.Value));
        }

        // 2. Hookload range filter (if enabled)
        if (Profile.FilterByRange && (Profile.MinHooklaod > 0 || Profile.MaxHookload > 0))
        {
            double minHkld = Profile.MinHooklaod;
            double maxHkld = Profile.MaxHookload > 0 ? Profile.MaxHookload : double.MaxValue;
            query = query.Where(p =>
            {
                double val = p.DynamicValue > 0 ? p.DynamicValue :
                             p.Avg > 0 ? p.Avg :
                             p.Max > 0 ? p.Max :
                             p.Min > 0 ? p.Min :
                             p.UserDynamicValue > 0 ? p.UserDynamicValue :
                             p.UserStaticValue;
                if (val <= 0) return true;
                return val >= minHkld && val <= maxHkld;
            });
        }

        // 2b. Rig states filter (if specified in profile)
        if (!string.IsNullOrWhiteSpace(Profile.PkupRigStates))
        {
            var allowedRigStates = new HashSet<int>();
            foreach (var token in Profile.PkupRigStates.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (int.TryParse(token, out int code))
                    allowedRigStates.Add(code);
            }

            if (allowedRigStates.Count > 0)
            {
                query = query.Where(p =>
                {
                    if (string.IsNullOrWhiteSpace(p.RigStates)) return true;
                    var parts = p.RigStates.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    foreach (var part in parts)
                    {
                        if (int.TryParse(part, out int c) && allowedRigStates.Contains(c))
                            return true;
                    }
                    return false;
                });
            }
        }

        // 3. User visibility check
        query = query.Where(p => p.UserVisible);

        var list = query.OrderBy(p => p.Depth).ToList();

        // 4. Downsampling (if enabled)
        if (Profile.DataPoints > 0 && list.Count > Profile.DataPoints * 5)
        {
            list = ApplyDownsampling(list, Profile.DataPoints, Profile.GroupFunctions);
        }

        return list;
    }

    private static List<PUSOPoint> ApplyDownsampling(List<PUSOPoint> points, int targetBuckets, string groupFunctions = "")
    {
        if (points.Count <= targetBuckets) return points;

        var tokens = (groupFunctions ?? string.Empty).ToUpperInvariant()
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .ToList();

        bool incMin = tokens.Count == 0 || tokens.Contains("0") || tokens.Contains("MIN");
        bool incMax = tokens.Count == 0 || tokens.Contains("1") || tokens.Contains("MAX");
        bool incAvg = tokens.Count == 0 || tokens.Contains("2") || tokens.Contains("AVG");

        // If all three are unchecked, default to showing min, max, avg
        if (!incMin && !incMax && !incAvg)
        {
            incMin = incMax = incAvg = true;
        }

        var result = new List<PUSOPoint>();
        int bucketSize = (int)Math.Ceiling((double)points.Count / targetBuckets);

        for (int i = 0; i < points.Count; i += bucketSize)
        {
            var bucket = points.Skip(i).Take(bucketSize).ToList();
            if (bucket.Count == 0) continue;

            // Representative points: min, max, and avg
            if (incMin)
            {
                var minPt = bucket.OrderBy(p => p.DynamicValue > 0 ? p.DynamicValue : p.Avg).First();
                result.Add(minPt);
            }
            if (incMax)
            {
                var maxPt = bucket.OrderByDescending(p => p.DynamicValue > 0 ? p.DynamicValue : p.Avg).First();
                result.Add(maxPt);
            }
            if (incAvg)
            {
                var avgPt = bucket[bucket.Count / 2];
                result.Add(avgPt);
            }
        }

        return result.Distinct().OrderBy(p => p.Depth).ToList();
    }
}
