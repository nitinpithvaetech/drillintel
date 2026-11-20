using System.Security.Cryptography;

namespace DrillIntel.Licensing.Core.Jws;

/// <summary>Resolves issuer keys only. Device keys must never be added to this set.</summary>
public interface ITrustedIssuerKeySet
{
    ECDsa? FindPublicKey(string keyId);
}

public sealed class TrustedIssuerKeySet : ITrustedIssuerKeySet, IDisposable
{
    private readonly Dictionary<string, ECDsa> _keys;

    public TrustedIssuerKeySet(IEnumerable<KeyValuePair<string, byte[]>> subjectPublicKeyInfoByKeyId)
    {
        ArgumentNullException.ThrowIfNull(subjectPublicKeyInfoByKeyId);
        _keys = new(StringComparer.Ordinal);
        foreach (var (keyId, encodedKey) in subjectPublicKeyInfoByKeyId)
        {
            if (string.IsNullOrWhiteSpace(keyId) || encodedKey is null || encodedKey.Length == 0)
                throw new ArgumentException("Trusted issuer keys require a key ID and SPKI bytes.", nameof(subjectPublicKeyInfoByKeyId));

            var key = ECDsa.Create();
            key.ImportSubjectPublicKeyInfo(encodedKey, out var bytesRead);
            if (bytesRead != encodedKey.Length || !_keys.TryAdd(keyId, key))
            {
                key.Dispose();
                throw new ArgumentException("Issuer key data is duplicated or has trailing bytes.", nameof(subjectPublicKeyInfoByKeyId));
            }
        }
    }

    public ECDsa? FindPublicKey(string keyId) => _keys.GetValueOrDefault(keyId);

    public void Dispose()
    {
        foreach (var key in _keys.Values)
            key.Dispose();
        _keys.Clear();
    }
}
