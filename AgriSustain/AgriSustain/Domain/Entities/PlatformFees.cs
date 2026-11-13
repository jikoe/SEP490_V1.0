using System.Text.RegularExpressions;

namespace AgriSustain.Domain.Entities;

/// <summary>UC-FIN-05 fee deducted from Farmers (BR-FEE-01, BR-FEE-07, BR-FEE-09).</summary>
public sealed class FeeType
{
    public const string GroupPlatformCommission = "PlatformCommission";
    public const string GroupPaymentProcessing = "PaymentProcessing";
    public const string GroupService = "Service";
    public const string GroupTaxWithholding = "TaxWithholding";
    public const string GroupOther = "Other";

    public const string RefundFull = "Full";
    public const string RefundProportional = "Proportional";
    public const string RefundNone = "NonRefundable";

    public const string StatusDraft = "Draft";
    public const string StatusActive = "Active";

    public static readonly IReadOnlyList<string> Groups =
        [GroupPlatformCommission, GroupPaymentProcessing, GroupService, GroupTaxWithholding, GroupOther];
    public static readonly IReadOnlyList<string> RefundModes = [RefundFull, RefundProportional, RefundNone];

    private static readonly Regex s_code = new("^[A-Z0-9_]{3,30}$", RegexOptions.Compiled);

    public long FeeTypeId { get; set; }
    public string FeeCode { get; set; } = null!;
    public string FeeName { get; set; } = null!;
    public string? Description { get; set; }
    public string FeeGroup { get; set; } = null!;
    public string RefundMode { get; set; } = null!;
    public bool IsMandatory { get; set; }
    public string Status { get; set; } = StatusDraft;
    public DateTime CreatedAt { get; set; }
    public ICollection<FeePolicy> Policies { get; set; } = new List<FeePolicy>();

    /// <summary>
    /// Fee code format: 3–30 characters A–Z, 0–9 or underscore.
    /// </summary>
    /// <param name="code">Code to check.</param>
    /// <returns>True when valid.</returns>
    public static bool IsValidCode(string? code) => code is not null && s_code.IsMatch(code);

    /// <summary>BR-FEE-03: only commission and service fees may differ by product category.</summary>
    public bool AllowsCategoryScope => FeeGroup is GroupPlatformCommission or GroupService;

    /// <summary>BR-FEE-07: Tax Withholding is mandatory.</summary>
    public static bool IsMandatoryGroup(string group) => group == GroupTaxWithholding;
}

/// <summary>A versioned, approved rate of a fee type (BR-FEE-02 to BR-FEE-04).</summary>
public sealed class FeePolicy
{
    public const string MethodPercentage = "Percentage";
    public const string MethodFixedPerOrder = "FixedPerOrder";
    public const string MethodFixedPerItem = "FixedPerItem";
    public const string BaseItemSubtotal = "ItemSubtotal";
    public const string BaseSellerOrderTotal = "SellerOrderTotal";

    public const string ApprovalDraft = "Draft";
    public const string ApprovalPending = "PendingApproval";
    public const string ApprovalApproved = "Approved";
    public const string ApprovalRejected = "Rejected";

    public const string StateDraft = "Draft";
    public const string StatePending = "PendingApproval";
    public const string StateRejected = "Rejected";
    public const string StateScheduled = "Scheduled";
    public const string StateActive = "Active";
    public const string StateExpired = "Expired";

    public static readonly IReadOnlyList<string> Methods = [MethodPercentage, MethodFixedPerOrder, MethodFixedPerItem];
    public static readonly IReadOnlyList<string> Bases = [BaseItemSubtotal, BaseSellerOrderTotal];
    public static readonly IReadOnlyList<string> OrderTypes = ["Retail", "Wholesale", "Contract"];

    public long FeePolicyId { get; set; }
    public long FeeTypeId { get; set; }
    public FeeType FeeType { get; set; } = null!;
    public string PolicyCode { get; set; } = null!;
    public string Method { get; set; } = null!;
    public string CalculationBase { get; set; } = null!;
    public decimal? Rate { get; set; }
    public decimal? FixedAmount { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string OrderType { get; set; } = null!;
    public bool AppliesToAllCategories { get; set; } = true;
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    /// <summary>An end date waiting for Administrator approval (UC-FIN-05 Step 2 – End-date).</summary>
    public DateOnly? PendingEffectiveTo { get; set; }
    public string ApprovalStatus { get; set; } = ApprovalDraft;
    public string Reason { get; set; } = null!;
    public string? ReviewNote { get; set; }
    public long CreatedByUserId { get; set; }
    public long? ApprovedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string RequestKey { get; set; } = null!;
    public byte[] RowVersion { get; set; } = [];
    public ICollection<FeePolicyCategory> Categories { get; set; } = new List<FeePolicyCategory>();

    /// <summary>
    /// The lifecycle shown to staff. Scheduled / Active / Expired are derived from the approved
    /// dates, so a policy takes effect on its date without a background job.
    /// </summary>
    /// <param name="today">Current date in Vietnam.</param>
    /// <returns>Draft, PendingApproval, Rejected, Scheduled, Active or Expired.</returns>
    public string State(DateOnly today) => ApprovalStatus switch
    {
        ApprovalDraft => StateDraft,
        ApprovalPending => StatePending,
        ApprovalRejected => StateRejected,
        _ when EffectiveFrom > today => StateScheduled,
        _ when EffectiveTo is { } to && to < today => StateExpired,
        _ => StateActive
    };

    /// <summary>
    /// Whether the policy is approved and its period contains the date.
    /// </summary>
    /// <param name="date">Day to evaluate.</param>
    /// <returns>True when it applies to orders of that day.</returns>
    public bool IsActiveOn(DateOnly date) => ApprovalStatus == ApprovalApproved && EffectiveFrom <= date
        && (EffectiveTo is null || EffectiveTo >= date);

    /// <summary>
    /// True when both periods share at least one day.
    /// </summary>
    /// <param name="from">Start of the other period.</param>
    /// <param name="to">End of the other period, or null for open-ended.</param>
    /// <returns>True on overlap.</returns>
    public bool Overlaps(DateOnly from, DateOnly? to) =>
        EffectiveFrom <= (to ?? DateOnly.MaxValue) && from <= (EffectiveTo ?? DateOnly.MaxValue);

    /// <summary>
    /// Whether the policy applies to a category: always for an all-categories policy, otherwise only for its linked categories.
    /// </summary>
    /// <param name="categoryId">Category of the item, or null.</param>
    /// <returns>True when covered.</returns>
    public bool Covers(long? categoryId) => AppliesToAllCategories
        || categoryId is long id && Categories.Any(category => category.CategoryId == id);
}

public sealed class FeePolicyCategory
{
    public long FeePolicyId { get; set; }
    public FeePolicy FeePolicy { get; set; } = null!;
    public long CategoryId { get; set; }
}

/// <summary>Fee snapshot of one order item, stored when the order is placed (BR-FEE-05).</summary>
public sealed class OrderItemFee
{
    public long OrderItemFeeId { get; set; }
    public long OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;
    public long FeeTypeId { get; set; }
    public long FeePolicyId { get; set; }
    public string FeeCode { get; set; } = null!;
    public string FeeGroup { get; set; } = null!;
    public string RefundMode { get; set; } = null!;
    public long? CategoryId { get; set; }
    public decimal? RateSnapshot { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal Amount { get; set; }
    public decimal ReversedAmount { get; set; }

    /// <summary>
    /// Fee still charged to the Farmer after reversals.
    /// </summary>
    public decimal NetAmount => Amount - ReversedAmount;

    /// <summary>BR-FEE-08: a cancelled seller order carries no fee.</summary>
    public void ReverseAll() => ReversedAmount = Amount;

    /// <summary>
    /// BR-FEE-08: after a refund of <paramref name="refundedShare"/> (0..1) of the seller order,
    /// Full fees are reversed, Proportional fees are reversed for that share, Non-refundable fees stay.
    /// A reversal never decreases, so repeated refunds only add to it.
    /// </summary>
    /// <param name="refundedShare">Refunded part of the seller order, clamped to 0..1.</param>
    public void ReverseForRefund(decimal refundedShare)
    {
        decimal share = Math.Clamp(refundedShare, 0, 1);
        decimal target = RefundMode switch
        {
            FeeType.RefundFull => Amount,
            FeeType.RefundProportional => decimal.Round(Amount * share, 0, MidpointRounding.AwayFromZero),
            _ => 0
        };
        ReversedAmount = Math.Max(ReversedAmount, Math.Min(Amount, target));
    }
}

public partial class OrderItem
{
    public virtual ICollection<OrderItemFee> Fees { get; set; } = new List<OrderItemFee>();
}
