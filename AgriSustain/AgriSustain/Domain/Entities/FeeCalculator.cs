namespace AgriSustain.Domain.Entities;

/// <summary>One item of a seller order, as seen by the fee calculation.</summary>
public sealed record FeeItemInput(int Index, long? CategoryId, decimal Subtotal);

public sealed record FeeLineResult(int ItemIndex, FeePolicy Policy, decimal BaseAmount, decimal Amount);

/// <summary>
/// BR-FEE-03 and BR-FEE-06: works out every applicable fee of one seller order, per item. Amounts are
/// whole VND rounded half up; minimum and maximum apply per seller order; total fees never exceed the
/// seller order amount.
/// </summary>
public static class FeeCalculator
{
    public static IReadOnlyList<FeeLineResult> Calculate(IReadOnlyList<FeeItemInput> items,
        decimal sellerOrderTotal, IEnumerable<FeePolicy> activePolicies)
    {
        var results = new List<FeeLineResult>();
        if (items.Count == 0)
        {
            return results;
        }
        decimal itemsTotal = items.Sum(item => item.Subtotal);
        foreach (var byType in activePolicies.GroupBy(policy => policy.FeeTypeId))
        {
            var policies = byType.ToList();
            // A category-specific policy overrides the all-categories policy of the same fee type.
            var chosen = items
                .Select(item => (Item: item, Policy: policies.FirstOrDefault(p => !p.AppliesToAllCategories
                        && p.Covers(item.CategoryId)) ?? policies.FirstOrDefault(p => p.AppliesToAllCategories)))
                .Where(pair => pair.Policy is not null)
                .GroupBy(pair => pair.Policy!);
            foreach (var group in chosen)
            {
                var policy = group.Key;
                var groupItems = group.Select(pair => pair.Item).ToList();
                var raw = groupItems.Select(item =>
                {
                    decimal baseAmount = policy.CalculationBase == FeePolicy.BaseSellerOrderTotal && itemsTotal > 0
                        ? sellerOrderTotal * item.Subtotal / itemsTotal
                        : item.Subtotal;
                    decimal amount = policy.Method switch
                    {
                        FeePolicy.MethodPercentage => baseAmount * (policy.Rate ?? 0) / 100,
                        FeePolicy.MethodFixedPerItem => policy.FixedAmount ?? 0,
                        _ => 0
                    };
                    return (Item: item, Base: baseAmount, Amount: amount);
                }).ToList();
                if (policy.Method == FeePolicy.MethodFixedPerOrder)
                {
                    decimal groupTotal = groupItems.Sum(item => item.Subtotal);
                    raw = raw.Select(row => row with
                    {
                        Amount = groupTotal > 0 ? (policy.FixedAmount ?? 0) * row.Item.Subtotal / groupTotal
                            : (policy.FixedAmount ?? 0) / groupItems.Count
                    }).ToList();
                }
                decimal total = Money(raw.Sum(row => row.Amount));
                if (policy.MinAmount is { } min && total < min)
                {
                    total = min;
                }
                if (policy.MaxAmount is { } max && total > max)
                {
                    total = max;
                }
                var amounts = Allocate(total, raw.Select(row => row.Amount > 0 ? row.Amount : row.Item.Subtotal).ToList());
                results.AddRange(raw.Select((row, index) =>
                    new FeeLineResult(row.Item.Index, policy, Money(row.Base), amounts[index])));
            }
        }
        decimal fees = results.Sum(line => line.Amount);
        if (fees > sellerOrderTotal && fees > 0)
        {
            // Total fees never exceed the seller order amount.
            var capped = Allocate(Money(Math.Max(0, sellerOrderTotal)), results.Select(line => line.Amount).ToList());
            results = results.Select((line, index) => line with { Amount = capped[index] }).ToList();
        }
        return results;
    }

    public static decimal Money(decimal value) => decimal.Round(value, 0, MidpointRounding.AwayFromZero);

    /// <summary>Splits a whole-VND total by weight; the rounding remainder goes to the largest share.</summary>
    public static IReadOnlyList<decimal> Allocate(decimal total, IReadOnlyList<decimal> weights)
    {
        if (weights.Count == 0)
        {
            return [];
        }
        decimal sum = weights.Sum();
        var shares = weights.Select(weight => sum > 0 ? Money(total * weight / sum) : Money(total / weights.Count))
            .ToList();
        decimal remainder = total - shares.Sum();
        if (remainder != 0)
        {
            int largest = shares.IndexOf(shares.Max());
            shares[largest] += remainder;
        }
        return shares;
    }
}
