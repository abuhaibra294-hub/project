using System.Security.Cryptography;
using System.Text.Json;
using DareCare.Application.Contracts;
using DareCare.Infrastructure.Identity;
using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using OtpNet;

namespace DareCare.Api.Security;

public sealed class AuthenticatorService(
    UserManager<ApplicationUser> users,
    JwtTokenService tokens,
    AuthStateStore store,
    DareCareDbContext db,
    IDataProtectionProvider protection,
    TimeProvider clock)
{
    private const string FactorName = "Authenticator";
    private const string ChallengeName = "AuthenticatorLogin";
    private const int MaxAttempts = 5;

    public async Task<AuthenticatorSetupResponse?> SetupAsync(ApplicationUser user, CancellationToken ct)
    {
        if (user.TwoFactorEnabled) return null;
        var raw = await store.ReadAsync(user.Id, FactorName, ct);
        var previous = UnprotectFactor(user.Id, raw);
        // If another request enabled the factor after our account read, leave it intact.
        if (previous?.SetupExpiresAtUtc is null && previous is not null) return null;

        var key = Base32Encoding.ToString(RandomNumberGenerator.GetBytes(20));
        var expiry = clock.GetUtcNow().AddMinutes(10);
        var next = new Factor(key, expiry, user.SecurityStamp, -1, [], 0, null);
        if (!await store.TryWriteAsync(user.Id, FactorName, raw, ProtectFactor(user.Id, next), ct)) return null;

        var label = Uri.EscapeDataString(user.Email ?? user.UserName ?? user.Id.ToString());
        return new(key, $"otpauth://totp/DareCare:{label}?secret={key}&issuer=DareCare&digits=6&period=30", expiry);
    }

    public async Task<AuthenticatorEnabledResponse?> EnableAsync(ApplicationUser user, string code, CancellationToken ct)
    {
        if (user.TwoFactorEnabled) return null;
        var raw = await store.ReadAsync(user.Id, FactorName, ct);
        var factor = UnprotectFactor(user.Id, raw);
        if (factor?.SetupExpiresAtUtc is not { } expiry || expiry <= clock.GetUtcNow() ||
            factor.SetupSecurityStamp != user.SecurityStamp || factor.FailedAttempts >= MaxAttempts) return null;
        if (!VerifyTotp(factor, code, out var step))
        {
            await RecordFailureAsync(user.Id, raw!, factor, ct);
            return null;
        }

        var codes = Enumerable.Range(0, 8)
            .Select(_ => Convert.ToHexString(RandomNumberGenerator.GetBytes(10))).ToArray();
        var next = factor with
        {
            SetupExpiresAtUtc = null, SetupSecurityStamp = null, LastStep = step,
            RecoveryHashes = codes.Select(RefreshTokenCodec.Hash).ToArray(), FailedAttempts = 0, LockedUntilUtc = null
        };

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await store.TryWriteAsync(user.Id, FactorName, raw, ProtectFactor(user.Id, next), ct) ||
            !await UpdateFactorStatusAsync(user, enabled: true, ct)) return null;
        var session = await tokens.IssueAsync(user, ct);
        await transaction.CommitAsync(ct);
        return new(codes, session);
    }

    public async Task<AuthResponse?> DisableAsync(ApplicationUser user, string code, CancellationToken ct)
    {
        if (!user.TwoFactorEnabled) return null;
        var raw = await store.ReadAsync(user.Id, FactorName, ct);
        var factor = UsableFactor(user, raw);
        if (factor is null) return null;
        if (VerifyFactor(factor, code) is null)
        {
            await RecordFailureAsync(user.Id, raw!, factor, ct);
            return null;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await store.TryDeleteAsync(user.Id, FactorName, raw!, ct) ||
            !await UpdateFactorStatusAsync(user, enabled: false, ct)) return null;
        var session = await tokens.IssueAsync(user, ct);
        await transaction.CommitAsync(ct);
        return session;
    }

    public async Task<AuthenticatorChallengeResponse?> StartLoginAsync(ApplicationUser user, CancellationToken ct)
    {
        var raw = await store.ReadAsync(user.Id, ChallengeName, ct);
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var challenge = new LoginChallenge(RefreshTokenCodec.Hash(token), clock.GetUtcNow().AddMinutes(5),
            user.SecurityStamp, user.TokenVersion);
        if (!await store.TryWriteAsync(user.Id, ChallengeName, raw, JsonSerializer.Serialize(challenge), ct))
            return null;
        return new(true, $"{user.Id:N}.{token}", challenge.ExpiresAtUtc);
    }

    public async Task<AuthResponse?> FinishLoginAsync(
        string challengeToken, string code, bool requireConfirmedEmail, CancellationToken ct)
    {
        var split = challengeToken.Split('.', 2);
        if (split.Length != 2 || !Guid.TryParseExact(split[0], "N", out var userId)) return null;
        var rawChallenge = await store.ReadAsync(userId, ChallengeName, ct);
        if (rawChallenge is null) return null;
        var challenge = JsonSerializer.Deserialize<LoginChallenge>(rawChallenge)!;
        if (challenge.ExpiresAtUtc <= clock.GetUtcNow() || !CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(challenge.TokenHash), Convert.FromHexString(RefreshTokenCodec.Hash(split[1]))))
            return null;

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is not { IsActive: true, IsPendingInvitation: false, TwoFactorEnabled: true } ||
            user.SecurityStamp != challenge.SecurityStamp || user.TokenVersion != challenge.TokenVersion ||
            (requireConfirmedEmail && !user.EmailConfirmed) || await users.IsLockedOutAsync(user)) return null;

        var rawFactor = await store.ReadAsync(userId, FactorName, ct);
        var factor = UsableFactor(user, rawFactor);
        if (factor is null) return null;
        var next = VerifyFactor(factor, code);
        if (next is null)
        {
            await RecordFailureAsync(userId, rawFactor!, factor, ct);
            return null;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (!await store.TryDeleteAsync(userId, ChallengeName, rawChallenge, ct) ||
            !await store.TryWriteAsync(userId, FactorName, rawFactor, ProtectFactor(userId, next), ct)) return null;
        if (user.AccessFailedCount != 0 && !(await users.ResetAccessFailedCountAsync(user)).Succeeded) return null;
        var session = await tokens.IssueAsync(user, ct);
        await transaction.CommitAsync(ct);
        return session;
    }

    private async Task<bool> UpdateFactorStatusAsync(ApplicationUser user, bool enabled, CancellationToken ct)
    {
        user.TokenVersion++;
        var updated = await users.SetTwoFactorEnabledAsync(user, enabled);
        if (!updated.Succeeded) return false;
        await tokens.RevokeAllAsync(user.Id, ct);
        return true;
    }

    private Factor? UsableFactor(ApplicationUser user, string? raw)
    {
        var factor = UnprotectFactor(user.Id, raw);
        return factor is not null && factor.SetupExpiresAtUtc is null &&
            !(factor.LockedUntilUtc > clock.GetUtcNow()) ? factor : null;
    }

    private Factor? VerifyFactor(Factor factor, string code)
    {
        if (VerifyTotp(factor, code, out var step))
            return factor with { LastStep = step, FailedAttempts = 0, LockedUntilUtc = null };

        code = code.Trim().ToUpperInvariant();
        if (code.Length != 20) return null;
        var hash = RefreshTokenCodec.Hash(code);
        if (!factor.RecoveryHashes.Contains(hash, StringComparer.Ordinal)) return null;
        return factor with
        {
            RecoveryHashes = factor.RecoveryHashes.Where(saved => saved != hash).ToArray(),
            FailedAttempts = 0, LockedUntilUtc = null
        };
    }

    private bool VerifyTotp(Factor factor, string code, out long step)
    {
        step = -1;
        return EmailOtpService.IsCode(code) && new Totp(Base32Encoding.ToBytes(factor.Key))
            .VerifyTotp(clock.GetUtcNow().UtcDateTime, code, out step, VerificationWindow.RfcSpecifiedNetworkDelay) &&
            step > factor.LastStep;
    }

    private Task<bool> RecordFailureAsync(Guid userId, string raw, Factor factor, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var attempts = factor.LockedUntilUtc is { } previous && previous <= now ? 1 : factor.FailedAttempts + 1;
        var next = factor with { FailedAttempts = attempts, LockedUntilUtc = attempts >= MaxAttempts ? now.AddMinutes(15) : null };
        return store.TryWriteAsync(userId, FactorName, raw, ProtectFactor(userId, next), ct);
    }

    private IDataProtector Protector(Guid userId) =>
        protection.CreateProtector("DareCare.Authenticator.v1", userId.ToString("N"));

    private string ProtectFactor(Guid userId, Factor factor) =>
        Protector(userId).Protect(JsonSerializer.Serialize(factor));

    private Factor? UnprotectFactor(Guid userId, string? value) =>
        value is null ? null : JsonSerializer.Deserialize<Factor>(Protector(userId).Unprotect(value));

    private sealed record Factor(string Key, DateTimeOffset? SetupExpiresAtUtc, string? SetupSecurityStamp,
        long LastStep, string[] RecoveryHashes, int FailedAttempts, DateTimeOffset? LockedUntilUtc);

    private sealed record LoginChallenge(string TokenHash, DateTimeOffset ExpiresAtUtc, string? SecurityStamp, int TokenVersion);
}
