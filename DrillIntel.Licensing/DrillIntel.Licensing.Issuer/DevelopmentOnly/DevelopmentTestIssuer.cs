using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DrillIntel.Licensing.Core.Jws;

namespace DrillIntel.Licensing.Issuer.DevelopmentOnly;

/// <summary>
/// Development-only ephemeral signer for local end-to-end tests. Its public key travels with
/// the test bundle and is accepted only by the Debug verifier. It is not a production signer.
/// </summary>
public static class DevelopmentTestIssuer
{
    public const string KeyId = "dev-test-only-2026";
    public static ECDsa CreateEphemeralTestKey() => ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public static string Sign<T>(T payload, ECDsa key)
    {
        var header = Encoding.UTF8.GetBytes("{\"alg\":\"ES256\",\"kid\":\"dev-test-only-2026\"}");
        var body = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var encodedHeader = Base64Url.Encode(header);
        var encodedPayload = Base64Url.Encode(body);
        var signingInput = Encoding.ASCII.GetBytes(encodedHeader + "." + encodedPayload);
        var signature = key.SignData(signingInput, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{encodedHeader}.{encodedPayload}.{Base64Url.Encode(signature)}";
    }
}
