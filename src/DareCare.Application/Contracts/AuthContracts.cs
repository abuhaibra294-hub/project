using System.ComponentModel.DataAnnotations;

namespace DareCare.Application.Contracts;

public sealed class RegisterPatientRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";
    [Required, MinLength(12), MaxLength(128)]
    public string Password { get; set; } = "";
    [Required, Phone, StringLength(32)]
    public string PhoneNumber { get; set; } = "";
}

public sealed class RegisterNurseRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";
    [Required, MinLength(12), MaxLength(128)]
    public string Password { get; set; } = "";
    [Required, Phone, StringLength(32)]
    public string PhoneNumber { get; set; } = "";
    [Required, StringLength(80, MinimumLength = 3)]
    public string LicenseNumber { get; set; } = "";
    [Range(0, 60)]
    public int YearsOfExperience { get; set; }
}

public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = "";
    [Required]
    public string Password { get; set; } = "";
}

public sealed record AuthResponse(
    Guid UserId,
    string AccessToken,
    DateTime ExpiresAtUtc,
    IReadOnlyList<string> Roles,
    string RefreshToken,
    DateTime RefreshTokenExpiresAtUtc);
