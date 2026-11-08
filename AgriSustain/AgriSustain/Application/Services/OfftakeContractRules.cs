using AgriSustain.Application.DTOs.Offtake;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class OfftakeContractRules
{
    public static bool Verified(string status) => status is "Success" or "Successful" or "Succeeded" or "Paid" or "Completed";
    public static bool IsProcessedPayout(FarmerPayout payout) => payout.PayoutStatus is "Processed" or "Paid"
        && payout.ProcessedAt.HasValue && !string.IsNullOrWhiteSpace(payout.BankTransferRef);
    public static bool OpenDispute(DisputeTicket ticket) => ticket.TicketStatus is not ("Resolved" or "Closed" or "Rejected");
    public static bool Confirmed(ContractSignature signature) => signature.ConfirmationStatus == "Confirmed" && signature.ConfirmedAt.HasValue;
    public static ContractVersion Current(OfftakeContext context) => context.Contract.ContractVersions.Single(x => x.VersionNo == context.Contract.CurrentVersionNo);
    public static IEnumerable<ContractLine> Lines(OfftakeContext context) => Current(context).ContractLines;
    public static IEnumerable<ContractMilestone> Milestones(OfftakeContext context) => Current(context).ContractMilestones;
    public static bool Feedback(string? text) => text?.Trim().Length is >= 20 and <= 500;
    public static bool Text(string? text, int max = 1000) => !string.IsNullOrWhiteSpace(text) && text.Trim().Length <= max;

    public static string Version(OfftakeContext context)
    {
        string input = JsonSerializer.Serialize(new
        {
            context.Contract.CurrentVersionNo,
            context.Contract.Status,
            History = context.History.LastOrDefault()?.AuditLogId,
            Signatures = Current(context).ContractSignatures.OrderBy(x => x.ContractSignatureId)
                .Select(x => new { x.ContractSignatureId, x.ConfirmationStatus, x.ConfirmedAt }),
            Milestones = Milestones(context).OrderBy(x => x.ContractMilestoneId)
                .Select(x => new { x.ContractMilestoneId, x.DeliveredQuantity, x.AcceptedQuantity, x.PaidAmount, x.PaymentStatus, x.FulfillmentStatus })
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
    }

    public string? ValidateConfirmation(OfftakeContext context, long? seasonId, int version)
    {
        if (!seasonId.HasValue || version <= 0) return "MSG02";
        if (version != context.Contract.CurrentVersionNo) return "MSG163";
        if (context.Contract.Status is not ("Proposed" or "Pending" or "PendingConfirmation" or "Negotiating" or "AwaitingDeposit")) return "MSG131";
        var season = context.Seasons.SingleOrDefault(x => x.CropSeasonId == seasonId);
        var lines = Lines(context).ToList();
        if (season is null || lines.Count == 0 || lines.Any(x => x.ProductId != season.ProductId || x.UnitSnapshot != season.YieldUnit)
            || season.Status is "Cancelled" or "Crop Failure" or "Completed") return "MSG120";
        decimal committed = context.OtherCommitments.Where(x => x.CropSeasonId == seasonId).Sum(x => x.B2bcontract.Status == "Terminated" ? x.ContractMilestones.Sum(m => m.DeliveredQuantity) : x.CommittedQuantity);
        if (lines.Sum(x => x.CommittedQuantity) + committed + context.PreOrderCommitments.Where(x => x.CropSeasonId == seasonId).Sum(x => x.CommittedYield) > season.EstimatedYield) return "MSG120";
        if (Milestones(context).Any(x => x.DeliveryQuantity > 0 && x.DueDate < season.EstimatedHarvestDate)) return "MSG120";
        return null;
    }

    public bool CanExecute(OfftakeContext context)
    {
        var signatures = Current(context).ContractSignatures;
        return signatures.Any(x => x.PartyRole == "Farmer" && Confirmed(x))
            && signatures.Any(x => x.PartyRole == "WholesaleBuyer" && Confirmed(x))
            && DepositPaid(context) >= context.Contract.DepositAmount;
    }

    public static decimal DepositPaid(OfftakeContext context) => context.Contract.PaymentTransactions
        .Where(x => x.PaymentPurpose.Contains("Deposit") && Verified(x.PaymentStatus) && x.PaidAt.HasValue).Sum(x => x.Amount);

    public bool PreserveAcceptedCommitments(OfftakeContext context) =>
        !context.Contract.SellerOrders.SelectMany(x => x.Shipments).Any()
        && !context.Contract.PaymentTransactions.Any(x => Verified(x.PaymentStatus))
        && !context.Disputes.Any(OpenDispute);

    public string? ValidateDispatch(OfftakeContext context, DispatchContractDeliveryRequest input)
    {
        if (input.MilestoneId <= 0 || input.BatchQuantities == null || input.BatchQuantities.Count == 0) return "MSG02";
        if (context.Contract.Status != "Active") return "MSG131";
        if (DepositPaid(context) < context.Contract.DepositAmount) return "MSG152";
        if (!CanExecute(context)) return "MSG131";
        var milestone = OfftakeContractRules.Milestones(context).SingleOrDefault(x => x.ContractMilestoneId == input.MilestoneId);
        var line = OfftakeContractRules.Lines(context).SingleOrDefault(x => x.ContractLineId == milestone?.ContractLineId);
        if (milestone == null || line == null || milestone.DeliveryQuantity is not > 0
            || milestone.DueDate > DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))) return "MSG120";
        if (input.BatchQuantities == null || input.BatchQuantities.Count is < 1 or > 50 || input.BatchQuantities.Select(x => x.HarvestBatchId).Distinct().Count() != input.BatchQuantities.Count
            || input.BatchQuantities.Any(x => x.Quantity <= 0 || x.Quantity > 999999999999m || x.Quantity != decimal.Round(x.Quantity, 3))) return "MSG120";
        decimal quantity = input.BatchQuantities.Sum(x => x.Quantity);
        if (line.AgreedPrice.HasValue && quantity * line.AgreedPrice.Value > 9999999999999999.99m) return "MSG120";
        if (quantity > milestone.DeliveryQuantity - milestone.DeliveredQuantity
            || quantity > line.CommittedQuantity - OfftakeContractRules.Milestones(context)
                .Where(x => x.ContractLineId == line.ContractLineId).Sum(x => x.DeliveredQuantity)) return "MSG120";
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        foreach (var selected in input.BatchQuantities)
        {
            var batch = context.Batches.SingleOrDefault(x => x.HarvestBatchId == selected.HarvestBatchId);
            if (batch == null || batch.ProductId != line.ProductId || batch.CropSeasonId != line.CropSeasonId
                || batch.ActualHarvestDate == null || batch.ActualHarvestDate > today || batch.StockEnteredAt == null
                || batch.ExpirationDate < today || batch.TraceabilityQrcode is not { IsRevoked: false }
                || batch.Status is not ("Available" or "InStock" or "Harvested" or "In Stock")
                || selected.Quantity > batch.AvailableQuantity - batch.ReservedQuantity) return "MSG139";
        }
        return null;
    }

    public bool CanComplete(OfftakeContext context)
    {
        var milestones = Milestones(context).ToList();
        if (context.Contract.Status != "Active" || milestones.Count == 0
            || context.Disputes.Any(OpenDispute)
            || context.Contract.ContractTerminationRequest?.RequestStatus is "Pending" or "PendingReview") return false;
        if (milestones.Any(x => (x.DeliveryQuantity > 0 && (x.AcceptedQuantity < x.DeliveryQuantity
                || x.DeliveredQuantity < x.AcceptedQuantity)) || (x.Amount > 0 && x.PaymentStatus != "Paid"))) return false;
        var items = milestones.SelectMany(x => x.Shipments).SelectMany(x => x.ShipmentItems).ToList();
        if (items.Count == 0 || items.Any(x => x.ReconciliationStatus is not ("Accepted" or "Reconciled" or "Resolved")
            || x.AcceptedQuantity == null || x.ReceivedQuantity == null || x.AcceptedQuantity > x.ReceivedQuantity
            || x.ReceivedQuantity > x.ShippedQuantity)) return false;
        if (context.Refunds.Any(x => x.RefundStatus is not ("Success" or "Successful" or "Succeeded" or "Completed" or "Rejected"))) return false;
        if (context.Payouts.Any(x => !IsProcessedPayout(x) && x.PayoutStatus is not ("Cancelled" or "Failed"))) return false;
        if (milestones.Any(x => x.AcceptedQuantity > 0 && !x.Invoices.Any(i => i.InvoiceStatus != "Cancelled"))) return false;
        decimal invoiced = milestones.SelectMany(x => x.Invoices).Where(x => x.InvoiceStatus != "Cancelled").Sum(x => x.TotalAmount);
        decimal settled = context.Payouts.Where(IsProcessedPayout)
            .SelectMany(x => x.PayoutItems).Where(x => milestones.Any(m => m.ContractMilestoneId == x.ContractMilestoneId || m.Invoices.Any(i => i.InvoiceId == x.InvoiceLine.InvoiceId))).Sum(x => x.SourceAmountSettled);
        decimal verifiedReceipts = context.Contract.PaymentTransactions.Where(x => Verified(x.PaymentStatus) && x.PaidAt != null).Sum(x => x.Amount);
        decimal completedRefunds = context.Refunds.Where(x => Verified(x.RefundStatus) && x.ProcessedAt != null).Sum(x => x.Amount);
        if (invoiced > settled || invoiced > verifiedReceipts - completedRefunds) return false;
        decimal applied = context.Contract.PaymentTransactions.SelectMany(x => x.ContractDepositAllocations).Sum(x => x.Amount);
        decimal refunded = context.Refunds.Where(x => Verified(x.RefundStatus)).Sum(x => x.Amount);
        return DepositPaid(context) <= applied + refunded;
    }
}
