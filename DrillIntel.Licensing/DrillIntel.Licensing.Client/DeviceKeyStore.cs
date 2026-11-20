using System.Security.Cryptography;
using System.Security.AccessControl;
using DrillIntel.Licensing.Core.Canonical;

namespace DrillIntel.Licensing.Client;

public sealed class DeviceKeySession : IDisposable
{
    private readonly CngKey _key;
    private readonly ECDsaCng _signingKey;

    internal DeviceKeySession(CngKey key, string protection, string? fallbackReason)
    {
        _key = key;
        _signingKey = new ECDsaCng(key);
        KeyProtection = protection;
        FallbackReason = fallbackReason;
        PublicKeySubjectPublicKeyInfo = _signingKey.ExportSubjectPublicKeyInfo();
        PublicKeyHash = DevicePublicKeyHash.Compute(PublicKeySubjectPublicKeyInfo);
    }

    public string KeyProtection { get; }
    public string? FallbackReason { get; }
    public byte[] PublicKeySubjectPublicKeyInfo { get; }
    public string PublicKeyHash { get; }
    public ECDsa SigningKey => _signingKey;

    public void Dispose()
    {
        _signingKey.Dispose();
        _key.Dispose();
    }
}

/// <summary>
/// Opens or creates one non-exportable, machine-wide P-256 signing key. It tries the TPM
/// Platform Crypto Provider first, then the software CNG provider. The key DACL grants
/// authenticated crew accounts read/execute access so each Windows profile on a rig can use it.
/// </summary>
public static class DeviceKeyStore
{
    public const string MachineKeyName = "DrillIntel.Licensing.DeviceKey.v1";
    private const int NteBadKeyset = unchecked((int)0x80090016);

    public static DeviceKeySession OpenOrCreate()
    {
        Exception? tpmFailure = null;
        try
        {
            var key = OpenOrCreateWithProvider(CngProvider.MicrosoftPlatformCryptoProvider);
            return new DeviceKeySession(key, "tpm", null);
        }
        catch (Exception ex) when (ex is CryptographicException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            tpmFailure = ex;
        }

        try
        {
            var key = OpenOrCreateWithProvider(CngProvider.MicrosoftSoftwareKeyStorageProvider);
            var message = $"TPM key unavailable; using a non-exportable software CNG key. {tpmFailure?.Message}";
            return new DeviceKeySession(key, "software", message);
        }
        catch (Exception softwareFailure) when (softwareFailure is CryptographicException or PlatformNotSupportedException or UnauthorizedAccessException)
        {
            throw new CryptographicException(
                "Could not open or create a machine-wide device key with either the TPM or software CNG provider. " +
                $"TPM: {tpmFailure?.Message} Software: {softwareFailure.Message}", softwareFailure);
        }
    }

    private static CngKey OpenOrCreateWithProvider(CngProvider provider)
    {
        try
        {
            return ValidateKey(CngKey.Open(MachineKeyName, provider, CngKeyOpenOptions.MachineKey));
        }
        catch (CryptographicException ex) when (ex.HResult == NteBadKeyset)
        {
            // The named key is absent in this provider. Other errors (including ACL denial)
            // surface to the caller instead of being mistaken for a missing key.
        }

        CngKey? created = null;
        try
        {
            var parameters = new CngKeyCreationParameters
            {
                Provider = provider,
                KeyCreationOptions = CngKeyCreationOptions.MachineKey,
                ExportPolicy = CngExportPolicies.None,
                KeyUsage = CngKeyUsages.Signing
            };
            parameters.Parameters.Add(new CngProperty(
                "Security Descr",
                GetCrewMachineKeySecurityDescriptor(),
                (CngPropertyOptions)4)); // DACL_SECURITY_INFORMATION for the CNG security-descriptor property.

            created = CngKey.Create(CngAlgorithm.ECDsaP256, MachineKeyName, parameters);
            return ValidateKey(created);
        }
        catch (CryptographicException createFailure)
        {
            created?.Dispose();
            // Another app instance can win key creation. Reopen only if that key now exists.
            try
            {
                return ValidateKey(CngKey.Open(MachineKeyName, provider, CngKeyOpenOptions.MachineKey));
            }
            catch (CryptographicException reopenFailure)
            {
                throw new CryptographicException(
                    $"Could not create the machine-wide key with provider '{provider}'. " +
                    $"Creation failed: {createFailure.Message} Reopen check: {reopenFailure.Message}",
                    createFailure);
            }
        }
    }

    private static CngKey ValidateKey(CngKey key)
    {
        if (key.Algorithm != CngAlgorithm.ECDsaP256 || key.KeySize != 256 || key.ExportPolicy != CngExportPolicies.None)
        {
            key.Dispose();
            throw new CryptographicException("The stored device key is not a non-exportable P-256 signing key.");
        }
        return key;
    }

    private static byte[] GetCrewMachineKeySecurityDescriptor()
    {
        // SYSTEM and local administrators keep full control. Authenticated Windows users can
        // open and use the private key, but the key's export policy still forbids export.
        var descriptor = new RawSecurityDescriptor("D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGX;;;AU)");
        var bytes = new byte[descriptor.BinaryLength];
        descriptor.GetBinaryForm(bytes, 0);
        return bytes;
    }
}
