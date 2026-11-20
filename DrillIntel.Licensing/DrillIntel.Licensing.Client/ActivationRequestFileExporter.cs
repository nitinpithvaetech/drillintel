using System.Text;
using System.Security.Cryptography;
using DrillIntel.Licensing.Core.Models;

namespace DrillIntel.Licensing.Client;

public static class ActivationRequestFileExporter
{
    /// <summary>Writes a compact signed request without overwriting an earlier request file.</summary>
    public static ActivationRequest Export(
        string outputPath,
        string licenseId,
        DeviceKeySession deviceKey,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(deviceKey);
        return Export(outputPath, licenseId, deviceKey.SigningKey, deviceKey.KeyProtection, nowUtc);
    }

    public static ActivationRequest Export(
        string outputPath,
        string licenseId,
        ECDsa devicePrivateKey,
        string keyProtection,
        DateTimeOffset nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(devicePrivateKey);
        var fullPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var jws = ActivationRequestBuilder.CreateSignedRequest(
            licenseId, devicePrivateKey, keyProtection, nowUtc, out var request);

        using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(jws);
        writer.Flush();
        stream.Flush(flushToDisk: true);
        return request;
    }
}
