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
            query = query.Where(p => p.StartDate == DateTime.MinValue || p.StartDate <= toDate.Value || (p.EndDate != DateTime.MinValue && p.StartDate <= toDate.Value));
        }

        // 2. Hookload range filter (if enabled)
        if (Profile.FilterByRange && (Profile.MinHooklaod > 0 || Profile.MaxHookload > 0))
        {
            double minHkld = Profile.MinHooklaod;
            double maxHkld = Profile.MaxHookload > 0 ? Profile.MaxHookload : double.MaxValue;
            query = query.Where(p =>
            {
                double val = p.DynamicValue > 0 ? p.DynamicValue : p.Avg;
                return val >= minHkld && val <= maxHkld;
            });
        }

        // 3. User visibility check
        query = query.Where(p => p.UserVisible);

        var list = query.OrderBy(p => p.Depth).ToList();

        // 4. Downsampling (if enabled)
        if (Profile.DataPoints > 0 && list.Count > Profile.DataPoints * 5)
        {
            list = ApplyDownsampling(list, Profile.DataPoints);
        }

        return list;
    }

    private static List<PUSOPoint> ApplyDownsampling(List<PUSOPoint> points, int targetBuckets)
    {
        if (points.Count <= targetBuckets) return points;

        var result = new List<PUSOPoint>();
        int bucketSize = (int)Math.Ceiling((double)points.Count / targetBuckets);

        for (int i = 0; i < points.Count; i += bucketSize)
        {
            var bucket = points.Skip(i).Take(bucketSize).ToList();
            if (bucket.Count == 0) continue;

            // Representative points: min, max, and avg
            var minPt = bucket.OrderBy(p => p.DynamicValue > 0 ? p.DynamicValue : p.Avg).First();
            var maxPt = bucket.OrderByDescending(p => p.DynamicValue > 0 ? p.DynamicValue : p.Avg).First();
            var avgPt = bucket[bucket.Count / 2];

            result.Add(minPt);
            if (!ReferenceEquals(maxPt, minPt)) result.Add(maxPt);
            if (!ReferenceEquals(avgPt, minPt) && !ReferenceEquals(avgPt, maxPt)) result.Add(avgPt);
        }

        return result.Distinct().OrderBy(p => p.Depth).ToList();
    }
}
