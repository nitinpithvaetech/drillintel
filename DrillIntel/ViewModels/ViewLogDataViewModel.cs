using System;
using System.Collections.Generic;
using System.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DrillIntel.ViewModels;

public partial class ViewLogDataViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Log Data Viewer";

    [ObservableProperty]
    private string _logType = "DepthLog";

    [ObservableProperty]
    private string _logName = string.Empty;

    [ObservableProperty]
    private string _tableName = string.Empty;

    [ObservableProperty]
    private string _wellName = string.Empty;

    [ObservableProperty]
    private string _recordCountText = "0 records";

    [ObservableProperty]
    private int _recordCount;

    [ObservableProperty]
    private int _columnCount;

    [ObservableProperty]
    private DataView? _dataView;

    [ObservableProperty]
    private string _searchText = string.Empty;

    private readonly DataTable? _rawTable;

    public event Action? RequestClose;

    public ViewLogDataViewModel(DataTable table, string logType, string logName, string tableName, string wellName)
    {
        _rawTable = table ?? new DataTable();
        LogType = logType;
        LogName = logName;
        TableName = tableName;
        WellName = wellName;
        Title = $"{logType} Data — {logName}";
        RecordCount = _rawTable.Rows.Count;
        ColumnCount = _rawTable.Columns.Count;
        RecordCountText = RecordCount >= 1000
            ? $"First {RecordCount:N0} records • {ColumnCount} channels"
            : $"{RecordCount:N0} records • {ColumnCount} channels";
        DataView = _rawTable.DefaultView;
    }

    [RelayCommand]
    private void Close()
    {
        RequestClose?.Invoke();
    }

    partial void OnSearchTextChanged(string value)
    {
        if (_rawTable == null || _rawTable.Columns.Count == 0 || DataView == null) return;
        try
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                DataView.RowFilter = string.Empty;
                RecordCountText = RecordCount >= 1000
                    ? $"First {RecordCount:N0} records • {ColumnCount} channels"
                    : $"{RecordCount:N0} records • {ColumnCount} channels";
            }
            else
            {
                var filterParts = new List<string>();
                string escaped = value.Trim().Replace("'", "''");
                foreach (DataColumn col in _rawTable.Columns)
                {
                    if (col.DataType == typeof(string))
                    {
                        filterParts.Add($"[{col.ColumnName}] LIKE '%{escaped}%'");
                    }
                    else if (double.TryParse(value.Trim(), out _))
                    {
                        filterParts.Add($"Convert([{col.ColumnName}], 'System.String') LIKE '%{escaped}%'");
                    }
                }
                DataView.RowFilter = filterParts.Count > 0 ? string.Join(" OR ", filterParts) : string.Empty;
                RecordCountText = $"{DataView.Count:N0} of {RecordCount:N0} records";
            }
        }
        catch
        {
            // Ignore filter syntax exceptions
        }
    }
}

