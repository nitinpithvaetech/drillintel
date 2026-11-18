using System.Security.Cryptography;
using DrillIntel.Licensing.Client;
using DrillIntel.Licensing.Issuer.Requests;

namespace DrillIntel.Licensing.Tests;

public sealed class ActivationRequestTests
{
    [Fact]
    public void SignedRequest_VerifiesProofOfDeviceKeyPossession()
    {
        using var deviceKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var compact = ActivationRequestBuilder.CreateSignedRequest(
            "LIC-TEST-001", deviceKey, "software", DateTimeOffset.Parse("2026-11-18T10:00:00Z"), out var built);

        var verified = ActivationRequestVerifier.Verify(compact);

        Assert.Equal(built.RequestId, verified.RequestId);
        Assert.Equal(built.LicenseId, verified.LicenseId);
        Assert.Equal(built.DevicePublicKeyHash, verified.DevicePublicKeyHash);
        Assert.Equal("software", verified.KeyProtection);
    }

    [Fact]
    public void ModifiedRequestSignature_IsRejected()
    {
        using var deviceKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var compact = ActivationRequestBuilder.CreateSignedRequest(
            "LIC-TEST-001", deviceKey, "tpm", DateTimeOffset.UtcNow, out _);
        var pieces = compact.Split('.');
        pieces[2] = (pieces[2][0] == 'A' ? 'B' : 'A') + pieces[2][1..];

        Assert.Throws<CryptographicException>(() => ActivationRequestVerifier.Verify(string.Join('.', pieces)));
    }

    [Fact]
    public void ExportedRequestFile_CanBeVerifiedAndIsNeverOverwritten()
    {
        var directory = Path.Combine(Path.GetTempPath(), "DrillIntelRequestTests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "activation-request.jws");
        try
        {
            using var deviceKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var request = ActivationRequestFileExporter.Export(
                path, "LIC-TEST-001", deviceKey, "software", DateTimeOffset.Parse("2026-11-18T10:00:00Z"));

            var verified = ActivationRequestVerifier.Verify(File.ReadAllText(path));

            Assert.Equal(request.RequestId, verified.RequestId);
            Assert.Equal(request.DevicePublicKeyHash, verified.DevicePublicKeyHash);
            Assert.Throws<IOException>(() => ActivationRequestFileExporter.Export(
                path, "LIC-TEST-001", deviceKey, "software", DateTimeOffset.UtcNow));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }
}
