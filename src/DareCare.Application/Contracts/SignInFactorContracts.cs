using System.ComponentModel.DataAnnotations;

namespace DareCare.Application.Contracts;

public sealed class GoogleSignInRequest
{
    [Required, StringLength(8192)]
    public string IdToken { get; set; } = "";
}

public sealed class GoogleLinkRequest
{
    [Required, StringLength(8192)]
    public string IdToken { get; set; } = "";
    [Required, StringLength(128)]
    public string CurrentPassword { get; set; } = "";
}

public sealed class AuthenticatorCodeRequest
{
    [Required, StringLength(32)]
    public string Code { get; set; } = "";
}

public sealed class AuthenticatorLoginRequest
{
    [Required, StringLength(128)]
    public string ChallengeToken { get; set; } = "";
    [Required, StringLength(32)]
    public string Code { get; set; } = "";
}

public sealed record AuthenticatorChallengeResponse(bool TwoFactorRequired, string ChallengeToken, DateTimeOffset ExpiresAtUtc);
public sealed record AuthenticatorSetupResponse(string SharedKey, string AuthenticatorUri, DateTimeOffset ExpiresAtUtc);
public sealed record AuthenticatorEnabledResponse(IReadOnlyList<string> RecoveryCodes, AuthResponse Session);
public sealed record GoogleSignInConfigResponse(string? WebClientId);
