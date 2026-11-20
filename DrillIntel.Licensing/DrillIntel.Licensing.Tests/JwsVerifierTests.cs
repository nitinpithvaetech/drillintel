using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DrillIntel.Licensing.Core.Jws;

namespace DrillIntel.Licensing.Tests;

public sealed class JwsVerifierTests
{
    // Fixed ES256 test vector, using the public test key from RFC 7515 Appendix A.3.
    // The RFC's example private key is test-only and is not used by production code.
    private const string KeyId = "issuer-test-1";
    private const string PublicKeySpkiBase64 = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEf83OJ3D2xF1Bg8vub9tLe1gHMzV76e8Tus9uPHvRVEXH8UTNG72bfocs3+257rn0s2ldbqkLJK2KRiMohYjlrQ==";
    private const string FixedCompactJws = "eyJhbGciOiJFUzI1NiIsImtpZCI6Imlzc3Vlci10ZXN0LTEifQ.eyJ2YWx1ZSI6ImZpeGVkLXZlY3RvciJ9.Eu3pDY9dmUWc-_l6okGqtJHIEqHg_UdrtlxeeonjfHnROvipeOU3xI5OhXb-PTREcEbqmOS7fGQjNw0U9VOglg";

    [Fact]
    public void FixedEs256Vector_VerifiesAndReturnsPayload()
    {
        using var keys = CreateTrustedKeys();

        var verified = JwsVerifier.Verify(FixedCompactJws, keys);
        using var payload = JsonDocument.Parse(verified.Payload);

        Assert.Equal(KeyId, verified.KeyId);
        Assert.Equal("fixed-vector", payload.RootElement.GetProperty("value").GetString());
    }

    [Fact]
    public void ModifiedPayload_IsRejected()
    {
        using var keys = CreateTrustedKeys();
        var pieces = FixedCompactJws.Split('.');
        pieces[1] = pieces[1][..^1] + (pieces[1][^1] == 'A' ? 'B' : 'A');

        Assert.Throws<CryptographicException>(() => JwsVerifier.Verify(string.Join('.', pieces), keys));
    }

    [Fact]
    public void UnknownIssuerKeyId_IsRejected()
    {
        using var keys = CreateTrustedKeys();
        const string unknownKidHeader = "eyJhbGciOiJFUzI1NiIsImtpZCI6Im5vdC10cnVzdGVkIn0";
        var pieces = FixedCompactJws.Split('.');
        pieces[0] = unknownKidHeader;

        Assert.Throws<CryptographicException>(() => JwsVerifier.Verify(string.Join('.', pieces), keys));
    }

    private static TrustedIssuerKeySet CreateTrustedKeys() => new(
    [
        new KeyValuePair<string, byte[]>(KeyId, Convert.FromBase64String(PublicKeySpkiBase64))
    ]);
}
