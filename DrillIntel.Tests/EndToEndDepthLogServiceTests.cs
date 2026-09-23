using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Projects;
using DrillIntel.Services;
using Xunit;

namespace DrillIntel.Tests;

public class EndToEndDepthLogServiceTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ProjectSession _session;
    private readonly WellDataRepository _repo;
    private readonly ImportDepthLogService _depthLogService;

    private const string WellSDataPath = @"D:\Vumax-Well-Raw-Data\WellS well data\WellS-DepthLog-Data.csv";

    public EndToEndDepthLogServiceTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"drillintel_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(_tempDbPath);

        _session = new ProjectSession();
        _session.Load(_tempDbPath);

        _repo = new WellDataRepository(_session);
        _depthLogService = new ImportDepthLogService(_repo, _session);
    }

    public void Dispose()
    {
        _session.Dispose();
        try
        {
            if (File.Exists(_tempDbPath)) File.Delete(_tempDbPath);
        }
        catch { }
    }

    [Fact]
    public async Task ImportFromFileAsync_StreamsAndPersistsRealDataset()
    {
        Assert.True(File.Exists(WellSDataPath), $"File not found: {WellSDataPath}");

        var options = new DepthLogImportOptions
        {
            LogName = "WellS_DepthLog_Test",
            WellName = "TestWell_A",
            ColumnHeadingRow = 1,
            ImportFromRow = 2,
            ColumnMappings = new Dictionary<string, string>
            {
                ["DEPTH"] = "DEPTH"
            }
        };

        var logs = await _depthLogService.ImportFromFileAsync(WellSDataPath, options);

        Assert.Single(logs);
        var log = logs[0];
        Assert.Equal("WellS_DepthLog_Test", log.nameLog);
        Assert.Equal("TestWell_A", log.nameWell);
        Assert.Equal("DEPTH", log.indexCurve);
        Assert.Equal("measured depth", log.indexType);

        Assert.Equal("2899.9", log.startIndex);
        Assert.Equal("3321.9", log.endIndex);
        Assert.Equal("0.1", log.stepIncrement);
        Assert.Equal("3321.9", log.lastDataIndex);

        // Verify database table was created and has 4221 rows
        var dataService = _session.GetDataService();
        var dt = dataService.GetTable($"SELECT COUNT(*) FROM [{log.__dataTableName}]");
        Assert.NotNull(dt);
        long count = Convert.ToInt64(dt.Rows[0][0]);
        Assert.Equal(4221, count);

        // Verify VMX_DEPTH_LOG registration
        var dtLog = dataService.GetTable($"SELECT * FROM VMX_DEPTH_LOG WHERE LOG_ID = '{log.ObjectID}'");
        Assert.NotNull(dtLog);
        Assert.Equal(1, dtLog.Rows.Count);

        // Verify VMX_DEPTH_LOG_COLUMNS
        var dtCols = dataService.GetTable($"SELECT * FROM VMX_DEPTH_LOG_COLUMNS WHERE LOG_ID = '{log.ObjectID}'");
        Assert.NotNull(dtCols);
        Assert.True(dtCols.Rows.Count > 0);
    }

    [Fact]
    public void ImportFromFile_BlocksWhenDepthChannelNotMapped()
    {
        var tempBase = Path.GetTempFileName();
        var tempCsv = Path.ChangeExtension(tempBase, ".csv");
        try
        {
            File.WriteAllText(tempCsv, "HOOKLOAD,ROTOR_RPM,PUMP_PRESS\n120.5,60,2500\n121.0,62,2510\n");

            var options = new DepthLogImportOptions
            {
                LogName = "MissingDepthLog",
                WellName = "TestWell",
                ColumnHeadingRow = 1,
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string>
                {
                    ["HOOKLOAD"] = "HKLD"
                }
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                _depthLogService.ImportFromFile(tempCsv, options));

            Assert.Contains("You must map and select DEPTH channel. Please map and select the depth channel to continue", ex.Message);
        }
        finally
        {
            if (File.Exists(tempCsv)) File.Delete(tempCsv);
            if (File.Exists(tempBase)) File.Delete(tempBase);
        }
    }

    [Fact]
    public void Service_GetHeadersAndPreviewRows_WorkAcrossFormats()
    {
        var headers = _depthLogService.GetHeaders(WellSDataPath, columnHeadingRow: 1);
        Assert.NotEmpty(headers);
        Assert.Contains(headers, h => h.Equals("DEPTH", StringComparison.OrdinalIgnoreCase));

        var preview = _depthLogService.GetPreviewRows(WellSDataPath, importFromRow: 2, maxRows: 5);
        Assert.Equal(5, preview.Count);
    }
}
