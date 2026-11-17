using DareCare.Api.Security;
using DareCare.Application.Contracts;
using DareCare.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DareCare.Api.Controllers;

[ApiController]
[Route("api/v1/auth/authenticator")]
[Authorize]
[EnableRateLimiting("auth")]
public sealed class AuthenticatorController(
    UserManager<ApplicationUser> users,
    AuthenticatorService authenticator,
    IConfiguration config) : ControllerBase
{
    [HttpPost("setup")]
    [ProducesResponseType<AuthenticatorSetupResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Setup(CancellationToken ct)
    {
        var user = await CurrentAsync();
        if (user is null) return Unauthorized();
        var setup = await authenticator.SetupAsync(user, ct);
        return setup is null ? Conflict(new { message = "Authenticator setup is unavailable. Reload and try again." }) : Ok(setup);
    }

    [HttpPost("enable")]
    [ProducesResponseType<AuthenticatorEnabledResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Enable([FromBody] AuthenticatorCodeRequest input, CancellationToken ct)
    {
        var user = await CurrentAsync();
        if (user is null) return Unauthorized();
        var result = await authenticator.EnableAsync(user, input.Code.Trim(), ct);
        return result is null ? BadRequest(new { message = "Invalid code or expired setup." }) : Ok(result);
    }

    [HttpPost("disable")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Disable([FromBody] AuthenticatorCodeRequest input, CancellationToken ct)
    {
        var user = await CurrentAsync();
        if (user is null) return Unauthorized();
        var result = await authenticator.DisableAsync(user, input.Code.Trim(), ct);
        return result is null ? BadRequest(new { message = "Invalid code or authenticator temporarily locked." }) : Ok(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CompleteAuthenticatorLogin([FromBody] AuthenticatorLoginRequest input, CancellationToken ct)
    {
        var result = await authenticator.FinishLoginAsync(input.ChallengeToken, input.Code.Trim(),
            config.GetValue<bool?>("Auth:RequireConfirmedEmail") ?? true, ct);
        return result is null ? Unauthorized(new { message = "Invalid code or expired sign-in challenge." }) : Ok(result);
    }

    private Task<ApplicationUser?> CurrentAsync() => users.FindByIdAsync(User.CurrentUserId().ToString());
}
