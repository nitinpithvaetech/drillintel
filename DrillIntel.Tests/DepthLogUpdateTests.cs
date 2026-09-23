using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Models;
using DrillIntel.Projects;
using DrillIntel.Services;
using Xunit;

namespace DrillIntel.Tests;

public class DepthLogUpdateTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ProjectSession _session;
    private readonly WellDataRepository _repo;
    private readonly ImportDepthLogService _depthLogService;

    public DepthLogUpdateTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"drillintel_updatetest_{Guid.NewGuid():N}.dintel");
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
    public async Task UpdateLogic_ReplacesValuesInMappedColumns_PreservesUnmappedColumns()
    {
        // 1. Create an initial DepthLog with DEPTH, ROP, HKLD
        string initialCsv = Path.Combine(Path.GetTempPath(), $"initial_{Guid.NewGuid():N}.csv");
        File.WriteAllText(initialCsv,
            "DEPTH,ROP,HKLD\n" +
            "1000.0,15.5,50.0\n" +
            "1001.0,16.0,52.0\n" +
            "1002.0,17.5,55.0\n");

        var initialOptions = new DepthLogImportOptions
        {
            LogName = "TestDepthLog_1",
            WellName = "Well_Update_Test",
            ColumnHeadingRow = 1,
            ImportFromRow = 2,
            ColumnMappings = new Dictionary<string, string>
            {
                ["DEPTH"] = "DEPTH",
                ["ROP"] = "ROP",
                ["HKLD"] = "HKLD"
            }
        };

        var initialLogs = await _depthLogService.ImportFromFileAsync(initialCsv, initialOptions);
        var initialLog = initialLogs[0];
        string targetTable = initialLog.__dataTableName;

        // Verify initial state
        var conn = _session.GetConnection();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT ROP, HKLD FROM [{targetTable}] WHERE DEPTH = 1000.0;";
            using var rdr = cmd.ExecuteReader();
            Assert.True(rdr.Read());
            Assert.Equal(15.5, Convert.ToDouble(rdr["ROP"]));
            Assert.Equal(50.0, Convert.ToDouble(rdr["HKLD"]));
        }

        // 2. Prepare update file: Update ROP only (do not include HKLD)
        string updateCsv = Path.Combine(Path.GetTempPath(), $"update_{Guid.NewGuid():N}.csv");
        File.WriteAllText(updateCsv,
            "DEPTH,ROP\n" +
            "1000.0,99.9\n" +
            "1001.0,88.8\n" +
            "1002.0,77.7\n");

        var updateOptions = new DepthLogImportOptions
        {
            OperationType = OperationType.UpdateData,
            LogName = "TestDepthLog_1",
            TargetTableName = targetTable,
            ColumnHeadingRow = 1,
            ImportFromRow = 2
        };

        var updatedLogs = await _depthLogService.ImportFromFileAsync(updateCsv, updateOptions);
        Assert.Single(updatedLogs);

        // 3. Verify: ROP values are replaced with new values, and HKLD is untouched!
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT ROP, HKLD FROM [{targetTable}] WHERE DEPTH = 1000.0;";
            using var rdr = cmd.ExecuteReader();
            Assert.True(rdr.Read());
            Assert.Equal(99.9, Convert.ToDouble(rdr["ROP"])); // Mapped column updated
            Assert.Equal(50.0, Convert.ToDouble(rdr["HKLD"])); // Unmapped column preserved
        }

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT ROP, HKLD FROM [{targetTable}] WHERE DEPTH = 1001.0;";
            using var rdr = cmd.ExecuteReader();
            Assert.True(rdr.Read());
            Assert.Equal(88.8, Convert.ToDouble(rdr["ROP"]));
            Assert.Equal(52.0, Convert.ToDouble(rdr["HKLD"]));
        }

        File.Delete(initialCsv);
        File.Delete(updateCsv);
    }

    [Fact]
    public async Task Restrictions_DoNotCreateNewColumns_And_DoNotAlterColumnNames()
    {
        // 1. Initial table creation
        string initialCsv = Path.Combine(Path.GetTempPath(), $"initial_{Guid.NewGuid():N}.csv");
        File.WriteAllText(initialCsv,
            "DEPTH,ROP\n" +
            "500.0,10.0\n" +
            "501.0,12.0\n");

        var initialOptions = new DepthLogImportOptions
        {
            LogName = "SchemaPreserve_Log",
            WellName = "Well_Schema_Test",
            ColumnHeadingRow = 1,
            ImportFromRow = 2
        };

        var initialLogs = await _depthLogService.ImportFromFileAsync(initialCsv, initialOptions);
        string targetTable = initialLogs[0].__dataTableName;

        // Get table columns before update
        var columnsBefore = await _repo.GetTableColumnsAsync(targetTable);

        // 2. Update with file containing extra columns not in target table
        string updateCsv = Path.Combine(Path.GetTempPath(), $"update_{Guid.NewGuid():N}.csv");
        File.WriteAllText(updateCsv,
            "DEPTH,ROP,EXTRA_COL_1,UNWANTED_COL_2,TEMP_C\n" +
            "500.0,25.0,999,888,45.2\n" +
            "501.0,30.0,999,888,46.1\n");

        var updateOptions = new DepthLogImportOptions
        {
            OperationType = OperationType.UpdateData,
            LogName = "SchemaPreserve_Log",
            TargetTableName = targetTable,
            ColumnHeadingRow = 1,
            ImportFromRow = 2
        };

        await _depthLogService.ImportFromFileAsync(updateCsv, updateOptions);

        // 3. Verify: Columns after update MUST BE EXACTLY identical to columns before update
        var columnsAfter = await _repo.GetTableColumnsAsync(targetTable);
        Assert.Equal(columnsBefore, columnsAfter);

        // Neither EXTRA_COL_1 nor UNWANTED_COL_2 nor TEMP_C must exist in the table
        Assert.DoesNotContain("EXTRA_COL_1", columnsAfter, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("UNWANTED_COL_2", columnsAfter, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("TEMP_C", columnsAfter, StringComparer.OrdinalIgnoreCase);

        // Verify ROP was updated
        var conn = _session.GetConnection();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT ROP FROM [{targetTable}] WHERE DEPTH = 500.0;";
            var ropVal = Convert.ToDouble(cmd.ExecuteScalar());
            Assert.Equal(25.0, ropVal);
        }

        File.Delete(initialCsv);
        File.Delete(updateCsv);
    }

    [Fact]
    public async Task MappingRules_AutoMapsMatchingVuMaxColumnNames()
    {
        // 1. Initial DepthLog
        string initialCsv = Path.Combine(Path.GetTempPath(), $"initial_{Guid.NewGuid():N}.csv");
        File.WriteAllText(initialCsv,
            "DEPTH,ROP,SPPA,TORQ\n" +
            "200.0,5.0,1500,2000\n");

        var initialOptions = new DepthLogImportOptions
        {
            LogName = "AutoMap_Log",
            WellName = "Well_AutoMap_Test",
            ColumnHeadingRow = 1,
            ImportFromRow = 2
        };

        var initialLogs = await _depthLogService.ImportFromFileAsync(initialCsv, initialOptions);
        string targetTable = initialLogs[0].__dataTableName;

        // 2. Update file has matching column names in different casing and standard alias for DEPTH (DEPT)
        string updateCsv = Path.Combine(Path.GetTempPath(), $"update_{Guid.NewGuid():N}.csv");
        File.WriteAllText(updateCsv,
            "DEPT,rop,SPPA\n" + // DEPT matches DEPTH, rop matches ROP, SPPA matches SPPA; TORQ is unmapped
            "200.0,42.0,1850\n");

        var updateOptions = new DepthLogImportOptions
        {
            OperationType = OperationType.UpdateData,
            LogName = "AutoMap_Log",
            TargetTableName = targetTable,
            ColumnHeadingRow = 1,
            ImportFromRow = 2
        };

        await _depthLogService.ImportFromFileAsync(updateCsv, updateOptions);

        // 3. Verify auto-mapped columns were updated, and unmapped TORQ is preserved
        var conn = _session.GetConnection();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT ROP, SPPA, TORQ FROM [{targetTable}] WHERE DEPTH = 200.0;";
            using var rdr = cmd.ExecuteReader();
            Assert.True(rdr.Read());
            Assert.Equal(42.0, Convert.ToDouble(rdr["ROP"])); // auto-mapped from 'rop'
            Assert.Equal(1850.0, Convert.ToDouble(rdr["SPPA"])); // auto-mapped from 'SPPA'
            Assert.Equal(2000.0, Convert.ToDouble(rdr["TORQ"])); // unmapped kept as-is
        }

        File.Delete(initialCsv);
        File.Delete(updateCsv);
    }

    [Fact]
    public async Task MandatoryDepthValidation_FailsIfDepthNotMapped()
    {
        string dummyCsv = Path.Combine(Path.GetTempPath(), $"nodepth_{Guid.NewGuid():N}.csv");
        File.WriteAllText(dummyCsv,
            "ROP,HKLD\n" +
            "10.0,50.0\n");

        var mappings = new List<ChannelMapping>
        {
            new ChannelMapping { CsvColumnHeader = "ROP", MappedVumaxChannel = "ROP" }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _repo.StreamUpdateDepthDataAsync(
                "dummyTable",
                dummyCsv,
                mappings,
                1,
                2);
        });

        Assert.Contains("You must map and select DEPTH channel", ex.Message);
        File.Delete(dummyCsv);
    }

    [Fact]
    public async Task UpdateLogic_PreservesTargetTableNameFormat_LikeVuMaxHashFormat()
    {
        // 1. Initial import creates table with VuMax format depthLog{part1}#{part2}
        string initialCsv = Path.Combine(Path.GetTempPath(), $"initial_{Guid.NewGuid():N}.csv");
        File.WriteAllText(initialCsv,
            "DEPTH,ROP\n" +
            "100.0,12.0\n");

        var initialOptions = new DepthLogImportOptions
        {
            LogName = "VuMaxHash_Log",
            WellName = "Well_Hash_Test",
            ColumnHeadingRow = 1,
            ImportFromRow = 2
        };

        var initialLogs = await _depthLogService.ImportFromFileAsync(initialCsv, initialOptions);
        string targetTable = initialLogs[0].__dataTableName;

        // Verify name format (e.g., depthLog11026259#41675093)
        Assert.StartsWith("depthLog", targetTable, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#", targetTable);

        // 2. Perform update
        string updateCsv = Path.Combine(Path.GetTempPath(), $"update_{Guid.NewGuid():N}.csv");
        File.WriteAllText(updateCsv,
            "DEPTH,ROP\n" +
            "100.0,99.0\n");

        var updateOptions = new DepthLogImportOptions
        {
            OperationType = OperationType.UpdateData,
            LogName = "VuMaxHash_Log",
            TargetTableName = targetTable,
            ColumnHeadingRow = 1,
            ImportFromRow = 2
        };

        var updatedLogs = await _depthLogService.ImportFromFileAsync(updateCsv, updateOptions);

        // Verify target table name in DepthLog metadata and database is strictly unchanged
        Assert.Equal(targetTable, updatedLogs[0].__dataTableName);

        var conn = _session.GetConnection();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{targetTable}';";
            var count = Convert.ToInt32(cmd.ExecuteScalar());
            Assert.Equal(1, count);
        }

        File.Delete(initialCsv);
        File.Delete(updateCsv);
    }

    [Fact]
    public async Task UpdateLogic_NullValuesInUpdateFile_SetToNullInTargetColumn_LeavesOtherColumnsIntact()
    {
        string initialCsv = Path.Combine(Path.GetTempPath(), $"initial_{Guid.NewGuid():N}.csv");
        File.WriteAllText(initialCsv,
            "DEPTH,ROP,HKLD\n" +
            "200.0,20.0,150.0\n");

        var initialOptions = new DepthLogImportOptions
        {
            LogName = "NullTest_Log",
            WellName = "Well_Null_Test",
            ColumnHeadingRow = 1,
            ImportFromRow = 2
        };

        var initialLogs = await _depthLogService.ImportFromFileAsync(initialCsv, initialOptions);
        string targetTable = initialLogs[0].__dataTableName;

        // Update with -999.25 (null) for ROP
        string updateCsv = Path.Combine(Path.GetTempPath(), $"update_{Guid.NewGuid():N}.csv");
        File.WriteAllText(updateCsv,
            "DEPTH,ROP\n" +
            "200.0,-999.25\n");

        var updateOptions = new DepthLogImportOptions
        {
            OperationType = OperationType.UpdateData,
            LogName = "NullTest_Log",
            TargetTableName = targetTable,
            ColumnHeadingRow = 1,
            ImportFromRow = 2
        };

        await _depthLogService.ImportFromFileAsync(updateCsv, updateOptions);

        var conn = _session.GetConnection();
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"SELECT ROP, HKLD FROM [{targetTable}] WHERE DEPTH = 200.0;";
            using var rdr = cmd.ExecuteReader();
            Assert.True(rdr.Read());
            Assert.True(rdr.IsDBNull(0)); // ROP set to DBNull
            Assert.Equal(150.0, Convert.ToDouble(rdr["HKLD"])); // HKLD completely preserved
        }

        File.Delete(initialCsv);
        File.Delete(updateCsv);
    }
}
