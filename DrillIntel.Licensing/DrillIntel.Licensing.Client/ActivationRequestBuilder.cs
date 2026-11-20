using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DrillIntel.Licensing.Core.Canonical;
using DrillIntel.Licensing.Core.Jws;
using DrillIntel.Licensing.Core.Models;

namespace DrillIntel.Licensing.Client;

public static class ActivationRequestBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>Creates a signed request using the caller's local device key; no issuer key is involved.</summary>
    public static string CreateSignedRequest(
        string licenseId,
        ECDsa devicePrivateKey,
        string keyProtection,
        DateTimeOffset nowUtc,
        out ActivationRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(licenseId);
        ArgumentNullException.ThrowIfNull(devicePrivateKey);
        if (keyProtection is not ("tpm" or "software"))
            throw new ArgumentOutOfRangeException(nameof(keyProtection), "Expected the informational value 'tpm' or 'software'.");

        var publicKey = devicePrivateKey.ExportSubjectPublicKeyInfo();
        var keyHash = DevicePublicKeyHash.Compute(publicKey);
        request = new ActivationRequest
        {
            RequestId = Guid.NewGuid().ToString("D"),
            LicenseId = licenseId,
            DevicePublicKey = Convert.ToBase64String(publicKey),
            DevicePublicKeyHash = keyHash,
            KeyProtection = keyProtection,
            TimestampUtc = nowUtc.ToUniversalTime()
        };

        var header = JsonSerializer.SerializeToUtf8Bytes(new { alg = "ES256", kid = keyHash, typ = "JWT" }, JsonOptions);
        var payload = JsonSerializer.SerializeToUtf8Bytes(request, JsonOptions);
        var encodedHeader = Base64Url.Encode(header);
        var encodedPayload = Base64Url.Encode(payload);
        var input = Encoding.ASCII.GetBytes(encodedHeader + "." + encodedPayload);
        var signature = devicePrivateKey.SignData(
            input,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{encodedHeader}.{encodedPayload}.{Base64Url.Encode(signature)}";
    }
}
