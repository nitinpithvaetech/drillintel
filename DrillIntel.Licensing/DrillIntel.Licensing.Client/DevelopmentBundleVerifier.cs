using DrillIntel.Licensing.Core.Jws;
using DrillIntel.Licensing.Core.Verification;

namespace DrillIntel.Licensing.Client;

/// <summary>Accepts development bundles only in Debug builds. Never use this as production trust.</summary>
public static class DevelopmentBundleVerifier
{
    public const string TestKeyId = "dev-test-only-2026";

    public static async Task<LicenseVerificationResult> VerifyAsync(
        string entitlementJws,
        string activationJws,
        byte[] developmentIssuerPublicKeySpki,
        string localDevicePublicKeyHash,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
#if DEBUG
        using var trustedKeys = new TrustedIssuerKeySet([
            new KeyValuePair<string, byte[]>(TestKeyId, developmentIssuerPublicKeySpki)
        ]);
        return await new LicenseVerificationService(trustedKeys, new MemoryStateStore())
            .VerifyAsync(entitlementJws, activationJws, localDevicePublicKeyHash, nowUtc, cancellationToken)
            .ConfigureAwait(false);
#else
        return new LicenseVerificationResult(false,
            "Development test bundles are disabled in Release builds.", null, 0, null, false);
#endif
    }

    private sealed class MemoryStateStore : ILicenseStateStore
    {
        private readonly Dictionary<string, LicenseLocalState> _states = new(StringComparer.Ordinal);
        public ValueTask<LicenseLocalState> ReadAsync(string licenseId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_states.GetValueOrDefault(licenseId, new LicenseLocalState(0, null)));
        public ValueTask WriteAsync(string licenseId, LicenseLocalState state, CancellationToken cancellationToken = default)
        {
            _states[licenseId] = state;
            return ValueTask.CompletedTask;
        }
    }
}
