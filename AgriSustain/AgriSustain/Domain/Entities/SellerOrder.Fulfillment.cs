namespace AgriSustain.Domain.Entities;
public partial class SellerOrder
{
    public void Confirm() {
        if (Status != "Pending") throw new InvalidOperationException("Order is not pending.");
        Status = "Confirmed";
    }
    public void MarkReadyToShip() {
        if (Status is not ("Confirmed" or "Preparing")) throw new InvalidOperationException("Order is not confirmed.");
        Status = "ReadyToShip";
    }
    public void MarkShipping() {
        if (Status is not ("ReadyToShip" or "Ready")) throw new InvalidOperationException("Order is not ready.");
        Status = "Shipping";
    }
    public void Cancel(string reason) {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Rejection reason required.");
        if (Status is not ("Pending" or "Confirmed" or "Preparing" or "ReadyToShip" or "Ready"))
            throw new InvalidOperationException("Order already dispatched or closed.");
        Status = "Cancelled";
    }
}
