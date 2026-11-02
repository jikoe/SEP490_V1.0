namespace AgriSustain.Domain.Entities;
public partial class Cart
{
    public CartItem? FindLine(long productId, long? batchId) => CartItems.SingleOrDefault(
        item => item.ProductId == productId && item.HarvestBatchId == batchId && item.PreOrderCampaignId == null);
    public void RemoveLines(IEnumerable<long> itemIds) {
        var ids = itemIds.ToHashSet();
        foreach (var item in CartItems.Where(item => ids.Contains(item.CartItemId)).ToArray()) CartItems.Remove(item);
    }
}
