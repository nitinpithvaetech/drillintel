using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Dapper;
using DrillIntel.Data;
using DrillIntel.Data.Objects.DataObjects.Models;
using DrillIntel.Data.Objects.DataObjects.Services;
using DrillIntel.Models;
using DrillIntel.Projects;
using DrillIntel.Services;
using DrillIntel.ViewModels;
using Xunit;

namespace DrillIntel.Tests;

public class TimeLogImportRulesTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ProjectSession _session;
    private readonly WellDataRepository _repo;

    public TimeLogImportRulesTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"drillintel_timelog_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(_tempDbPath);

        _session = new ProjectSession();
        _session.Load(_tempDbPath);

        _repo = new WellDataRepository(_session);
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

    // =========================================================================
    // 1. TimeLogDateTimeParser Tests
    // =========================================================================

    [Fact]
    public void DateTimeParser_ParsesIsoFormat_Correctly()
    {
        var options = new TimeLogDateTimeOptions
        {
            DateFormat = DateFormatType.ISOFormat,
            SingleDateTimeColIdx = 0
        };

        var tokens = new[] { "2024-05-10 14:30:45", "100.5" };
        bool success = TimeLogDateTimeParser.TryParse(tokens, options, out string formatted);

        Assert.True(success);
        Assert.Equal("10-May-2024 14:30:45", formatted);
    }

    [Fact]
    public void DateTimeParser_ParsesBritishFormat_Correctly()
    {
        var options = new TimeLogDateTimeOptions
        {
            DateFormat = DateFormatType.DDMMYYYYFormat,
            SingleDateTimeColIdx = 0
        };

        var tokens = new[] { "10-05-2024 14:30:45", "100.5" };
        bool success = TimeLogDateTimeParser.TryParse(tokens, options, out string formatted);

        Assert.True(success);
        Assert.Equal("10-May-2024 14:30:45", formatted);
    }

    [Fact]
    public void DateTimeParser_ParsesAmericanFormat_Correctly()
    {
        var options = new TimeLogDateTimeOptions
        {
            DateFormat = DateFormatType.MMDDYYYYFormat,
            SingleDateTimeColIdx = 0
        };

        var tokens = new[] { "05-10-2024 14:30:45", "100.5" };
        bool success = TimeLogDateTimeParser.TryParse(tokens, options, out string formatted);

        Assert.True(success);
        Assert.Equal("10-May-2024 14:30:45", formatted);
    }

    [Fact]
    public void DateTimeParser_ParsesSeparateDateAndTimeColumns_Correctly()
    {
        var options = new TimeLogDateTimeOptions
        {
            IsDatetimeInSeperatorColumn = true,
            DateColNo = 1, // 1-based (index 0)
            TimeColNo = 2, // 1-based (index 1)
            DateFormat = DateFormatType.ISOFormat
        };

        var tokens = new[] { "2024-06-15", "08:15:30", "500.0" };
        bool success = TimeLogDateTimeParser.TryParse(tokens, options, out string formatted);

        Assert.True(success);
        Assert.Equal("15-Jun-2024 08:15:30", formatted);
    }

    [Fact]
    public void DateTimeParser_ParsesCustomSeparator_Correctly()
    {
        var options = new TimeLogDateTimeOptions
        {
            DatetimeSeparator = "#",
            SingleDateTimeColIdx = 0,
            DateFormat = DateFormatType.ISOFormat
        };

        var tokens = new[] { "14:30:45#2024-05-10", "100.5" };
        bool success = TimeLogDateTimeParser.TryParse(tokens, options, out string formatted);

        Assert.True(success);
        Assert.Equal("10-May-2024 14:30:45", formatted);
    }

    [Fact]
    public void DateTimeParser_ReturnsFalse_ForInvalidDate()
    {
        var options = new TimeLogDateTimeOptions
        {
            SingleDateTimeColIdx = 0,
            DateFormat = DateFormatType.ISOFormat
        };

        var tokens = new[] { "not-a-valid-date", "100.5" };
        bool success = TimeLogDateTimeParser.TryParse(tokens, options, out string formatted);

        Assert.False(success);
    }

    // =========================================================================
    // 2. StreamImportDataAsync (New TimeLog Import) Tests
    // =========================================================================

    [Fact]
    public async Task StreamImportDataAsync_TimeLog_SavesStandardDateTimeAndDynamicColumns()
    {
        string tableName = "timeLog_test_import";
        var conn = _session.GetConnection();
        await conn.ExecuteAsync($@"
            CREATE TABLE [{tableName}] (
                DATA_INDEX DECIMAL(8),
                [DATETIME] DATETIME NOT NULL,
                [DEPTH] DECIMAL(16,5),
                [HKLD] DECIMAL(16,5),
                [CUSTOM_FLOW] DECIMAL(16,5)
            );");

        string csvPath = Path.Combine(Path.GetTempPath(), $"timelog_test_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath,
                "DATE_TIME,DEPTH,HKLD,CUSTOM_FLOW\n" +
                "2024-01-01 10:00:00,1000.0,55.5,1200.0\n" +
                "2024-01-01 10:00:10,1000.5,56.0,1210.0\n" +
                "2024-01-01 10:00:20,1001.0,57.2,1220.0\n");

            var mappings = new List<ChannelMapping>
            {
                new ChannelMapping { CsvColumnHeader = "DATE_TIME", MappedVumaxChannel = "DATETIME" },
                new ChannelMapping { CsvColumnHeader = "DEPTH", MappedVumaxChannel = "DEPTH" },
                new ChannelMapping { CsvColumnHeader = "HKLD", MappedVumaxChannel = "HKLD" },
                new ChannelMapping { CsvColumnHeader = "CUSTOM_FLOW", MappedVumaxChannel = "CUSTOM_FLOW" }
            };

            var options = new TimeLogDateTimeOptions
            {
                DateFormat = DateFormatType.ISOFormat
            };

            var result = await _repo.StreamImportDataAsync(
                tableName,
                csvPath,
                mappings,
                columnHeadingRow: 1,
                importFromRow: 2,
                delimiter: ",",
                worksheetName: null,
                progress: null,
                cancellationToken: default,
                dateTimeOptions: options);

            Assert.Equal(3, result.TotalRows);
            Assert.Equal(100.0, result.QcScore);
            Assert.Equal("01-Jan-2024 10:00:00", result.MinDate);
            Assert.Equal("01-Jan-2024 10:00:20", result.MaxDate);

            // Verify stored values in SQLite
            var rows = (await conn.QueryAsync<dynamic>($"SELECT DATETIME, DEPTH, HKLD, CUSTOM_FLOW FROM [{tableName}];")).AsList();
            Assert.Equal(3, rows.Count);
            Assert.Equal("01-Jan-2024 10:00:00", (string)rows[0].DATETIME);
            Assert.Equal(1000.0, (double)rows[0].DEPTH);
            Assert.Equal(55.5, (double)rows[0].HKLD);
            Assert.Equal(1200.0, (double)rows[0].CUSTOM_FLOW);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task StreamImportDataAsync_TimeLog_SplitDateTime_InsertsDataSuccessfully()
    {
        string tableName = "timeLog_test_split_datetime";
        var conn = _session.GetConnection();
        await conn.ExecuteAsync($@"
            CREATE TABLE [{tableName}] (
                DATA_INDEX DECIMAL(8),
                [DATETIME] DATETIME NOT NULL,
                [DEPTH] DECIMAL(16,5),
                [HKLD] DECIMAL(16,5)
            );");

        string csvPath = Path.Combine(Path.GetTempPath(), $"timelog_split_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath,
                "Date,Time,Depth,Hookload\n" +
                "2024-05-01,14:30:00,1500.0,75.0\n" +
                "2024-05-01,14:30:10,1500.5,75.5\n" +
                "2024-05-01,14:30:20,1501.0,76.0\n");

            // Notice mappings do NOT contain DATETIME (simulates split datetime where Date and Time are not mapped to DATETIME)
            var mappings = new List<ChannelMapping>
            {
                new ChannelMapping { CsvColumnHeader = "Depth", MappedVumaxChannel = "DEPTH" },
                new ChannelMapping { CsvColumnHeader = "Hookload", MappedVumaxChannel = "HKLD" }
            };

            var options = new TimeLogDateTimeOptions
            {
                IsDatetimeInSeperatorColumn = true,
                DateColNo = 0,
                TimeColNo = 1,
                DateFormat = DateFormatType.ISOFormat
            };

            var result = await _repo.StreamImportDataAsync(
                tableName,
                csvPath,
                mappings,
                columnHeadingRow: 1,
                importFromRow: 2,
                delimiter: ",",
                worksheetName: null,
                progress: null,
                cancellationToken: default,
                dateTimeOptions: options);

            Assert.Equal(3, result.TotalRows);
            Assert.Equal(100.0, result.QcScore);
            Assert.Equal("01-May-2024 14:30:00", result.MinDate);
            Assert.Equal("01-May-2024 14:30:20", result.MaxDate);

            // Verify stored values in SQLite
            var rows = (await conn.QueryAsync<dynamic>($"SELECT DATA_INDEX, DATETIME, DEPTH, HKLD FROM [{tableName}];")).AsList();
            Assert.Equal(3, rows.Count);
            Assert.Equal("01-May-2024 14:30:00", (string)rows[0].DATETIME);
            Assert.Equal(1500.0, (double)rows[0].DEPTH);
            Assert.Equal(75.0, (double)rows[0].HKLD);
            Assert.Equal("01-May-2024 14:30:20", (string)rows[2].DATETIME);
            Assert.Equal(1501.0, (double)rows[2].DEPTH);
            Assert.Equal(76.0, (double)rows[2].HKLD);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task StreamImportDataAsync_TimeLog_SplitDateTime_BritishFormat_InsertsDataSuccessfully()
    {
        string tableName = "timeLog_test_split_british";
        var conn = _session.GetConnection();
        await conn.ExecuteAsync($@"
            CREATE TABLE [{tableName}] (
                DATA_INDEX DECIMAL(8),
                [DATETIME] DATETIME NOT NULL,
                [DEPTH] DECIMAL(16,5)
            );");

        string csvPath = Path.Combine(Path.GetTempPath(), $"timelog_split_british_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath,
                "Date,Time,Depth\n" +
                "25-12-2024,18:00:00,2000.0\n" +
                "25-12-2024,18:00:15,2000.2\n");

            var mappings = new List<ChannelMapping>
            {
                new ChannelMapping { CsvColumnHeader = "Depth", MappedVumaxChannel = "DEPTH" }
            };

            var options = new TimeLogDateTimeOptions
            {
                IsDatetimeInSeperatorColumn = true,
                DateColNo = 0,
                TimeColNo = 1,
                DateFormat = DateFormatType.DDMMYYYYFormat
            };

            var result = await _repo.StreamImportDataAsync(
                tableName,
                csvPath,
                mappings,
                columnHeadingRow: 1,
                importFromRow: 2,
                delimiter: ",",
                dateTimeOptions: options);

            Assert.Equal(2, result.TotalRows);
            Assert.Equal("25-Dec-2024 18:00:00", result.MinDate);
            Assert.Equal("25-Dec-2024 18:00:15", result.MaxDate);

            var rows = (await conn.QueryAsync<dynamic>($"SELECT DATETIME, DEPTH FROM [{tableName}];")).AsList();
            Assert.Equal(2, rows.Count);
            Assert.Equal("25-Dec-2024 18:00:00", (string)rows[0].DATETIME);
            Assert.Equal(2000.0, (double)rows[0].DEPTH);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task StreamImportDataAsync_TimeLog_CalculatesQcScore_Correctly()
    {
        string tableName = "timeLog_test_qc";
        var conn = _session.GetConnection();
        await conn.ExecuteAsync($@"
            CREATE TABLE [{tableName}] (
                DATA_INDEX DECIMAL(8),
                [DATETIME] DATETIME NOT NULL,
                [DEPTH] DECIMAL(16,5),
                [HKLD] DECIMAL(16,5)
            );");

        string csvPath = Path.Combine(Path.GetTempPath(), $"timelog_qc_{Guid.NewGuid():N}.csv");
        try
        {
            // 4 rows total: 2 valid rows, 1 row with negative hookload (-10), 1 row with invalid date
            File.WriteAllText(csvPath,
                "DATE_TIME,DEPTH,HKLD\n" +
                "2024-01-01 10:00:00,1000.0,50.0\n" +
                "2024-01-01 10:00:10,1000.5,-10.0\n" +
                "invalid-date,1001.0,52.0\n" +
                "2024-01-01 10:00:30,1001.5,53.0\n");

            var mappings = new List<ChannelMapping>
            {
                new ChannelMapping { CsvColumnHeader = "DATE_TIME", MappedVumaxChannel = "DATETIME" },
                new ChannelMapping { CsvColumnHeader = "DEPTH", MappedVumaxChannel = "DEPTH" },
                new ChannelMapping { CsvColumnHeader = "HKLD", MappedVumaxChannel = "HKLD" }
            };

            var options = new TimeLogDateTimeOptions
            {
                DateFormat = DateFormatType.ISOFormat
            };

            var result = await _repo.StreamImportDataAsync(
                tableName,
                csvPath,
                mappings,
                columnHeadingRow: 1,
                importFromRow: 2,
                delimiter: ",",
                worksheetName: null,
                progress: null,
                cancellationToken: default,
                dateTimeOptions: options);

            Assert.Equal(4, result.TotalRows);
            // 2 valid rows out of 4 total rows = 50%
            Assert.Equal(50.0, result.QcScore);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    // =========================================================================
    // 3. StreamUpdateTimeDataAsync (TimeLog Update) Tests
    // =========================================================================

    [Fact]
    public async Task StreamUpdateTimeDataAsync_UpsertsOnDateTime_AndRetainsUnmappedColumns()
    {
        string tableName = "timeLog_test_update";
        var conn = _session.GetConnection();

        // Target table has DATETIME, DEPTH, HKLD, and an unmapped RPM column
        await conn.ExecuteAsync($@"
            CREATE TABLE [{tableName}] (
                DATA_INDEX DECIMAL(8),
                [DATETIME] DATETIME NOT NULL,
                [DEPTH] DECIMAL(16,5),
                [HKLD] DECIMAL(16,5),
                [RPM] DECIMAL(16,5)
            );
            CREATE UNIQUE INDEX [{tableName}_PK] ON [{tableName}]([DATETIME]);");

        // Seed initial data
        await conn.ExecuteAsync($@"
            INSERT INTO [{tableName}] (DATA_INDEX, [DATETIME], [DEPTH], [HKLD], [RPM])
            VALUES (0, '01-Jan-2024 10:00:00', 1000.0, 50.0, 120.0);
            INSERT INTO [{tableName}] (DATA_INDEX, [DATETIME], [DEPTH], [HKLD], [RPM])
            VALUES (1, '01-Jan-2024 10:00:10', 1001.0, 52.0, 125.0);");

        string csvPath = Path.Combine(Path.GetTempPath(), $"timelog_update_{Guid.NewGuid():N}.csv");
        try
        {
            // Update CSV has new DEPTH and HKLD values for the same timestamps, and no RPM column
            File.WriteAllText(csvPath,
                "DATE_TIME,DEPTH,HKLD\n" +
                "2024-01-01 10:00:00,1005.0,75.0\n" +
                "2024-01-01 10:00:10,1006.0,80.0\n");

            var mappings = new List<ChannelMapping>
            {
                new ChannelMapping { CsvColumnHeader = "DATE_TIME", MappedVumaxChannel = "DATETIME" },
                new ChannelMapping { CsvColumnHeader = "DEPTH", MappedVumaxChannel = "DEPTH" },
                new ChannelMapping { CsvColumnHeader = "HKLD", MappedVumaxChannel = "HKLD" }
            };

            var options = new TimeLogDateTimeOptions
            {
                DateFormat = DateFormatType.ISOFormat
            };

            var result = await _repo.StreamUpdateTimeDataAsync(
                tableName,
                csvPath,
                mappings,
                columnHeadingRow: 1,
                importFromRow: 2,
                delimiter: ",",
                worksheetName: null,
                dateTimeOptions: options,
                updateMethod: UpdateMethodType.DateTimeComaparision);

            Assert.Equal(2, result.TotalRows);
            Assert.Equal(100.0, result.QcScore);

            // Verify in database:
            // 1. DEPTH and HKLD are updated to new values
            // 2. RPM retained its original values (120.0 and 125.0)
            var rows = (await conn.QueryAsync<dynamic>($"SELECT DATETIME, DEPTH, HKLD, RPM FROM [{tableName}] ORDER BY DATETIME;")).AsList();
            Assert.Equal(2, rows.Count);

            Assert.Equal("01-Jan-2024 10:00:00", (string)rows[0].DATETIME);
            Assert.Equal(1005.0, (double)rows[0].DEPTH);
            Assert.Equal(75.0, (double)rows[0].HKLD);
            Assert.Equal(120.0, (double)rows[0].RPM); // RETAINED!

            Assert.Equal("01-Jan-2024 10:00:10", (string)rows[1].DATETIME);
            Assert.Equal(1006.0, (double)rows[1].DEPTH);
            Assert.Equal(80.0, (double)rows[1].HKLD);
            Assert.Equal(125.0, (double)rows[1].RPM); // RETAINED!
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task StreamUpdateTimeDataAsync_Throws_WhenKeyChannelMissing()
    {
        string tableName = "timeLog_test_nokey";
        var conn = _session.GetConnection();
        await conn.ExecuteAsync($@"
            CREATE TABLE [{tableName}] (
                [DATETIME] DATETIME NOT NULL,
                [DEPTH] DECIMAL(16,5)
            );");

        string csvPath = Path.Combine(Path.GetTempPath(), $"timelog_nokey_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath, "DEPTH,RPM\n100.0,50.0\n");

            // Mappings do NOT include DATETIME
            var mappings = new List<ChannelMapping>
            {
                new ChannelMapping { CsvColumnHeader = "DEPTH", MappedVumaxChannel = "DEPTH" }
            };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                _repo.StreamUpdateTimeDataAsync(
                    tableName,
                    csvPath,
                    mappings,
                    columnHeadingRow: 1,
                    importFromRow: 2,
                    dateTimeOptions: null,
                    updateMethod: UpdateMethodType.DateTimeComaparision));

            Assert.Contains("You must map and select DATE/TIME channel", ex.Message);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task StreamUpdateTimeDataAsync_Throws_WhenRowsAreInvalid()
    {
        var mappings = new List<ChannelMapping>
        {
            new ChannelMapping { CsvColumnHeader = "DATETIME", MappedVumaxChannel = "DATETIME" }
        };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _repo.StreamUpdateTimeDataAsync("tbl", "dummy.csv", mappings, columnHeadingRow: 0, importFromRow: 2));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _repo.StreamUpdateTimeDataAsync("tbl", "dummy.csv", mappings, columnHeadingRow: 1, importFromRow: 0));
    }

    [Fact]
    public void ViewModel_TimeLogDefaults_AreProperlyConfigured()
    {
        var vm = new ImportDataViewModel(_session);
        vm.TypeOfDataInput = ImportDataType.TimeLogData;

        Assert.True(vm.ShowDateTimeSettings);
        Assert.True(vm.IsTimeLog);
        Assert.False(vm.IsDepthLog);
        Assert.Equal("Import Timelog Wizard", vm.WizardTitle);
        Assert.Equal("Timelog Name", vm.LogNameHint);
        Assert.Equal("Enter the name of the timelog.", vm.LogNameToolTip);
    }

    [Fact]
    public void ViewModel_TimeLog_MappingChannels_ContainVuMaxStandardChannels()
    {
        var channels = ImportDataViewModel.GetDefaultMappingChannels(ImportDataType.TimeLogData);

        var expectedMnemonics = new[] { "DEPTH", "HKLD", "RPM", "SPPA", "BPOS", "CIRC", "STOR", "HDTH" };
        foreach (var mnemonic in expectedMnemonics)
        {
            Assert.Contains(channels, c => c.Mnemonic.Equals(mnemonic, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void ViewModel_TimeLog_Subtitle_ReflectsUpdateVsNewMode()
    {
        var vm = new ImportDataViewModel(_session);
        vm.TypeOfDataInput = ImportDataType.TimeLogData;

        vm.OperationType = OperationType.NewData;
        Assert.Contains("auto-mapped standard VuMax channels", vm.MappingSubtitle);

        vm.OperationType = OperationType.UpdateData;
        Assert.Contains("Only the DATE/TIME channel mapping is required", vm.MappingSubtitle);
        Assert.Contains("Unmapped existing columns retain current database values", vm.MappingSubtitle);
    }

    [Fact]
    public async Task ViewModel_TimeLog_AutoDetectsSplitDateTime_AndGeneratesMergedDateTimePreview()
    {
        var vm = new ImportDataViewModel(_session);
        vm.TypeOfDataInput = ImportDataType.TimeLogData;

        string csvPath = Path.Combine(Path.GetTempPath(), $"timelog_vm_split_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath,
                "Date,Time,Depth,HKLD\n" +
                "2024-05-01,10:00:00,1000.0,50.0\n" +
                "2024-05-02,00:00:00,1000.5,52.0\n");

            vm.DropFileCommand.Execute(csvPath);
            int retries = 0;
            while ((vm.IsLoading || vm.PreviewRows.Rows.Count == 0) && retries++ < 100)
            {
                await Task.Delay(50);
            }

            // Verify auto-detection
            Assert.True(vm.IsDatetimeInSeperatorColumn);
            Assert.Equal(0, vm.DateColNo);
            Assert.Equal(1, vm.TimeColNo);
            Assert.Equal(DateFormatType.ISOFormat, vm.DateFormat);

            // Verify preview columns and rows
            Assert.Contains("DATETIME", vm.PreviewColumns);
            Assert.Equal("DATETIME", vm.PreviewColumns[0]);
            Assert.NotNull(vm.PreviewRows);
            Assert.Equal(2, vm.PreviewRows.Rows.Count);
            Assert.True(vm.PreviewRows.Columns.Contains("DATETIME"));

            // Verify merged values for row 1 and row 2
            Assert.Equal("01-May-2024 10:00:00", vm.PreviewRows.Rows[0]["DATETIME"]);
            Assert.Equal("02-May-2024 00:00:00", vm.PreviewRows.Rows[1]["DATETIME"]);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task TimeLog_EndToEnd_SplitDateTimeImport_CreatesSingleTableWithData()
    {
        var dataService = _session.GetDataService();
        var conn = _session.GetConnection();

        // 1. Setup TimeLog domain object with DATETIME curve and DEPTH curve
        var timeLog = new TimeLog
        {
            ObjectID = Guid.NewGuid().ToString(),
            nameLog = "TestSplitImport",
            nameWell = "Well A",
            __WellName = "Well A",
            creationDate = DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss"),
            DontCalcHoleDepth = true
        };

        timeLog.logCurves["DATETIME"] = new LogChannel
        {
            mnemonic = "DATETIME",
            curveDescription = "Date and Time",
            typeLogData = "DateTime",
            ColumnOrder = 0,
            witsmlMnemonic = "DATETIME"
        };
        timeLog.logCurves["DEPTH"] = new LogChannel
        {
            mnemonic = "DEPTH",
            curveDescription = "Depth",
            typeLogData = "Double",
            ColumnOrder = 1,
            witsmlMnemonic = "DEPTH"
        };

        string lastError = string.Empty;
        bool addSuccess = TimeLogService.addTimeLog(dataService, timeLog, ref lastError);
        Assert.True(addSuccess);
        Assert.False(string.IsNullOrWhiteSpace(timeLog.__dataTableName));

        string tableName = timeLog.__dataTableName;

        // 2. Prepare CSV with split Date and Time
        string csvPath = Path.Combine(Path.GetTempPath(), $"timelog_e2e_split_{Guid.NewGuid():N}.csv");
        try
        {
            File.WriteAllText(csvPath,
                "Date,Time,Depth\n" +
                "2024-03-15,09:00:00,1200.0\n" +
                "2024-03-15,09:00:10,1200.5\n" +
                "2024-03-15,09:00:20,1201.0\n");

            var mappings = new List<ChannelMapping>
            {
                new ChannelMapping { CsvColumnHeader = "Depth", MappedVumaxChannel = "DEPTH" }
            };

            var options = new TimeLogDateTimeOptions
            {
                IsDatetimeInSeperatorColumn = true,
                DateColNo = 0,
                TimeColNo = 1,
                DateFormat = DateFormatType.ISOFormat
            };

            // 3. Execute streaming import
            var result = await _repo.StreamImportDataAsync(
                tableName,
                csvPath,
                mappings,
                columnHeadingRow: 1,
                importFromRow: 2,
                dateTimeOptions: options);

            Assert.Equal(3, result.TotalRows);
            Assert.Equal(100.0, result.QcScore);

            // 4. Update TimeLog metadata
            timeLog.startIndex = result.MinDate ?? "";
            timeLog.endIndex = result.MaxDate ?? "";
            await _repo.LogTimeLogAsync(timeLog);

            // 5. Verify exactly 1 table with prefix "timeLog" was created
            var tableCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name LIKE 'timeLog%';");
            Assert.Equal(1, tableCount);

            // 6. Verify data rows exist and are NOT empty
            var rowCount = await conn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM [{tableName}];");
            Assert.Equal(3, rowCount);

            var firstRow = await conn.QueryFirstAsync<dynamic>($"SELECT DATETIME, DEPTH FROM [{tableName}] ORDER BY DATA_INDEX ASC;");
            Assert.Equal("15-Mar-2024 09:00:00", (string)firstRow.DATETIME);
            Assert.Equal(1200.0, (double)firstRow.DEPTH);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    // =========================================================================
    // 3. User Requirements Tests (Multi-Day, Uniqueness, Sequentiality, Excel)
    // =========================================================================

    [Fact]
    public async Task TimeLog_MultiDay_PreservesUniqueRowDates_DoesNotRepeatSameDateAcrossRows()
    {
        string tableName = "timeLog_test_multiday";
        var conn = _session.GetConnection();
        await conn.ExecuteAsync($@"
            CREATE TABLE [{tableName}] (
                DATA_INDEX DECIMAL(8),
                [DATETIME] DATETIME NOT NULL,
                [DEPTH] DECIMAL(16,5)
            );");

        string csvPath = Path.Combine(Path.GetTempPath(), $"timelog_multiday_{Guid.NewGuid():N}.csv");
        try
        {
            // Multi-day drilling log spanning 3 different calendar dates
            File.WriteAllText(csvPath,
                "Date,Time,Depth\n" +
                "2024-05-01,23:59:40,2000.0\n" +
                "2024-05-01,23:59:50,2000.5\n" +
                "2024-05-02,00:00:00,2001.0\n" +
                "2024-05-02,00:00:10,2001.5\n" +
                "2024-05-03,12:00:00,2005.0\n");

            var mappings = new List<ChannelMapping>
            {
                new ChannelMapping { CsvColumnHeader = "Depth", MappedVumaxChannel = "DEPTH" }
            };

            var options = new TimeLogDateTimeOptions
            {
                IsDatetimeInSeperatorColumn = true,
                DateColNo = 0,
                TimeColNo = 1,
                DateFormat = DateFormatType.ISOFormat
            };

            var result = await _repo.StreamImportDataAsync(
                tableName,
                csvPath,
                mappings,
                columnHeadingRow: 1,
                importFromRow: 2,
                dateTimeOptions: options);

            Assert.Equal(5, result.TotalRows);
            Assert.True(result.IsSequential);
            Assert.Equal(0, result.NonSequentialCount);
            Assert.Equal("01-May-2024 23:59:40", result.MinDate);
            Assert.Equal("03-May-2024 12:00:00", result.MaxDate);

            // Verify stored DateTime values across all 5 rows
            var rows = (await conn.QueryAsync<dynamic>($"SELECT DATA_INDEX, DATETIME, DEPTH FROM [{tableName}] ORDER BY DATA_INDEX ASC;")).AsList();
            Assert.Equal(5, rows.Count);

            // Row 1 & 2: 01-May-2024
            Assert.Equal("01-May-2024 23:59:40", (string)rows[0].DATETIME);
            Assert.Equal("01-May-2024 23:59:50", (string)rows[1].DATETIME);

            // Row 3 & 4: 02-May-2024 (Date must advance to May 2nd, NOT repeat May 1st!)
            Assert.Equal("02-May-2024 00:00:00", (string)rows[2].DATETIME);
            Assert.Equal("02-May-2024 00:00:10", (string)rows[3].DATETIME);

            // Row 5: 03-May-2024 (Date must advance to May 3rd, NOT repeat May 1st!)
            Assert.Equal("03-May-2024 12:00:00", (string)rows[4].DATETIME);

            // Ensure distinct dates were preserved (not collapsed to single date)
            var distinctDates = new HashSet<string>();
            foreach (var r in rows)
            {
                string dtStr = (string)r.DATETIME;
                distinctDates.Add(dtStr.Substring(0, 11)); // e.g. "01-May-2024"
            }
            Assert.Equal(3, distinctDates.Count);
            Assert.Contains("01-May-2024", distinctDates);
            Assert.Contains("02-May-2024", distinctDates);
            Assert.Contains("03-May-2024", distinctDates);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task TimeLog_DuplicateDatesWithVaryingTimes_GeneratesUniqueAndAccurateDateTimes()
    {
        string tableName = "timeLog_test_dup_dates";
        var conn = _session.GetConnection();
        await conn.ExecuteAsync($@"
            CREATE TABLE [{tableName}] (
                DATA_INDEX DECIMAL(8),
                [DATETIME] DATETIME NOT NULL,
                [DEPTH] DECIMAL(16,5)
            );");

        string csvPath = Path.Combine(Path.GetTempPath(), $"timelog_dup_dates_{Guid.NewGuid():N}.csv");
        try
        {
            // Same date with varying times (standard high-frequency drilling data)
            File.WriteAllText(csvPath,
                "Date,Time,Depth\n" +
                "2024-07-20,08:00:00,3000.0\n" +
                "2024-07-20,08:00:05,3000.2\n" +
                "2024-07-20,08:00:10,3000.4\n" +
                "2024-07-20,08:00:15,3000.6\n" +
                "2024-07-20,08:00:20,3000.8\n");

            var mappings = new List<ChannelMapping>
            {
                new ChannelMapping { CsvColumnHeader = "Depth", MappedVumaxChannel = "DEPTH" }
            };

            var options = new TimeLogDateTimeOptions
            {
                IsDatetimeInSeperatorColumn = true,
                DateColNo = 0,
                TimeColNo = 1,
                DateFormat = DateFormatType.ISOFormat
            };

            var result = await _repo.StreamImportDataAsync(
                tableName,
                csvPath,
                mappings,
                columnHeadingRow: 1,
                importFromRow: 2,
                dateTimeOptions: options);

            Assert.Equal(5, result.TotalRows);
            Assert.True(result.IsSequential);
            Assert.Equal(0, result.NonSequentialCount);

            var rows = (await conn.QueryAsync<dynamic>($"SELECT DATETIME FROM [{tableName}] ORDER BY DATA_INDEX ASC;")).AsList();
            Assert.Equal(5, rows.Count);

            // Each row must have a unique DateTime
            var distinctDateTimes = new HashSet<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                string dtStr = (string)rows[i].DATETIME;
                Assert.True(distinctDateTimes.Add(dtStr), $"Duplicate DateTime found at row {i}: {dtStr}");
            }
            Assert.Equal(5, distinctDateTimes.Count);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public async Task TimeLog_NonSequentialTimestamps_FlagsWarningAndRecordsValidationMessages()
    {
        string tableName = "timeLog_test_nonseq";
        var conn = _session.GetConnection();
        await conn.ExecuteAsync($@"
            CREATE TABLE [{tableName}] (
                DATA_INDEX DECIMAL(8),
                [DATETIME] DATETIME NOT NULL,
                [DEPTH] DECIMAL(16,5)
            );");

        string csvPath = Path.Combine(Path.GetTempPath(), $"timelog_nonseq_{Guid.NewGuid():N}.csv");
        try
        {
            // Row 3 jumps backwards in time
            File.WriteAllText(csvPath,
                "Date,Time,Depth\n" +
                "2024-05-10,14:00:00,1000.0\n" +
                "2024-05-10,14:05:00,1000.5\n" +
                "2024-05-10,14:02:00,1001.0\n" + // Earlier than 14:05:00!
                "2024-05-10,14:10:00,1001.5\n");

            var mappings = new List<ChannelMapping>
            {
                new ChannelMapping { CsvColumnHeader = "Depth", MappedVumaxChannel = "DEPTH" }
            };

            var options = new TimeLogDateTimeOptions
            {
                IsDatetimeInSeperatorColumn = true,
                DateColNo = 0,
                TimeColNo = 1,
                DateFormat = DateFormatType.ISOFormat
            };

            var result = await _repo.StreamImportDataAsync(
                tableName,
                csvPath,
                mappings,
                columnHeadingRow: 1,
                importFromRow: 2,
                dateTimeOptions: options);

            Assert.Equal(4, result.TotalRows);
            Assert.False(result.IsSequential);
            Assert.Equal(1, result.NonSequentialCount);
            Assert.NotEmpty(result.ValidationMessages);
            Assert.Contains("earlier than previous timestamp", result.ValidationMessages[0]);
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }

    [Fact]
    public void TimeLog_ExcelFormattingQuirks_StripsEmbeddedTimeAndBaseDatesCleanly()
    {
        // 1. Date column with embedded 00:00:00, Time column with Excel base date 1899-12-30
        bool ok1 = TimeLogDateTimeParser.TryMergeRowTokens(
            "2024-08-15 00:00:00",
            "1899-12-30 15:45:30",
            DateFormatType.ISOFormat,
            out var merged1);

        Assert.True(ok1);
        Assert.Equal(new DateTime(2024, 8, 15, 15, 45, 30), merged1);
        Assert.Equal("15-Aug-2024 15:45:30", TimeLogDateTimeParser.FormatDateTime(merged1));

        // 2. Date column with British format and embedded 12:00:00 AM, 12-hr AM/PM Time column
        bool ok2 = TimeLogDateTimeParser.TryMergeRowTokens(
            "15/08/2024 12:00:00 AM",
            "03:45:30 PM",
            DateFormatType.DDMMYYYYFormat,
            out var merged2);

        Assert.True(ok2);
        Assert.Equal(new DateTime(2024, 8, 15, 15, 45, 30), merged2);
        Assert.Equal("15-Aug-2024 15:45:30", TimeLogDateTimeParser.FormatDateTime(merged2));

        // 3. Sub-second millisecond preservation
        bool ok3 = TimeLogDateTimeParser.TryMergeRowTokens(
            "2024-08-15",
            "15:45:30.250",
            DateFormatType.ISOFormat,
            out var merged3);

        Assert.True(ok3);
        Assert.Equal(250, merged3.Millisecond);
        Assert.Equal("15-Aug-2024 15:45:30.250", TimeLogDateTimeParser.FormatDateTime(merged3));
    }

    [Fact]
    public void TimeLog_AutoDetectSeparateColumns_IdentifiesHeadersAccurately()
    {
        // Headers with Date and Time
        var headers1 = new List<string> { "Date", "Time", "Depth", "Hookload" };
        bool detected1 = TimeLogDateTimeParser.TryDetectSeparateDateTimeColumns(headers1, out int dIdx1, out int tIdx1);
        Assert.True(detected1);
        Assert.Equal(0, dIdx1);
        Assert.Equal(1, tIdx1);

        // Headers with LOG_DATE and LOG_TIME
        var headers2 = new List<string> { "INDEX", "LOG_DATE", "LOG_TIME", "ROP" };
        bool detected2 = TimeLogDateTimeParser.TryDetectSeparateDateTimeColumns(headers2, out int dIdx2, out int tIdx2);
        Assert.True(detected2);
        Assert.Equal(1, dIdx2);
        Assert.Equal(2, tIdx2);

        // Combined column already present (DATETIME) -> should NOT detect as separate
        var headers3 = new List<string> { "DATETIME", "DEPTH", "RPM" };
        bool detected3 = TimeLogDateTimeParser.TryDetectSeparateDateTimeColumns(headers3, out _, out _);
        Assert.False(detected3);
    }

    [Fact]
    public void TimeLog_ValidateDateTimeSequence_ValidatesMultiDayAndDuplicateDates()
    {
        var dates = new List<DateTime>
        {
            new DateTime(2024, 5, 1, 10, 0, 0),
            new DateTime(2024, 5, 1, 10, 0, 10),
            new DateTime(2024, 5, 1, 10, 0, 20),
            new DateTime(2024, 5, 2, 8, 0, 0),
            new DateTime(2024, 5, 2, 8, 0, 10)
        };

        var result = TimeLogDateTimeParser.ValidateDateTimeSequence(dates);
        Assert.True(result.IsValid);
        Assert.True(result.IsSequential);
        Assert.Equal(0, result.NonSequentialCount);
        Assert.Equal(0, result.DuplicateTimestampCount);
        Assert.Equal(5, result.ValidRows);
        Assert.Equal(5, result.DuplicateDateVaryingTimeCount); // All 5 rows have sibling times on same date
    }

    [Fact]
    public async Task TimeLog_StreamUpdateTimeDataAsync_WithSeparateDateTime_UpsertsAccurately()
    {
        string tableName = "timeLog_test_update_split";
        var conn = _session.GetConnection();
        await conn.ExecuteAsync($@"
            CREATE TABLE [{tableName}] (
                DATA_INDEX DECIMAL(8),
                [DATETIME] DATETIME NOT NULL,
                [DEPTH] DECIMAL(16,5),
                [HKLD] DECIMAL(16,5)
            );");

        // Seed 2 existing rows
        await conn.ExecuteAsync($@"
            INSERT INTO [{tableName}] (DATA_INDEX, DATETIME, DEPTH, HKLD) VALUES
            (1, '01-May-2024 10:00:00', 1000.0, 50.0),
            (2, '01-May-2024 10:00:10', 1000.5, 52.0);");

        string csvPath = Path.Combine(Path.GetTempPath(), $"timelog_update_split_{Guid.NewGuid():N}.csv");
        try
        {
            // CSV with split Date & Time: row 1 updates existing row (HKLD changes from 50 -> 60), row 2 is a new timestamp
            File.WriteAllText(csvPath,
                "Date,Time,Depth,HKLD\n" +
                "2024-05-01,10:00:00,1000.0,60.0\n" +
                "2024-05-01,10:00:20,1001.0,55.0\n");

            var mappings = new List<ChannelMapping>
            {
                new ChannelMapping { CsvColumnHeader = "Depth", MappedVumaxChannel = "DEPTH" },
                new ChannelMapping { CsvColumnHeader = "HKLD", MappedVumaxChannel = "HKLD" }
            };

            var options = new TimeLogDateTimeOptions
            {
                IsDatetimeInSeperatorColumn = true,
                DateColNo = 0,
                TimeColNo = 1,
                DateFormat = DateFormatType.ISOFormat
            };

            var result = await _repo.StreamUpdateTimeDataAsync(
                tableName,
                csvPath,
                mappings,
                columnHeadingRow: 1,
                importFromRow: 2,
                delimiter: ",",
                worksheetName: null,
                dateTimeOptions: options,
                updateMethod: UpdateMethodType.DateTimeComaparision);

            Assert.Equal(2, result.TotalRows);
            Assert.True(result.IsSequential);

            // Verify SQLite table state: Row 1 updated to HKLD = 60, Row 2 unaffected (HKLD = 52), Row 3 inserted
            var rows = (await conn.QueryAsync<dynamic>($"SELECT DATETIME, DEPTH, HKLD FROM [{tableName}] ORDER BY DATETIME ASC;")).AsList();
            Assert.Equal(3, rows.Count);

            var row1 = rows.First(r => (string)r.DATETIME == "01-May-2024 10:00:00");
            Assert.Equal(60.0, (double)row1.HKLD); // Updated!

            var row2 = rows.First(r => (string)r.DATETIME == "01-May-2024 10:00:10");
            Assert.Equal(52.0, (double)row2.HKLD); // Preserved!

            var row3 = rows.First(r => (string)r.DATETIME == "01-May-2024 10:00:20");
            Assert.Equal(55.0, (double)row3.HKLD); // Inserted!
        }
        finally
        {
            if (File.Exists(csvPath)) File.Delete(csvPath);
        }
    }
}


