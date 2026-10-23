using AgriSustain.Domain.Exceptions;

namespace AgriSustain.Domain.Entities.Ordering;

public sealed class HarvestBatch
{
    public long Id
    {
        get; init;
    }
    public long ProductId
    {
        get; init;
    }
    public required string Code
    {
        get; init;
    }
    public DateOnly? ExpirationDate
    {
        get; init;
    }
    public string Status { get; init; } = "Available";
    public decimal AvailableQuantity
    {
        get; private set;
    }
    public decimal ReservedQuantity
    {
        get; private set;
    }

    /// <summary>
    /// Creates a batch stock projection with nonnegative available quantity; refuses invalid
    /// inventory before it can enter the transaction.
    /// </summary>
    public HarvestBatch(decimal availableQuantity)
    {
        if (availableQuantity < 0)
        {
            throw new BusinessRuleException("Tồn kho không được âm.");
        }

        AvailableQuantity = availableQuantity;
    }

    /// <summary>
    /// Checks positive demand, available stock, sellable status and the Vietnam business-date
    /// expiry boundary without changing inventory.
    /// </summary>
    public bool CanReserve(decimal quantity, DateOnly today) =>
        quantity > 0 && AvailableQuantity >= quantity && Status == "Available"
        && (ExpirationDate == null || ExpirationDate >= today);

    /// <summary>
    /// Moves eligible quantity from available to reserved exactly once for the staged placement.
    /// Rejects insufficient/expired stock; SQL replacements still require a conditional update.
    /// </summary>
    public void Reserve(decimal quantity, DateOnly today)
    {
        if (!CanReserve(quantity, today))
        {
            throw new BusinessRuleException("Lô hàng không đủ điều kiện giữ hàng.");
        }

        AvailableQuantity -= quantity;
        ReservedQuantity += quantity;
    }

    /// <summary>
    /// Detaches mutable stock counters so failed placement can discard its reservations without
    /// affecting the committed batch.
    /// </summary>
    public HarvestBatch Copy() => (HarvestBatch)MemberwiseClone();

    /// <summary>
    /// Rehydrates both SQL counters without reserving stock again. The adapter supplies trusted
    /// persisted identity and eligibility fields; negative reserved quantity is rejected.
    /// </summary>
    public static HarvestBatch RestorePersisted(HarvestBatch batch, decimal reserved)
    {
        if (reserved < 0)
        {
            throw new BusinessRuleException("Tồn giữ không được âm.");
        }
        var restored = batch.Copy();
        restored.ReservedQuantity = reserved;
        return restored;
    }
}
