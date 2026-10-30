using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AgriSustain.Application.DTOs.Cart;
using AgriSustain.Application.Common;
using AgriSustain.Domain.Entities;
using Microsoft.Extensions.Options;

namespace AgriSustain.Application.Services;

public sealed class QuantityOptions
{
    public Dictionary<string, decimal> Steps { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kg"] = 1m, ["g"] = 1m, ["hộp"] = 1m, ["túi"] = 1m,
        ["quả"] = 1m, ["bó"] = 1m, ["lít"] = 1m
    };
}

public sealed class CartPricingService(IOptions<QuantityOptions> options, TimeProvider clock)
{
    public CartAvailabilityService Availability { get; } = new(options, clock);
    public decimal PriceLine(Product product, decimal quantity) =>
        RetailPricing.UnitPrice(product, quantity);

    public decimal CalculateSelectedSubtotal(IEnumerable<CartLineResponse> lines) =>
        lines.Where(line => line.IsSelected).Sum(line => line.Subtotal);

    public CartResponse Calculate(Cart? cart, IReadOnlyList<string>? messages = null,
        bool canProceed = false)
    {
        var lines = cart?.CartItems.OrderBy(x => x.AddedAt).ThenBy(x => x.CartItemId)
            .Select(item =>
            {
                var product = item.Product;
                bool available = Availability.ValidateLine(product, item.HarvestBatch)
                    && item.PreOrderCampaignId is null;
                decimal price = PriceLine(product, item.Quantity);
                return new CartLineResponse(item.CartItemId, product.ProductId,
                    item.HarvestBatchId, product.ProductName, product.FarmerProfile.FarmName,
                    product.FarmerProfileId, product.Unit, item.HarvestBatch?.BatchCode ?? "—",
                    product.ProductImages.OrderByDescending(x => x.IsPrimary)
                        .ThenBy(x => x.DisplayOrder).FirstOrDefault()?.ImageUrl,
                    item.Quantity, product.BaseRetailPrice, price, product.WholesalePrice,
                    product.WholesaleMinQuantity, decimal.Round(price * item.Quantity, 2),
                    item.HarvestBatch?.AvailableQuantity ?? 0, Availability.StepFor(product.Unit),
                    item.IsSelected && available, available,
                    available ? null : CartMessages.Unavailable(product.ProductName));
            }).ToList() ?? new List<CartLineResponse>();
        string Canonical(decimal amount) => amount.ToString("0.###", CultureInfo.InvariantCulture);
        string text = string.Join("|", lines.Where(x => x.IsSelected).Select(x =>
            $"{x.CartItemId}:{x.ProductId}:{x.HarvestBatchId}:{Canonical(x.Quantity)}:"
            + $"{Canonical(x.UnitPrice)}:"
            + $"{Canonical(x.QuantityStep)}:{x.Unit}:{Canonical(x.RetailPrice)}:"
                    + $"{Canonical(x.WholesalePrice ?? 0)}:"
                    + $"{Canonical(x.WholesaleMinQuantity ?? 0)}:{x.IsAvailable}"));
        string token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        return new CartResponse(lines, CalculateSelectedSubtotal(lines),
            lines.Count(x => x.IsSelected), token, canProceed, messages ?? Array.Empty<string>());
    }
}
