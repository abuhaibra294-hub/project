using System.ComponentModel.DataAnnotations;

namespace DareCare.Application.Contracts;

public sealed class UpdateAccountRequest
{
    [Required, StringLength(160, MinimumLength = 2)]
    public string FullName { get; set; } = string.Empty;
    [Required, Phone, StringLength(32)]
    public string PhoneNumber { get; set; } = string.Empty;
}

public sealed class UpdatePatientProfileRequest
{
    [StringLength(120)]
    public string? City { get; set; }
    [StringLength(400)]
    public string? DefaultAddress { get; set; }
    [StringLength(160)]
    public string? EmergencyContactName { get; set; }
    [Phone, StringLength(32)]
    public string? EmergencyContactPhone { get; set; }
}

public sealed class RefreshRequest
{
    [Required, StringLength(256, MinimumLength = 40)]
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;
    [Required, MinLength(12), MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class EmailRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
}

public sealed class EmailTokenRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
    [Required, StringLength(8192)]
    public string Code { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
    [Required, StringLength(8192)]
    public string Code { get; set; } = string.Empty;
    [Required, MinLength(12), MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class CreatePharmacyRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
    [Required, StringLength(160, MinimumLength = 2)]
    public string ContactName { get; set; } = string.Empty;
    [Required, Phone, StringLength(32)]
    public string PhoneNumber { get; set; } = string.Empty;
    [Required, StringLength(180, MinimumLength = 3)]
    public string BusinessName { get; set; } = string.Empty;
    [Required, StringLength(80, MinimumLength = 3)]
    public string LicenseNumber { get; set; } = string.Empty;
    [Required, StringLength(120)]
    public string City { get; set; } = string.Empty;
    [Required, StringLength(400)]
    public string Address { get; set; } = string.Empty;
}

public sealed class UpdatePharmacyRequest
{
    [Required, StringLength(180, MinimumLength = 3)]
    public string BusinessName { get; set; } = string.Empty;
    [Required, StringLength(120)]
    public string City { get; set; } = string.Empty;
    [Required, StringLength(400)]
    public string Address { get; set; } = string.Empty;
}

public sealed class ChangeUserStatusRequest
{
    [Required]
    public bool? IsActive { get; set; }
}
