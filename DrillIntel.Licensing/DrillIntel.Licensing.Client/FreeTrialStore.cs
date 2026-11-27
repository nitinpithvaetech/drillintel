using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DrillIntel.Licensing.Client;

public sealed record FreeTrialStatus(bool IsExpired, int DaysRemaining, DateTimeOffset ExpiresAtUtc);

/// <summary>Stores the first-run trial clock encrypted for the current Windows user.</summary>
public static class FreeTrialStore
{
    public const int TrialDurationDays = 7;
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("DrillIntel.FreeTrial.v1");
    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "DrillIntel", "Licensing", "FreeTrial.bin");

    private sealed record TrialState(DateTimeOffset StartedAtUtc, DateTimeOffset LastSeenAtUtc);

    public static FreeTrialStatus StartOrContinue(DateTimeOffset nowUtc)
    {
        nowUtc = nowUtc.ToUniversalTime();
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);

        TrialState state;
        if (!File.Exists(StatePath))
        {
            state = new TrialState(nowUtc, nowUtc);
            Save(state);
        }
        else
        {
            try
            {
                var protectedBytes = File.ReadAllBytes(StatePath);
                var bytes = ProtectedData.Unprotect(protectedBytes, Entropy, DataProtectionScope.CurrentUser);
                state = JsonSerializer.Deserialize<TrialState>(bytes)
                    ?? throw new InvalidDataException("Trial state is empty.");
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException or IOException)
            {
                // A missing/corrupt/unreadable existing trial record must not silently restart the trial.
                return new FreeTrialStatus(true, 0, nowUtc);
            }

            if (nowUtc < state.LastSeenAtUtc)
                return new FreeTrialStatus(true, 0, state.StartedAtUtc.AddDays(TrialDurationDays));

            state = state with { LastSeenAtUtc = nowUtc };
            Save(state);
        }

        var expiresAt = state.StartedAtUtc.AddDays(TrialDurationDays);
        var remaining = expiresAt - nowUtc;
        var daysRemaining = remaining <= TimeSpan.Zero ? 0 : (int)Math.Ceiling(remaining.TotalDays);
        return new FreeTrialStatus(daysRemaining == 0, daysRemaining, expiresAt);
    }

    private static void Save(TrialState state)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state);
        var protectedBytes = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        var temporaryPath = StatePath + ".tmp";
        File.WriteAllBytes(temporaryPath, protectedBytes);
        File.Move(temporaryPath, StatePath, overwrite: true);
    }
}
