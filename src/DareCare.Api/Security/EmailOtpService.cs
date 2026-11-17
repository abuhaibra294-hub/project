using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DareCare.Api.Services;
using DareCare.Infrastructure.Identity;

namespace DareCare.Api.Security;

public enum EmailOtpPurpose
{
    VerifyEmail,
    ResetPassword
}

public sealed class EmailOtpService(
    AuthStateStore store,
    IAccountEmailService email,
    IConfiguration configuration,
    TimeProvider clock)
{
    private const int LifetimeMinutes = 10;
    private const int MaxAttempts = 5;
    private static readonly TimeSpan SendInterval = TimeSpan.FromMinutes(1);
    private readonly byte[] key = HMACSHA256.HashData(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!),
        "DareCare.EmailOtp.v1"u8);

    public static bool IsCode(string code) => code.Length == 6 && code.All(char.IsAsciiDigit);

    public async Task SendAsync(ApplicationUser user, EmailOtpPurpose purpose, CancellationToken ct)
    {
        var name = StateName(purpose);
        var raw = await store.ReadAsync(user.Id, name, ct);
        var previous = raw is null ? null : JsonSerializer.Deserialize<OtpState>(raw);
        var now = clock.GetUtcNow();
        if (previous is not null && now - previous.SentAtUtc < SendInterval) return;

        var code = RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
        var salt = Guid.NewGuid();
        var next = new OtpState(salt, Convert.ToHexString(Digest(user, purpose, salt, code)), now, 0);
        var updated = JsonSerializer.Serialize(next);
        if (!await store.TryWriteAsync(user.Id, name, raw, updated, ct)) return;
        try
        {
            await email.SendOtpAsync(user, code, purpose, LifetimeMinutes, ct);
        }
        catch
        {
            // A failed delivery should not stop the user from trying again.
            await store.TryDeleteAsync(user.Id, name, updated, CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> TryConsumeAsync(ApplicationUser user, EmailOtpPurpose purpose, string code, CancellationToken ct)
    {
        if (!IsCode(code)) return false;
        var name = StateName(purpose);
        var raw = await store.ReadAsync(user.Id, name, ct);
        if (raw is null) return false;
        var state = JsonSerializer.Deserialize<OtpState>(raw)!;
        if (state.Hash is null || state.FailedAttempts >= MaxAttempts ||
            state.SentAtUtc.AddMinutes(LifetimeMinutes) <= clock.GetUtcNow()) return false;

        var valid = CryptographicOperations.FixedTimeEquals(Convert.FromHexString(state.Hash),
            Digest(user, purpose, state.Salt, code));
        var next = valid ? state with { Hash = null } : state with { FailedAttempts = state.FailedAttempts + 1 };
        var saved = await store.TryWriteAsync(user.Id, name, raw, JsonSerializer.Serialize(next), ct);
        return valid && saved;
    }

    private byte[] Digest(ApplicationUser user, EmailOtpPurpose purpose, Guid salt, string code) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{user.Id:N}:{user.SecurityStamp}:{purpose}:{salt:N}:{code}"));

    private static string StateName(EmailOtpPurpose purpose) => $"EmailOtp.{purpose}";
    private sealed record OtpState(Guid Salt, string? Hash, DateTimeOffset SentAtUtc, int FailedAttempts);
}
