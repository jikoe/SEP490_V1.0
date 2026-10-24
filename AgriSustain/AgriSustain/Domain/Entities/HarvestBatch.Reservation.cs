namespace AgriSustain.Domain.Entities;

public partial class HarvestBatch
{
    // Cart/fulfillment and checkout still use different saleability policies (Harvested status,
    // expiry on the same day); keep both until the team agrees on one rule.
    public bool IsSaleable(DateOnly today) => StockEnteredAt.HasValue
        && ActualHarvestDate.HasValue && ActualHarvestDate <= today
        && (!ExpirationDate.HasValue || ExpirationDate > today)
        && Status is "Available" or "Harvested" or "InStock";

    public bool IsReservableForCheckout(DateOnly today)
    {
        return Status is "Available" or "InStock"
            && ActualHarvestDate.HasValue && StockEnteredAt.HasValue
            && (!ExpirationDate.HasValue || ExpirationDate >= today);
    }

    public void Reserve(decimal quantity, DateOnly today)
    {
        if (quantity <= 0 || !IsReservableForCheckout(today) || AvailableQuantity < quantity)
        {
            throw new InvalidOperationException("The batch cannot satisfy this reservation.");
        }
        AvailableQuantity -= quantity;
        ReservedQuantity += quantity;
    }

    public void ConsumeReservation(decimal quantity)
    {
        if (quantity <= 0 || ReservedQuantity < quantity)
        {
            throw new InvalidOperationException("Reservation balance is insufficient.");
        }

        ReservedQuantity -= quantity;
    }

    public void ReleaseReservation(decimal quantity)
    {
        ConsumeReservation(quantity);
        AvailableQuantity += quantity;
    }
}
