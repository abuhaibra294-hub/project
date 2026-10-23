namespace DareCare.Domain.Entities;

// One immutable rating per completed visit. Review comments are PRIVATE to their patient author.
// The public profile and matching endpoints only expose aggregates, not patient data.
public sealed class NurseReview
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid BookingId { get; private set; }
    public Booking Booking { get; private set; } = null!;
    public Guid PatientUserId { get; private set; }
    public Guid NurseProfileId { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    private NurseReview() { } // EF Core

    public static NurseReview Create(Guid bookingId, Guid patientId, Guid nurseId,
        int rating, string? comment)
    {
        if (bookingId == Guid.Empty || patientId == Guid.Empty || nurseId == Guid.Empty)
            throw new ArgumentException("A review must refer to a booking, patient and nurse.");
        if (rating is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be 1 through 5.");
        comment = comment?.Trim();
        if (comment?.Length > 1000)
            throw new ArgumentException("Comment must be at most 1000 characters.", nameof(comment));
        return new NurseReview
        {
            BookingId = bookingId, PatientUserId = patientId,
            NurseProfileId = nurseId, Rating = rating,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment
        };
    }
}
