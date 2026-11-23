using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using DrillIntel.Licensing.Core.Jws;

namespace DrillIntel.Licensing.Client;

public static class ProductionIssuerTrust
{
    private const string ResourceName = "DrillIntel.Licensing.Client.ProductionIssuerPublicKey.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static TrustedIssuerKeySet LoadEmbedded()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName);
        if (stream is null)
            throw new InvalidOperationException(
                "This DrillIntel build has no production issuer public key configured. Build the Release installer with the public key exported by License Admin.");

        var config = JsonSerializer.Deserialize<ProductionIssuerPublicKeyDocument>(stream, JsonOptions)
            ?? throw new InvalidDataException("The embedded production issuer public key is invalid.");
        if (config.SchemaVersion != 1 || config.Algorithm != "ES256" || string.IsNullOrWhiteSpace(config.KeyId))
            throw new InvalidDataException("The embedded production issuer public key has unsupported metadata.");
        var key = Convert.FromBase64String(config.PublicKeySpki);
        using (var publicKey = ECDsa.Create())
        {
            publicKey.ImportSubjectPublicKeyInfo(key, out var bytesRead);
            if (bytesRead != key.Length || publicKey.KeySize != 256)
                throw new InvalidDataException("The embedded production issuer key must be a P-256 public key with no trailing data.");
        }
        return new TrustedIssuerKeySet([new KeyValuePair<string, byte[]>(config.KeyId, key)]);
    }

    private sealed record ProductionIssuerPublicKeyDocument(int SchemaVersion, string KeyId, string Algorithm, string PublicKeySpki);
}
