namespace AgriSustain.Domain.Entities;

public partial class B2bcontract
{
    public void RejectProposal(string reason)
    {
        var current = ContractVersions.Single(x => x.VersionNo == CurrentVersionNo);
        current.FeedbackReason = reason;
        current.VersionStatus = "Rejected";
        Status = "Rejected";
    }

    public void MarkCompleted(DateTime now)
    {
        Status = "Completed";
        CompletedAt ??= now;
    }
}

public partial class ContractSignature
{
    public void RecordConfirmation(DateTime now)
    {
        ConfirmationStatus = "Confirmed";
        ConfirmedAt ??= now;
    }
}

public partial class Shipment
{
    public void MarkDispatchedOnce(DateTime now)
    {
        if (ShippedAt.HasValue) return;
        ShipmentStatus = "InTransit";
        ShippedAt = now;
    }
}
