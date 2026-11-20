using System.Security.Cryptography;

namespace DrillIntel.Licensing.Core.Canonical;

public static class DevicePublicKeyHash
{
    /// <summary>Hashes the exact SubjectPublicKeyInfo DER bytes and returns lowercase hex.</summary>
    public static string Compute(ReadOnlySpan<byte> subjectPublicKeyInfoDer) =>
        Convert.ToHexString(SHA256.HashData(subjectPublicKeyInfoDer)).ToLowerInvariant();
}
