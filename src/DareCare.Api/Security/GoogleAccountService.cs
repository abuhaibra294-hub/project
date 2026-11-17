using System.ComponentModel.DataAnnotations;
using DareCare.Domain;
using DareCare.Domain.Entities;
using DareCare.Infrastructure.Identity;
using DareCare.Infrastructure.Persistence;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DareCare.Api.Security;

public sealed record GoogleIdentity(string Subject, string Email, string? Name);

public interface IGoogleIdentityVerifier
{
    string? WebClientId { get; }
    bool IsConfigured { get; }
    Task<GoogleIdentity?> VerifyAsync(string token);
}

public sealed class GoogleIdentityVerifier(IConfiguration configuration, TimeProvider clock) : IGoogleIdentityVerifier
{
    private readonly string[] clientIds = new[]
        {
            configuration["Auth:Google:WebClientId"],
            configuration["Auth:Google:FlutterServerClientId"]
        }.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!.Trim()).Distinct().ToArray();

    public string? WebClientId => configuration["Auth:Google:WebClientId"]?.Trim() is { Length: > 0 } id ? id : null;
    public bool IsConfigured => clientIds.Length > 0;

    public async Task<GoogleIdentity?> VerifyAsync(string token)
    {
        if (!IsConfigured) return null;
        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(token, new() { Audience = clientIds });
        }
        catch (InvalidJwtException)
        {
            return null;
        }

        var now = clock.GetUtcNow().ToUnixTimeSeconds();
        if (payload.IssuedAtTimeSeconds is not { } issued || issued < now - 300 || issued > now + 30 ||
            !payload.EmailVerified || payload.Email is not { Length: <= 256 } email ||
            !new EmailAddressAttribute().IsValid(email) || payload.Subject is not { Length: > 0 and <= 255 })
            return null;

        // Google is authoritative for Gmail and verified Workspace mailboxes.
        if (!email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(payload.HostedDomain)) return null;
        return new(payload.Subject, email.Trim().ToLowerInvariant(), payload.Name);
    }
}

public sealed class GoogleAccountService(UserManager<ApplicationUser> users, DareCareDbContext db)
{
    private const string Provider = "Google";

    public async Task<(ApplicationUser? User, bool LinkRequired)> FindOrCreatePatientAsync(
        GoogleIdentity identity, CancellationToken ct)
    {
        var linked = await users.FindByLoginAsync(Provider, identity.Subject);
        if (linked is not null) return (linked, false);
        if (await users.FindByEmailAsync(identity.Email) is not null) return (null, true);

        var name = string.IsNullOrWhiteSpace(identity.Name) ? identity.Email.Split('@')[0] : identity.Name.Trim();
        var user = new ApplicationUser
        {
            Email = identity.Email, UserName = identity.Email, EmailConfirmed = true,
            FullName = name[..Math.Min(name.Length, 160)]
        };

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var created = await users.CreateAsync(user);
            if (!created.Succeeded)
                return (null, created.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"));
            var role = await users.AddToRoleAsync(user, AppRoles.Patient);
            if (!role.Succeeded) throw new InvalidOperationException("Could not assign the patient role.");
            var login = await users.AddLoginAsync(user, new UserLoginInfo(Provider, identity.Subject, Provider));
            if (!login.Succeeded) return (null, true);

            db.Patients.Add(new PatientProfile { UserId = user.Id });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return (user, false);
        }
        catch (DbUpdateException ex) when (DbConflict.IsUniqueViolation(ex))
        {
            return (null, true);
        }
    }

    public async Task<bool> IsPatientAsync(ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        return roles.Count == 1 && roles.Contains(AppRoles.Patient);
    }

    public async Task<bool> LinkAsync(ApplicationUser user, GoogleIdentity identity)
    {
        if (!user.EmailConfirmed || !string.Equals(user.Email, identity.Email, StringComparison.OrdinalIgnoreCase) ||
            !await IsPatientAsync(user)) return false;
        var current = (await users.GetLoginsAsync(user)).SingleOrDefault(login => login.LoginProvider == Provider);
        if (current is not null) return current.ProviderKey == identity.Subject;
        try
        {
            return (await users.AddLoginAsync(user, new UserLoginInfo(Provider, identity.Subject, Provider))).Succeeded;
        }
        catch (DbUpdateException ex) when (DbConflict.IsUniqueViolation(ex))
        {
            return false;
        }
    }
}
