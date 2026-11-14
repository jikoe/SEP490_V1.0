using System.Data;
using AgriSustain.Application.DTOs.Revenue;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

/// <summary>Read-only ledger. Invoice allocation and deposit application are references, not new receipts.</summary>
public sealed class FarmerRevenueRepository(AgriSustainContext db, IConfiguration configuration,
    IFinanceFarmerPayoutRepository? payouts = null, FarmerPayoutRules? payoutRules = null)
    : IFarmerRevenueRepository, IFarmerPaymentRequestRepository
{
    public Task<long?> FarmerIdAsync(long actor, CancellationToken ct) => db.FarmerProfiles.AsNoTracking()
        .Where(x => x.UserId == actor && x.User.Status == "Active" && x.User.Role == "Farmer")
        .Select(x => (long?)x.FarmerProfileId).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<long>> FinanceRecipientsAsync(CancellationToken ct) => await db.Users.AsNoTracking()
        .Where(x => x.Status == "Active" && (x.Role == "FinanceStaff" || x.Role == "Finance Staff"))
        .Select(x => x.UserId).ToListAsync(ct);

    public Task<bool> CanReadAsync(long actor, CancellationToken ct) => db.FarmerProfiles.AsNoTracking()
        .AnyAsync(x => x.UserId == actor && x.User.Role == "Farmer" && x.User.Status == "Active", ct);

    public async Task<object> SeasonsAsync(long actor, CancellationToken ct) => await db.CropSeasons.AsNoTracking()
        .Where(x => x.FarmerProfile.UserId == actor).OrderByDescending(x => x.PlantingDate)
        .Select(x => new { id = x.CropSeasonId, name = x.SeasonName }).ToListAsync(ct);

    public async Task<IReadOnlyList<FarmerRevenueEvent>> ReadFinancialEventsAsync(long actor, DateTime asOf, CancellationToken ct)
    {
        // One read transaction keeps totals/details from mixing concurrent financial updates.
        await using var snapshot = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct) : null;
        var invoiceLines = await db.InvoiceLines.AsNoTracking().Where(x =>
                (x.OrderItem != null && x.OrderItem.SellerOrder.FarmerProfile.UserId == actor)
                || (x.ShipmentItem != null && x.ShipmentItem.Shipment.SellerOrder.FarmerProfile.UserId == actor))
            .Include(x => x.Invoice).ThenInclude(x => x.PaymentInvoiceAllocations).ThenInclude(x => x.PaymentTransaction)
            .Include(x => x.OrderItem!).ThenInclude(x => x.ContractLine!).ThenInclude(x => x.CropSeason)
            .Include(x => x.OrderItem!).ThenInclude(x => x.OrderItemBatchAllocations).ThenInclude(x => x.HarvestBatch).ThenInclude(x => x.CropSeason)
            .Include(x => x.OrderItem!).ThenInclude(x => x.SellerOrder).ThenInclude(x => x.Order)
            .Include(x => x.ShipmentItem!).ThenInclude(x => x.OrderItemBatchAllocation).ThenInclude(x => x.OrderItem).ThenInclude(x => x.SellerOrder).ThenInclude(x => x.Order)
            .Include(x => x.ShipmentItem!).ThenInclude(x => x.OrderItemBatchAllocation).ThenInclude(x => x.HarvestBatch).ThenInclude(x => x.CropSeason)
            .Include(x => x.ShipmentItem!).ThenInclude(x => x.Shipment).ThenInclude(x => x.SellerOrder).ThenInclude(x => x.Order)
            .Include(x => x.PayoutItems).ThenInclude(x => x.FarmerPayout).AsSplitQuery().ToListAsync(ct);
        var payments = await db.PaymentTransactions.AsNoTracking().Where(x =>
                (x.B2bcontract != null && x.B2bcontract.FarmerProfile.UserId == actor)
                || (x.ContractMilestone != null && x.ContractMilestone.B2bcontract.FarmerProfile.UserId == actor)
                || (x.Order != null && x.Order.SellerOrders.Any(s => s.FarmerProfile.UserId == actor)))
            .Include(x => x.Order!).ThenInclude(x => x.SellerOrders).ThenInclude(x => x.OrderItems)
            .Include(x => x.B2bcontract!).ThenInclude(x => x.ContractLines).ThenInclude(x => x.CropSeason)
            .Include(x => x.ContractMilestone!).ThenInclude(x => x.ContractLine!).ThenInclude(x => x.CropSeason)
            .Include(x => x.PaymentInvoiceAllocations)
            .Include(x => x.ContractDepositAllocations).ThenInclude(x => x.ContractMilestone).ThenInclude(x => x.ContractLine!).ThenInclude(x => x.CropSeason)
            .Include(x => x.RefundTransactions).ThenInclude(x => x.DisputeTicket!).ThenInclude(x => x.SellerOrder)
            .AsSplitQuery().ToListAsync(ct);
        var retail = await db.OrderItems.AsNoTracking().Where(x => x.SellerOrder.FarmerProfile.UserId == actor
                && !x.InvoiceLines.Any() && x.SellerOrder.B2bcontractId == null)
            .Include(x => x.SellerOrder).ThenInclude(x => x.Order)
            .Include(x => x.OrderItemBatchAllocations).ThenInclude(x => x.HarvestBatch).ThenInclude(x => x.CropSeason)
            .Include(x => x.ContractLine!).ThenInclude(x => x.CropSeason).ToListAsync(ct);
        var disputes = await db.DisputeTickets.AsNoTracking().Where(x =>
            (x.B2bcontract != null && x.B2bcontract.FarmerProfile.UserId == actor)
            || (x.SellerOrder != null && x.SellerOrder.FarmerProfile.UserId == actor)).ToListAsync(ct);
        var bank = await db.UserBankAccounts.AsNoTracking().Where(x => x.UserId == actor && x.IsDefault).SingleOrDefaultAsync(ct);
        var farmer = await db.FarmerProfiles.AsNoTracking().SingleAsync(x => x.UserId == actor, ct);
        var output = new List<FarmerRevenueEvent>();
        // Request previews and dashboard balances use the same eligibility and historical fee rules.
        var payoutContext = payouts == null ? null : await payouts.LoadContextAsync(farmer.FarmerProfileId, null, false, ct);
        var available = payoutContext == null || payoutRules == null ? null
            : payoutRules.ValidateEligibleProceeds(payoutContext, null, DateTime.UtcNow).ToDictionary(x => x.Id);
        var invoiceIds = invoiceLines.Select(x => x.InvoiceId).Distinct().ToList();
        var invoiceTotals = await db.InvoiceLines.AsNoTracking().Where(x => invoiceIds.Contains(x.InvoiceId))
            .GroupBy(x => x.InvoiceId).Select(g => new { Id = g.Key, Amount = g.Sum(x => x.LineAmount) })
            .ToDictionaryAsync(x => x.Id, x => x.Amount, ct);
        foreach (var line in invoiceLines.Where(x => x.Invoice.InvoiceStatus != "Cancelled" && x.Invoice.IssuedAt <= asOf))
        {
            var item = line.OrderItem ?? line.ShipmentItem?.OrderItemBatchAllocation.OrderItem;
            if (item == null) continue;
            var season = line.ShipmentItem?.OrderItemBatchAllocation.HarvestBatch.CropSeason ?? item.ContractLine?.CropSeason;
            if (season == null)
            {
                var linked = item.OrderItemBatchAllocations.Select(x => x.HarvestBatch.CropSeason).DistinctBy(x => x.CropSeasonId).ToList();
                if (linked.Count == 1) season = linked[0];
            }
            var seller = item.SellerOrder;
            string channel = Channel(seller, item);
            string stage = seller.B2bcontractId.HasValue ? "Milestone" : "Goods";
            string reference = line.Invoice.InvoiceNumber;
            output.Add(new("invoice:" + line.InvoiceLineId, line.Invoice.IssuedAt, reference, "Sales", channel,
                season?.CropSeasonId, season?.SeasonName ?? "Chưa liên kết mùa vụ", stage, "Verified",
                Sales: line.LineAmount, Quantity: line.Quantity, UnitPrice: line.UnitPriceSnapshot));
            // Proceeds use invoice-linked receipts, including applied deposits once, and settled source amounts.
            decimal invoiceTotal = invoiceTotals.GetValueOrDefault(line.InvoiceId);
            decimal fraction = invoiceTotal > 0 ? line.LineAmount / invoiceTotal : 0;
            decimal collected = line.Invoice.PaymentInvoiceAllocations.Where(x => OfftakeContractRules.Verified(x.PaymentTransaction.PaymentStatus)
                    && x.PaymentTransaction.PaidAt <= asOf && x.CreatedAt <= asOf).Sum(x => x.Amount) * fraction;
            decimal settled = line.PayoutItems.Where(x => OfftakeContractRules.IsProcessedPayout(x.FarmerPayout) && x.FarmerPayout.ProcessedAt <= asOf)
                .Sum(x => x.SourceAmountSettled);
            decimal prepared = line.PayoutItems.Where(x => !OfftakeContractRules.IsProcessedPayout(x.FarmerPayout) && x.FarmerPayout.PayoutStatus is not ("Cancelled" or "Failed" or "Rejected"))
                .Sum(x => x.SourceAmountSettled > 0 ? x.SourceAmountSettled : x.GrossAmount);
            decimal refunds = line.Invoice.PaymentInvoiceAllocations.Sum(allocation =>
                payments.Where(p => p.PaymentTransactionId == allocation.PaymentTransactionId).SelectMany(p => p.RefundTransactions)
                    .Where(r => OfftakeContractRules.Verified(r.RefundStatus) && r.ProcessedAt <= asOf
                        && (r.PaymentInvoiceAllocationId == null || r.PaymentInvoiceAllocationId == allocation.PaymentInvoiceAllocationId))
                    .Sum(r => r.Amount * (r.PaymentInvoiceAllocationId.HasValue ? 1 :
                        allocation.PaymentTransaction.Amount > 0 ? allocation.Amount / allocation.PaymentTransaction.Amount : 0)) * fraction);
            decimal remaining = Math.Max(0, Math.Min(line.LineAmount, collected) - settled - refunds);
            string? reason = disputes.Any(x => OfftakeContractRules.OpenDispute(x)
                && (x.SellerOrderId == seller.SellerOrderId || (seller.B2bcontractId.HasValue && x.B2bcontractId == seller.B2bcontractId)))
                ? "Chênh lệch hoặc tranh chấp chưa giải quyết" : null;
            if (payments.SelectMany(x => x.RefundTransactions).Any(r => r.RefundStatus is not ("Success" or "Successful" or "Succeeded" or "Completed" or "Rejected" or "Cancelled")
                && (r.PaymentInvoiceAllocation?.InvoiceId == line.InvoiceId || r.DisputeTicket?.SellerOrderId == seller.SellerOrderId)))
                reason ??= "Đang xử lý hoàn tiền liên quan";
            reason ??= bank is not { VerificationStatus: "Verified" } ? "Chưa có tài khoản nhận tiền đã xác minh"
                : bank.PayoutHoldUntil > asOf ? "Tài khoản ngân hàng đang trong thời gian giữ chi trả"
                : farmer.VerificationStatus != "Approved" ? "Hồ sơ nông trại chưa đủ điều kiện chi trả"
                : line.ShipmentItem?.ReconciliationStatus is "Pending" or "Disputed" ? "Chưa hoàn tất nghiệm thu"
                : null;
            double delay = Math.Max(0, configuration.GetValue("FarmerRevenue:PayoutDelayHours", 48d));
            if ((seller.ReceivedAt ?? line.ShipmentItem?.Shipment?.ReceivedAt ?? asOf).AddHours(delay) > asOf)
                reason ??= "Chưa hết thời gian chờ quyết toán";
            decimal held = reason != null ? remaining : Math.Min(remaining, prepared);
            decimal eligible = remaining - held;
            decimal accruedCommission = 0;
            if (available?.TryGetValue(line.InvoiceLineId, out var source) == true)
            {
                var reserved = line.PayoutItems.Where(x => !FarmerPayoutRules.Paid(x.FarmerPayout)
                    && x.FarmerPayout.PayoutStatus is not ("Cancelled" or "Failed" or "Rejected")).ToList();
                eligible = source.Eligible ? source.Net : 0;
                held = (source.Eligible ? 0 : source.Net) + reserved.Sum(x => x.NetAmount);
                accruedCommission = source.Commission + reserved.Sum(x => x.CommissionAmount);
                reason = source.Eligible ? null : source.HoldReason;
            }
            if (remaining > 0) output.Add(new("balance:" + line.InvoiceLineId, line.Invoice.IssuedAt, reference, "Proceeds", channel,
                season?.CropSeasonId, season?.SeasonName ?? "Chưa liên kết mùa vụ", "Settlement", held > 0 ? "Held" : "Eligible",
                Commission: accruedCommission, Eligible: eligible, Held: held, HoldReason: reason ?? (prepared > 0 ? "Khoản đã được giữ cho phiếu chi, chưa chuyển thành công" : null)));
            foreach (var payoutItem in line.PayoutItems.Where(x => x.FarmerPayout.PayoutStatus == "Processed"
                && x.FarmerPayout.ProcessedAt != null && x.FarmerPayout.ProcessedAt <= asOf))
            {
                var payout = payoutItem.FarmerPayout;
                output.Add(new("payout:" + payoutItem.PayoutItemId, payout.ProcessedAt!.Value, payout.PayoutCode, "Payout", channel,
                    season?.CropSeasonId, season?.SeasonName ?? "Chưa liên kết mùa vụ", "Settlement", "Processed",
                    Commission: payoutItem.CommissionAmount, Payout: payoutItem.NetAmount,
                    TransferReference: payout.BankTransferRef, Deductions: payoutItem.GrossAmount - payoutItem.CommissionAmount - payoutItem.NetAmount));
            }
        }
        foreach (var item in retail.Where(x => x.SellerOrder.Status is "Delivered" or "Received" or "Completed"))
        {
            var seller = item.SellerOrder;
            var linked = item.OrderItemBatchAllocations.Select(x => x.HarvestBatch.CropSeason).DistinctBy(x => x.CropSeasonId).ToList();
            var season = linked.Count == 1 ? linked[0] : null;
            output.Add(new("order:" + item.OrderItemId, seller.ReceivedAt ?? seller.DeliveredAt ?? seller.Order.OrderDate,
                seller.SellerOrderCode, "Sales", Channel(seller, item), season?.CropSeasonId,
                season?.SeasonName ?? "Chưa liên kết mùa vụ", "Goods", "Verified", Sales: item.SubTotal,
                Quantity: item.Quantity, UnitPrice: item.UnitPriceSnapshot));
        }
        foreach (var payment in payments.Where(x => OfftakeContractRules.Verified(x.PaymentStatus) && x.PaidAt != null && x.PaidAt <= asOf))
        {
            bool deposit = payment.PaymentPurpose.Contains("Deposit", StringComparison.OrdinalIgnoreCase);
            var season = payment.ContractMilestone?.ContractLine?.CropSeason;
            if (season == null && payment.B2bcontract != null)
            {
                var seasons = payment.B2bcontract.ContractLines.Where(x => x.ContractVersionId > 0).Select(x => x.CropSeason).Where(x => x != null).DistinctBy(x => x!.CropSeasonId).ToList();
                if (seasons.Count == 1) season = seasons[0];
            }
            decimal amount = payment.Amount;
            string channel = payment.Order?.OrderType == "Retail" ? "B2C" : "B2B";
            // Shared checkout receipts are split by the stored seller amounts, not attributed in full to every farm.
            if (payment.Order != null)
            {
                decimal all = payment.Order.SellerOrders.Sum(x => x.FinalAmount);
                decimal owned = payment.Order.SellerOrders.Where(x => x.FarmerProfileId == farmer.FarmerProfileId).Sum(x => x.FinalAmount);
                amount = all > 0 ? decimal.Round(payment.Amount * owned / all, 2) : 0;
            }
            decimal applied = payment.ContractDepositAllocations.Where(x => x.CreatedAt <= asOf).Sum(x => x.Amount)
                + payment.PaymentInvoiceAllocations.Where(x => x.ContractDepositAllocationId == null && x.CreatedAt <= asOf).Sum(x => x.Amount);
            decimal refundedUnapplied = payment.RefundTransactions.Where(x => OfftakeContractRules.Verified(x.RefundStatus)
                && x.ProcessedAt <= asOf && x.ContractDepositAllocationId == null && x.PaymentInvoiceAllocationId == null).Sum(x => x.Amount);
            decimal remainingDeposit = deposit ? Math.Max(0, payment.Amount - applied - refundedUnapplied)
                * (payment.Amount > 0 ? amount / payment.Amount : 0) : 0;
            output.Add(new("receipt:" + payment.PaymentTransactionId, payment.PaidAt!.Value, payment.TransactionCode,
                "Receipt", channel, season?.CropSeasonId, season?.SeasonName ?? "Chưa liên kết mùa vụ",
                deposit ? "Deposit" : payment.B2bcontractId.HasValue || payment.ContractMilestoneId.HasValue ? "Milestone" : "Goods", remainingDeposit > 0 ? "Held" : "Verified",
                Receipts: amount, Deposits: remainingDeposit,
                TransferReference: payment.ProviderTransactionReference));
            if (!deposit)
            {
                decimal allocatedOwned = payment.PaymentInvoiceAllocations.Where(x => x.CreatedAt <= asOf)
                    .Sum(x => invoiceTotals.GetValueOrDefault(x.InvoiceId) > 0
                        ? x.Amount * invoiceLines.Where(l => l.InvoiceId == x.InvoiceId).Sum(l => l.LineAmount)
                            / invoiceTotals[x.InvoiceId] : 0);
                decimal unallocated = Math.Max(0, amount - allocatedOwned);
                if (unallocated > 0)
                    output.Add(new("unallocated:" + payment.PaymentTransactionId, payment.PaidAt.Value, payment.TransactionCode,
                        "Proceeds", channel, season?.CropSeasonId, season?.SeasonName ?? "Chưa liên kết mùa vụ", "Settlement", "Held",
                        Held: unallocated, HoldReason: "Khoản thu chờ phân bổ, hóa đơn hoặc đối soát; chưa đủ điều kiện chi trả"));
            }
            foreach (var allocation in payment.ContractDepositAllocations.Where(x => x.CreatedAt <= asOf))
                output.Add(new("deposit-applied:" + allocation.ContractDepositAllocationId, allocation.CreatedAt, payment.TransactionCode,
                    "DepositApplied", "B2B", allocation.ContractMilestone.ContractLine?.CropSeasonId,
                    allocation.ContractMilestone.ContractLine?.CropSeason?.SeasonName ?? "Chưa liên kết mùa vụ", "Deposit", "Verified", EventAmount: allocation.Amount));
            foreach (var refund in payment.RefundTransactions.Where(x => OfftakeContractRules.Verified(x.RefundStatus) && x.ProcessedAt != null && x.ProcessedAt <= asOf))
            {
                decimal share = payment.Amount > 0 ? amount / payment.Amount : 0;
                if (refund.DisputeTicket?.SellerOrder != null)
                    share = refund.DisputeTicket.SellerOrder.FarmerProfileId == farmer.FarmerProfileId ? 1 : 0;
                output.Add(new("refund:" + refund.RefundTransactionId, refund.ProcessedAt!.Value, refund.RefundCode, "Refund", channel,
                    season?.CropSeasonId, season?.SeasonName ?? "Chưa liên kết mùa vụ", deposit ? "Deposit" : "Settlement", "Verified",
                    Refunds: decimal.Round(refund.Amount * share, 2),
                    TransferReference: refund.GatewayReference));
            }
        }
        if (snapshot != null) await snapshot.CommitAsync(ct);
        return output;
    }

    private static string Channel(SellerOrder seller, OrderItem item) => seller.Order.OrderType == "Retail" ? "B2C" : "B2B";
}

