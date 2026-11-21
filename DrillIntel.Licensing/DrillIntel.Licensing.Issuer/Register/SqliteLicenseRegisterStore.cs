using System.Globalization;
using System.Security.Principal;
using Microsoft.Data.Sqlite;
using DrillIntel.Licensing.Issuer.Requests;

namespace DrillIntel.Licensing.Issuer.Register;

/// <summary>Office-side read/write operations for the license register.</summary>
public sealed class SqliteLicenseRegisterStore(SqliteRegisterDatabase database)
{
    public async Task<IReadOnlyList<CustomerSummary>> GetCustomersAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c.CustomerId, c.Name, c.Contact,
                   (SELECT COUNT(*) FROM Rigs r WHERE r.CustomerId = c.CustomerId) AS RigCount,
                   (SELECT COUNT(*) FROM Licenses l JOIN Rigs lr ON lr.RigId = l.RigId WHERE lr.CustomerId = c.CustomerId) AS LicenseCount
            FROM Customers c
            ORDER BY c.Name COLLATE NOCASE;
            """;
        var customers = new List<CustomerSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            customers.Add(new CustomerSummary(
                reader.GetInt64(0), reader.GetString(1), NullableString(reader, 2), reader.GetInt32(3), reader.GetInt32(4)));
        return customers;
    }

    public async Task<CustomerWorkspace?> GetWorkspaceAsync(long customerId, CancellationToken cancellationToken = default)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var customer = await GetCustomerAsync(connection, customerId, cancellationToken).ConfigureAwait(false);
        if (customer is null)
            return null;

        var rigs = await ReadAsync(connection, """
            SELECT RigId, CustomerId, RigName FROM Rigs WHERE CustomerId = $customerId ORDER BY RigName COLLATE NOCASE;
            """, r => new RigRecord(r.GetInt64(0), r.GetInt64(1), r.GetString(2)), customerId, cancellationToken);

        var licenses = await ReadAsync(connection, """
            SELECT l.LicenseId, l.RigId, r.RigName, l.Plan, l.StartUtc, l.ExpiryUtc, l.MaxDevices, l.Status,
                   COALESCE((SELECT MAX(er.Revision) FROM EntitlementRevisions er WHERE er.LicenseId = l.LicenseId), 0)
            FROM Licenses l JOIN Rigs r ON r.RigId = l.RigId
            WHERE r.CustomerId = $customerId ORDER BY l.ExpiryUtc DESC;
            """, r => new LicenseRecord(r.GetString(0), r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetString(4),
                r.GetString(5), r.GetInt32(6), r.GetString(7), r.GetInt64(8)), customerId, cancellationToken);

        var devices = await ReadForCustomerAsync(connection, """
            SELECT d.DeviceId, d.LicenseId, d.KeyHash, d.KeyProtection, d.Status, d.ActivatedAtUtc,
                   d.ActivationExpiryUtc, d.ReplacementForDeviceId
            FROM Devices d JOIN Licenses l ON l.LicenseId = d.LicenseId JOIN Rigs r ON r.RigId = l.RigId
            WHERE r.CustomerId = $customerId ORDER BY d.ActivatedAtUtc DESC;
            """, r => new DeviceRecord(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4),
                r.GetString(5), r.GetString(6), r.IsDBNull(7) ? null : r.GetInt64(7)), customerId, cancellationToken);

        var revisions = await ReadForCustomerAsync(connection, """
            SELECT er.LicenseId, er.Revision, er.IssuedAtUtc, er.Kid, er.SignedFileRef, er.FileSha256
            FROM EntitlementRevisions er JOIN Licenses l ON l.LicenseId = er.LicenseId JOIN Rigs r ON r.RigId = l.RigId
            WHERE r.CustomerId = $customerId ORDER BY er.IssuedAtUtc DESC;
            """, r => new EntitlementRevisionRecord(r.GetString(0), r.GetInt64(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5)),
            customerId, cancellationToken);

        var activationFiles = await ReadForCustomerAsync(connection, """
            SELECT af.ActivationFileId, af.LicenseId, af.DeviceId, af.RequestId, af.Revision, af.Kid,
                   af.IssuedAtUtc, af.ActivationExpiryUtc, af.ArchiveRef, af.FileSha256
            FROM ActivationFiles af JOIN Licenses l ON l.LicenseId = af.LicenseId JOIN Rigs r ON r.RigId = l.RigId
            WHERE r.CustomerId = $customerId ORDER BY af.IssuedAtUtc DESC;
            """, r => new ActivationFileRecord(r.GetString(0), r.GetString(1), r.GetInt64(2), NullableString(r, 3), r.GetInt64(4),
                r.GetString(5), r.GetString(6), r.GetString(7), r.GetString(8), r.GetString(9)), customerId, cancellationToken);

        var requests = await ReadForCustomerAsync(connection, """
            SELECT q.RequestId, q.LicenseId, q.KeyHash, q.ReceivedAtUtc, q.Outcome, q.OutcomeDetails
            FROM Requests q JOIN Licenses l ON l.LicenseId = q.LicenseId JOIN Rigs r ON r.RigId = l.RigId
            WHERE r.CustomerId = $customerId ORDER BY q.ReceivedAtUtc DESC;
            """, r => new RequestRecord(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), NullableString(r, 5)),
            customerId, cancellationToken);

        var requestInbox = await ReadForCustomerAsync(connection, """
            SELECT q.RequestId, q.LicenseId, q.KeyHash, q.KeyProtection, q.TimestampUtc,
                   q.ReceivedAtUtc, q.SourceFile, q.Status
            FROM RequestInbox q JOIN Licenses l ON l.LicenseId = q.LicenseId JOIN Rigs r ON r.RigId = l.RigId
            WHERE r.CustomerId = $customerId ORDER BY q.ReceivedAtUtc DESC;
            """, r => new PendingRequestRecord(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3),
                r.GetString(4), r.GetString(5), r.GetString(6), r.GetString(7)), customerId, cancellationToken);

        var payments = await ReadForCustomerAsync(connection, """
            SELECT p.OrderRef, p.LicenseId, p.AmountMinor, p.Currency, p.Processor, p.Status, p.PaidAtUtc
            FROM Payments p JOIN Licenses l ON l.LicenseId = p.LicenseId JOIN Rigs r ON r.RigId = l.RigId
            WHERE r.CustomerId = $customerId ORDER BY p.PaidAtUtc DESC;
            """, r => new PaymentRecord(r.GetString(0), r.GetString(1), r.GetInt64(2), r.GetString(3), r.GetString(4), r.GetString(5), NullableString(r, 6)),
            customerId, cancellationToken);

        var audit = await ReadForCustomerAsync(connection, """
            SELECT a.TimestampUtc, a.Operator, a.Action, a.LicenseId, a.Details
            FROM AuditLog a
            WHERE a.LicenseId IN (
                SELECT l.LicenseId FROM Licenses l JOIN Rigs r ON r.RigId = l.RigId WHERE r.CustomerId = $customerId)
               OR (a.LicenseId IS NULL AND a.Details LIKE $customerPattern)
            ORDER BY a.TimestampUtc DESC LIMIT 500;
            """, r => new AuditRecord(r.GetString(0), r.GetString(1), r.GetString(2), NullableString(r, 3), NullableString(r, 4)),
            customerId, cancellationToken, ("$customerPattern", $"%customerId={customerId}%"));

        return new CustomerWorkspace(customer, rigs, licenses, devices, revisions, activationFiles, requests, requestInbox, payments, audit);
    }

    public async Task<PendingRequestRecord> ImportActivationRequestAsync(string compactJws, string sourceFile, CancellationToken cancellationToken = default)
    {
        var request = ActivationRequestVerifier.Verify(compactJws);
        await using var connection = await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = UtcNow();
        var licenseExists = false;
        await using (var license = connection.CreateCommand())
        {
            license.Transaction = (SqliteTransaction)transaction;
            license.CommandText = "SELECT 1 FROM Licenses WHERE LicenseId = $id;";
            license.Parameters.AddWithValue("$id", request.LicenseId);
            licenseExists = await license.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null;
        }
        if (!licenseExists)
            throw new InvalidOperationException($"No license with ID '{request.LicenseId}' exists in this register.");

        var status = "pending";
        await using (var duplicate = connection.CreateCommand())
        {
            duplicate.Transaction = (SqliteTransaction)transaction;
            duplicate.CommandText = "SELECT RequestId FROM RequestInbox WHERE LicenseId = $license AND KeyHash = $hash LIMIT 1;";
            duplicate.Parameters.AddWithValue("$license", request.LicenseId);
            duplicate.Parameters.AddWithValue("$hash", request.DevicePublicKeyHash);
            if (await duplicate.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
                status = "duplicate";
        }

        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = """
                INSERT INTO RequestInbox(RequestId, LicenseId, KeyHash, KeyProtection, TimestampUtc, ReceivedAtUtc, SourceFile, Status)
                VALUES ($request, $license, $hash, $protection, $timestamp, $received, $source, $status)
                ON CONFLICT(RequestId) DO NOTHING;
                """;
            command.Parameters.AddWithValue("$request", request.RequestId);
            command.Parameters.AddWithValue("$license", request.LicenseId);
            command.Parameters.AddWithValue("$hash", request.DevicePublicKeyHash);
            command.Parameters.AddWithValue("$protection", request.KeyProtection);
            command.Parameters.AddWithValue("$timestamp", request.TimestampUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$received", now);
            command.Parameters.AddWithValue("$source", Path.GetFullPath(sourceFile));
            command.Parameters.AddWithValue("$status", status);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await InsertAuditAsync(connection, (SqliteTransaction)transaction, request.LicenseId, "ActivationRequestImported",
            $"requestId={request.RequestId}; keyHash={request.DevicePublicKeyHash}; status={status}", now, cancellationToken);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new PendingRequestRecord(request.RequestId, request.LicenseId, request.DevicePublicKeyHash, request.KeyProtection,
            request.TimestampUtc.ToString("O", CultureInfo.InvariantCulture), now, Path.GetFullPath(sourceFile), status);
    }

    public async Task<long> CreateCustomerAsync(string name, string? contact, string? notes, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await using var connection = await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = UtcNow();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = "INSERT INTO Customers(Name, Contact, Notes, CreatedAtUtc) VALUES ($name, $contact, $notes, $now); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$name", name.Trim());
            command.Parameters.AddWithValue("$contact", DbValue(contact));
            command.Parameters.AddWithValue("$notes", DbValue(notes));
            command.Parameters.AddWithValue("$now", now);
            var id = (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
            await InsertAuditAsync(connection, (SqliteTransaction)transaction, null, "CustomerCreated", $"customerId={id}; name={name.Trim()}", now, cancellationToken);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return id;
        }
    }

    public async Task<long> CreateRigAsync(long customerId, string rigName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rigName);
        await using var connection = await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = UtcNow();
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = "INSERT INTO Rigs(CustomerId, RigName, CreatedAtUtc) VALUES ($customerId, $name, $now); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$customerId", customerId);
        command.Parameters.AddWithValue("$name", rigName.Trim());
        command.Parameters.AddWithValue("$now", now);
        var id = (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) ?? 0L);
        await InsertAuditAsync(connection, (SqliteTransaction)transaction, null, "RigCreated", $"customerId={customerId}; rigId={id}; rig={rigName.Trim()}", now, cancellationToken);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return id;
    }

    public async Task<string> CreateLicenseAsync(
        long rigId,
        string plan,
        DateTimeOffset startUtc,
        DateTimeOffset expiryUtc,
        int maxDevices = 3,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(plan) || plan.Length > 32 ||
            !System.Text.RegularExpressions.Regex.IsMatch(plan, @"^[1-9][0-9]{0,3}-(day|month)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new ArgumentOutOfRangeException(nameof(plan), "Plan must be a positive day or month term (for example, 1-day or 6-month).");
        if (expiryUtc <= startUtc || maxDevices < 1)
            throw new ArgumentException("License dates and device limit are invalid.");

        var licenseId = $"DIL-{DateTime.UtcNow:yyyy}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
        await using var connection = await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = UtcNow();
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = """
            INSERT INTO Licenses(LicenseId, RigId, Plan, StartUtc, ExpiryUtc, MaxDevices, Status, CreatedAtUtc)
            VALUES ($licenseId, $rigId, $plan, $start, $expiry, $maxDevices, 'suspended', $now);
            """;
        command.Parameters.AddWithValue("$licenseId", licenseId);
        command.Parameters.AddWithValue("$rigId", rigId);
        command.Parameters.AddWithValue("$plan", plan);
        command.Parameters.AddWithValue("$start", startUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$expiry", expiryUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$maxDevices", maxDevices);
        command.Parameters.AddWithValue("$now", now);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await InsertAuditAsync(connection, (SqliteTransaction)transaction, licenseId, "LicenseRecordCreated",
            $"rigId={rigId}; plan={plan}; status=suspended; signed entitlement not issued", now, cancellationToken);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return licenseId;
    }

    private static async Task<CustomerRecord?> GetCustomerAsync(SqliteConnection connection, long id, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CustomerId, Name, Contact, Notes FROM Customers WHERE CustomerId = $id;";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        return await reader.ReadAsync(ct).ConfigureAwait(false)
            ? new CustomerRecord(reader.GetInt64(0), reader.GetString(1), NullableString(reader, 2), NullableString(reader, 3))
            : null;
    }

    private static async Task<List<T>> ReadAsync<T>(SqliteConnection connection, string sql, Func<SqliteDataReader, T> map,
        long customerId, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$customerId", customerId);
        return await ReadRowsAsync(command, map, ct).ConfigureAwait(false);
    }

    private static async Task<List<T>> ReadForCustomerAsync<T>(SqliteConnection connection, string sql, Func<SqliteDataReader, T> map,
        long customerId, CancellationToken ct, params (string Name, object Value)[] extraParameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$customerId", customerId);
        foreach (var (name, value) in extraParameters)
            command.Parameters.AddWithValue(name, value);
        return await ReadRowsAsync(command, map, ct).ConfigureAwait(false);
    }

    private static async Task<List<T>> ReadRowsAsync<T>(SqliteCommand command, Func<SqliteDataReader, T> map, CancellationToken ct)
    {
        var result = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            result.Add(map(reader));
        return result;
    }

    private static async Task InsertAuditAsync(SqliteConnection connection, SqliteTransaction transaction, string? licenseId,
        string action, string details, string now, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO AuditLog(TimestampUtc, Operator, Action, LicenseId, Details) VALUES ($time, $operator, $action, $licenseId, $details);";
        command.Parameters.AddWithValue("$time", now);
        command.Parameters.AddWithValue("$operator", Environment.UserName);
        command.Parameters.AddWithValue("$action", action);
        command.Parameters.AddWithValue("$licenseId", DbValue(licenseId));
        command.Parameters.AddWithValue("$details", details);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private static string UtcNow() => DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    private static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    private static string? NullableString(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
}
