namespace AgriSustain.Domain.Entities;

/// <summary>
/// UC-PS-01 Manage Promotions and Vouchers. The displayed lifecycle (SRS entity Voucher:
/// Inactive → Active → Depleted · Expired, plus Scheduled before StartDate) is derived from the
/// stored IsActive flag, dates and usage; it is never stored separately.
/// </summary>
public partial class Voucher
{
    public const string TypePercentage = "Percentage";
    public const string TypeFixedAmount = "FixedAmount";

    public const string StatusActive = "Active";
    public const string StatusInactive = "Inactive";
    public const string StatusScheduled = "Scheduled";
    public const string StatusExpired = "Expired";
    public const string StatusDepleted = "Depleted";

    public string LifecycleStatus(DateTime now)
    {
        if (!IsActive)
        {
            return StatusInactive;
        }

        if (EndDate <= now)
        {
            return StatusExpired;
        }

        if (UsageLimit.HasValue && UsedCount >= UsageLimit)
        {
            return StatusDepleted;
        }

        return StartDate > now ? StatusScheduled : StatusActive;
    }

    /// <summary>Discount terms customers may already have received; frozen once the voucher is used.</summary>
    public bool HasSameTerms(string code, string discountType, decimal discountValue,
        decimal? maxDiscountAmount, decimal minOrderAmount) =>
        VoucherCode == code
        && DiscountType == discountType
        && DiscountValue == discountValue
        && MaxDiscountAmount == maxDiscountAmount
        && MinOrderAmount == minOrderAmount;
}
