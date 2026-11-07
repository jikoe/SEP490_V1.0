namespace AgriSustain.Domain.Entities;

/// <summary>
/// Voucher use lifecycle: Reserved at checkout (counts towards the limits while the order is unpaid),
/// Applied once the order is paid (counted in Voucher.UsedCount), Released when the unpaid order is
/// cancelled or expires (BR-PV-10). A refund never releases an Applied use.
/// </summary>
public static class VoucherRedemptionStatus
{
    public const string Reserved = "Reserved";
    public const string Applied = "Applied";
    public const string Released = "Released";
}
