using System.Globalization;
using System.Security.Principal;
using Microsoft.Data.Sqlite;
using DrillIntel.Licensing.Core.Models;
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
            FROM (
                SELECT RequestId, LicenseId, KeyHash, ReceivedAtUtc, Outcome, OutcomeDetails FROM Requests
                UNION ALL
                SELECT inbox.RequestId, inbox.LicenseId, inbox.KeyHash, inbox.ReceivedAtUtc, inbox.Status,
                       CASE WHEN inbox.Status = 'duplicate' THEN 'Duplicate device request' ELSE 'Rejected or archived request' END
                FROM RequestInbox inbox
                WHERE inbox.Status IN ('duplicate', 'rejected')
                  AND NOT EXISTS (SELECT 1 FROM Requests processed WHERE processed.RequestId = inbox.RequestId)
            ) q JOIN Licenses l ON l.LicenseId = q.LicenseId JOIN Rigs r ON r.RigId = l.RigId
            WHERE r.CustomerId = $customerId ORDER BY q.ReceivedAtUtc DESC;
            """, r => new RequestRecord(r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), NullableString(r, 5)),
            customerId, cancellationToken);

        var requestInbox = await ReadForCustomerAsync(connection, """
            SELECT q.RequestId, q.LicenseId, q.KeyHash, q.KeyProtection, q.TimestampUtc,
                   q.ReceivedAtUtc, q.SourceFile, COALESCE(processed.Outcome, q.Status)
            FROM RequestInbox q JOIN Licenses l ON l.LicenseId = q.LicenseId JOIN Rigs r ON r.RigId = l.RigId
            LEFT JOIN Requests processed ON processed.RequestId = q.RequestId
            WHERE r.CustomerId = $customerId AND q.Status = 'pending' ORDER BY q.ReceivedAtUtc DESC;
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

    public async Task RejectActivationRequestAsync(string requestId, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        reason = reason.Trim();
        if (reason.Length > 500)
            throw new ArgumentOutOfRangeException(nameof(reason), "The reason must be 500 characters or fewer.");

        await using var connection = await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = UtcNow();
        string licenseId;
        string keyHash;
        string receivedAtUtc;
        await using (var find = connection.CreateCommand())
        {
            find.Transaction = (SqliteTransaction)transaction;
            find.CommandText = "SELECT LicenseId, KeyHash, ReceivedAtUtc FROM RequestInbox WHERE RequestId=$request AND Status='pending';";
            find.Parameters.AddWithValue("$request", requestId);
            await using var reader = await find.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException("This request is no longer pending. Refresh the register and try again.");
            licenseId = reader.GetString(0);
            keyHash = reader.GetString(1);
            receivedAtUtc = reader.GetString(2);
        }

        await using (var history = connection.CreateCommand())
        {
            history.Transaction = (SqliteTransaction)transaction;
            history.CommandText = "INSERT INTO Requests(RequestId, LicenseId, KeyHash, ReceivedAtUtc, Outcome, OutcomeDetails) VALUES ($request,$license,$hash,$received,'rejected',$details);";
            history.Parameters.AddWithValue("$request", requestId);
            history.Parameters.AddWithValue("$license", licenseId);
            history.Parameters.AddWithValue("$hash", keyHash);
            history.Parameters.AddWithValue("$received", receivedAtUtc);
            history.Parameters.AddWithValue("$details", $"Rejected/archived by admin: {reason}");
            await history.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var archive = connection.CreateCommand())
        {
            archive.Transaction = (SqliteTransaction)transaction;
            archive.CommandText = "UPDATE RequestInbox SET Status='rejected' WHERE RequestId=$request AND Status='pending';";
            archive.Parameters.AddWithValue("$request", requestId);
            if (await archive.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) != 1)
                throw new InvalidOperationException("This request changed while it was being archived. Refresh the register and try again.");
        }

        await InsertAuditAsync(connection, (SqliteTransaction)transaction, licenseId, "ActivationRequestRejectedArchived",
            $"requestId={requestId}; keyHash={keyHash}; reason={reason}", now, cancellationToken);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task CreatePaymentAsync(
        string orderRef,
        string licenseId,
        long amountMinor,
        string currency,
        string processor,
        string status,
        DateTimeOffset? paidAtUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderRef);
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(processor);
        if (orderRef.Trim().Length > 100 || processor.Trim().Length > 80 || amountMinor < 0)
            throw new ArgumentOutOfRangeException(nameof(amountMinor), "Payment details exceed supported limits.");
        currency = currency.Trim().ToUpperInvariant();
        if (currency.Length != 3 || !currency.All(char.IsAsciiLetter))
            throw new ArgumentException("Currency must be a three-letter ISO code, such as USD or INR.", nameof(currency));
        if (status is not ("paid" or "refunded" or "chargeback"))
            throw new ArgumentOutOfRangeException(nameof(status));
        if (status == "paid" && amountMinor <= 0)
            throw new ArgumentOutOfRangeException(nameof(amountMinor), "A paid amount must be greater than zero.");

        await using var connection = await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = UtcNow();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = "INSERT INTO Payments(OrderRef, LicenseId, AmountMinor, Currency, Processor, Status, PaidAtUtc) VALUES ($order, $license, $amount, $currency, $processor, $status, $paidAt);";
            command.Parameters.AddWithValue("$order", orderRef.Trim());
            command.Parameters.AddWithValue("$license", licenseId);
            command.Parameters.AddWithValue("$amount", amountMinor);
            command.Parameters.AddWithValue("$currency", currency);
            command.Parameters.AddWithValue("$processor", processor.Trim());
            command.Parameters.AddWithValue("$status", status);
            command.Parameters.AddWithValue("$paidAt", status == "paid" && paidAtUtc.HasValue
                ? paidAtUtc.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                : DBNull.Value);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        await InsertAuditAsync(connection, (SqliteTransaction)transaction, licenseId, "PaymentRecorded",
            $"orderRef={orderRef.Trim()}; amountMinor={amountMinor}; currency={currency}; processor={processor.Trim()}; status={status}", now, cancellationToken);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordProductionIssuanceAsync(
        ActivationRequest request,
        long revision,
        string keyId,
        DateTimeOffset issuedAtUtc,
        DateTimeOffset activationExpiryUtc,
        string archiveRef,
        string entitlementSha256,
        string activationSha256,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var connection = await database.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var now = UtcNow();

        string licenseStatus;
        int maxDevices;
        await using (var license = connection.CreateCommand())
        {
            license.Transaction = (SqliteTransaction)transaction;
            license.CommandText = "SELECT Status, MaxDevices FROM Licenses WHERE LicenseId=$license;";
            license.Parameters.AddWithValue("$license", request.LicenseId);
            await using var reader = await license.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                throw new InvalidOperationException("The request's license no longer exists in this register.");
            licenseStatus = reader.GetString(0);
            maxDevices = reader.GetInt32(1);
        }
        if (licenseStatus == "revoked")
            throw new InvalidOperationException("This license is revoked and cannot activate another PC.");

        await using (var pending = connection.CreateCommand())
        {
            pending.Transaction = (SqliteTransaction)transaction;
            pending.CommandText = "SELECT 1 FROM RequestInbox WHERE RequestId=$request AND LicenseId=$license AND KeyHash=$hash AND Status='pending';";
            pending.Parameters.AddWithValue("$request", request.RequestId);
            pending.Parameters.AddWithValue("$license", request.LicenseId);
            pending.Parameters.AddWithValue("$hash", request.DevicePublicKeyHash);
            if (await pending.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
                throw new InvalidOperationException("This request is no longer pending. Refresh the register and select a pending request.");
        }

        await using (var activeCount = connection.CreateCommand())
        {
            activeCount.Transaction = (SqliteTransaction)transaction;
            activeCount.CommandText = "SELECT COUNT(*) FROM Devices WHERE LicenseId=$license AND Status='active';";
            activeCount.Parameters.AddWithValue("$license", request.LicenseId);
            var count = Convert.ToInt32(await activeCount.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
            if (count >= maxDevices)
                throw new InvalidOperationException("This license has reached its maximum active PC count.");
        }

        await using (var deviceExists = connection.CreateCommand())
        {
            deviceExists.Transaction = (SqliteTransaction)transaction;
            deviceExists.CommandText = "SELECT 1 FROM Devices WHERE LicenseId=$license AND KeyHash=$hash;";
            deviceExists.Parameters.AddWithValue("$license", request.LicenseId);
            deviceExists.Parameters.AddWithValue("$hash", request.DevicePublicKeyHash);
            if (await deviceExists.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not null)
                throw new InvalidOperationException("This PC is already recorded for this license.");
        }

        await using (var revisionRecord = connection.CreateCommand())
        {
            revisionRecord.Transaction = (SqliteTransaction)transaction;
            revisionRecord.CommandText = "INSERT INTO EntitlementRevisions(LicenseId, Revision, IssuedAtUtc, Kid, SignedFileRef, FileSha256) VALUES ($license,$revision,$issued,$kid,$file,$hash);";
            revisionRecord.Parameters.AddWithValue("$license", request.LicenseId);
            revisionRecord.Parameters.AddWithValue("$revision", revision);
            revisionRecord.Parameters.AddWithValue("$issued", issuedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            revisionRecord.Parameters.AddWithValue("$kid", keyId);
            revisionRecord.Parameters.AddWithValue("$file", archiveRef + "#entitlement.jws");
            revisionRecord.Parameters.AddWithValue("$hash", entitlementSha256);
            await revisionRecord.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        long deviceId;
        await using (var device = connection.CreateCommand())
        {
            device.Transaction = (SqliteTransaction)transaction;
            device.CommandText = "INSERT INTO Devices(LicenseId, KeyHash, KeyProtection, Status, ActivatedAtUtc, ActivationExpiryUtc) VALUES ($license,$hash,$protection,'active',$activated,$expiry); SELECT last_insert_rowid();";
            device.Parameters.AddWithValue("$license", request.LicenseId);
            device.Parameters.AddWithValue("$hash", request.DevicePublicKeyHash);
            device.Parameters.AddWithValue("$protection", request.KeyProtection);
            device.Parameters.AddWithValue("$activated", issuedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            device.Parameters.AddWithValue("$expiry", activationExpiryUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            deviceId = Convert.ToInt64(await device.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
        }

        var activationFileId = Guid.NewGuid().ToString("D");
        await using (var processed = connection.CreateCommand())
        {
            processed.Transaction = (SqliteTransaction)transaction;
            processed.CommandText = "INSERT INTO Requests(RequestId, LicenseId, KeyHash, ReceivedAtUtc, Outcome, OutcomeDetails) VALUES ($request,$license,$hash,$received,'issued',$details);";
            processed.Parameters.AddWithValue("$request", request.RequestId);
            processed.Parameters.AddWithValue("$license", request.LicenseId);
            processed.Parameters.AddWithValue("$hash", request.DevicePublicKeyHash);
            processed.Parameters.AddWithValue("$received", now);
            processed.Parameters.AddWithValue("$details", $"deviceId={deviceId}; revision={revision}; activationFileId={activationFileId}");
            await processed.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using (var activationFile = connection.CreateCommand())
        {
            activationFile.Transaction = (SqliteTransaction)transaction;
            activationFile.CommandText = "INSERT INTO ActivationFiles(ActivationFileId, LicenseId, DeviceId, RequestId, Revision, Kid, IssuedAtUtc, ActivationExpiryUtc, ArchiveRef, FileSha256) VALUES ($id,$license,$device,$request,$revision,$kid,$issued,$expiry,$archive,$hash);";
            activationFile.Parameters.AddWithValue("$id", activationFileId);
            activationFile.Parameters.AddWithValue("$license", request.LicenseId);
            activationFile.Parameters.AddWithValue("$device", deviceId);
            activationFile.Parameters.AddWithValue("$request", request.RequestId);
            activationFile.Parameters.AddWithValue("$revision", revision);
            activationFile.Parameters.AddWithValue("$kid", keyId);
            activationFile.Parameters.AddWithValue("$issued", issuedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            activationFile.Parameters.AddWithValue("$expiry", activationExpiryUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            activationFile.Parameters.AddWithValue("$archive", archiveRef + "#activation.jws");
            activationFile.Parameters.AddWithValue("$hash", activationSha256);
            await activationFile.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (licenseStatus == "suspended")
        {
            await using var activate = connection.CreateCommand();
            activate.Transaction = (SqliteTransaction)transaction;
            activate.CommandText = "UPDATE Licenses SET Status='active' WHERE LicenseId=$license AND Status='suspended';";
            activate.Parameters.AddWithValue("$license", request.LicenseId);
            await activate.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await InsertAuditAsync(connection, (SqliteTransaction)transaction, request.LicenseId, "ProductionActivationIssued",
            $"requestId={request.RequestId}; deviceId={deviceId}; keyHash={request.DevicePublicKeyHash}; revision={revision}; keyId={keyId}; archive={archiveRef}", now, cancellationToken);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
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
