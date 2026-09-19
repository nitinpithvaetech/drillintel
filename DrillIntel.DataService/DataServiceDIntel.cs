using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace DrillIntel.Data
{

    /// <summary> test1
    /// Low-level SQLite infrastructure class for the DrillIntel .drint / .dintel project database.
    ///
    /// RESPONSIBILITY BOUNDARY:
    ///   - Owns ONE SQLite connection for the lifetime of the instance.
    ///   - Provides connection lifecycle, transactions, parameterized execution,
    ///     scalar/reader/DataTable queries, WAL + busy_timeout configuration, and
    ///     retry handling for transient SQLITE_BUSY / SQLITE_LOCKED errors.
    ///   - Domain-agnostic: knows nothing about wells, logs, channels, or calculation logic.
    ///
    /// CONCURRENCY MODEL:
    ///   Each simultaneous processor creates its OWN DataServiceDIntel instance for READS,
    ///   enabling concurrent reads in WAL mode without contention. WRITES are serialized
    ///   through DrintWriteCoordinator.
    /// </summary>
    public class DataServiceDIntel : IDataServiceDIntel
    {
        // ---------------------------------------------------------------
        // Public state
        // ---------------------------------------------------------------

        public string ConnectionString { get; private set; } = string.Empty;
        public string DatabaseFilePath { get; private set; } = string.Empty;
        public string LastError { get; private set; } = string.Empty;
        public bool IsInTransaction => _activeTransaction != null;

        /// <summary>Default command timeout in seconds.</summary>
        public int CommandTimeoutSeconds { get; set; } = 300;

        /// <summary>SQLite busy_timeout (ms) before returning SQLITE_BUSY.</summary>
        public int BusyTimeoutMilliseconds { get; set; } = 5000;

        /// <summary>How many times transient SQLITE_BUSY/LOCKED errors are retried.</summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>Industry (LAS/WITS) convention for a missing/null numeric reading.</summary>
        public const double NULL_NUMERIC_VALUE = -999.25;

        private SqliteConnection? _connection;
        private SqliteTransaction? _activeTransaction;
        private bool _disposed;

        public DataServiceDIntel() { }

        public DataServiceDIntel(string dbFilePath) => OpenConnection(dbFilePath);

        // ---------------------------------------------------------------
        // Connection lifecycle
        // ---------------------------------------------------------------

        /// <summary>
        /// Opens and configures a dedicated SQLite connection with WAL mode and performance PRAGMAs.
        /// Re-applies all PRAGMAs upon every open to guarantee settings across connection pooling.
        /// </summary>
        public bool OpenConnection(string dbFilePath)
        {
            try
            {
                CloseConnection();

                DatabaseFilePath = dbFilePath;
                ConnectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = dbFilePath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Pooling = true
                }.ToString();

                _connection = new SqliteConnection(ConnectionString);
                _connection.Open();

                ApplyConnectionPragmas(_connection);

                LastError = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.ToString();
                return false;
            }
        }

        private void ApplyConnectionPragmas(SqliteConnection connection)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                $"PRAGMA journal_mode=WAL; " +
                $"PRAGMA busy_timeout={BusyTimeoutMilliseconds}; " +
                $"PRAGMA foreign_keys=ON; " +
                $"PRAGMA synchronous=NORMAL; " +
                $"PRAGMA cache_size=-20000; " +
                $"PRAGMA temp_store=MEMORY; " +
                $"PRAGMA wal_autocheckpoint=1000;";
            cmd.ExecuteNonQuery();
        }

        public bool CheckConnection()
        {
            try
            {
                if (_connection is null || _connection.State != ConnectionState.Open) return false;
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT 1;";
                cmd.ExecuteScalar();
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        public bool IsConnectionOpen() => _connection?.State == ConnectionState.Open;

        public DbConnection GetDbConnection()
        {
            RequireConnection();
            return _connection!;
        }

        public void CloseConnection()
        {
            try
            {
                _activeTransaction?.Rollback();
            }
            catch { /* best-effort rollback */ }
            finally
            {
                _activeTransaction?.Dispose();
                _activeTransaction = null;
            }

            _connection?.Dispose();
            _connection = null;
        }

        // ---------------------------------------------------------------
        // Transactions
        // ---------------------------------------------------------------

        public bool StartTransaction() => BeginTransaction();

        public bool BeginTransaction()
        {
            try
            {
                RequireConnection();
                if (_activeTransaction != null)
                    throw new InvalidOperationException("A transaction is already active on this instance.");

                _activeTransaction = _connection!.BeginTransaction();
                LastError = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        public bool Commit()
        {
            try
            {
                if (_activeTransaction is null) return false;
                _activeTransaction.Commit();
                LastError = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
            finally
            {
                _activeTransaction?.Dispose();
                _activeTransaction = null;
            }
        }

        public bool RollBack()
        {
            try
            {
                if (_activeTransaction is null) return false;
                _activeTransaction.Rollback();
                LastError = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
            finally
            {
                _activeTransaction?.Dispose();
                _activeTransaction = null;
            }
        }

        // ---------------------------------------------------------------
        // Command primitives & Resilience
        // ---------------------------------------------------------------

        public DbCommand CreateCommand(string sql)
        {
            RequireConnection();
            var cmd = _connection!.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = CommandTimeoutSeconds;
            cmd.Transaction = _activeTransaction;
            return cmd;
        }

        public SqliteCommand CreateSqliteCommand(string sql) => (SqliteCommand)CreateCommand(sql);

        public static void AddParameter(DbCommand cmd, string name, object? value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        private static void AddParameters(DbCommand cmd, IDictionary<string, object?>? parameters)
        {
            if (parameters is null) return;
            foreach (var kvp in parameters)
                AddParameter(cmd, kvp.Key, kvp.Value);
        }

        private void RequireConnection()
        {
            if (_connection is null || _connection.State != ConnectionState.Open)
                throw new InvalidOperationException("No open database connection. Call OpenConnection(dbFilePath) first.");
        }

        private T ExecuteSafely<T>(Func<T> action, T fallback)
        {
            var attempt = 0;
            while (true)
            {
                try
                {
                    var result = action();
                    LastError = string.Empty;
                    return result;
                }
                catch (SqliteException ex) when (IsTransient(ex) && attempt < MaxRetryAttempts)
                {
                    attempt++;
                    Thread.Sleep(100 * attempt); // Linear backoff: 100ms, 200ms, 300ms...
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    return fallback;
                }
            }
        }

        private static bool IsTransient(SqliteException ex) =>
            ex.SqliteErrorCode == 5 /* SQLITE_BUSY */ || ex.SqliteErrorCode == 6 /* SQLITE_LOCKED */;

        // ---------------------------------------------------------------
        // Execute (INSERT / UPDATE / DELETE / DDL)
        // ---------------------------------------------------------------

        public bool ExecuteNonQuery(string sql) => ExecuteNonQuery(sql, null);

        public bool ExecuteNonQuery(string sql, IDictionary<string, object?>? parameters) =>
            ExecuteSafely(() =>
            {
                using var cmd = CreateCommand(sql);
                AddParameters(cmd, parameters);
                cmd.ExecuteNonQuery();
                return true;
            }, false);

        public bool ExecuteNonQueryWithTransaction(string sql)
        {
            if (IsInTransaction)
            {
                LastError = "ExecuteNonQueryWithTransaction cannot be used while a transaction is already active.";
                return false;
            }

            if (!BeginTransaction()) return false;

            if (ExecuteNonQuery(sql))
                return Commit();

            RollBack();
            return false;
        }

        public bool RunScript(string sqlScript) => ExecuteNonQueryWithTransaction(sqlScript);

        public bool RunScriptFromFile(string filePath)
        {
            try
            {
                return RunScript(File.ReadAllText(filePath));
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                return false;
            }
        }

        // ---------------------------------------------------------------
        // Scalar getters
        // ---------------------------------------------------------------

        public object? GetValue(string query, IDictionary<string, object?>? parameters = null) =>
            GetValueFromDatabase(query, parameters);

        public object? GetValueFromDatabase(string query) => GetValueFromDatabase(query, null);

        public object? GetValueFromDatabase(string query, IDictionary<string, object?>? parameters) =>
            ExecuteSafely<object?>(() =>
            {
                using var cmd = CreateCommand(query);
                AddParameters(cmd, parameters);
                var result = cmd.ExecuteScalar();
                return (result is null or DBNull) ? null : result;
            }, null);

        public string GetStringValueFromDatabase(string query) =>
            GetValueFromDatabase(query)?.ToString() ?? string.Empty;

        public double GetDoubleValueFromDatabase(string query) =>
            TryToDouble(GetValueFromDatabase(query), NULL_NUMERIC_VALUE);

        public DateTime GetDateTimeValueFromDatabase(string query)
        {
            var value = GetValueFromDatabase(query);
            return value != null && DateTime.TryParse(value.ToString(), out var dt) ? dt : DateTime.MinValue;
        }

        public bool GetBooleanValueFromDatabase(string query)
        {
            var value = GetValueFromDatabase(query);
            return value switch
            {
                null => false,
                long l => l != 0,
                bool b => b,
                _ => value.ToString() == "1" ||
                     string.Equals(value.ToString(), "true", StringComparison.OrdinalIgnoreCase)
            };
        }

        public object? GetValueFromDatabaseEx(string query, out string error)
        {
            var result = GetValueFromDatabase(query);
            error = LastError;
            return result;
        }

        // ---------------------------------------------------------------
        // Table & Reader getters
        // ---------------------------------------------------------------

        public DataTable GetTable(string query) => GetTable(query, (IDictionary<string, object?>?)null);

        public DataTable GetTable(string query, IDictionary<string, object?>? parameters) =>
            ExecuteSafely(() =>
            {
                using var cmd = CreateCommand(query);
                AddParameters(cmd, parameters);
                using var reader = cmd.ExecuteReader();
                var table = new DataTable();
                table.Load(reader);
                return table;
            }, new DataTable());

        public DataTable GetTable(string query, int limitRows) =>
            GetTable(query.TrimEnd(';', ' ') + $" LIMIT {limitRows}");

        public DbDataReader ExecuteReader(string query, IDictionary<string, object?>? parameters = null)
        {
            var cmd = CreateCommand(query);
            AddParameters(cmd, parameters);
            return cmd.ExecuteReader();
        }

        public SqliteDataReader ExecuteSqliteReader(string query, IDictionary<string, object?>? parameters = null) =>
            (SqliteDataReader)ExecuteReader(query, parameters);

        public bool IsRecordExist(string query)
        {
            var value = GetValueFromDatabase($"SELECT EXISTS({query.TrimEnd(';')}) AS RecordExists");
            return value != null && Convert.ToInt64(value) != 0;
        }

        public bool TableExists(string tableName)
        {
            var value = GetValueFromDatabase(
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name;",
                new Dictionary<string, object?> { ["@name"] = tableName });
            return value != null && Convert.ToInt64(value) > 0;
        }

        // ---------------------------------------------------------------
        // Async API with CancellationToken support
        // ---------------------------------------------------------------

        public async Task<bool> ExecuteNonQueryAsync(
            string sql, IDictionary<string, object?>? parameters = null, CancellationToken ct = default)
        {
            using var cmd = CreateCommand(sql);
            AddParameters(cmd, parameters);
            await cmd.ExecuteNonQueryAsync(ct);
            return true;
        }

        public async Task<DataTable> GetTableAsync(
            string query, IDictionary<string, object?>? parameters = null, CancellationToken ct = default)
        {
            using var cmd = CreateCommand(query);
            AddParameters(cmd, parameters);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            var table = new DataTable();
            table.Load(reader);
            return table;
        }

        public async Task<object?> GetValueAsync(
            string query, IDictionary<string, object?>? parameters = null, CancellationToken ct = default)
        {
            using var cmd = CreateCommand(query);
            AddParameters(cmd, parameters);
            var result = await cmd.ExecuteScalarAsync(ct);
            return (result is null or DBNull) ? null : result;
        }

        // ---------------------------------------------------------------
        // Diagnostics
        // ---------------------------------------------------------------

        public bool CheckIntegrity(out string result)
        {
            result = GetStringValueFromDatabase("PRAGMA integrity_check;");
            return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
        }

        // ---------------------------------------------------------------
        // Null-safety helpers
        // ---------------------------------------------------------------

        public static object CheckNull(object? obj, object defaultValue) =>
            (obj is null || obj.ToString() == string.Empty) ? defaultValue : obj;

        public static double CheckNumericNull(object? obj) =>
            (obj is null || obj.ToString() == string.Empty) ? NULL_NUMERIC_VALUE : TryToDouble(obj, NULL_NUMERIC_VALUE);

        public static double CheckCapacity(double number) =>
            number > 9999999999 ? NULL_NUMERIC_VALUE : number;

        public static byte ToByteBool(object? value) =>
            string.Equals(value?.ToString(), "True", StringComparison.OrdinalIgnoreCase) ? (byte)1 : (byte)0;

        private static double TryToDouble(object? value, double fallback) =>
            value != null && double.TryParse(value.ToString(), out var d) ? d : fallback;

        // ---------------------------------------------------------------
        // IDisposable
        // ---------------------------------------------------------------

        public void Dispose()
        {
            if (_disposed) return;
            CloseConnection();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
