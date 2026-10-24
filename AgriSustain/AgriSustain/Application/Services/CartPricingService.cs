using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AgriSustain.Application.DTOs.Cart;
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
    public decimal StepFor(string unit) => options.Value.Steps.TryGetValue(unit, out decimal step)
        && step is > 0 and <= 1000000 ? step : 1m;

    public bool IsQuantityValid(decimal quantity, string unit) => quantity is > 0 and <= 999999999m
        && quantity % StepFor(unit) == 0 && decimal.Round(quantity, 3) == quantity;

    public decimal MaximumQuantity(HarvestBatch batch) =>
        decimal.Floor(batch.AvailableQuantity / StepFor(batch.Product.Unit))
            * StepFor(batch.Product.Unit);

    public bool IsAvailable(Product product, HarvestBatch? batch)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().AddHours(7).DateTime);
        return product.BaseRetailPrice > 0 && product.ApprovalStatus == "Approved" && product.IsPublished
            && product.Category.IsActive && product.FarmerProfile.VerificationStatus == "Approved"
            && product.FarmerProfile.User.Status == "Active"
            && batch is not null && batch.ProductId == product.ProductId
            && batch.IsSaleable(today) && MaximumQuantity(batch) > 0;
    }

    public decimal UnitPrice(Product product, decimal quantity) =>
        RetailPricing.UnitPrice(product, quantity);

    public CartResponse Calculate(Cart? cart, IReadOnlyList<string>? messages = null,
        bool canProceed = false)
    {
        var lines = cart?.CartItems.OrderBy(x => x.AddedAt).ThenBy(x => x.CartItemId)
            .Select(item =>
            {
                var product = item.Product;
                bool available = IsAvailable(product, item.HarvestBatch)
                    && item.PreOrderCampaignId is null;
                decimal price = UnitPrice(product, item.Quantity);
                return new CartLineResponse(item.CartItemId, product.ProductId,
                    item.HarvestBatchId, product.ProductName, product.FarmerProfile.FarmName,
                    product.FarmerProfileId, product.Unit, item.HarvestBatch?.BatchCode ?? "—",
                    product.ProductImages.OrderByDescending(x => x.IsPrimary)
                        .ThenBy(x => x.DisplayOrder).FirstOrDefault()?.ImageUrl,
                    item.Quantity, product.BaseRetailPrice, price, product.WholesalePrice,
                    product.WholesaleMinQuantity, decimal.Round(price * item.Quantity, 2),
                    item.HarvestBatch?.AvailableQuantity ?? 0, StepFor(product.Unit),
                    item.IsSelected && available, available,
                    available ? null : "Không đủ hàng hoặc sản phẩm/lô không còn được bán (MSG88).");
            }).ToList() ?? new List<CartLineResponse>();
        string Canonical(decimal amount) => amount.ToString("0.###", CultureInfo.InvariantCulture);
        string text = string.Join("|", lines.Where(x => x.IsSelected).Select(x =>
            $"{x.CartItemId}:{x.ProductId}:{x.HarvestBatchId}:{Canonical(x.Quantity)}:"
            + $"{Canonical(x.UnitPrice)}:{Canonical(x.AvailableQuantity)}:"
            + $"{Canonical(x.QuantityStep)}:{x.Unit}:{Canonical(x.RetailPrice)}:"
                    + $"{Canonical(x.WholesalePrice ?? 0)}:"
                    + $"{Canonical(x.WholesaleMinQuantity ?? 0)}:{x.IsAvailable}"));
        string token = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
        return new CartResponse(lines, lines.Where(x => x.IsSelected).Sum(x => x.Subtotal),
            lines.Count(x => x.IsSelected), token, canProceed, messages ?? Array.Empty<string>());
    }
}



