using System.Security.Cryptography;
using System.Text;

namespace AgriSustain.Domain.Entities;

/// <summary>
/// UC-PS-01 Manage Promotions and Vouchers. The displayed lifecycle (BR-PV-06: Scheduled → Active →
/// Expired or Exhausted, plus a manual Deactivated) is derived from the stored IsActive flag, dates
/// and usage; it is never stored separately.
/// </summary>
public partial class Voucher
{
    public const string TypePercentage = "Percentage";
    public const string TypeFixedAmount = "FixedAmount";

    public const string StatusScheduled = "Scheduled";
    public const string StatusActive = "Active";
    public const string StatusExpired = "Expired";
    public const string StatusExhausted = "Exhausted";
    public const string StatusDeactivated = "Deactivated";

    public string LifecycleStatus(DateTime now)
    {
        if (!IsActive)
        {
            return StatusDeactivated;
        }

        if (EndDate <= now)
        {
            return StatusExpired;
        }

        if (UsageLimit.HasValue && UsedCount >= UsageLimit)
        {
            return StatusExhausted;
        }

        return StartDate > now ? StatusScheduled : StatusActive;
    }

    /// <summary>
    /// BR-PV-07: after the first use or reservation, or once the start time has passed, the
    /// economic terms are frozen so every customer gets the same conditions.
    /// </summary>
    public bool TermsLocked(DateTime now, int redemptions) =>
        UsedCount > 0 || redemptions > 0 || StartDate <= now;

    /// <summary>Terms that BR-PV-07 locks: code, discount, maximum, minimum, per-customer limit and start.</summary>
    public bool HasSameTerms(string code, string discountType, decimal discountValue,
        decimal? maxDiscountAmount, decimal minOrderAmount, int perCustomerLimit, DateTime startDate) =>
        VoucherCode == code
        && DiscountType == discountType
        && DiscountValue == discountValue
        && MaxDiscountAmount == maxDiscountAmount
        && MinOrderAmount == minOrderAmount
        && PerCustomerLimit == perCustomerLimit
        && StartDate == startDate;

    /// <summary>BR-PV-08: a deactivated voucher can come back only while it still has time and uses.</summary>
    public bool CanReactivate(DateTime now) =>
        EndDate > now && (!UsageLimit.HasValue || UsedCount < UsageLimit);

    /// <summary>
    /// Token of the values shown to Promotion Staff; a save sent with an older token was made on stale
    /// data (MSG-PV-15).
    /// </summary>
    public string Version()
    {
        string state = string.Join('|', VoucherCode, VoucherName, Description, DiscountType, DiscountValue,
            MaxDiscountAmount, MinOrderAmount, UsageLimit, PerCustomerLimit, UsedCount,
            StartDate.Ticks, EndDate.Ticks, IsActive);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(state)))[..16];
    }
}
