using System.Security.Cryptography;
using System.Text.Json;
using DrillIntel.Licensing.Core.Jws;
using DrillIntel.Licensing.Core.Models;

namespace DrillIntel.Licensing.Core.Verification;

public sealed record LicenseLocalState(long AcceptedRevision, DateTimeOffset? GreatestSeenUtc);

public interface ILicenseStateStore
{
    ValueTask<LicenseLocalState> ReadAsync(string licenseId, CancellationToken cancellationToken = default);
    ValueTask WriteAsync(string licenseId, LicenseLocalState state, CancellationToken cancellationToken = default);
}

public sealed record LicenseVerificationResult(
    bool IsValid,
    string? FailureReason,
    string? LicenseId,
    long AcceptedRevision,
    DateTimeOffset? EffectiveUtc,
    bool StateWasPersisted,
    string? Plan = null,
    DateTimeOffset? LicenseExpiryUtc = null,
    DateTimeOffset? ActivationExpiryUtc = null);

/// <summary>Applies the client verification order using issuer keys only.</summary>
public sealed class LicenseVerificationService(
    ITrustedIssuerKeySet trustedIssuerKeys,
    ILicenseStateStore stateStore)
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false
    };

    public async Task<LicenseVerificationResult> VerifyAsync(
        string entitlementJws,
        string activationJws,
        string localDevicePublicKeyHash,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        nowUtc = nowUtc.ToUniversalTime();
        string? licenseId = null;
        string? plan = null;
        DateTimeOffset? licenseExpiryUtc = null;
        DateTimeOffset? activationExpiryUtc = null;
        long revision = 0;
        try
        {
            var verifiedEntitlement = JwsVerifier.Verify(entitlementJws, trustedIssuerKeys);
            var entitlement = verifiedEntitlement.DeserializePayload<Entitlement>(PayloadOptions);
            licenseId = entitlement.LicenseId;
            revision = entitlement.Revision;
            plan = entitlement.Plan;
            licenseExpiryUtc = entitlement.ExpiryUtc;
            ValidateEntitlement(entitlement);

            var state = await stateStore.ReadAsync(licenseId, cancellationToken).ConfigureAwait(false);
            if (entitlement.Revision < state.AcceptedRevision)
                throw new CryptographicException("This entitlement revision is older than the revision already accepted on this PC.");

            var verifiedActivation = JwsVerifier.Verify(activationJws, trustedIssuerKeys);
            var activation = verifiedActivation.DeserializePayload<Activation>(PayloadOptions);
            ValidateActivation(activation);
            activationExpiryUtc = activation.ActivationExpiryUtc;

            if (!StringComparer.Ordinal.Equals(entitlement.LicenseId, activation.LicenseId))
                throw new CryptographicException("The activation belongs to a different license.");
            if (!StringComparer.OrdinalIgnoreCase.Equals(activation.DevicePublicKeyHash, localDevicePublicKeyHash))
                throw new CryptographicException("The activation does not match this PC's device key.");
            if (entitlement.RevokedDeviceKeyHashes.Contains(localDevicePublicKeyHash, StringComparer.OrdinalIgnoreCase))
                throw new CryptographicException("This PC's device key has been revoked.");

            var effectiveUtc = state.GreatestSeenUtc is { } greatest && greatest > nowUtc ? greatest : nowUtc;
            if (effectiveUtc < entitlement.StartUtc)
                throw new CryptographicException("The license term has not started yet.");
            if (effectiveUtc >= entitlement.ExpiryUtc)
                throw new CryptographicException("The license term has expired.");
            if (effectiveUtc >= activation.ActivationExpiryUtc)
                throw new CryptographicException("This PC's activation has expired and must be refreshed.");

            var persistSucceeded = true;
            try
            {
                await stateStore.WriteAsync(
                    licenseId,
                    new LicenseLocalState(entitlement.Revision, effectiveUtc),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                // A local-state write problem is diagnostic; it must not stop safety features.
                persistSucceeded = false;
            }

            return new LicenseVerificationResult(true, null, licenseId, revision, effectiveUtc, persistSucceeded,
                plan, licenseExpiryUtc, activationExpiryUtc);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException or InvalidOperationException)
        {
            return new LicenseVerificationResult(false, ex.Message, licenseId, revision, null, false,
                plan, licenseExpiryUtc, activationExpiryUtc);
        }
    }

    private static void ValidateEntitlement(Entitlement entitlement)
    {
        if (entitlement.SchemaVersion != 1)
            throw new CryptographicException($"Unsupported entitlement schema version {entitlement.SchemaVersion}.");
        if (string.IsNullOrWhiteSpace(entitlement.LicenseId) || entitlement.Revision < 1 ||
            string.IsNullOrWhiteSpace(entitlement.Customer) || string.IsNullOrWhiteSpace(entitlement.Rig))
            throw new CryptographicException("The entitlement is missing required identity or revision fields.");
        if (string.IsNullOrWhiteSpace(entitlement.Plan) || entitlement.Plan.Length > 32 ||
            !System.Text.RegularExpressions.Regex.IsMatch(entitlement.Plan, @"^[1-9][0-9]{0,3}-(day|month)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant) ||
            entitlement.MaxDevices < 1)
            throw new CryptographicException("The entitlement contains an unsupported plan or device limit.");
        if (entitlement.StartUtc.Offset != TimeSpan.Zero || entitlement.ExpiryUtc.Offset != TimeSpan.Zero ||
            entitlement.IssuedAtUtc.Offset != TimeSpan.Zero || entitlement.ExpiryUtc <= entitlement.StartUtc)
            throw new CryptographicException("Entitlement times must be valid UTC values.");
    }

    private static void ValidateActivation(Activation activation)
    {
        if (activation.SchemaVersion != 1 || string.IsNullOrWhiteSpace(activation.LicenseId) ||
            string.IsNullOrWhiteSpace(activation.DevicePublicKeyHash) ||
            activation.ActivatedAtUtc.Offset != TimeSpan.Zero || activation.ActivationExpiryUtc.Offset != TimeSpan.Zero ||
            activation.ActivationExpiryUtc <= activation.ActivatedAtUtc)
            throw new CryptographicException("The activation is incomplete or contains invalid UTC times.");
    }
}
