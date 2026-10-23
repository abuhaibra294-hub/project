using DareCare.Domain;
using DareCare.Domain.Entities;
using Xunit;

namespace DareCare.Domain.Tests;

public sealed class SupportTicketTests
{
    [Fact]
    public void ValidTicket_HasOpenStatus_AndTrimsSubject()
    {
        var ticket = SupportTicket.Create(Guid.NewGuid(), SupportTicketCategory.Booking, "  Visit question  ");
        Assert.Equal("Visit question", ticket.Subject);
        Assert.Equal(SupportTicketStatus.Open, ticket.Status);
    }

    [Fact]
    public void TicketStatus_FollowsControlledLifecycle()
    {
        var ticket = SupportTicket.Create(Guid.NewGuid(), SupportTicketCategory.Other, "General assistance");
        ticket.ChangeStatus(SupportTicketStatus.InProgress);
        ticket.ChangeStatus(SupportTicketStatus.Resolved);
        ticket.ChangeStatus(SupportTicketStatus.InProgress);
        ticket.ChangeStatus(SupportTicketStatus.Closed);
        Assert.Throws<InvalidOperationException>(() => ticket.ChangeStatus(SupportTicketStatus.Open));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public void ShortOrEmptySubject_IsRejected(string subject) =>
        Assert.Throws<ArgumentException>(() =>
            SupportTicket.Create(Guid.NewGuid(), SupportTicketCategory.Other, subject));

    [Fact]
    public void Message_RejectsBlankAndTooLong()
    {
        var ticket = Guid.NewGuid();
        var user = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => SupportMessage.Create(ticket, user, "  ", false));
        Assert.Throws<ArgumentException>(() => SupportMessage.Create(ticket, user, new string('x', 4001), false));
        Assert.Equal("Hello", SupportMessage.Create(ticket, user, " Hello ", false).Body);
    }
}
