using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DrillIntel.Licensing.Core.Jws;

public sealed record VerifiedJws(byte[] Payload, string KeyId)
{
    public T DeserializePayload<T>(JsonSerializerOptions? options = null) =>
        JsonSerializer.Deserialize<T>(Payload, options)
        ?? throw new JsonException("The signed JWS payload was empty.");
}

/// <summary>Strict compact JWS ES256 verification over the original encoded header.payload bytes.</summary>
public static class JwsVerifier
{
    public static VerifiedJws Verify(string compactJws, ITrustedIssuerKeySet trustedKeys)
    {
        ArgumentNullException.ThrowIfNull(trustedKeys);
        return VerifyCore(compactJws, keyId => trustedKeys.FindPublicKey(keyId));
    }

    /// <summary>
    /// Verifies a device request with its carried device public key. License files must use
    /// the trusted-issuer overload above; never pass a device key to that trust set.
    /// </summary>
    public static VerifiedJws VerifyWithPublicKey(string compactJws, ECDsa publicKey, string expectedKeyId)
    {
        ArgumentNullException.ThrowIfNull(publicKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(expectedKeyId);
        return VerifyCore(compactJws, keyId => StringComparer.Ordinal.Equals(keyId, expectedKeyId) ? publicKey : null);
    }

    private static VerifiedJws VerifyCore(string compactJws, Func<string, ECDsa?> resolveKey)
    {
        var parts = compactJws.Split('.');
        if (parts.Length != 3 || parts.Any(string.IsNullOrEmpty))
            throw new CryptographicException("The file is not a three-part compact JWS.");

        byte[] headerBytes;
        byte[] signature;
        try
        {
            headerBytes = Base64Url.Decode(parts[0]);
            signature = Base64Url.Decode(parts[2]);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("The JWS contains invalid base64url data.", ex);
        }

        string keyId;
        using (var header = JsonDocument.Parse(headerBytes))
        {
            var root = header.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("alg", out var alg) || alg.GetString() != "ES256" ||
                !root.TryGetProperty("kid", out var kid) || kid.ValueKind != JsonValueKind.String)
                throw new CryptographicException("JWS requires alg ES256 and a key ID.");

            var seenHeaders = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!seenHeaders.Add(property.Name))
                    throw new CryptographicException("Duplicate JWS header parameters are not accepted.");

            if (root.TryGetProperty("crit", out var crit) && crit.ValueKind != JsonValueKind.Null)
                throw new CryptographicException("Critical JWS extensions are not supported.");

            foreach (var untrustedKeyHeader in new[] { "jwk", "jku", "x5u", "x5c" })
                if (root.TryGetProperty(untrustedKeyHeader, out _))
                    throw new CryptographicException("Key material in JWS headers is not accepted.");

            keyId = kid.GetString()!;
        }

        var verificationKey = resolveKey(keyId)
            ?? throw new CryptographicException($"Signing key '{keyId}' is not trusted for this file.");
        var signingInput = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
        if (signature.Length != 64 || !verificationKey.VerifyData(
                signingInput,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
            throw new CryptographicException("JWS signature verification failed.");

        // Payload decoding and parsing deliberately happen only after signature verification.
        try
        {
            return new VerifiedJws(Base64Url.Decode(parts[1]), keyId);
        }
        catch (FormatException ex)
        {
            throw new CryptographicException("The JWS payload has invalid base64url data.", ex);
        }
    }
}

public static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Decode(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Any(ch => !(char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_')))
            throw new FormatException("Base64url may contain only ASCII letters, digits, '-' and '_'.");
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
        return Convert.FromBase64String(base64);
    }
}
