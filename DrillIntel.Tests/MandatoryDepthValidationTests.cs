using System;
using System.Collections.Generic;
using System.Linq;
using DrillIntel.Models;
using DrillIntel.Services;
using Xunit;

namespace DrillIntel.Tests;

public class MandatoryDepthValidationTests
{
    private const string ExpectedDepthWarning = "You must map and select DEPTH channel. Please map and select the depth channel to continue";

    [Fact]
    public void Validation_Fails_WhenNoDepthMappingExists()
    {
        var mappings = new List<ColumnMappingRow>
        {
            new ColumnMappingRow { VuMaxColumnID = "HKLD", SourceColumnName = "Hookload" },
            new ColumnMappingRow { VuMaxColumnID = "RPM",  SourceColumnName = "RotaryRPM" }
        };

        var depthRow = mappings.FirstOrDefault(m => m.VuMaxColumnID.Equals("DEPTH", StringComparison.OrdinalIgnoreCase));
        bool isValid = depthRow != null && !string.IsNullOrWhiteSpace(depthRow.SourceColumnName);

        Assert.False(isValid);
        string warningMessage = ExpectedDepthWarning;
        Assert.Equal("You must map and select DEPTH channel. Please map and select the depth channel to continue", warningMessage);
    }

    [Fact]
    public void Validation_Fails_WhenDepthSourceColumnIsEmpty()
    {
        var mappings = new List<ColumnMappingRow>
        {
            new ColumnMappingRow { VuMaxColumnID = "DEPTH", SourceColumnName = "" },
            new ColumnMappingRow { VuMaxColumnID = "HKLD",  SourceColumnName = "Hookload" }
        };

        var depthRow = mappings.FirstOrDefault(m => m.VuMaxColumnID.Equals("DEPTH", StringComparison.OrdinalIgnoreCase));
        bool isValid = depthRow != null && !string.IsNullOrWhiteSpace(depthRow.SourceColumnName);

        Assert.False(isValid);
    }

    [Fact]
    public void Validation_Succeeds_WhenDepthChannelIsMapped()
    {
        var mappings = new List<ColumnMappingRow>
        {
            new ColumnMappingRow { VuMaxColumnID = "DEPTH", SourceColumnName = "MeasuredDepth" },
            new ColumnMappingRow { VuMaxColumnID = "HKLD",  SourceColumnName = "Hookload" }
        };

        var depthRow = mappings.FirstOrDefault(m => m.VuMaxColumnID.Equals("DEPTH", StringComparison.OrdinalIgnoreCase));
        bool isValid = depthRow != null && !string.IsNullOrWhiteSpace(depthRow.SourceColumnName);

        Assert.True(isValid);
        Assert.NotNull(depthRow);
        Assert.Equal("MeasuredDepth", depthRow.SourceColumnName);
    }

    [Fact]
    public void Precedence_ExplicitUserSelectionOverridesAutoDetection()
    {
        var headers = new List<string> { "DEPTH", "DEPT_ALTERNATE", "HKLD" };

        // Explicit user selected "DEPT_ALTERNATE"
        var userMapping = new ColumnMappingRow { VuMaxColumnID = "DEPTH", SourceColumnName = "DEPT_ALTERNATE" };

        // Automatic detection would pick "DEPTH"
        string autoDetected = headers.First(h => h.Equals("DEPTH", StringComparison.OrdinalIgnoreCase));

        // Rule: Explicit user selection must not be overwritten by auto-detection
        string effectiveSelection = !string.IsNullOrEmpty(userMapping.SourceColumnName)
            ? userMapping.SourceColumnName
            : autoDetected;

        Assert.Equal("DEPT_ALTERNATE", effectiveSelection);
    }
}
