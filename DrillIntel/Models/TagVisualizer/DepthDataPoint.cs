using System;

namespace DrillIntel.Models.TagVisualizer;

/// <summary>
/// Simple point representing a timestamped depth telemetry sample.
/// </summary>
public struct DepthDataPoint
{
    public DateTime DateTime { get; }
    public double Depth { get; }
    public double? HoleDepth { get; }

    public DepthDataPoint(DateTime dateTime, double depth, double? holeDepth = null)
    {
        DateTime = dateTime;
        Depth = depth;
        HoleDepth = holeDepth;
    }
}

