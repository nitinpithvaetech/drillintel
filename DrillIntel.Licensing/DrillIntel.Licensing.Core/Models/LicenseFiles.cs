namespace DrillIntel.Licensing.Core.Models;

public sealed record Entitlement
{
    public int SchemaVersion { get; init; } = 1;
    public required string LicenseId { get; init; }
    public required long Revision { get; init; }
    public required DateTimeOffset IssuedAtUtc { get; init; }
    public required string Customer { get; init; }
    public required string Rig { get; init; }
    public required string Plan { get; init; }
    public required DateTimeOffset StartUtc { get; init; }
    public required DateTimeOffset ExpiryUtc { get; init; }
    public int MaxDevices { get; init; } = 3;
    public IReadOnlyList<string> RevokedDeviceKeyHashes { get; init; } = [];
}

public sealed record Activation
{
    public int SchemaVersion { get; init; } = 1;
    public required string LicenseId { get; init; }
    public required string DevicePublicKeyHash { get; init; }
    public required DateTimeOffset ActivatedAtUtc { get; init; }
    public required DateTimeOffset ActivationExpiryUtc { get; init; }
}

public sealed record ActivationRequest
{
    public int SchemaVersion { get; init; } = 1;
    public required string RequestId { get; init; }
    public required string LicenseId { get; init; }
    public required string DevicePublicKey { get; init; }
    public required string DevicePublicKeyHash { get; init; }
    public required string KeyProtection { get; init; }
    public required DateTimeOffset TimestampUtc { get; init; }
}
