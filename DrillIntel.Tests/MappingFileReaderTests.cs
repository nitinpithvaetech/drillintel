using System;
using System.IO;
using DrillIntel.Services.Readers;
using Xunit;

namespace DrillIntel.Tests;

public class MappingFileReaderTests
{
    [Fact]
    public void MappingFileReader_ParsesRealLegacyVmfFile()
    {
        var vmfPath = @"D:\Vumax-Well-Raw-Data\WGH-2 ST 5Sec Dataset\DepthLog\Mapping.vmf";
        Assert.True(File.Exists(vmfPath), $"Legacy mapping file must exist at {vmfPath}");

        var reader = new MappingFileReader();
        var result = reader.LoadMappingFile(vmfPath);

        Assert.True(result.Success);
        Assert.Equal(2, result.ImportFromRow);
        Assert.Equal(1, result.ColumnHeadingRow);
        Assert.False(result.DateTimeInSeparateCol);
        Assert.Equal(0, result.DateColNo);
        Assert.Equal(0, result.TimeColNo);
        Assert.Equal("ISO", result.DateFormat);

        // Column mappings
        Assert.True(result.TargetToColumnIndex.ContainsKey("DEPTH"));
        Assert.Equal(0, result.TargetToColumnIndex["DEPTH"]);

        Assert.True(result.TargetToColumnIndex.ContainsKey("DBPOS"));
        Assert.Equal(2, result.TargetToColumnIndex["DBPOS"]);

        Assert.True(result.TargetToColumnIndex.ContainsKey("MUDGAS_TGAS"));
        Assert.Equal(3, result.TargetToColumnIndex["MUDGAS_TGAS"]);
    }

    [Fact]
    public void MappingFileReader_ParsesJsonDictionaryMapping()
    {
        var json = @"{
            ""DEPTH"": ""MeasuredDepth"",
            ""HKLD"": ""Hook_Load"",
            ""RPM"": ""RotarySpeed""
        }";

        var tempFile = Path.GetTempFileName();
        var jsonFile = Path.ChangeExtension(tempFile, ".json");
        try
        {
            File.WriteAllText(jsonFile, json);

            var reader = new MappingFileReader();
            var result = reader.LoadMappingFile(jsonFile);

            Assert.True(result.Success);
            Assert.Equal("MeasuredDepth", result.TargetToSourceColumn["DEPTH"]);
            Assert.Equal("Hook_Load", result.TargetToSourceColumn["HKLD"]);
            Assert.Equal("RotarySpeed", result.TargetToSourceColumn["RPM"]);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(jsonFile)) File.Delete(jsonFile);
        }
    }

    [Fact]
    public void MappingFileReader_ParsesJsonArrayMapping()
    {
        var json = @"[
            { ""Target"": ""DEPTH"", ""Source"": ""MD"" },
            { ""Target"": ""HKLD"", ""Source"": ""HKLA"" }
        ]";

        var tempFile = Path.GetTempFileName();
        var jsonFile = Path.ChangeExtension(tempFile, ".json");
        try
        {
            File.WriteAllText(jsonFile, json);

            var reader = new MappingFileReader();
            var result = reader.LoadMappingFile(jsonFile);

            Assert.True(result.Success);
            Assert.Equal("MD", result.TargetToSourceColumn["DEPTH"]);
            Assert.Equal("HKLA", result.TargetToSourceColumn["HKLD"]);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(jsonFile)) File.Delete(jsonFile);
        }
    }

    [Fact]
    public void MappingFileReader_ParsesCsvMapping()
    {
        var csv = @"Target,Source
DEPTH,Measured_Depth
HKLD,HookLoad_klbs
ROP,RateOfPenetration";

        var tempFile = Path.GetTempFileName();
        var csvFile = Path.ChangeExtension(tempFile, ".csv");
        try
        {
            File.WriteAllText(csvFile, csv);

            var reader = new MappingFileReader();
            var result = reader.LoadMappingFile(csvFile);

            Assert.True(result.Success);
            Assert.Equal("Measured_Depth", result.TargetToSourceColumn["DEPTH"]);
            Assert.Equal("HookLoad_klbs", result.TargetToSourceColumn["HKLD"]);
            Assert.Equal("RateOfPenetration", result.TargetToSourceColumn["ROP"]);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
            if (File.Exists(csvFile)) File.Delete(csvFile);
        }
    }
}

