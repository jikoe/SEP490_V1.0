using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Payouts;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class FarmerPayoutRules(IConfiguration configuration)
{
    public static bool Paid(FarmerPayout payout) => payout.PayoutStatus is "Processed" or "Paid" or "Succeeded" or "Completed"
        && payout.ProcessedAt != null && !string.IsNullOrWhiteSpace(payout.BankTransferRef);
    public static bool Captured(PaymentTransaction payment) => FinancePaymentRules.Captured.Contains(payment.PaymentStatus)
        && payment.PaidAt != null;
    public static bool Refunded(RefundTransaction refund) => FinancePaymentRules.Refunded.Contains(refund.RefundStatus)
        && refund.ProcessedAt != null;
    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static string Digits(string? value) => new((value ?? "").Where(char.IsDigit).ToArray());
    public static string BankKey(string? value) => new((value ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public string? ValidateVerifiedRecipient(FarmerPayoutContext context, DateTime now)
    {
        if (context.Farmer.User.Status != "Active" || context.Farmer.VerificationStatus != "Approved") return "SUSPENDED";
        var bank = context.Bank;
        if (bank == null || bank.VerificationStatus != "Verified"
            || bank.BankCodeNavigation is not { IsActive: true } || bank.PayoutHoldUntil > now
            || (bank.AccountNumber.Length is < 6 or > 19 || !bank.AccountNumber.All(char.IsAsciiDigit)) || string.IsNullOrWhiteSpace(bank.AccountHolder)) return "ACCOUNT_HOLD";
        return null;
    }

    public PayoutBank? Recipient(FarmerPayoutContext context) => context.Bank is not { } bank ? null : new(
        bank.BankCode, bank.BankCodeNavigation.FullName, bank.BankCodeNavigation.ShortName,
        bank.AccountNumber, bank.AccountHolder, bank.VerifiedAt);

    public IReadOnlyList<PayoutSource> ValidateEligibleProceeds(FarmerPayoutContext context, long? excludingPayout, DateTime now)
    {
        var result = new List<PayoutSource>();
        string? bankBlock = ValidateVerifiedRecipient(context, now);
        double delay = Math.Max(0, configuration.GetValue("Payouts:WaitingHours", 48d));
        foreach (var line in context.Lines.Where(x => x.Invoice.InvoiceStatus != "Cancelled"))
        {
            var item = line.OrderItem ?? line.ShipmentItem?.OrderItemBatchAllocation.OrderItem;
            if (item == null) continue;
            var seller = item.SellerOrder;
            if (seller.FarmerProfileId != context.Farmer.FarmerProfileId) continue;
            decimal invoiceTotal = context.InvoiceTotals.GetValueOrDefault(line.InvoiceId);
            decimal ratio = invoiceTotal > 0 ? line.LineAmount / invoiceTotal : 0;
            var allocations = line.Invoice.PaymentInvoiceAllocations.Where(x => Captured(x.PaymentTransaction)).ToList();
            decimal collected = allocations.Sum(x => x.Amount) * ratio;
            // The platform-funded voucher is never deducted from a Farmer's agreed invoice price.
            var order = seller.Order;
            if (order != null && order.PaymentTransactions.Where(Captured).Sum(x => x.Amount) >= order.FinalAmount
                && order.TotalAmount > 0 && order.VoucherDiscount > 0)
                collected += order.VoucherDiscount.Value * line.LineAmount / order.TotalAmount;
            collected = Math.Min(line.LineAmount, collected);
            var claims = context.Claims.Where(x => x.InvoiceLineId == line.InvoiceLineId && x.FarmerPayoutId != excludingPayout).ToList();
            decimal settled = claims.Where(x => Paid(x.FarmerPayout)).Sum(x => Math.Max(x.GrossAmount, x.SourceAmountSettled));
            decimal held = claims.Where(x => !Paid(x.FarmerPayout) && x.FarmerPayout.PayoutStatus is not ("Failed" or "Cancelled" or "Rejected"))
                .Sum(x => x.GrossAmount);
            var refunds = context.Refunds.Where(x =>
                (x.PaymentInvoiceAllocationId.HasValue && line.Invoice.PaymentInvoiceAllocations.Any(a => a.PaymentInvoiceAllocationId == x.PaymentInvoiceAllocationId))
                || (x.DisputeTicket?.SellerOrderId.HasValue == true ? x.DisputeTicket.SellerOrderId == seller.SellerOrderId
                    : line.Invoice.PaymentInvoiceAllocations.Any(a => a.PaymentTransactionId == x.PaymentTransactionId))
                || (seller.B2bcontractId.HasValue && x.DisputeTicket?.B2bcontractId == seller.B2bcontractId)).ToList();
            decimal sellerInvoiced = context.Lines.Where(x => (x.OrderItem?.SellerOrderId
                ?? x.ShipmentItem?.Shipment.SellerOrderId) == seller.SellerOrderId).Sum(x => x.LineAmount);
            decimal refunded = refunds.Where(Refunded).Sum(refund =>
            {
                if (refund.PaymentInvoiceAllocationId.HasValue) return refund.Amount * ratio;
                if (refund.DisputeTicket?.SellerOrderId == seller.SellerOrderId)
                    return sellerInvoiced > 0 ? refund.Amount * line.LineAmount / sellerInvoiced : 0;
                decimal appliedToInvoice = line.Invoice.PaymentInvoiceAllocations
                    .Where(x => x.PaymentTransactionId == refund.PaymentTransactionId).Sum(x => x.Amount);
                return refund.PaymentTransaction.Amount > 0
                    ? refund.Amount * appliedToInvoice / refund.PaymentTransaction.Amount * ratio : 0;
            });
            decimal gross = decimal.Round(Math.Max(0, collected - settled - held - refunded), 2);
            string? blocked = bankBlock;
            if (context.Disputes.Any(x => OfftakeContractRules.OpenDispute(x)
                && (x.SellerOrderId == seller.SellerOrderId || (seller.B2bcontractId.HasValue && x.B2bcontractId == seller.B2bcontractId)))
                || refunds.Any(x => !Refunded(x) && x.RefundStatus is not ("Rejected" or "Cancelled"))) blocked ??= "DISPUTE_HOLD";
            if (line.ShipmentItem is { } shipped)
            {
                if (shipped.AcceptedQuantity == null || shipped.ReceivedQuantity == null
                    || shipped.AcceptedQuantity > shipped.ReceivedQuantity || shipped.ReceivedQuantity > shipped.ShippedQuantity
                    || line.Quantity > shipped.AcceptedQuantity || shipped.ReconciliationStatus is not ("Reconciled" or "Accepted" or "Resolved"))
                    blocked ??= "ACCEPTANCE_HOLD";
            }
            else if (seller.ReceivedAt == null || seller.Status is not ("Completed" or "Received")) blocked ??= "ACCEPTANCE_HOLD";
            DateTime? acceptedAt = line.ShipmentItem?.Shipment.ReceivedAt ?? seller.ReceivedAt;
            if (acceptedAt == null || acceptedAt.Value.AddHours(delay) > now) blocked ??= "WAITING_PERIOD";
            if (collected <= 0) blocked ??= "UNPAID_HOLD";
            if (gross <= 0) blocked ??= "ALREADY_HELD";
            decimal commission = 0, deduction = 0, rate = 0;
            long? policyId = null;
            decimal fraction = line.LineAmount > 0 ? gross / line.LineAmount : 0;
            if (item.Fees.Count > 0)
            {
                decimal itemFraction = item.SubTotal > 0 ? line.LineAmount / item.SubTotal : 0;
                commission = decimal.Round(item.Fees.Where(x => x.FeeGroup == FeeType.GroupPlatformCommission)
                    .Sum(x => Math.Max(0, x.NetAmount)) * itemFraction * fraction, 0, MidpointRounding.AwayFromZero);
                deduction = decimal.Round(item.Fees.Where(x => x.FeeGroup != FeeType.GroupPlatformCommission)
                    .Sum(x => Math.Max(0, x.NetAmount)) * itemFraction * fraction, 0, MidpointRounding.AwayFromZero);
                rate = gross > 0 ? decimal.Round(commission / gross * 100, 4) : 0;
                if (item.Fees.Any(x => x.ReversedAmount < 0 || x.ReversedAmount > x.Amount)) blocked ??= "FEE_INVALID";
            }
            else
            {
                // Legacy sources keep the historical commission policy instead of applying today's fee schedule.
                var policy = context.Policies.Where(x => x.CategoryId == item.Product.CategoryId
                    && (x.ApplicableOrderType == (seller.B2bcontractId.HasValue ? "Contract" : order!.OrderType) || x.ApplicableOrderType == "All")
                    && x.EffectiveDate <= DateOnly.FromDateTime(line.Invoice.IssuedAt))
                    .OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.CommissionPolicyId).FirstOrDefault();
                if (policy != null)
                {
                    rate = policy.CommissionRate;
                    policyId = policy.CommissionPolicyId;
                    commission = decimal.Round(gross * rate / 100, 0, MidpointRounding.AwayFromZero);
                }
            }
            decimal net = decimal.Floor(gross - commission - deduction);
            deduction += gross - commission - deduction - net;
            if (net <= 0 || rate < 0 || rate > 100 || commission + deduction > gross) blocked ??= "FEE_INVALID";
            result.Add(new(line.InvoiceLineId, line.Invoice.InvoiceNumber, item.ProductNameSnapshot ?? item.Product.ProductName,
                line.Invoice.IssuedAt, gross, commission, deduction, net, rate, policyId, blocked == null,
                blocked == null ? null : FinancePayoutMessages.Text(blocked), settled, held, line.Quantity, line.UnitPriceSnapshot, seller.Order.OrderType == "Retail" ? "B2C" : "B2B"));
        }
        return result;
    }

    public string BalanceVersion(FarmerPayoutContext context, IEnumerable<PayoutSource> sources) => Hash(JsonSerializer.Serialize(new
    {
        context.Farmer.FarmerProfileId,
        context.Farmer.User.Status,
        context.Farmer.VerificationStatus,
        Bank = context.Bank == null ? null : new
        {
            context.Bank.BankCode,
            context.Bank.AccountNumber,
            context.Bank.AccountHolder,
            context.Bank.VerificationStatus,
            context.Bank.VerifiedAt,
            context.Bank.PayoutHoldUntil
        },
        Sources = sources.OrderBy(x => x.Id)
    }));

    public string Version(FarmerPayoutContext context) => Hash(JsonSerializer.Serialize(new
    {
        context.Payout!.FarmerPayoutId,
        context.Payout.PayoutStatus,
        Amount = context.Payout.Amount.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
        NetAmount = context.Payout.NetAmount.ToString("G29", System.Globalization.CultureInfo.InvariantCulture),
        context.Payout.RecipientBankCode,
        context.Payout.RecipientAccountNumber,
        context.Payout.RecipientAccountHolder,
        context.Payout.BankTransferRef,
        context.Payout.ProcessedAt,
        LastAudit = context.History.LastOrDefault()?.AuditLogId,
        Bank = context.Bank == null ? null : new
        {
            context.Bank.BankCode,
            context.Bank.AccountNumber,
            context.Bank.VerificationStatus,
            context.Bank.PayoutHoldUntil
        }
    }));

    public string? ValidateBeforeTransfer(FarmerPayoutContext context, DateTime now)
    {
        if (ValidateVerifiedRecipient(context, now) is { } blocked) return blocked;
        var payout = context.Payout!;
        if (payout.RecipientBankCode != context.Bank!.BankCode
            || Digits(payout.RecipientAccountNumber) != Digits(context.Bank.AccountNumber)
            || payout.RecipientAccountHolder != context.Bank.AccountHolder) return "ACCOUNT_CHANGED";
        var available = ValidateEligibleProceeds(context, payout.FarmerPayoutId, now).ToDictionary(x => x.Id);
        if (payout.PayoutItems.Count == 0 || payout.NetAmount <= 0 || payout.NetAmount != decimal.Floor(payout.NetAmount)) return "MSG172";
        foreach (var item in payout.PayoutItems)
            if (!available.TryGetValue(item.InvoiceLineId, out var source) || !source.Eligible || source.Gross != item.GrossAmount
                || source.Commission != item.CommissionAmount || source.Net != item.NetAmount)
                return "MSG172";
        return null;
    }
}
