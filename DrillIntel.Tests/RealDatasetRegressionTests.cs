using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DrillIntel.Services.Readers;
using Xunit;

namespace DrillIntel.Tests;

public class RealDatasetRegressionTests
{
    private const string WellSDataPath = @"D:\Vumax-Well-Raw-Data\WellS well data\WellS-DepthLog-Data.csv";
    private const string WghCsvPath = @"D:\Vumax-Well-Raw-Data\WGH-2 ST 5Sec Dataset\DepthLog\depthlog.csv";
    private const string WghLasPath = @"D:\Vumax-Well-Raw-Data\WGH-2 ST 5Sec Dataset\DepthLog\Depthlog.las";
    private const string WghXlsxPath = @"D:\Vumax-Well-Raw-Data\WGH-2 ST 5Sec Dataset\DepthLog\depthlog.xlsx";

    [Fact]
    public void WellSDepthLog_Csv_StreamsAllRowsWithAccurateMetadata()
    {
        Assert.True(File.Exists(WellSDataPath), $"File not found: {WellSDataPath}");

        var reader = new CsvDepthReader();
        var headers = reader.GetHeaders(WellSDataPath, columnHeadingRow: 1);

        Assert.NotEmpty(headers);
        Assert.Contains(headers, h => h.Equals("DEPTH", StringComparison.OrdinalIgnoreCase));

        int depthColIdx = headers.FindIndex(h => h.Equals("DEPTH", StringComparison.OrdinalIgnoreCase));
        Assert.True(depthColIdx >= 0);

        int rowCount = 0;
        double minDepth = double.MaxValue;
        double maxDepth = double.MinValue;
        var diffs = new List<double>();
        double? prevDepth = null;

        foreach (var row in reader.StreamDataRows(WellSDataPath, importFromRow: 2))
        {
            rowCount++;
            if (depthColIdx < row.Length && double.TryParse(row[depthColIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
            {
                if (d < minDepth) minDepth = d;
                if (d > maxDepth) maxDepth = d;

                if (prevDepth.HasValue)
                {
                    var diff = Math.Round(Math.Abs(d - prevDepth.Value), 4);
                    if (diff > 0) diffs.Add(diff);
                }
                prevDepth = d;
            }
        }

        Assert.Equal(4221, rowCount);
        Assert.Equal(2899.90, minDepth, 2);
        Assert.Equal(3321.90, maxDepth, 2);

        var modeStep = diffs.GroupBy(d => d).OrderByDescending(g => g.Count()).First().Key;
        Assert.Equal(0.1, modeStep, 2);
    }

    [Fact]
    public void WghDepthLog_Csv_StreamsAllRowsWithAccurateMetadata()
    {
        Assert.True(File.Exists(WghCsvPath), $"File not found: {WghCsvPath}");

        var reader = new CsvDepthReader();
        var headers = reader.GetHeaders(WghCsvPath, columnHeadingRow: 1);

        Assert.NotEmpty(headers);
        Assert.Equal("DEPTH", headers[0], ignoreCase: true);

        int rowCount = 0;
        double minDepth = double.MaxValue;
        double maxDepth = double.MinValue;
        var diffs = new List<double>();
        double? prevDepth = null;

        foreach (var row in reader.StreamDataRows(WghCsvPath, importFromRow: 2))
        {
            rowCount++;
            if (row.Length > 0 && double.TryParse(row[0], NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
            {
                if (d < minDepth) minDepth = d;
                if (d > maxDepth) maxDepth = d;

                if (prevDepth.HasValue)
                {
                    var diff = Math.Round(Math.Abs(d - prevDepth.Value), 4);
                    if (diff > 0) diffs.Add(diff);
                }
                prevDepth = d;
            }
        }

        Assert.Equal(17767, rowCount);
        Assert.Equal(4003.0, minDepth, 1);
        Assert.Equal(21769.0, maxDepth, 1);

        var modeStep = diffs.GroupBy(d => d).OrderByDescending(g => g.Count()).First().Key;
        Assert.Equal(1.0, modeStep, 1);
    }

    [Fact]
    public void WghDepthLog_Las_ParsesHeaderAndStreamsData()
    {
        Assert.True(File.Exists(WghLasPath), $"File not found: {WghLasPath}");

        var reader = new LasDepthReader();
        var metadata = reader.ExtractMetadata(WghLasPath);

        Assert.NotNull(metadata);
        Assert.Equal("DEPTH", metadata.IndexCurve, ignoreCase: true);
        Assert.True(double.TryParse(metadata.StartIndex, NumberStyles.Any, CultureInfo.InvariantCulture, out double startD));
        Assert.Equal(1.0, startD, 1);
        Assert.True(double.TryParse(metadata.EndIndex, NumberStyles.Any, CultureInfo.InvariantCulture, out double endD));
        Assert.Equal(6000.0, endD, 1);
        Assert.True(double.TryParse(metadata.StepIncrement, NumberStyles.Any, CultureInfo.InvariantCulture, out double stepD));
        Assert.Equal(1.0, stepD, 1);

        var headers = reader.GetHeaders(WghLasPath);
        Assert.NotEmpty(headers);
        Assert.Equal("DEPTH", headers[0], ignoreCase: true);

        var preview = reader.GetPreviewRows(WghLasPath, importFromRow: 1, maxRows: 10);
        Assert.NotEmpty(preview);
        Assert.Equal(10, preview.Count);

        // Verify first row depth matches STRT
        Assert.True(double.TryParse(preview[0][0], NumberStyles.Any, CultureInfo.InvariantCulture, out double firstD));
        Assert.Equal(4003.0, firstD, 1);
    }

    [Fact]
    public void WghDepthLog_Xlsx_ParsesWorksheetsAndRows()
    {
        Assert.True(File.Exists(WghXlsxPath), $"File not found: {WghXlsxPath}");

        var reader = new ExcelDepthReader();
        var sheets = reader.GetWorksheets(WghXlsxPath);

        Assert.NotEmpty(sheets);

        var headers = reader.GetHeaders(WghXlsxPath, columnHeadingRow: 1, worksheetName: sheets[0]);
        Assert.NotEmpty(headers);
        Assert.Equal("DEPTH", headers[0], ignoreCase: true);

        var preview = reader.GetPreviewRows(WghXlsxPath, importFromRow: 2, maxRows: 10, worksheetName: sheets[0]);
        Assert.NotEmpty(preview);
        Assert.Equal(10, preview.Count);

        Assert.True(double.TryParse(preview[0][0], NumberStyles.Any, CultureInfo.InvariantCulture, out double firstD));
        Assert.Equal(4003.0, firstD, 1);
    }
}
