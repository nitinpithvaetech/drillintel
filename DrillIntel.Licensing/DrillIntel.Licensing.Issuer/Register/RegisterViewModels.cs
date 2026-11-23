namespace DrillIntel.Licensing.Issuer.Register;

public sealed record CustomerSummary(
    long CustomerId,
    string Name,
    string? Contact,
    int RigCount,
    int LicenseCount);

public sealed record CustomerRecord(long CustomerId, string Name, string? Contact, string? Notes);
public sealed record RigRecord(long RigId, long CustomerId, string RigName);
public sealed record LicenseRecord(
    string LicenseId,
    long RigId,
    string RigName,
    string Plan,
    string StartUtc,
    string ExpiryUtc,
    int MaxDevices,
    string Status,
    long CurrentRevision);
public sealed record DeviceRecord(
    long DeviceId,
    string LicenseId,
    string KeyHash,
    string KeyProtection,
    string Status,
    string ActivatedAtUtc,
    string ActivationExpiryUtc,
    long? ReplacementForDeviceId);
public sealed record EntitlementRevisionRecord(
    string LicenseId,
    long Revision,
    string IssuedAtUtc,
    string Kid,
    string SignedFileRef,
    string FileSha256);
public sealed record ActivationFileRecord(
    string ActivationFileId,
    string LicenseId,
    long DeviceId,
    string? RequestId,
    long Revision,
    string Kid,
    string IssuedAtUtc,
    string ActivationExpiryUtc,
    string ArchiveRef,
    string FileSha256);
public sealed record RequestRecord(
    string RequestId,
    string LicenseId,
    string KeyHash,
    string ReceivedAtUtc,
    string Outcome,
    string? OutcomeDetails);
public sealed record PendingRequestRecord(
    string RequestId,
    string LicenseId,
    string KeyHash,
    string KeyProtection,
    string TimestampUtc,
    string ReceivedAtUtc,
    string SourceFile,
    string Status);
public sealed record PaymentRecord(
    string OrderRef,
    string LicenseId,
    long AmountMinor,
    string Currency,
    string Processor,
    string Status,
    string? PaidAtUtc)
{
    public string AmountDisplay => $"{Currency} {AmountMinor / 100m:N2}";
}
public sealed record AuditRecord(
    string TimestampUtc,
    string Operator,
    string Action,
    string? LicenseId,
    string? Details);

public sealed record CustomerWorkspace(
    CustomerRecord Customer,
    IReadOnlyList<RigRecord> Rigs,
    IReadOnlyList<LicenseRecord> Licenses,
    IReadOnlyList<DeviceRecord> Devices,
    IReadOnlyList<EntitlementRevisionRecord> EntitlementRevisions,
    IReadOnlyList<ActivationFileRecord> ActivationFiles,
    IReadOnlyList<RequestRecord> Requests,
    IReadOnlyList<PendingRequestRecord> RequestInbox,
    IReadOnlyList<PaymentRecord> Payments,
    IReadOnlyList<AuditRecord> AuditLog);
