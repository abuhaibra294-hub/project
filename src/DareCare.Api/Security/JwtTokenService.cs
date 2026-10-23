using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DareCare.Application.Contracts;
using DareCare.Infrastructure.Identity;
using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace DareCare.Api.Security;

public sealed class JwtTokenService(
    IConfiguration configuration,
    UserManager<ApplicationUser> userManager,
    DareCareDbContext db)
{
    public async Task<AuthResponse> IssueAsync(ApplicationUser user, CancellationToken ct)
    {
        var (value, session) = NewRefreshSession(user);
        db.RefreshSessions.Add(session);
        await db.SaveChangesAsync(ct);
        return await BuildResponseAsync(user, value, session.ExpiresAtUtc);
    }

    // A refresh token can be used exactly once; rotation is protected by an atomic SQL update.
    public async Task<AuthResponse?> RotateAsync(string rawToken, CancellationToken ct)
    {
        if (!RefreshTokenCodec.HasPlausibleFormat(rawToken)) return null;
        var hash = RefreshTokenCodec.Hash(rawToken);
        var old = await db.RefreshSessions.AsNoTracking().SingleOrDefaultAsync(s => s.TokenHash == hash, ct);
        if (old is null) return null;

        if (old.RevokedAtUtc is not null)
        {
            // A rotated token presented again may be stolen. Revoke only this device's family.
            if (old.ReplacedByHash is not null)
                await RevokeFamilyAsync(old.FamilyId, ct);
            return null;
        }

        var now = DateTime.UtcNow;
        if (old.ExpiresAtUtc <= now) return null;
        var user = await userManager.FindByIdAsync(old.UserId.ToString());
        if (user is null || !user.IsActive || user.IsPendingInvitation ||
            old.TokenVersion != user.TokenVersion ||
            ((configuration.GetValue<bool?>("Auth:RequireConfirmedEmail") ?? true) && !user.EmailConfirmed) ||
            await userManager.IsLockedOutAsync(user)) return null;

        var (value, next) = NewRefreshSession(user, old.FamilyId);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var affected = await db.RefreshSessions
            .Where(s => s.Id == old.Id && s.RevokedAtUtc == null && s.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.RevokedAtUtc, now)
                .SetProperty(s => s.ReplacedByHash, next.TokenHash), ct);
        if (affected != 1)
        {
            await tx.RollbackAsync(ct);
            return null;
        }

        db.RefreshSessions.Add(next);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await BuildResponseAsync(user, value, next.ExpiresAtUtc);
    }

    public async Task RevokeDeviceAsync(Guid userId, string rawToken, CancellationToken ct)
    {
        if (!RefreshTokenCodec.HasPlausibleFormat(rawToken)) return;
        var hash = RefreshTokenCodec.Hash(rawToken);
        var session = await db.RefreshSessions.AsNoTracking()
            .Where(s => s.UserId == userId && s.TokenHash == hash)
            .Select(s => new { s.FamilyId }).SingleOrDefaultAsync(ct);
        if (session is not null) await RevokeFamilyAsync(session.FamilyId, ct);
    }

    public Task<int> RevokeAllAsync(Guid userId, CancellationToken ct) =>
        db.RefreshSessions.Where(s => s.UserId == userId && s.RevokedAtUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAtUtc, DateTime.UtcNow), ct);

    private Task<int> RevokeFamilyAsync(Guid familyId, CancellationToken ct) =>
        db.RefreshSessions.Where(s => s.FamilyId == familyId && s.RevokedAtUtc == null)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAtUtc, DateTime.UtcNow), ct);

    private (string Value, RefreshSession Session) NewRefreshSession(ApplicationUser user, Guid? familyId = null)
    {
        var raw = RefreshTokenCodec.Generate();
        var session = new RefreshSession
        {
            UserId = user.Id,
            FamilyId = familyId ?? Guid.NewGuid(),
            TokenVersion = user.TokenVersion,
            TokenHash = RefreshTokenCodec.Hash(raw),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(configuration.GetValue<int>("Auth:RefreshTokenDays", 14))
        };
        return (raw, session);
    }

    private async Task<AuthResponse> BuildResponseAsync(ApplicationUser user, string refreshToken, DateTime refreshExpiry)
    {
        var roles = await userManager.GetRolesAsync(user);
        var now = DateTime.UtcNow;
        var expiration = now.AddMinutes(configuration.GetValue<int>("Jwt:ExpiryMinutes", 15));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new("name", user.FullName),
            new("token_version", user.TokenVersion.ToString(System.Globalization.CultureInfo.InvariantCulture))
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        var jwt = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            notBefore: now,
            expires: expiration,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new AuthResponse(user.Id, new JwtSecurityTokenHandler().WriteToken(jwt),
            expiration, roles.ToList(), refreshToken, refreshExpiry);
    }
}
