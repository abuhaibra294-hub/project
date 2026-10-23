namespace DareCare.Domain;

public enum PharmacyOrderStatus
{
    Quoted = 0,
    Confirmed = 1,
    Preparing = 2,
    OutForDelivery = 3,
    Delivered = 4,
    Declined = 5
}
