namespace AgriSustain.Domain.Entities;

public partial class HarvestBatch
{
    public bool IsSaleable(DateOnly today) => StockEnteredAt.HasValue
        && ActualHarvestDate.HasValue && ActualHarvestDate <= today
        && (!ExpirationDate.HasValue || ExpirationDate > today)
        && Status is "Available" or "Harvested" or "InStock";

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

