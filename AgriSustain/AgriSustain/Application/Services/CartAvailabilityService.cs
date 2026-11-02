using AgriSustain.Application.Common;
using AgriSustain.Domain.Entities;
using Microsoft.Extensions.Options;
namespace AgriSustain.Application.Services;

public sealed class CartAvailabilityService(IOptions<QuantityOptions> options, TimeProvider clock)
{
    public decimal StepFor(string unit) => options.Value.Steps.TryGetValue(unit, out var step)
        && step is > 0 and <= 1000000 ? step : 1m;
    public bool ValidateQuantity(decimal quantity, string unit) => quantity is > 0 and <= 999999999m
        && decimal.Round(quantity, 3) == quantity && quantity % StepFor(unit) == 0;
    public decimal NormalizeQuantity(decimal quantity, string unit, decimal stock) =>
        decimal.Max(0, decimal.Floor(decimal.Min(quantity, decimal.Min(stock, 999999999m))
            / StepFor(unit)) * StepFor(unit));
    public decimal MaximumQuantity(HarvestBatch batch) => NormalizeQuantity(batch.AvailableQuantity,
        batch.Product.Unit, batch.AvailableQuantity);
    public bool ValidateLine(Product product, HarvestBatch? batch)
    {
        var today = DateOnly.FromDateTime(clock.GetUtcNow().AddHours(7).DateTime);
        return product.BaseRetailPrice > 0 && product.ApprovalStatus == "Approved" && product.IsPublished
            && product.Category.IsActive && product.FarmerProfile.VerificationStatus == "Approved"
            && product.FarmerProfile.User.Status == "Active" && batch is not null
            && batch.ProductId == product.ProductId && batch.IsSaleable(today) && MaximumQuantity(batch) > 0;
    }
    public IReadOnlyList<string> ReconcileAvailability(Cart? cart, bool selectedOnly = false)
    {
        var messages = new List<string>();
        foreach (var item in cart?.CartItems.Where(x => !selectedOnly || x.IsSelected).ToArray() ?? [])
        {
            if (item.PreOrderCampaignId is not null || !ValidateLine(item.Product, item.HarvestBatch))
            {
                item.DeselectUnavailable();
                messages.Add(CartMessages.Unavailable(item.Product.ProductName));
            }
            else if (item.HarvestBatch is { } batch)
            {
                decimal normalized = NormalizeQuantity(item.Quantity, item.Product.Unit, batch.AvailableQuantity);
                if (normalized <= 0)
                {
                    item.DeselectUnavailable();
                    messages.Add(CartMessages.Unavailable(item.Product.ProductName));
                }
                else if (normalized != item.Quantity)
                {
                    item.ChangeQuantity(normalized);
                    messages.Add(CartMessages.Adjusted(item.Product.ProductName, batch.AvailableQuantity,
                        item.Quantity, item.Product.Unit));
                }
            }
        }
        return messages;
    }
    public bool CompareSelection(string displayed, string refreshed) => displayed == refreshed;
}
