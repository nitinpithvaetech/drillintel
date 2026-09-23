using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DrillIntel.Data;
using DrillIntel.Models;
using DrillIntel.Projects;
using DrillIntel.Services;
using DrillIntel.ViewModels;
using Xunit;

namespace DrillIntel.Tests;

public class DepthLogImportRulesTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ProjectSession _session;
    private readonly WellDataRepository _repo;
    private readonly ImportDepthLogService _depthLogService;

    public DepthLogImportRulesTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"drillintel_rules_{Guid.NewGuid():N}.dintel");
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
    public void DateTimeSettings_ShowDateTimeSettings_TrueForTimeLog_FalseForDepthLog()
    {
        var vm = new ImportDataViewModel(_session);

        // Default is TimeLogData
        vm.TypeOfDataInput = ImportDataType.TimeLogData;
        Assert.True(vm.ShowDateTimeSettings);
        Assert.True(vm.IsTimeLog);
        Assert.False(vm.IsDepthLog);

        // When switching to DepthLogData
        vm.TypeOfDataInput = ImportDataType.DepthLogData;
        Assert.False(vm.ShowDateTimeSettings);
        Assert.False(vm.IsTimeLog);
        Assert.True(vm.IsDepthLog);

        // Default row numbers are populated as 1 and 2 so file headers and preview immediately show
        Assert.Equal(1, vm.ColumnHeadingRow);
        Assert.Equal(2, vm.ImportFromRow);
    }

    [Fact]
    public void DepthLog_PopulateDefaultRows_WhenSelectingDepthLog()
    {
        var vm = new ImportDataViewModel(_session);
        vm.TypeOfDataInput = ImportDataType.DepthLogData;

        Assert.Equal(1, vm.ColumnHeadingRow);
        Assert.Equal(2, vm.ImportFromRow);
    }

    [Fact]
    public void ImportFromFile_ThrowsException_WhenColumnHeadingRowNotSpecified()
    {
        string csvPath = Path.Combine(Path.GetTempPath(), $"rules_test_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath, "DEPTH,GR\n100.0,45.0\n101.0,46.0\n");

            var options = new DepthLogImportOptions
            {
                LogName = "TestDepthLog",
                WellName = "TestWell",
                // ColumnHeadingRow not set (null)
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string> { ["DEPTH"] = "DEPTH" }
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                _depthLogService.ImportFromFile(csvPath, options));

            Assert.Contains("Column Heading Row is mandatory and must be specified to import DepthLog.", ex.Message);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public void ImportFromFile_ThrowsException_WhenColumnHeadingRowIsZeroOrNegative()
    {
        string csvPath = Path.Combine(Path.GetTempPath(), $"rules_test_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath, "DEPTH,GR\n100.0,45.0\n101.0,46.0\n");

            var options = new DepthLogImportOptions
            {
                LogName = "TestDepthLog",
                WellName = "TestWell",
                ColumnHeadingRow = 0, // Invalid <= 0
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string> { ["DEPTH"] = "DEPTH" }
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                _depthLogService.ImportFromFile(csvPath, options));

            Assert.Contains("Column Heading Row is mandatory and must be specified to import DepthLog.", ex.Message);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public void ImportFromFile_ThrowsException_WhenImportFromRowNotSpecified()
    {
        string csvPath = Path.Combine(Path.GetTempPath(), $"rules_test_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath, "DEPTH,GR\n100.0,45.0\n101.0,46.0\n");

            var options = new DepthLogImportOptions
            {
                LogName = "TestDepthLog",
                WellName = "TestWell",
                ColumnHeadingRow = 1,
                // ImportFromRow not set (null)
                ColumnMappings = new Dictionary<string, string> { ["DEPTH"] = "DEPTH" }
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                _depthLogService.ImportFromFile(csvPath, options));

            Assert.Contains("Import from Row is mandatory and must be specified to import DepthLog.", ex.Message);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public void ImportFromFile_ThrowsException_WhenImportFromRowIsZeroOrNegative()
    {
        string csvPath = Path.Combine(Path.GetTempPath(), $"rules_test_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath, "DEPTH,GR\n100.0,45.0\n101.0,46.0\n");

            var options = new DepthLogImportOptions
            {
                LogName = "TestDepthLog",
                WellName = "TestWell",
                ColumnHeadingRow = 1,
                ImportFromRow = -1, // Invalid <= 0
                ColumnMappings = new Dictionary<string, string> { ["DEPTH"] = "DEPTH" }
            };

            var ex = Assert.Throws<InvalidOperationException>(() =>
                _depthLogService.ImportFromFile(csvPath, options));

            Assert.Contains("Import from Row is mandatory and must be specified to import DepthLog.", ex.Message);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task ImportFromFileAsync_ThrowsException_WhenMandatoryRowsMissing()
    {
        string csvPath = Path.Combine(Path.GetTempPath(), $"rules_test_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath, "DEPTH,GR\n100.0,45.0\n101.0,46.0\n");

            var optionsWithoutHeading = new DepthLogImportOptions
            {
                LogName = "TestDepthLog",
                WellName = "TestWell",
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string> { ["DEPTH"] = "DEPTH" }
            };

            var ex1 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _depthLogService.ImportFromFileAsync(csvPath, optionsWithoutHeading));
            Assert.Contains("Column Heading Row is mandatory and must be specified to import DepthLog.", ex1.Message);

            var optionsWithoutImport = new DepthLogImportOptions
            {
                LogName = "TestDepthLog",
                WellName = "TestWell",
                ColumnHeadingRow = 1,
                ColumnMappings = new Dictionary<string, string> { ["DEPTH"] = "DEPTH" }
            };

            var ex2 = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _depthLogService.ImportFromFileAsync(csvPath, optionsWithoutImport));
            Assert.Contains("Import from Row is mandatory and must be specified to import DepthLog.", ex2.Message);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task DepthLogImport_Succeeds_WhenBothRowsProvided()
    {
        string csvPath = Path.Combine(Path.GetTempPath(), $"rules_test_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath, "DEPTH,GR\n100.0,45.0\n101.0,46.0\n");

            var options = new DepthLogImportOptions
            {
                LogName = "ValidDepthLog",
                WellName = "Well_Valid_Rows",
                ColumnHeadingRow = 1,
                ImportFromRow = 2,
                ColumnMappings = new Dictionary<string, string> { ["DEPTH"] = "DEPTH", ["GR"] = "GR" }
            };

            var logs = await _depthLogService.ImportFromFileAsync(csvPath, options);
            Assert.Single(logs);
            Assert.Equal("ValidDepthLog", logs[0].nameLog);

            var cols = await _repo.GetTableColumnsAsync(logs[0].__dataTableName);
            Assert.Contains("DEPTH", cols, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("GR", cols, StringComparer.OrdinalIgnoreCase);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task StreamImportDataAsync_Throws_WhenRowsAreInvalid()
    {
        var mappings = new List<ChannelMapping>
        {
            new ChannelMapping { CsvColumnHeader = "DEPTH", MappedVumaxChannel = "DEPTH" }
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _repo.StreamImportDataAsync("test_tbl", "dummy.csv", mappings, 0, 2));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _repo.StreamImportDataAsync("test_tbl", "dummy.csv", mappings, 1, 0));
    }

    [Fact]
    public async Task StreamUpdateDepthDataAsync_Throws_WhenRowsAreInvalid()
    {
        var mappings = new List<ChannelMapping>
        {
            new ChannelMapping { CsvColumnHeader = "DEPTH", MappedVumaxChannel = "DEPTH" }
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _repo.StreamUpdateDepthDataAsync("test_tbl", "dummy.csv", mappings, 0, 2));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _repo.StreamUpdateDepthDataAsync("test_tbl", "dummy.csv", mappings, 1, -1));
    }

    [Fact]
    public async Task ProcessFile_PopulatesDropdownAndPreviewData_ForDepthLog()
    {
        string csvPath = Path.Combine(Path.GetTempPath(), $"preview_test_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath, "DEPTH,GR,ROP\n100.0,45.0,15.2\n101.0,46.0,16.1\n");

            var vm = new ImportDataViewModel(_session)
            {
                TypeOfDataInput = ImportDataType.DepthLogData
            };

            // Call ProcessFile via DropFileCommand
            vm.DropFileCommand.Execute(csvPath);

            // Wait for async RefreshPreviewAsync to complete
            int attempts = 0;
            while (vm.IsLoading && attempts++ < 50)
            {
                await Task.Delay(50);
            }

            // Verify preview columns and rows
            Assert.NotEmpty(vm.PreviewColumns);
            Assert.Contains("DEPTH", vm.PreviewColumns);
            Assert.Contains("GR", vm.PreviewColumns);
            Assert.Contains("ROP", vm.PreviewColumns);

            Assert.NotNull(vm.PreviewRows);
            Assert.Equal(2, vm.PreviewRows.Rows.Count);

            // Verify mapping dropdown options
            Assert.NotEmpty(vm.ColumnMappings);
            var depthMapping = vm.ColumnMappings.First(m => m.VuMaxColumnID == "DEPTH");
            Assert.Contains("DEPTH", depthMapping.AvailableSourceColumns);
            Assert.Contains("GR", depthMapping.AvailableSourceColumns);
            Assert.Contains("ROP", depthMapping.AvailableSourceColumns);
            Assert.Equal("DEPTH", depthMapping.SourceColumnName);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }
}

