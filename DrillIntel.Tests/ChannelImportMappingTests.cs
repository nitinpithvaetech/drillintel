using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Projects;
using DrillIntel.Services;
using Xunit;

namespace DrillIntel.Tests;

public class ChannelImportMappingTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ProjectSession _session;
    private readonly WellDataRepository _repo;
    private readonly ImportDepthLogService _depthLogService;

    public ChannelImportMappingTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"drillintel_channeltest_{Guid.NewGuid():N}.dintel");
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
    public async Task ChannelImport_MappedVuMaxChannelStoresImportedValues_AndDoesNotAddImportedColumn_AndAddsUnmappedColumn()
    {
        // CSV with imported columns: DEPTH, GAMMARAY, SRVGRA_TF
        string csvPath = Path.Combine(Path.GetTempPath(), $"channel_test_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath,
                "DEPTH,GAMMARAY,SRVGRA_TF\n" +
                "100.0,2500.5,12.3\n" +
                "101.0,2501.0,14.5\n" +
                "102.0,2502.5,16.8\n");

            // Mapping: DEPTH (VuMax) is mapped to GAMMARAY (imported)
            // SRVGRA_TF is unmapped
            var options = new DepthLogImportOptions
            {
                LogName = "ChannelMappedDepthLog",
                WellName = "Well_Channel_Test",
                ColumnHeadingRow = 1,
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string>
                {
                    ["GAMMARAY"] = "DEPTH"
                }
            };

            var logs = await _depthLogService.ImportFromFileAsync(csvPath, options);
            Assert.Single(logs);
            var log = logs[0];
            string tableName = log.__dataTableName;

            // 1. Verify table columns in target depthLog table
            var tableCols = await _repo.GetTableColumnsAsync(tableName);

            // • DEPTH must exist in target table
            Assert.Contains("DEPTH", tableCols, StringComparer.OrdinalIgnoreCase);
            // • SRVGRA_TF must be dynamically created in target table
            Assert.Contains("SRVGRA_TF", tableCols, StringComparer.OrdinalIgnoreCase);
            // • GAMMARAY column must NOT be created or kept in target table
            Assert.DoesNotContain("GAMMARAY", tableCols, StringComparer.OrdinalIgnoreCase);

            // 2. Verify values stored:
            // Values from GAMMARAY must be stored in DEPTH
            // Values from SRVGRA_TF must be stored in SRVGRA_TF
            var dataService = _session.GetDataService();
            var dt = dataService.GetTable($"SELECT DEPTH, SRVGRA_TF FROM [{tableName}] ORDER BY DATA_INDEX ASC;");
            Assert.NotNull(dt);
            Assert.Equal(3, dt.Rows.Count);

            // Row 1: GAMMARAY value 2500.5 stored in DEPTH; SRVGRA_TF stored in SRVGRA_TF
            Assert.Equal(2500.5, Convert.ToDouble(dt.Rows[0]["DEPTH"]));
            Assert.Equal(12.3, Convert.ToDouble(dt.Rows[0]["SRVGRA_TF"]));

            // Row 2: GAMMARAY value 2501.0 stored in DEPTH; SRVGRA_TF stored in SRVGRA_TF
            Assert.Equal(2501.0, Convert.ToDouble(dt.Rows[1]["DEPTH"]));
            Assert.Equal(14.5, Convert.ToDouble(dt.Rows[1]["SRVGRA_TF"]));

            // Row 3: GAMMARAY value 2502.5 stored in DEPTH; SRVGRA_TF stored in SRVGRA_TF
            Assert.Equal(2502.5, Convert.ToDouble(dt.Rows[2]["DEPTH"]));
            Assert.Equal(16.8, Convert.ToDouble(dt.Rows[2]["SRVGRA_TF"]));

            // 3. Verify VMX_TIME_LOG logging (Requirement 3)
            var dtTimeLog = dataService.GetTable($"SELECT * FROM VMX_TIME_LOG WHERE LOG_ID = '{log.ObjectID}';");
            Assert.NotNull(dtTimeLog);
            Assert.Equal(1, dtTimeLog.Rows.Count);
            Assert.Equal("Success", dtTimeLog.Rows[0]["COMMENTS"]?.ToString());
            Assert.Contains("QC:", dtTimeLog.Rows[0]["DESCRIPTION"]?.ToString() ?? "");
            Assert.Equal(100.0, Convert.ToDouble(dtTimeLog.Rows[0]["QC_SCORE"]));

            // Also check VMX_TIME_LOG_SUMMARY
            var dtTimeSummary = dataService.GetTable($"SELECT * FROM VMX_TIME_LOG_SUMMARY WHERE LogId = '{log.ObjectID}';");
            Assert.NotNull(dtTimeSummary);
            Assert.Equal(1, dtTimeSummary.Rows.Count);
            Assert.Equal("Success", dtTimeSummary.Rows[0]["ImportStatus"]?.ToString());
            Assert.Equal(100.0, Convert.ToDouble(dtTimeSummary.Rows[0]["QcScore"]));
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task ChannelImport_QCChecks_DetectMissingValues_AndInvalidRanges()
    {
        string csvPath = Path.Combine(Path.GetTempPath(), $"qc_test_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath,
                "DEPTH,HKLD,RPM\n" +
                "100.0,50.0,60.0\n" +    // Valid
                "101.0,-10.0,60.0\n" +   // Invalid range: negative HKLD
                "102.0,50.0,-5.0\n" +    // Invalid range: negative RPM
                "-999.25,50.0,60.0\n" +  // Missing value: null sentinel depth
                "104.0,50.0,60.0\n");    // Valid

            var options = new DepthLogImportOptions
            {
                LogName = "QCTestDepthLog",
                WellName = "Well_QC_Test",
                ColumnHeadingRow = 1,
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string>
                {
                    ["DEPTH"] = "DEPTH",
                    ["HKLD"] = "HKLD",
                    ["RPM"] = "RPM"
                }
            };

            var logs = await _depthLogService.ImportFromFileAsync(csvPath, options);
            Assert.Single(logs);
            var log = logs[0];

            // 5 total rows, 2 valid rows -> QC Score = 40.0%
            var dataService = _session.GetDataService();
            var dtTimeLog = dataService.GetTable($"SELECT COMMENTS, DESCRIPTION, QC_SCORE FROM VMX_TIME_LOG WHERE LOG_ID = '{log.ObjectID}';");
            Assert.NotNull(dtTimeLog);
            Assert.Equal(1, dtTimeLog.Rows.Count);
            Assert.Equal("Success", dtTimeLog.Rows[0]["COMMENTS"]?.ToString());
            Assert.Contains("QC: 40.0%", dtTimeLog.Rows[0]["DESCRIPTION"]?.ToString() ?? "");
            Assert.Equal(40.0, Convert.ToDouble(dtTimeLog.Rows[0]["QC_SCORE"]));
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task ChannelImport_StandardMapping_CreatesAllUnmappedAsDynamicColumns()
    {
        string csvPath = Path.Combine(Path.GetTempPath(), $"std_channel_test_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath,
                "MD,GR,CALI,SONIC\n" +
                "1000.0,65.2,8.5,70.1\n" +
                "1001.0,68.4,8.5,71.2\n");

            // Mapping: MD -> DEPTH
            // GR, CALI, SONIC unmapped -> created dynamically as columns in table
            var options = new DepthLogImportOptions
            {
                LogName = "MultiChannelLog",
                WellName = "Well_Multi_Test",
                ColumnHeadingRow = 1,
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string>
                {
                    ["MD"] = "DEPTH"
                }
            };

            var logs = await _depthLogService.ImportFromFileAsync(csvPath, options);
            var log = logs[0];
            string tableName = log.__dataTableName;

            var cols = await _repo.GetTableColumnsAsync(tableName);
            Assert.Contains("DEPTH", cols, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("GR", cols, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("CALI", cols, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("SONIC", cols, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("MD", cols, StringComparer.OrdinalIgnoreCase);

            var dataService = _session.GetDataService();
            var dt = dataService.GetTable($"SELECT DEPTH, GR, CALI, SONIC FROM [{tableName}];");
            Assert.NotNull(dt);
            Assert.Equal(2, dt.Rows.Count);
            Assert.Equal(1000.0, Convert.ToDouble(dt.Rows[0]["DEPTH"]));
            Assert.Equal(65.2, Convert.ToDouble(dt.Rows[0]["GR"]));
            Assert.Equal(8.5, Convert.ToDouble(dt.Rows[0]["CALI"]));
            Assert.Equal(70.1, Convert.ToDouble(dt.Rows[0]["SONIC"]));
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task ChannelImport_ReverseMappingFormat_VuMaxToSourceKey_WorksIdentically()
    {
        // Test that options.ColumnMappings with ["DEPTH"] = "GAMMARAY" (VuMax -> Source) also resolves correctly
        string csvPath = Path.Combine(Path.GetTempPath(), $"rev_map_test_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath,
                "DEPTH,GAMMARAY,SRVGRA_TF\n" +
                "100.0,3000.5,55.5\n" +
                "101.0,3001.0,56.5\n");

            var options = new DepthLogImportOptions
            {
                LogName = "ReverseMapDepthLog",
                WellName = "Well_RevMap_Test",
                ColumnHeadingRow = 1,
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string>
                {
                    ["DEPTH"] = "GAMMARAY" // Target VuMax channel as key, imported source header as value
                }
            };

            var logs = await _depthLogService.ImportFromFileAsync(csvPath, options);
            Assert.Single(logs);
            var log = logs[0];
            string tableName = log.__dataTableName;

            var tableCols = await _repo.GetTableColumnsAsync(tableName);
            Assert.Contains("DEPTH", tableCols, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("SRVGRA_TF", tableCols, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("GAMMARAY", tableCols, StringComparer.OrdinalIgnoreCase);

            var dataService = _session.GetDataService();
            var dt = dataService.GetTable($"SELECT DEPTH, SRVGRA_TF FROM [{tableName}] ORDER BY DATA_INDEX ASC;");
            Assert.NotNull(dt);
            Assert.Equal(2, dt.Rows.Count);
            Assert.Equal(3000.5, Convert.ToDouble(dt.Rows[0]["DEPTH"]));
            Assert.Equal(55.5, Convert.ToDouble(dt.Rows[0]["SRVGRA_TF"]));

            // Check VMX_TIME_LOG has QC score and status
            var dtTimeLog = dataService.GetTable($"SELECT COMMENTS, DESCRIPTION, QC_SCORE FROM VMX_TIME_LOG WHERE LOG_ID = '{log.ObjectID}';");
            Assert.NotNull(dtTimeLog);
            Assert.Equal(1, dtTimeLog.Rows.Count);
            Assert.Equal("Success", dtTimeLog.Rows[0]["COMMENTS"]?.ToString());
            Assert.Equal(100.0, Convert.ToDouble(dtTimeLog.Rows[0]["QC_SCORE"]));
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task ChannelImport_MultipleMappingsAndDynamicColumns_CorrectColumnsAndValues()
    {
        string csvPath = Path.Combine(Path.GetTempPath(), $"multi_map_test_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath,
                "DEPT,GAMMA,HOOKLOAD,SENSOR_X,SENSOR_Y\n" +
                "500.0,80.0,120.0,1.1,2.2\n" +
                "501.0,82.0,125.0,1.2,2.3\n");

            var options = new DepthLogImportOptions
            {
                LogName = "ComplexDepthLog",
                WellName = "Well_Complex_Test",
                ColumnHeadingRow = 1,
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string>
                {
                    ["DEPT"] = "DEPTH",
                    ["GAMMA"] = "GAMMARAY",
                    ["HOOKLOAD"] = "HKLD"
                }
            };

            var logs = await _depthLogService.ImportFromFileAsync(csvPath, options);
            var log = logs[0];
            string tableName = log.__dataTableName;

            var cols = await _repo.GetTableColumnsAsync(tableName);
            // Mapped columns must exist under mapped VuMax channel names
            Assert.Contains("DEPTH", cols, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("GAMMARAY", cols, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("HKLD", cols, StringComparer.OrdinalIgnoreCase);

            // Unmapped columns must be dynamically created
            Assert.Contains("SENSOR_X", cols, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("SENSOR_Y", cols, StringComparer.OrdinalIgnoreCase);

            // Original imported column names must NOT be added
            Assert.DoesNotContain("DEPT", cols, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("GAMMA", cols, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("HOOKLOAD", cols, StringComparer.OrdinalIgnoreCase);

            var dataService = _session.GetDataService();
            var dt = dataService.GetTable($"SELECT DEPTH, GAMMARAY, HKLD, SENSOR_X, SENSOR_Y FROM [{tableName}];");
            Assert.NotNull(dt);
            Assert.Equal(2, dt.Rows.Count);
            Assert.Equal(500.0, Convert.ToDouble(dt.Rows[0]["DEPTH"]));
            Assert.Equal(80.0, Convert.ToDouble(dt.Rows[0]["GAMMARAY"]));
            Assert.Equal(120.0, Convert.ToDouble(dt.Rows[0]["HKLD"]));
            Assert.Equal(1.1, Convert.ToDouble(dt.Rows[0]["SENSOR_X"]));
            Assert.Equal(2.2, Convert.ToDouble(dt.Rows[0]["SENSOR_Y"]));

            // Verify VMX_TIME_LOG has been updated
            var dtTimeLog = dataService.GetTable($"SELECT * FROM VMX_TIME_LOG WHERE LOG_ID = '{log.ObjectID}';");
            Assert.NotNull(dtTimeLog);
            Assert.Equal(1, dtTimeLog.Rows.Count);
            Assert.Equal("Success", dtTimeLog.Rows[0]["COMMENTS"]?.ToString());
            Assert.Equal(100.0, Convert.ToDouble(dtTimeLog.Rows[0]["QC_SCORE"]));
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }
}
