using System;
using System.Collections.Generic;
using System.IO;
using DrillIntel.Models;
using DrillIntel.Projects;
using DrillIntel.Services.Readers;
using DrillIntel.ViewModels;
using Xunit;

namespace DrillIntel.Tests;

public class TimeLogMappingFileTests : IDisposable
{
    private readonly string _tempDbPath;
    private readonly ProjectSession _session;

    public TimeLogMappingFileTests()
    {
        _tempDbPath = Path.Combine(Path.GetTempPath(), $"drillintel_test_{Guid.NewGuid():N}.dintel");
        SchemaInitializer.CreateDatabase(_tempDbPath);

        _session = new ProjectSession();
        _session.Load(_tempDbPath);
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
    public void GenerateMappingFileEntries_FormatsMappedChannelsWithZeroBasedColumnIndices()
    {
        var vm = new ImportDataViewModel(_session)
        {
            TypeOfDataInput = ImportDataType.TimeLogData,
            SuppressMessageBoxes = true
        };

        // Populate PreviewColumns simulating grid columns 0 to 27
        vm.PreviewColumns.Clear();
        for (int i = 0; i <= 30; i++)
        {
            vm.PreviewColumns.Add($"Col_{i}");
        }

        // Set specific headers at matching column indices from user example
        vm.PreviewColumns[2] = "Date_Time_Col";
        vm.PreviewColumns[3] = "Measured_Depth";
        vm.PreviewColumns[7] = "Hole_Depth";
        vm.PreviewColumns[8] = "Hook_Load";
        vm.PreviewColumns[23] = "Rate_Of_Penetration";
        vm.PreviewColumns[25] = "Rotary_Speed";
        vm.PreviewColumns[27] = "Surface_WOB";

        // Setup ColumnMappings in the grid
        vm.ColumnMappings.Clear();
        vm.ColumnMappings.Add(new ColumnMappingRow { VuMaxColumnID = "DATETIME", SourceColumnName = "Date_Time_Col" });
        vm.ColumnMappings.Add(new ColumnMappingRow { VuMaxColumnID = "DEPTH", SourceColumnName = "Measured_Depth" });
        vm.ColumnMappings.Add(new ColumnMappingRow { VuMaxColumnID = "HDTH", SourceColumnName = "Hole_Depth" });
        vm.ColumnMappings.Add(new ColumnMappingRow { VuMaxColumnID = "HKLD", SourceColumnName = "Hook_Load" });
        vm.ColumnMappings.Add(new ColumnMappingRow { VuMaxColumnID = "ROP", SourceColumnName = "Rate_Of_Penetration" });
        vm.ColumnMappings.Add(new ColumnMappingRow { VuMaxColumnID = "RPM", SourceColumnName = "Rotary_Speed" });
        vm.ColumnMappings.Add(new ColumnMappingRow { VuMaxColumnID = "SWOB", SourceColumnName = "Surface_WOB" });
        vm.ColumnMappings.Add(new ColumnMappingRow { VuMaxColumnID = "UNMAPPED", SourceColumnName = "" }); // Should be ignored

        var entries = vm.GenerateMappingFileEntries();

        var expectedEntries = new List<string>
        {
            "DATETIME~2",
            "DEPTH~3",
            "HDTH~7",
            "HKLD~8",
            "ROP~23",
            "RPM~25",
            "SWOB~27"
        };

        Assert.Equal(expectedEntries, entries);
    }

    [Fact]
    public void SaveMappingFile_WritesEntriesLineByLine_AndCanBeParsedByMappingFileReader()
    {
        var vm = new ImportDataViewModel(_session)
        {
            TypeOfDataInput = ImportDataType.TimeLogData,
            SuppressMessageBoxes = true
        };

        var entries = new List<string>
        {
            "DATETIME~2",
            "DEPTH~3",
            "HDTH~7",
            "HKLD~8",
            "ROP~23",
            "RPM~25",
            "SWOB~27"
        };

        string tempVmfPath = Path.Combine(Path.GetTempPath(), $"test_mapping_{Guid.NewGuid():N}.vmf");

        try
        {
            vm.SaveMappingFile(tempVmfPath, entries);

            Assert.True(File.Exists(tempVmfPath));
            var lines = File.ReadAllLines(tempVmfPath);
            Assert.Equal(entries, lines);

            // Test that MappingFileReader can parse this generated .vmf file
            var reader = new MappingFileReader();
            var result = reader.LoadMappingFile(tempVmfPath);

            Assert.True(result.Success);
            Assert.Equal(2, result.TargetToColumnIndex["DATETIME"]);
            Assert.Equal(3, result.TargetToColumnIndex["DEPTH"]);
            Assert.Equal(7, result.TargetToColumnIndex["HDTH"]);
            Assert.Equal(8, result.TargetToColumnIndex["HKLD"]);
            Assert.Equal(23, result.TargetToColumnIndex["ROP"]);
            Assert.Equal(25, result.TargetToColumnIndex["RPM"]);
            Assert.Equal(27, result.TargetToColumnIndex["SWOB"]);
        }
        finally
        {
            if (File.Exists(tempVmfPath)) File.Delete(tempVmfPath);
        }
    }

    [Fact]
    public void CreateMappingFile_EndToEnd_WithMockSaveFileDialog_SavesVmfFile()
    {
        var vm = new ImportDataViewModel(_session)
        {
            TypeOfDataInput = ImportDataType.TimeLogData,
            SuppressMessageBoxes = true
        };

        vm.PreviewColumns.Add("TIME_COL");
        vm.PreviewColumns.Add("DEPTH_COL");
        vm.ColumnMappings.Add(new ColumnMappingRow { VuMaxColumnID = "DATETIME", SourceColumnName = "TIME_COL" });
        vm.ColumnMappings.Add(new ColumnMappingRow { VuMaxColumnID = "DEPTH", SourceColumnName = "DEPTH_COL" });

        string tempVmfPath = Path.Combine(Path.GetTempPath(), $"e2e_mapping_{Guid.NewGuid():N}.vmf");

        try
        {
            vm.ShowSaveFileDialogFunc = dlg =>
            {
                dlg.FileName = tempVmfPath;
                return true;
            };

            vm.CreateMappingFile();

            Assert.True(File.Exists(tempVmfPath));
            var lines = File.ReadAllLines(tempVmfPath);
            Assert.Equal(new[] { "DATETIME~0", "DEPTH~1" }, lines);
        }
        finally
        {
            if (File.Exists(tempVmfPath)) File.Delete(tempVmfPath);
        }
    }

    [Fact]
    public void CreateMappingFile_WhenNotTimeLog_DoesNotProceed()
    {
        var vm = new ImportDataViewModel(_session)
        {
            TypeOfDataInput = ImportDataType.DepthLogData,
            SuppressMessageBoxes = true
        };

        bool dialogShown = false;
        vm.ShowSaveFileDialogFunc = dlg =>
        {
            dialogShown = true;
            return true;
        };

        vm.CreateMappingFile();
        Assert.False(dialogShown);
    }

    [Fact]
    public void CreateMappingFile_WhenNoMappedChannels_DoesNotShowSaveDialog()
    {
        var vm = new ImportDataViewModel(_session)
        {
            TypeOfDataInput = ImportDataType.TimeLogData,
            SuppressMessageBoxes = true
        };

        // All unmapped
        vm.ColumnMappings.Add(new ColumnMappingRow { VuMaxColumnID = "DEPTH", SourceColumnName = "" });

        bool dialogShown = false;
        vm.ShowSaveFileDialogFunc = dlg =>
        {
            dialogShown = true;
            return true;
        };

        vm.CreateMappingFile();
        Assert.False(dialogShown);
    }

    [Fact]
    public void IsTimeLog_IsTrueForTimeLog_AndFalseForDepthLog()
    {
        var vm = new ImportDataViewModel(_session);
        vm.TypeOfDataInput = ImportDataType.TimeLogData;
        Assert.True(vm.IsTimeLog);

        vm.TypeOfDataInput = ImportDataType.DepthLogData;
        Assert.False(vm.IsTimeLog);
    }
}

