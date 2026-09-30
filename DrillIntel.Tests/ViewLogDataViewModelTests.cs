using System;
using System.Data;
using DrillIntel.ViewModels;
using Xunit;

namespace DrillIntel.Tests;

public class ViewLogDataViewModelTests
{
    private static DataTable CreateSampleDataTable(int rowCount)
    {
        var table = new DataTable("TEST_LOG");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("DEPTH", typeof(double));
        table.Columns.Add("WOB", typeof(double));
        table.Columns.Add("ROP", typeof(double));
        table.Columns.Add("FORMATION", typeof(string));

        for (int i = 1; i <= rowCount; i++)
        {
            table.Rows.Add(i, 1000.0 + i * 0.5, 20.0 + (i % 10), 50.0 + (i % 5), $"Formation_{(i % 3) + 1}");
        }

        return table;
    }

    [Fact]
    public void Constructor_InitializesCorrectly_With1000Rows()
    {
        var table = CreateSampleDataTable(1000);

        var vm = new ViewLogDataViewModel(
            table,
            logType: "DepthLog",
            logName: "TestLog_01",
            tableName: "DEPTH_LOG_TEST",
            wellName: "Well-A");

        Assert.Equal(1000, vm.RecordCount);
        Assert.Equal(5, vm.ColumnCount);
        Assert.Equal("DepthLog Data — TestLog_01", vm.Title);
        Assert.Contains("First 1,000 records • 5 channels", vm.RecordCountText);
        Assert.NotNull(vm.DataView);
        Assert.Equal(1000, vm.DataView!.Count);
    }

    [Fact]
    public void Constructor_InitializesCorrectly_WithFewerRows()
    {
        var table = CreateSampleDataTable(350);

        var vm = new ViewLogDataViewModel(
            table,
            logType: "TimeLog",
            logName: "TimeLog_01",
            tableName: "TIME_LOG_TEST",
            wellName: "Well-B");

        Assert.Equal(350, vm.RecordCount);
        Assert.Equal(5, vm.ColumnCount);
        Assert.Contains("350 records • 5 channels", vm.RecordCountText);
    }

    [Fact]
    public void SearchText_FiltersDataView()
    {
        var table = CreateSampleDataTable(1000);

        var vm = new ViewLogDataViewModel(
            table,
            logType: "DepthLog",
            logName: "TestLog_01",
            tableName: "DEPTH_LOG_TEST",
            wellName: "Well-A");

        vm.SearchText = "Formation_2";

        Assert.NotNull(vm.DataView);
        Assert.True(vm.DataView!.Count < 1000);
        Assert.True(vm.DataView.Count > 0);
        Assert.Contains("of 1,000 records", vm.RecordCountText);

        // Clear search
        vm.SearchText = string.Empty;
        Assert.Equal(1000, vm.DataView.Count);
        Assert.Contains("First 1,000 records • 5 channels", vm.RecordCountText);
    }

    [Fact]
    public void CloseCommand_RaisesRequestClose()
    {
        var table = CreateSampleDataTable(10);

        var vm = new ViewLogDataViewModel(
            table,
            logType: "DepthLog",
            logName: "TestLog_01",
            tableName: "DEPTH_LOG_TEST",
            wellName: "Well-A");

        bool closeCalled = false;
        vm.RequestClose += () => closeCalled = true;

        vm.CloseCommand.Execute(null);

        Assert.True(closeCalled);
    }
}

