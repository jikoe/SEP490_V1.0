using AgriSustain.Domain.Entities;
using Xunit;

namespace AgriSustain.UnitTests.Finance;

/// <summary>UC-FIN-05 BR-FEE-03, BR-FEE-06 and BR-FEE-08.</summary>
public sealed class FeeCalculatorTests
{
    private static FeeType Type(long id, string code, string group = FeeType.GroupPlatformCommission,
        string refund = FeeType.RefundProportional) =>
        new() { FeeTypeId = id, FeeCode = code, FeeName = code, FeeGroup = group, RefundMode = refund };

    private static FeePolicy Policy(FeeType type, string method = FeePolicy.MethodPercentage, decimal? rate = 5,
        decimal? fixedAmount = null, long[]? categories = null, decimal? min = null, decimal? max = null,
        string @base = FeePolicy.BaseItemSubtotal)
    {
        var policy = new FeePolicy
        {
            FeeTypeId = type.FeeTypeId, FeeType = type, PolicyCode = type.FeeCode, Method = method, Rate = rate,
            FixedAmount = fixedAmount, MinAmount = min, MaxAmount = max, CalculationBase = @base, OrderType = "Retail",
            AppliesToAllCategories = categories is null, ApprovalStatus = FeePolicy.ApprovalApproved
        };
        foreach (long id in categories ?? [])
        {
            policy.Categories.Add(new FeePolicyCategory { CategoryId = id });
        }
        return policy;
    }

    [Fact(DisplayName = "UTC-FIN05-C01 A category rate overrides the all-categories rate per item (BR-FEE-03)")]
    public void CategoryPolicy_OverridesDefault()
    {
        var commission = Type(1, "COM");
        var lines = FeeCalculator.Calculate([new(0, 7, 100_000), new(1, 8, 50_000)], 160_000,
            [Policy(commission, rate: 5), Policy(commission, rate: 10, categories: [7])]);

        Assert.Equal(new[] { (0, 10_000m), (1, 2_500m) }, lines.Select(line => (line.ItemIndex, line.Amount)));
    }

    [Fact(DisplayName = "UTC-FIN05-C02 A fixed per-order fee is split by item value and rounded to whole VND")]
    public void FixedPerOrder_SplitByValue()
    {
        var lines = FeeCalculator.Calculate([new(0, 1, 20_000), new(1, 1, 10_000)], 40_000,
            [Policy(Type(2, "SVC", FeeType.GroupService), FeePolicy.MethodFixedPerOrder, null, 3_001)]);

        Assert.Equal(3_001m, lines.Sum(line => line.Amount));
        Assert.Equal(2_001m, lines[0].Amount);
    }

    [Fact(DisplayName = "UTC-FIN05-C03 Minimum and maximum apply per seller order")]
    public void MinAndMax_PerSellerOrder()
    {
        var small = FeeCalculator.Calculate([new(0, 1, 10_000)], 10_000, [Policy(Type(3, "PAY"), rate: 1, min: 500)]);
        var large = FeeCalculator.Calculate([new(0, 1, 10_000_000)], 10_000_000, [Policy(Type(3, "PAY"), rate: 2, max: 50_000)]);

        Assert.Equal(500m, small.Single().Amount);
        Assert.Equal(50_000m, large.Single().Amount);
    }

    [Fact(DisplayName = "UTC-FIN05-C04 Payment processing on the seller order total includes delivery")]
    public void SellerOrderTotal_IncludesDelivery()
    {
        var lines = FeeCalculator.Calculate([new(0, 1, 100_000)], 130_000,
            [Policy(Type(4, "PAY", FeeType.GroupPaymentProcessing), rate: 6, @base: FeePolicy.BaseSellerOrderTotal)]);

        Assert.Equal(7_800m, lines.Single().Amount);
    }

    [Fact(DisplayName = "UTC-FIN05-C05 Total fees never exceed the seller order amount")]
    public void TotalFees_Capped()
    {
        var lines = FeeCalculator.Calculate([new(0, 1, 1_000)], 1_000,
            [Policy(Type(5, "FIX"), FeePolicy.MethodFixedPerItem, null, 3_000)]);

        Assert.Equal(1_000m, lines.Sum(line => line.Amount));
    }

    [Theory(DisplayName = "UTC-FIN05-C06 Refund reverses fees by refund mode (BR-FEE-08)")]
    [InlineData(FeeType.RefundFull, 0.4, 1_000)]
    [InlineData(FeeType.RefundProportional, 0.4, 400)]
    [InlineData(FeeType.RefundNone, 0.4, 0)]
    public void Refund_ReversesByMode(string mode, double share, int reversed)
    {
        var fee = new OrderItemFee { Amount = 1_000, RefundMode = mode };

        fee.ReverseForRefund((decimal)share);

        Assert.Equal(reversed, fee.ReversedAmount);
    }

    [Fact(DisplayName = "UTC-FIN05-C07 A later, larger refund never lowers what was already reversed")]
    public void Refund_Monotonic()
    {
        var fee = new OrderItemFee { Amount = 1_000, RefundMode = FeeType.RefundProportional };

        fee.ReverseForRefund(0.5m);
        fee.ReverseForRefund(0.2m);
        fee.ReverseAll();

        Assert.Equal(1_000m, fee.ReversedAmount);
    }

    [Fact(DisplayName = "UTC-FIN05-C08 Policy state follows the approved dates (no background job needed)")]
    public void State_FollowsDates()
    {
        var today = new DateOnly(2026, 11, 7);
        var policy = new FeePolicy { ApprovalStatus = FeePolicy.ApprovalApproved, EffectiveFrom = today.AddDays(1) };

        Assert.Equal(FeePolicy.StateScheduled, policy.State(today));
        Assert.Equal(FeePolicy.StateActive, policy.State(today.AddDays(1)));
        policy.EffectiveTo = today.AddDays(3);
        Assert.Equal(FeePolicy.StateExpired, policy.State(today.AddDays(4)));
    }
}
