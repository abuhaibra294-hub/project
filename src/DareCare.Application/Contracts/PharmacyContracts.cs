using System.ComponentModel.DataAnnotations;

namespace DareCare.Application.Contracts;

public sealed class QuotePrescriptionRequest
{
    [Range(typeof(decimal), "0.01", "1000000")]
    public decimal Amount { get; set; }
    [StringLength(1500)]
    public string? Notes { get; set; }
}
