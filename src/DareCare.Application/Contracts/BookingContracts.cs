using System.ComponentModel.DataAnnotations;
using DareCare.Domain;

namespace DareCare.Application.Contracts;

public sealed class CreateBookingRequest
{
    public Guid NurseProfileId { get; set; }
    public Guid AvailabilitySlotId { get; set; }
    public Guid CareServiceId { get; set; }
    // Choose either a saved address or a free-text address, never both.
    public Guid? AddressId { get; set; }
    [StringLength(400)]
    public string? ServiceAddress { get; set; }
}

public sealed record BookingResponse(
    Guid Id,
    Guid PatientUserId,
    Guid NurseProfileId,
    Guid AvailabilitySlotId,
    string ServiceName,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string ServiceAddress,
    decimal PriceAtBooking,
    BookingStatus Status);

public sealed class SetApprovalRequest
{
    [Required]
    public bool? Approved { get; set; }
}
