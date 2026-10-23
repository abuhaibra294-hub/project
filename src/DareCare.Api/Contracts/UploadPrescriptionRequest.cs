using System.ComponentModel.DataAnnotations;

namespace DareCare.Api.Contracts;

public sealed class UploadPrescriptionRequest
{
    [Required]
    public Guid PharmacyProfileId { get; set; }
    public Guid? BookingId { get; set; }
    [StringLength(1000)]
    public string? Notes { get; set; }
    [Required]
    public IFormFile File { get; set; } = null!;
}
