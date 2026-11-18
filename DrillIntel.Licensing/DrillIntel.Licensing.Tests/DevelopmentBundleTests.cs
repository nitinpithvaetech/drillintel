using DrillIntel.Licensing.Client;
using DrillIntel.Licensing.Core.Models;
using DrillIntel.Licensing.Issuer.DevelopmentOnly;
using DrillIntel.Licensing.Core.Jws;
using System.Security.Cryptography;
using System.Text;

namespace DrillIntel.Licensing.Tests;

public sealed class DevelopmentBundleTests
{
    [Fact]
    public async Task DevelopmentIssuerBundle_VerifiesAndBindsToDeviceHash()
    {
        var now = DateTimeOffset.UtcNow;
        const string deviceHash = "af1df909c417d2c8428bb4e8ffa780b7b2d378fd1da53050b712ce250b28b5e8";
        var entitlement = new Entitlement
        {
            LicenseId = "DIL-TEST-2026",
            Revision = 1,
            IssuedAtUtc = now,
            Customer = "Test Customer",
            Rig = "Rig Test",
            Plan = "6-month",
            StartUtc = now.AddMinutes(-1),
            ExpiryUtc = now.AddMonths(6),
            MaxDevices = 3
        };
        var activation = new Activation
        {
            LicenseId = entitlement.LicenseId,
            DevicePublicKeyHash = deviceHash,
            ActivatedAtUtc = now,
            ActivationExpiryUtc = now.AddDays(90)
        };

        using var issuerKey = DevelopmentTestIssuer.CreateEphemeralTestKey();
        var issuerPublicKey = issuerKey.ExportSubjectPublicKeyInfo();
        var entitlementJws = DevelopmentTestIssuer.Sign(entitlement, issuerKey);
        var activationJws = DevelopmentTestIssuer.Sign(activation, issuerKey);
        var pieces = entitlementJws.Split('.');
        using (var directKey = ECDsa.Create())
        {
            directKey.ImportSubjectPublicKeyInfo(issuerPublicKey, out _);
            Assert.True(directKey.VerifyData(Encoding.ASCII.GetBytes(pieces[0] + "." + pieces[1]),
                Base64Url.Decode(pieces[2]), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        }
        using (var keySet = new TrustedIssuerKeySet([
                   new KeyValuePair<string, byte[]>(DevelopmentTestIssuer.KeyId, issuerPublicKey)]))
        {
            var trusted = keySet.FindPublicKey(DevelopmentTestIssuer.KeyId)!;
            Assert.True(trusted.VerifyData(Encoding.ASCII.GetBytes(pieces[0] + "." + pieces[1]),
                Base64Url.Decode(pieces[2]), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
            JwsVerifier.VerifyWithPublicKey(entitlementJws, trusted, DevelopmentTestIssuer.KeyId);
            JwsVerifier.Verify(entitlementJws, keySet);
            JwsVerifier.Verify(activationJws, keySet);
        }
        var result = await DevelopmentBundleVerifier.VerifyAsync(entitlementJws, activationJws, issuerPublicKey, deviceHash, now);

        Assert.True(result.IsValid, result.FailureReason);
        Assert.Equal(entitlement.LicenseId, result.LicenseId);
        Assert.Equal(1, result.AcceptedRevision);
        Assert.Equal(entitlement.ExpiryUtc, result.LicenseExpiryUtc);
        Assert.Equal(activation.ActivationExpiryUtc, result.ActivationExpiryUtc);
    }
}
