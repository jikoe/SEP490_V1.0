namespace AgriSustain.Domain.Entities;

public static class RetailPricing
{
    public static decimal Money(decimal value) => decimal.Round(value, 0,
        MidpointRounding.AwayFromZero);

    /// <summary>
    /// Wholesale applies only to a valid offer (threshold above 1, price within the retail
    /// price). Cart and checkout both use this rule so the shown and charged prices agree.
    /// </summary>
    public static decimal UnitPrice(Product product, decimal quantity)
    {
        return product.WholesaleMinQuantity is > 1 && product.WholesalePrice is > 0
            && product.WholesalePrice <= product.BaseRetailPrice
            && quantity >= product.WholesaleMinQuantity
            ? product.WholesalePrice.Value : product.BaseRetailPrice;
    }

    /// <summary>The last share receives the rounding remainder, preserving exact totals.</summary>
    public static IReadOnlyList<decimal> Split(decimal total, IReadOnlyList<decimal> weights)
    {
        if (total < 0 || weights.Any(x => x < 0) || weights.Sum() <= 0)
        {
            throw new ArgumentException("Invalid monetary allocation.");
        }
        var shares = new List<decimal>();
        var remaining = total;
        for (var index = 0; index < weights.Count; index++)
        {
            var share = index == weights.Count - 1 ? remaining
                : Math.Min(remaining, Money(total * weights[index] / weights.Sum()));
            shares.Add(share);
            remaining -= share;
        }
        return shares;
    }
}
