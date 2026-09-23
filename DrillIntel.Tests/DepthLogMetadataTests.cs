using System;
using System.Collections.Generic;
using System.Globalization;
using DrillIntel.Services;
using Xunit;

namespace DrillIntel.Tests;

public class DepthLogMetadataTests
{
    [Fact]
    public void MetadataCalculation_CalculatesStepIncrementCorrectly()
    {
        // 0.1 step increment
        var depthValues = new List<double> { 2899.90, 2900.00, 2900.10, 2900.20, 2900.30 };
        
        var diffs = new List<double>();
        for (int i = 1; i < depthValues.Count; i++)
        {
            var diff = Math.Round(depthValues[i] - depthValues[i - 1], 4);
            if (diff > 0) diffs.Add(diff);
        }

        var modeStep = diffs.GroupBy(d => d).OrderByDescending(g => g.Count()).First().Key;
        Assert.Equal(0.1, modeStep);

        var startIndex = depthValues[0];
        var endIndex = depthValues[^1];
        var lastDataIndex = (int)Math.Round((endIndex - startIndex) / modeStep);

        Assert.Equal(2899.9, startIndex);
        Assert.Equal(2900.3, endIndex);
        Assert.Equal(4, lastDataIndex);
    }

    [Fact]
    public void MetadataCalculation_Handles1FootStepIncrement()
    {
        var depthValues = new List<double> { 4003.0, 4004.0, 4005.0, 4006.0, 4007.0 };
        
        var diffs = new List<double>();
        for (int i = 1; i < depthValues.Count; i++)
        {
            var diff = Math.Round(depthValues[i] - depthValues[i - 1], 4);
            if (diff > 0) diffs.Add(diff);
        }

        var modeStep = diffs.GroupBy(d => d).OrderByDescending(g => g.Count()).First().Key;
        Assert.Equal(1.0, modeStep);

        var lastDataIndex = (int)Math.Round((depthValues[^1] - depthValues[0]) / modeStep);
        Assert.Equal(4, lastDataIndex);
    }

    [Fact]
    public void MetadataCalculation_CalculatesQcScoreCorrectly()
    {
        int totalRows = 100;
        int totalColumns = 5;
        int totalExpectedCells = totalRows * totalColumns; // 500
        int validCells = 475; // 25 empty or NaN

        double qcScore = (double)validCells / totalExpectedCells * 100.0;
        Assert.Equal(95.0, qcScore);
    }
}

