using Microsoft.Data.Sqlite;

namespace DrillIntel.Licensing.Issuer.Register;

/// <summary>Office-only register database. Do not use this file as a rig-side license store.</summary>
public sealed class SqliteRegisterDatabase
{
    private readonly string _connectionString;

    public SqliteRegisterDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var fullPath = Path.GetFullPath(databasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = true,
            ForeignKeys = true,
            DefaultTimeout = 15
        }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON; PRAGMA busy_timeout=15000;";
            await pragma.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = RegisterSchema.Sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await MigrateCustomPlanTermsAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    private static async Task MigrateCustomPlanTermsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var check = connection.CreateCommand();
        check.CommandText = "SELECT sql FROM sqlite_master WHERE type='table' AND name='Licenses';";
        var definition = (string?)await check.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (definition is null || !definition.Contains("'4-month'", StringComparison.OrdinalIgnoreCase))
            return;

        await using (var disableFk = connection.CreateCommand())
        {
            disableFk.CommandText = "PRAGMA foreign_keys=OFF;";
            await disableFk.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await using var migrate = connection.CreateCommand();
            migrate.CommandText = """
                BEGIN IMMEDIATE;
                CREATE TABLE Licenses_new (
                    LicenseId TEXT PRIMARY KEY,
                    RigId INTEGER NOT NULL REFERENCES Rigs(RigId),
                    Plan TEXT NOT NULL,
                    StartUtc TEXT NOT NULL,
                    ExpiryUtc TEXT NOT NULL,
                    MaxDevices INTEGER NOT NULL CHECK (MaxDevices > 0),
                    Status TEXT NOT NULL CHECK (Status IN ('active', 'suspended', 'revoked')),
                    CreatedAtUtc TEXT NOT NULL
                );
                INSERT INTO Licenses_new SELECT LicenseId, RigId, Plan, StartUtc, ExpiryUtc, MaxDevices, Status, CreatedAtUtc FROM Licenses;
                DROP TABLE Licenses;
                ALTER TABLE Licenses_new RENAME TO Licenses;
                CREATE INDEX IF NOT EXISTS IX_Licenses_RigId ON Licenses(RigId);
                COMMIT;
                """;
            await migrate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await using var rollback = connection.CreateCommand();
            rollback.CommandText = "ROLLBACK;";
            await rollback.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await using var enableFk = connection.CreateCommand();
            enableFk.CommandText = "PRAGMA foreign_keys=ON;";
            await enableFk.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
