using System.Text;
using DareCare.Api.Security;
using DareCare.Api.Services;
using DareCare.Application.Contracts;
using DareCare.Domain;
using DareCare.Domain.Entities;
using DareCare.Infrastructure.Identity;
using DareCare.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace DareCare.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    DareCareDbContext db,
    JwtTokenService tokens,
    IAccountEmailService emails,
    IConfiguration config,
    ILogger<AuthController> logger) : ControllerBase
{
    private bool RequireEmailConfirmation =>
        config.GetValue<bool?>("Auth:RequireConfirmedEmail") ?? true;

    [HttpPost("register/patient")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RegisterPatient([FromBody] RegisterPatientRequest input, CancellationToken ct)
    {
        if (RequireEmailConfirmation && !emails.IsConfigured) return EmailUnavailable();
        var user = NewUser(input.Email, input.FullName, input.PhoneNumber);
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var result = await userManager.CreateAsync(user, input.Password);
            if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(x => x.Description) });
            var assigned = await userManager.AddToRoleAsync(user, AppRoles.Patient);
            if (!assigned.Succeeded) return Problem("Unable to create account role.", statusCode: 500);
            db.Patients.Add(new PatientProfile { UserId = user.Id });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        await SendVerificationIfConfiguredAsync(user, ct);
        return RequireEmailConfirmation
            ? Accepted(new { message = "Check your email to verify your account before signing in." })
            : Ok(await tokens.IssueAsync(user, ct));
    }

    [HttpPost("register/nurse")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RegisterNurse([FromBody] RegisterNurseRequest input, CancellationToken ct)
    {
        if (RequireEmailConfirmation && !emails.IsConfigured) return EmailUnavailable();
        var license = input.LicenseNumber.Trim();
        if (await db.Nurses.AnyAsync(n => n.LicenseNumber == license, ct))
            return Conflict(new { message = "License number is already registered." });
        var user = NewUser(input.Email, input.FullName, input.PhoneNumber);
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var result = await userManager.CreateAsync(user, input.Password);
            if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(x => x.Description) });
            var assigned = await userManager.AddToRoleAsync(user, AppRoles.Nurse);
            if (!assigned.Succeeded) return Problem("Unable to create account role.", statusCode: 500);
            db.Nurses.Add(new NurseProfile
            {
                UserId = user.Id, LicenseNumber = license,
                YearsOfExperience = input.YearsOfExperience, IsApproved = false
            });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException ex) when (DbConflict.IsUniqueViolation(ex))
            {
                return Conflict(new { message = "License number is already registered." });
            }
            await tx.CommitAsync(ct);
        }
        await SendVerificationIfConfiguredAsync(user, ct);
        return RequireEmailConfirmation
            ? Accepted(new { message = "Verify your email. Your nursing license must also be approved before receiving bookings." })
            : Ok(await tokens.IssueAsync(user, ct));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login([FromBody] LoginRequest input, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(input.Email.Trim());
        if (user is null || !user.IsActive || user.IsPendingInvitation)
            return Unauthorized(new { message = "Invalid credentials." });

        var signIn = await signInManager.CheckPasswordSignInAsync(user, input.Password, lockoutOnFailure: true);
        if (!signIn.Succeeded) return Unauthorized(new { message = "Invalid credentials." });
        if (RequireEmailConfirmation && !user.EmailConfirmed)
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Confirm your email before signing in." });
        return Ok(await tokens.IssueAsync(user, ct));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest input, CancellationToken ct)
    {
        var result = await tokens.RotateAsync(input.RefreshToken, ct);
        return result is null ? Unauthorized(new { message = "Invalid or expired refresh token." }) : Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest input, CancellationToken ct)
    {
        await tokens.RevokeDeviceAsync(User.CurrentUserId(), input.RefreshToken, ct);
        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAll(CancellationToken ct)
    {
        var user = await userManager.FindByIdAsync(User.CurrentUserId().ToString());
        if (user is null) return Unauthorized();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        user.TokenVersion++;
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded) return Problem("Could not end sessions.", statusCode: 500);
        await tokens.RevokeAllAsync(user.Id, ct);
        await tx.CommitAsync(ct);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest input, CancellationToken ct)
    {
        if (input.CurrentPassword == input.NewPassword)
            return BadRequest(new { message = "New password must be different from the current password." });
        var user = await userManager.FindByIdAsync(User.CurrentUserId().ToString());
        if (user is null) return Unauthorized();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var changed = await userManager.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
        if (!changed.Succeeded) return BadRequest(new { errors = changed.Errors.Select(e => e.Description) });
        user.TokenVersion++;
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded) return Problem("Could not update account.", statusCode: 500);
        await tokens.RevokeAllAsync(user.Id, ct);
        await tx.CommitAsync(ct);
        return NoContent(); // Every existing access/refresh token is now invalid; sign in again.
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var id = User.CurrentUserId();
        var user = await db.Users.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.Id, x.Email, x.FullName, x.PhoneNumber,
                x.EmailConfirmed, x.RegisteredAtUtc, x.IsActive }).SingleOrDefaultAsync(ct);
        if (user is null) return Unauthorized();
        var roles = await userManager.GetRolesAsync((await userManager.FindByIdAsync(id.ToString()))!);
        var nurseApproved = roles.Contains(AppRoles.Nurse)
            ? await db.Nurses.Where(n => n.UserId == id).Select(n => (bool?)n.IsApproved).FirstOrDefaultAsync(ct) : null;
        var pharmacyApproved = roles.Contains(AppRoles.Pharmacy)
            ? await db.Pharmacies.Where(p => p.UserId == id).Select(p => (bool?)p.IsApproved).FirstOrDefaultAsync(ct) : null;
        return Ok(new { user.Id, user.Email, user.FullName, user.PhoneNumber,
            user.EmailConfirmed, user.RegisteredAtUtc, user.IsActive, roles,
            nurseApproved, pharmacyApproved });
    }

    [HttpPut("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateAccountRequest input)
    {
        var user = await userManager.FindByIdAsync(User.CurrentUserId().ToString());
        if (user is null) return Unauthorized();
        user.FullName = input.FullName.Trim();
        user.PhoneNumber = input.PhoneNumber.Trim();
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        return Ok(new { user.Id, user.Email, user.FullName, user.PhoneNumber });
    }

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResendVerification([FromBody] EmailRequest input, CancellationToken ct)
    {
        if (!emails.IsConfigured) return EmailUnavailable();
        var user = await userManager.FindByEmailAsync(input.Email.Trim());
        if (user is { IsActive: true, EmailConfirmed: false, IsPendingInvitation: false })
            await TrySendAsync(() => emails.SendVerificationAsync(user, ct), user.Id);
        return Accepted(new { message = "If this account needs verification, an email will be sent." });
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyEmail([FromBody] EmailTokenRequest input)
    {
        if (!TryDecodeCode(input.Code, out var code))
            return BadRequest(new { message = "Invalid verification code." });
        var user = await userManager.FindByEmailAsync(input.Email.Trim());
        if (user is null || user.IsPendingInvitation)
            return BadRequest(new { message = "Invalid verification code." });
        if (user.EmailConfirmed) return Ok(new { message = "Email is already verified." });
        var result = await userManager.ConfirmEmailAsync(user, code);
        return result.Succeeded ? Ok(new { message = "Email verified. You can now sign in." })
            : BadRequest(new { message = "Invalid or expired verification code." });
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ForgotPassword([FromBody] EmailRequest input, CancellationToken ct)
    {
        if (!emails.IsConfigured) return EmailUnavailable();
        var user = await userManager.FindByEmailAsync(input.Email.Trim());
        if (user is not null && (user.IsActive || user.IsPendingInvitation))
            await TrySendAsync(() => emails.SendPasswordResetAsync(user, ct), user.Id);
        return Accepted(new { message = "If the account exists, password reset instructions will be emailed." });
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest input, CancellationToken ct)
    {
        if (!TryDecodeCode(input.Code, out var code)) return BadRequest(new { message = "Invalid or expired reset code." });
        var user = await userManager.FindByEmailAsync(input.Email.Trim());
        if (user is null || (!user.IsActive && !user.IsPendingInvitation))
            return BadRequest(new { message = "Invalid or expired reset code." });

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var result = await userManager.ResetPasswordAsync(user, code, input.NewPassword);
        if (!result.Succeeded) return BadRequest(new { message = "Invalid reset code or password does not meet the policy." });
        if (user.IsPendingInvitation)
        {
            // Email invitation token possession proves mailbox ownership for this invited account.
            user.IsActive = true;
            user.IsPendingInvitation = false;
            user.EmailConfirmed = true;
        }
        user.TokenVersion++;
        var updated = await userManager.UpdateAsync(user);
        if (!updated.Succeeded) return Problem("Could not finish password reset.", statusCode: 500);
        await tokens.RevokeAllAsync(user.Id, ct);
        await tx.CommitAsync(ct);
        return Ok(new { message = "Password updated. Sign in with the new password." });
    }

    private ApplicationUser NewUser(string email, string name, string phone) => new()
    {
        Email = email.Trim().ToLowerInvariant(),
        UserName = email.Trim().ToLowerInvariant(),
        FullName = name.Trim(),
        PhoneNumber = phone.Trim()
    };

    private async Task SendVerificationIfConfiguredAsync(ApplicationUser user, CancellationToken ct)
    {
        if (emails.IsConfigured) await TrySendAsync(() => emails.SendVerificationAsync(user, ct), user.Id);
    }

    private async Task TrySendAsync(Func<Task> send, Guid userId)
    {
        try { await send(); }
        catch (Exception ex) { logger.LogError(ex, "Account email delivery failed for user id {UserId}.", userId); }
    }

    private ObjectResult EmailUnavailable() => StatusCode(StatusCodes.Status503ServiceUnavailable,
        new { message = "Account email delivery is not configured. Ask the administrator to set SMTP settings." });

    private static bool TryDecodeCode(string encoded, out string code)
    {
        try
        {
            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded));
            return code.Length > 0;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            code = string.Empty;
            return false;
        }
    }
}
