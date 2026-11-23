using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using DrillIntel.Licensing.Core.Verification;

namespace DrillIntel.Licensing.Client;

/// <summary>Imports production license packages and persists verified state for the current Windows user.</summary>
public static class ProductionActivationStore
{
    private const int MaximumEntryBytes = 128 * 1024;
    private static readonly string StoreDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DrillIntel", "Licensing");
    private static readonly string PackagePath = Path.Combine(StoreDirectory, "production-activation.dillicense");
    public static bool HasStoredPackage => File.Exists(PackagePath);

    public static async Task<LicenseVerificationResult> ImportAndVerifyAsync(
        string packagePath,
        string localDeviceKeyHash,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        var (entitlement, activation) = await ReadPackageAsync(packagePath, cancellationToken).ConfigureAwait(false);
        using var trustedKeys = ProductionIssuerTrust.LoadEmbedded();
        var state = new ProtectedFileLicenseStateStore();
        var result = await new LicenseVerificationService(trustedKeys, state)
            .VerifyAsync(entitlement, activation, localDeviceKeyHash, nowUtc, cancellationToken).ConfigureAwait(false);
        if (!result.IsValid)
            return result;
        if (!result.StateWasPersisted)
            return result with { IsValid = false, FailureReason = "The license was valid, but protected activation state could not be saved on this PC." };

        Directory.CreateDirectory(StoreDirectory);
        var temporaryPath = PackagePath + ".tmp";
        try
        {
            File.Copy(packagePath, temporaryPath, overwrite: true);
            File.Move(temporaryPath, PackagePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
        return result;
    }

    public static async Task<LicenseVerificationResult?> VerifyStoredAsync(
        string localDeviceKeyHash,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(PackagePath))
            return null;
        var (entitlement, activation) = await ReadPackageAsync(PackagePath, cancellationToken).ConfigureAwait(false);
        using var trustedKeys = ProductionIssuerTrust.LoadEmbedded();
        var result = await new LicenseVerificationService(trustedKeys, new ProtectedFileLicenseStateStore())
            .VerifyAsync(entitlement, activation, localDeviceKeyHash, nowUtc, cancellationToken).ConfigureAwait(false);
        return result.IsValid && !result.StateWasPersisted
            ? result with { IsValid = false, FailureReason = "The stored license state could not be refreshed securely on this PC." }
            : result;
    }

    private static async Task<(string Entitlement, string Activation)> ReadPackageAsync(string packagePath, CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
            if (!seen.Add(entry.FullName))
                throw new InvalidDataException("The license package contains duplicate entries.");
        var entitlement = await ReadTextAsync(archive, "entitlement.jws", cancellationToken).ConfigureAwait(false);
        var activation = await ReadTextAsync(archive, "activation.jws", cancellationToken).ConfigureAwait(false);
        return (entitlement, activation);
    }

    private static async Task<string> ReadTextAsync(ZipArchive archive, string name, CancellationToken cancellationToken)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidDataException($"License package is missing {name}.");
        if (entry.Length is <= 0 or > MaximumEntryBytes)
            throw new InvalidDataException($"License package entry {name} has an invalid size.");
        await using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
    }

    private sealed class ProtectedFileLicenseStateStore : ILicenseStateStore
    {
        private static readonly byte[] Entropy = "DrillIntel.ProductionLicenseState.v1"u8.ToArray();
        private static readonly string StatePath = Path.Combine(StoreDirectory, "production-license-state.bin");
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = false };

        public async ValueTask<LicenseLocalState> ReadAsync(string licenseId, CancellationToken cancellationToken = default)
        {
            await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var states = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
                return states.GetValueOrDefault(licenseId, new LicenseLocalState(0, null));
            }
            finally { Gate.Release(); }
        }

        public async ValueTask WriteAsync(string licenseId, LicenseLocalState state, CancellationToken cancellationToken = default)
        {
            await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var states = await ReadAllAsync(cancellationToken).ConfigureAwait(false);
                if (states.TryGetValue(licenseId, out var existing))
                {
                    state = new LicenseLocalState(
                        Math.Max(existing.AcceptedRevision, state.AcceptedRevision),
                        existing.GreatestSeenUtc is { } previous && (state.GreatestSeenUtc is null || previous > state.GreatestSeenUtc)
                            ? previous
                            : state.GreatestSeenUtc);
                }
                states[licenseId] = state;
                Directory.CreateDirectory(StoreDirectory);
                var clear = JsonSerializer.SerializeToUtf8Bytes(states, JsonOptions);
                var protectedBytes = ProtectedData.Protect(clear, Entropy, DataProtectionScope.CurrentUser);
                var temporaryPath = StatePath + ".tmp";
                try
                {
                    await File.WriteAllBytesAsync(temporaryPath, protectedBytes, cancellationToken).ConfigureAwait(false);
                    File.Move(temporaryPath, StatePath, overwrite: true);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(clear);
                    CryptographicOperations.ZeroMemory(protectedBytes);
                    if (File.Exists(temporaryPath))
                        File.Delete(temporaryPath);
                }
            }
            finally { Gate.Release(); }
        }

        private static async Task<Dictionary<string, LicenseLocalState>> ReadAllAsync(CancellationToken cancellationToken)
        {
            if (!File.Exists(StatePath))
                return new Dictionary<string, LicenseLocalState>(StringComparer.Ordinal);
            var protectedBytes = await File.ReadAllBytesAsync(StatePath, cancellationToken).ConfigureAwait(false);
            byte[]? clear = null;
            try
            {
                clear = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                return JsonSerializer.Deserialize<Dictionary<string, LicenseLocalState>>(clear, JsonOptions)
                    ?? new Dictionary<string, LicenseLocalState>(StringComparer.Ordinal);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(protectedBytes);
                if (clear is not null)
                    CryptographicOperations.ZeroMemory(clear);
            }
        }
    }
}
