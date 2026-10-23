using System.ComponentModel.DataAnnotations;
using DareCare.Domain;

namespace DareCare.Application.Contracts;

public sealed class SavePatientAddressRequest
{
    [Required, StringLength(50, MinimumLength = 2)]
    public string Label { get; set; } = string.Empty;
    [Required, StringLength(120, MinimumLength = 2)]
    public string City { get; set; } = string.Empty;
    [Required, StringLength(250, MinimumLength = 8)]
    public string FullAddress { get; set; } = string.Empty;
    [StringLength(300)]
    public string? Notes { get; set; }
    public bool IsDefault { get; set; }
}

public sealed record PatientAddressResponse(Guid Id, string Label, string City, string FullAddress,
    string? Notes, bool IsDefault, DateTime CreatedAtUtc);

public sealed class CreateSupportTicketRequest
{
    [Required, StringLength(160, MinimumLength = 5)]
    public string Subject { get; set; } = string.Empty;
    public SupportTicketCategory Category { get; set; } = SupportTicketCategory.Other;
    [Required, StringLength(4000, MinimumLength = 10)]
    public string Message { get; set; } = string.Empty;
}

public sealed class ReplySupportTicketRequest
{
    [Required, StringLength(4000, MinimumLength = 1)]
    public string Message { get; set; } = string.Empty;
}

public sealed class UpdateSupportStatusRequest
{
    [Required]
    public SupportTicketStatus? Status { get; set; }
}
