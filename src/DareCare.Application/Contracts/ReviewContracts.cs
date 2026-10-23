using System.ComponentModel.DataAnnotations;

namespace DareCare.Application.Contracts;

public sealed class CreateNurseReviewRequest
{
    [Range(1, 5)]
    public int Rating { get; set; }

    [StringLength(1000)]
    public string? Comment { get; set; }
}
