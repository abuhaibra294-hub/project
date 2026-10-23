using DareCare.Domain;
using DareCare.Domain.Entities;
using Xunit;

namespace DareCare.Domain.Tests;

public sealed class BookingTests
{
    [Fact]
    public void NewBooking_IsPending()
    {
        var booking = new Booking();
        Assert.Equal(BookingStatus.Pending, booking.Status);
    }

    [Fact]
    public void AcceptedVisit_CanProgressToCompletion()
    {
        var booking = new Booking();
        booking.TransitionTo(BookingStatus.Accepted);
        booking.TransitionTo(BookingStatus.InProgress);
        booking.TransitionTo(BookingStatus.Completed);
        Assert.Equal(BookingStatus.Completed, booking.Status);
    }

    [Theory]
    [InlineData(BookingStatus.Accepted)]
    [InlineData(BookingStatus.Rejected)]
    [InlineData(BookingStatus.Cancelled)]
    public void Pending_CanTransitionToSupportedOutcomes(BookingStatus next)
    {
        var booking = new Booking();
        booking.TransitionTo(next);
        Assert.Equal(next, booking.Status);
    }

    [Theory]
    [InlineData(BookingStatus.Completed)]
    [InlineData(BookingStatus.InProgress)]
    [InlineData(BookingStatus.Pending)]
    public void Pending_RejectsIllegalTransition(BookingStatus next)
    {
        var booking = new Booking();
        Assert.Throws<InvalidOperationException>(() => booking.TransitionTo(next));
    }

    [Fact]
    public void Completed_CannotBeCancelled()
    {
        var booking = new Booking();
        booking.TransitionTo(BookingStatus.Accepted);
        booking.TransitionTo(BookingStatus.InProgress);
        booking.TransitionTo(BookingStatus.Completed);
        Assert.Throws<InvalidOperationException>(() => booking.TransitionTo(BookingStatus.Cancelled));
    }
}
