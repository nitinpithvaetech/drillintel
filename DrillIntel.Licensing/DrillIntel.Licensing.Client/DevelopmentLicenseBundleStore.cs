using System.IO.Compression;
using System.Security.Cryptography;
using DrillIntel.Licensing.Core.Verification;

namespace DrillIntel.Licensing.Client;

/// <summary>Local persistence for the Debug-only development licensing test flow.</summary>
public static class DevelopmentLicenseBundleStore
{
    private static readonly string StoreDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DrillIntel", "Licensing");
    private static readonly string StoredBundlePath = Path.Combine(StoreDirectory, "development-activation.diltest");

    public static void SaveVerifiedBundle(string sourceBundlePath)
    {
#if DEBUG
        Directory.CreateDirectory(StoreDirectory);
        var temporaryPath = StoredBundlePath + ".tmp";
        try
        {
            File.Copy(sourceBundlePath, temporaryPath, overwrite: true);
            File.Move(temporaryPath, StoredBundlePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
#else
        throw new InvalidOperationException("Development activation persistence is disabled in Release builds.");
#endif
    }

    public static async Task<LicenseVerificationResult?> VerifyStoredAsync(
        string localDevicePublicKeyHash,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
#if DEBUG
        if (!File.Exists(StoredBundlePath))
            return null;

        using var archive = ZipFile.OpenRead(StoredBundlePath);
        var entitlement = await ReadTextAsync(archive, "entitlement.jws", cancellationToken).ConfigureAwait(false);
        var activation = await ReadTextAsync(archive, "activation.jws", cancellationToken).ConfigureAwait(false);
        var publicKeyText = await ReadTextAsync(archive, "issuer-public.spki", cancellationToken).ConfigureAwait(false);
        byte[] issuerPublicKey;
        try { issuerPublicKey = Convert.FromBase64String(publicKeyText); }
        catch (FormatException ex) { throw new CryptographicException("The stored development bundle has an invalid issuer key.", ex); }

        return await DevelopmentBundleVerifier.VerifyAsync(
            entitlement, activation, issuerPublicKey, localDevicePublicKeyHash, nowUtc, cancellationToken).ConfigureAwait(false);
#else
        await Task.CompletedTask.ConfigureAwait(false);
        return null;
#endif
    }

    private static async Task<string> ReadTextAsync(ZipArchive archive, string entryName, CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry(entryName) ?? throw new InvalidDataException($"Stored bundle is missing {entryName}.");
        if (entry.Length > 128 * 1024)
            throw new InvalidDataException($"Stored bundle entry {entryName} is unexpectedly large.");
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }
}
