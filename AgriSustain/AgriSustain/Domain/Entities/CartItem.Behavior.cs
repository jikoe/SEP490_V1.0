namespace AgriSustain.Domain.Entities;
public partial class CartItem
{
    public void ChangeQuantity(decimal quantity)
    {
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Quantity = quantity;
    }
    public void MergeQuantity(decimal quantity) => ChangeQuantity(Quantity + quantity);
    public void SetSelected(bool selected) => IsSelected = selected;
    public void DeselectUnavailable() => IsSelected = false;
}
