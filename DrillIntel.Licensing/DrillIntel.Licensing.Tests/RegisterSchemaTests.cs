using DrillIntel.Licensing.Issuer.Register;
using Microsoft.Data.Sqlite;

namespace DrillIntel.Licensing.Tests;

public sealed class RegisterSchemaTests
{
    [Fact]
    public async Task InitializeIsRepeatable_AndUniqueLicenseRevisionIsEnforced()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DrillIntelLicenseTests", Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "register.sqlite");
        try
        {
            var database = new SqliteRegisterDatabase(databasePath);
            await database.InitializeAsync();
            await database.InitializeAsync();

            await using var connection = await database.OpenConnectionAsync();
            await ExecuteAsync(connection, """
                INSERT INTO Customers (CustomerId, Name, CreatedAtUtc) VALUES (1, 'Test Customer', '2026-11-18T00:00:00Z');
                INSERT INTO Rigs (RigId, CustomerId, RigName, CreatedAtUtc) VALUES (1, 1, 'Rig 1', '2026-11-18T00:00:00Z');
                INSERT INTO Licenses (LicenseId, RigId, Plan, StartUtc, ExpiryUtc, MaxDevices, Status, CreatedAtUtc)
                VALUES ('LIC-TEST-001', 1, '6-month', '2026-11-18T00:00:00Z', '2027-05-18T00:00:00Z', 3, 'active', '2026-11-18T00:00:00Z');
                INSERT INTO EntitlementRevisions (LicenseId, Revision, IssuedAtUtc, Kid, SignedFileRef, FileSha256)
                VALUES ('LIC-TEST-001', 1, '2026-11-18T00:00:00Z', 'issuer-test', 'entitlements/1.jws', 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa');
                """);

            await Assert.ThrowsAsync<SqliteException>(() => ExecuteAsync(connection, """
                INSERT INTO EntitlementRevisions (LicenseId, Revision, IssuedAtUtc, Kid, SignedFileRef, FileSha256)
                VALUES ('LIC-TEST-001', 1, '2026-11-18T00:00:00Z', 'issuer-test', 'entitlements/duplicate.jws', 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb');
                """));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
