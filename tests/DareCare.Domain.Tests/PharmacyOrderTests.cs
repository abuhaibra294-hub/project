using DareCare.Domain;
using DareCare.Domain.Entities;
using Xunit;

namespace DareCare.Domain.Tests;

public sealed class PharmacyOrderTests
{
    [Fact]
    public void PharmacyOrderCompletesOnlyInTheCorrectSequence()
    {
        var order = new PharmacyOrder { QuoteAmount = 100m };
        var firstVersion = order.Version;
        order.TransitionTo(PharmacyOrderStatus.Confirmed);
        Assert.NotEqual(firstVersion, order.Version);
        order.TransitionTo(PharmacyOrderStatus.Preparing);
        order.TransitionTo(PharmacyOrderStatus.OutForDelivery);
        order.TransitionTo(PharmacyOrderStatus.Delivered);
        Assert.Equal(PharmacyOrderStatus.Delivered, order.Status);
    }

    [Fact]
    public void APatientCannotConfirmAfterDeclining()
    {
        var order = new PharmacyOrder();
        order.TransitionTo(PharmacyOrderStatus.Declined);
        Assert.Throws<InvalidOperationException>(() => order.TransitionTo(PharmacyOrderStatus.Confirmed));
    }

    [Fact]
    public void PharmacyCannotDispatchBeforePatientConfirmation()
    {
        var order = new PharmacyOrder();
        Assert.Throws<InvalidOperationException>(() => order.TransitionTo(PharmacyOrderStatus.OutForDelivery));
    }
}
