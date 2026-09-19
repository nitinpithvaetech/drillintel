using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace DrillIntel.Data
{
    /// <summary>
    /// Contract for low-level SQLite infrastructure operations against a DrillIntel database.
    /// </summary>
    public interface IDataServiceDIntel : IDisposable
    {
        string ConnectionString { get; }
        string DatabaseFilePath { get; }
        string LastError { get; }
        bool IsInTransaction { get; }
        int CommandTimeoutSeconds { get; set; }
        int BusyTimeoutMilliseconds { get; set; }
        int MaxRetryAttempts { get; set; }

        // Connection Lifecycle
        bool OpenConnection(string dbFilePath);
        bool CheckConnection();
        bool IsConnectionOpen();
        void CloseConnection();
        DbConnection GetDbConnection();

        // Transactions
        bool StartTransaction();
        bool BeginTransaction();
        bool Commit();
        bool RollBack();

        // Command Primitives
        DbCommand CreateCommand(string sql);

        // Execution (Sync)
        bool ExecuteNonQuery(string sql);
        bool ExecuteNonQuery(string sql, IDictionary<string, object?>? parameters);
        bool ExecuteNonQueryWithTransaction(string sql);
        bool RunScript(string sqlScript);
        bool RunScriptFromFile(string filePath);

        // Scalars (Sync)
        object? GetValue(string query, IDictionary<string, object?>? parameters = null);
        object? GetValueFromDatabase(string query);
        object? GetValueFromDatabase(string query, IDictionary<string, object?>? parameters);
        string GetStringValueFromDatabase(string query);
        double GetDoubleValueFromDatabase(string query);
        DateTime GetDateTimeValueFromDatabase(string query);
        bool GetBooleanValueFromDatabase(string query);
        object? GetValueFromDatabaseEx(string query, out string error);

        // Tables & Readers (Sync)
        DataTable GetTable(string query);
        DataTable GetTable(string query, IDictionary<string, object?>? parameters);
        DataTable GetTable(string query, int limitRows);
        DbDataReader ExecuteReader(string query, IDictionary<string, object?>? parameters = null);
        bool IsRecordExist(string query);
        bool TableExists(string tableName);

        // Async API
        Task<bool> ExecuteNonQueryAsync(string sql, IDictionary<string, object?>? parameters = null, CancellationToken ct = default);
        Task<DataTable> GetTableAsync(string query, IDictionary<string, object?>? parameters = null, CancellationToken ct = default);
        Task<object?> GetValueAsync(string query, IDictionary<string, object?>? parameters = null, CancellationToken ct = default);

        // Diagnostics
        bool CheckIntegrity(out string result);
    }
}
