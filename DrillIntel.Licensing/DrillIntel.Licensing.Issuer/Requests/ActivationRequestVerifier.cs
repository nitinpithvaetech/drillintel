using System.Security.Cryptography;
using System.Text.Json;
using DrillIntel.Licensing.Core.Canonical;
using DrillIntel.Licensing.Core.Jws;
using DrillIntel.Licensing.Core.Models;

namespace DrillIntel.Licensing.Issuer.Requests;

/// <summary>Validates proof of possession for requests before they reach the issuing workflow.</summary>
public static class ActivationRequestVerifier
{
    private static readonly JsonSerializerOptions PayloadOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false
    };

    public static ActivationRequest Verify(string compactJws)
    {
        var parts = compactJws.Split('.');
        if (parts.Length != 3 || parts.Any(string.IsNullOrEmpty))
            throw new CryptographicException("The request is not a compact JWS.");

        // The request must carry its own device key, so the issuer reads only that untrusted
        // key material first. All request fields remain untrusted until the signature verifies.
        using var payloadDocument = JsonDocument.Parse(Base64Url.Decode(parts[1]));
        var root = payloadDocument.RootElement;
        if (!root.TryGetProperty("devicePublicKey", out var publicKeyValue) || publicKeyValue.ValueKind != JsonValueKind.String ||
            !root.TryGetProperty("devicePublicKeyHash", out var hashValue) || hashValue.ValueKind != JsonValueKind.String)
            throw new CryptographicException("The request does not contain a device public key and hash.");

        byte[] spki;
        try { spki = Convert.FromBase64String(publicKeyValue.GetString()!); }
        catch (FormatException ex) { throw new CryptographicException("The device public key is not valid base64.", ex); }
        var computedHash = DevicePublicKeyHash.Compute(spki);
        var claimedHash = hashValue.GetString()!;
        if (!StringComparer.OrdinalIgnoreCase.Equals(computedHash, claimedHash))
            throw new CryptographicException("The request's device public-key hash does not match its key.");

        using var deviceKey = ECDsa.Create();
        try
        {
            deviceKey.ImportSubjectPublicKeyInfo(spki, out var bytesRead);
            if (bytesRead != spki.Length)
                throw new CryptographicException("The device public key has trailing data.");
        }
        catch (CryptographicException ex)
        {
            throw new CryptographicException("The device public key is not a valid SubjectPublicKeyInfo key.", ex);
        }

        var verified = JwsVerifier.VerifyWithPublicKey(compactJws, deviceKey, computedHash);
        var request = verified.DeserializePayload<ActivationRequest>(PayloadOptions);
        if (request.SchemaVersion != 1 || !Guid.TryParseExact(request.RequestId, "D", out _) ||
            string.IsNullOrWhiteSpace(request.LicenseId) || request.KeyProtection is not ("tpm" or "software") ||
            !StringComparer.OrdinalIgnoreCase.Equals(request.DevicePublicKeyHash, computedHash))
            throw new CryptographicException("The signed activation request has invalid or unsupported fields.");

        return request;
    }
}
