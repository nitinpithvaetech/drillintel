using System.Security.Cryptography;
using System.Text.Json;

namespace DrillIntel.Licensing.Issuer.Signing;

public sealed record ProductionIssuerPublicKey(int SchemaVersion, string KeyId, string Algorithm, string PublicKeySpki);

internal sealed record ProductionIssuerPrivateFile(
    int SchemaVersion,
    string KeyId,
    string Algorithm,
    string PublicKeySpki,
    string EncryptedPrivateKeyPkcs8);

/// <summary>Encrypted, password-protected issuer key material. Never commit these files.</summary>
public sealed class ProductionIssuerKeyFile : IDisposable
{
    private const int Pbkdf2Iterations = 600_000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private readonly ECDsa _privateKey;

    private ProductionIssuerKeyFile(string keyId, byte[] publicKeySpki, ECDsa privateKey)
    {
        KeyId = keyId;
        PublicKeySpki = publicKeySpki;
        _privateKey = privateKey;
    }

    public string KeyId { get; }
    public byte[] PublicKeySpki { get; }

    public static ProductionIssuerPublicKey Create(string privateKeyPath, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPath);
        ValidatePassword(password);
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyId = $"issuer-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..30];
        var publicKey = key.ExportSubjectPublicKeyInfo();
        var encryptedPrivateKey = key.ExportEncryptedPkcs8PrivateKey(
            password.AsSpan(),
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, Pbkdf2Iterations));
        var privateFile = new ProductionIssuerPrivateFile(
            1, keyId, "ES256", Convert.ToBase64String(publicKey), Convert.ToBase64String(encryptedPrivateKey));

        var fullPath = Path.GetFullPath(privateKeyPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporaryPath = fullPath + ".tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, JsonSerializer.SerializeToUtf8Bytes(privateFile, JsonOptions));
            if (File.Exists(fullPath))
                throw new IOException("A signing key file already exists. Choose a new path; existing keys are never overwritten.");
            File.Move(temporaryPath, fullPath);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        return new ProductionIssuerPublicKey(1, keyId, "ES256", Convert.ToBase64String(publicKey));
    }

    public static ProductionIssuerPublicKey ExportPublicKey(string privateKeyPath, string password, string outputPath)
    {
        using var issuerKey = Open(privateKeyPath, password);
        var publicDocument = new ProductionIssuerPublicKey(1, issuerKey.KeyId, "ES256", Convert.ToBase64String(issuerKey.PublicKeySpki));
        WritePublicKey(outputPath, publicDocument);
        return publicDocument;
    }

    public static void WritePublicKey(string outputPath, ProductionIssuerPublicKey publicKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(publicKey);
        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(fullPath, JsonSerializer.SerializeToUtf8Bytes(publicKey, JsonOptions));
    }

    public static ProductionIssuerKeyFile Open(string privateKeyPath, string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(privateKeyPath);
        ValidatePassword(password);
        var source = JsonSerializer.Deserialize<ProductionIssuerPrivateFile>(File.ReadAllBytes(privateKeyPath))
            ?? throw new InvalidDataException("The signing key file is empty or invalid.");
        if (source.SchemaVersion != 1 || source.Algorithm != "ES256" ||
            string.IsNullOrWhiteSpace(source.KeyId) || source.KeyId.Length > 64)
            throw new InvalidDataException("The signing key file has unsupported metadata.");

        byte[] publicKeyBytes;
        byte[] encryptedPrivateKeyBytes;
        try
        {
            publicKeyBytes = Convert.FromBase64String(source.PublicKeySpki);
            encryptedPrivateKeyBytes = Convert.FromBase64String(source.EncryptedPrivateKeyPkcs8);
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException("The signing key file contains invalid key data.", ex);
        }

        var key = ECDsa.Create();
        try
        {
            key.ImportEncryptedPkcs8PrivateKey(password.AsSpan(), encryptedPrivateKeyBytes, out var bytesRead);
            if (bytesRead != encryptedPrivateKeyBytes.Length || key.KeySize != 256 ||
                !CryptographicOperations.FixedTimeEquals(key.ExportSubjectPublicKeyInfo(), publicKeyBytes))
                throw new CryptographicException("The private and public signing key parts do not match.");
            return new ProductionIssuerKeyFile(source.KeyId, publicKeyBytes, key);
        }
        catch
        {
            key.Dispose();
            throw;
        }
    }

    public string Sign<T>(T payload)
    {
        var header = JsonSerializer.SerializeToUtf8Bytes(new { alg = "ES256", kid = KeyId });
        var body = JsonSerializer.SerializeToUtf8Bytes(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var encodedHeader = DrillIntel.Licensing.Core.Jws.Base64Url.Encode(header);
        var encodedPayload = DrillIntel.Licensing.Core.Jws.Base64Url.Encode(body);
        var input = System.Text.Encoding.ASCII.GetBytes(encodedHeader + "." + encodedPayload);
        var signature = _privateKey.SignData(input, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"{encodedHeader}.{encodedPayload}.{DrillIntel.Licensing.Core.Jws.Base64Url.Encode(signature)}";
    }

    public void Dispose() => _privateKey.Dispose();

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
            throw new ArgumentException("Use a password of at least 12 characters to protect the production signing key.", nameof(password));
    }
}
