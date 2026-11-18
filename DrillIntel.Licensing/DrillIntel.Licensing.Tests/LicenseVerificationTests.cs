using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DrillIntel.Licensing.Core.Canonical;
using DrillIntel.Licensing.Core.Jws;
using DrillIntel.Licensing.Core.Models;
using DrillIntel.Licensing.Core.Verification;

namespace DrillIntel.Licensing.Tests;

public sealed class LicenseVerificationTests
{
    [Fact]
    public async Task ValidFiles_AcceptAndPersistRevisionAndTime()
    {
        using var issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var trustedKeys = new TrustedIssuerKeySet(
        [new KeyValuePair<string, byte[]>("issuer-test", issuerKey.ExportSubjectPublicKeyInfo())]);
        var state = new MemoryLicenseStateStore();
        var now = DateTimeOffset.Parse("2026-11-18T10:00:00Z");
        var deviceHash = "device-hash-test";
        var entitlement = CreateEntitlement(now, ["another-device"]);
        var activation = CreateActivation(now, deviceHash);
        var verifier = new LicenseVerificationService(trustedKeys, state);

        var result = await verifier.VerifyAsync(
            Sign(entitlement, issuerKey), Sign(activation, issuerKey), deviceHash, now);

        Assert.True(result.IsValid, result.FailureReason);
        Assert.True(result.StateWasPersisted);
        Assert.Equal(2, state.Current.AcceptedRevision);
        Assert.Equal(now, state.Current.GreatestSeenUtc);
    }

    [Fact]
    public async Task ExpiredActivation_IsRejected()
    {
        using var issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var trustedKeys = new TrustedIssuerKeySet(
        [new KeyValuePair<string, byte[]>("issuer-test", issuerKey.ExportSubjectPublicKeyInfo())]);
        var now = DateTimeOffset.Parse("2026-11-18T10:00:00Z");
        var verifier = new LicenseVerificationService(trustedKeys, new MemoryLicenseStateStore());

        var result = await verifier.VerifyAsync(
            Sign(CreateEntitlement(now, []), issuerKey),
            Sign(new Activation
            {
                LicenseId = "LIC-TEST-001",
                DevicePublicKeyHash = "device-hash-test",
                ActivatedAtUtc = now.AddDays(-20),
                ActivationExpiryUtc = now.AddSeconds(-1)
            }, issuerKey),
            "device-hash-test",
            now);

        Assert.False(result.IsValid);
        Assert.Contains("activation has expired", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RevokedDevice_IsRejected()
    {
        using var issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var trustedKeys = new TrustedIssuerKeySet(
        [new KeyValuePair<string, byte[]>("issuer-test", issuerKey.ExportSubjectPublicKeyInfo())]);
        var now = DateTimeOffset.Parse("2026-11-18T10:00:00Z");
        var verifier = new LicenseVerificationService(trustedKeys, new MemoryLicenseStateStore());

        var result = await verifier.VerifyAsync(
            Sign(CreateEntitlement(now, ["device-hash-test"]), issuerKey),
            Sign(CreateActivation(now, "device-hash-test"), issuerKey),
            "device-hash-test",
            now);

        Assert.False(result.IsValid);
        Assert.Contains("revoked", result.FailureReason, StringComparison.OrdinalIgnoreCase);
    }

    private static Entitlement CreateEntitlement(DateTimeOffset now, IReadOnlyList<string> revoked) => new()
    {
        LicenseId = "LIC-TEST-001",
        Revision = 2,
        IssuedAtUtc = now,
        Customer = "Test Customer",
        Rig = "Rig 1",
        Plan = "6-month",
        StartUtc = now.AddDays(-1),
        ExpiryUtc = now.AddDays(120),
        MaxDevices = 3,
        RevokedDeviceKeyHashes = revoked
    };

    private static Activation CreateActivation(DateTimeOffset now, string deviceHash) => new()
    {
        LicenseId = "LIC-TEST-001",
        DevicePublicKeyHash = deviceHash,
        ActivatedAtUtc = now.AddDays(-1),
        ActivationExpiryUtc = now.AddDays(60)
    };

    // This test-only signer is intentionally located in Tests. Production signing remains in Issuer.
    private static string Sign<T>(T payload, ECDsa key)
    {
        var header = Base64Url.Encode(Encoding.UTF8.GetBytes("{\"alg\":\"ES256\",\"kid\":\"issuer-test\"}"));
        var body = Base64Url.Encode(JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
        var input = Encoding.ASCII.GetBytes(header + "." + body);
        var signature = key.SignData(input, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return header + "." + body + "." + Base64Url.Encode(signature);
    }

    private sealed class MemoryLicenseStateStore : ILicenseStateStore
    {
        public LicenseLocalState Current { get; private set; } = new(0, null);

        public ValueTask<LicenseLocalState> ReadAsync(string licenseId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Current);

        public ValueTask WriteAsync(string licenseId, LicenseLocalState state, CancellationToken cancellationToken = default)
        {
            Current = state;
            return ValueTask.CompletedTask;
        }
    }
}
