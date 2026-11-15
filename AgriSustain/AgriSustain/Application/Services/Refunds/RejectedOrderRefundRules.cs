using System.Security.Cryptography;
using System.Text;
using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.Refunds;

public sealed class RejectedOrderRefundRules
{
    public static bool Succeeded(RefundTransaction refund) => refund.RefundStatus is "Completed" or "Succeeded" or "Success";

    public bool Eligible(RejectedOrderRefundContext context, DateTime now)
    {
        var r = context.Refund;
        var account = context.Account;
        var payment = r.PaymentTransaction;
        bool paid = payment.PaidAt.HasValue
            && payment.PaymentStatus is "Paid" or "Success" or "Succeeded" or "Completed";
        decimal reserved = payment.RefundTransactions.Where(x => x.ReservesPayment).Sum(x => x.Amount);
        decimal sellerReserved = context.Order.Order.PaymentTransactions.SelectMany(x => x.RefundTransactions)
            .Where(x => x.RequestKey.StartsWith($"farmer-reject:{context.Order.SellerOrderId}:") && x.ReservesPayment)
            .Sum(x => x.Amount);
        return r.RefundStatus == "Pending" && context.Order.Status == "Cancelled"
            && context.Order.Order.BuyerUser.Role == "RetailCustomer" && paid
            && r.Amount > 0 && r.Amount == decimal.Floor(r.Amount)
            && reserved <= payment.Amount && sellerReserved <= context.Order.FinalAmount
            && (r.PaymentInvoiceAllocation == null ? context.Order.Order.SellerOrders.Count == 1 : (r.PaymentInvoiceAllocation.Invoice.SellerOrderId == context.Order.SellerOrderId
                && payment.RefundTransactions.Where(x => x.PaymentInvoiceAllocationId == r.PaymentInvoiceAllocationId
                    && x.ReservesPayment).Sum(x => x.Amount) <= r.PaymentInvoiceAllocation.Amount))
            && account != null && account.VerificationStatus == "Verified" && account.VerifiedAt.HasValue
            && account.BankCodeNavigation.IsActive && !(account.PayoutHoldUntil > now)
            && account.BankCode == r.ReceiverBankCode && account.AccountNumber == r.ReceiverAccountNumber
            && account.AccountHolder == r.ReceiverAccountHolder;
    }

    public string Version(RejectedOrderRefundContext context)
    {
        var r = context.Refund;
        string value = System.Text.Json.JsonSerializer.Serialize(new
        {
            r.RefundTransactionId, r.RefundStatus, r.Amount, r.ReceiverBankCode, r.ReceiverAccountNumber,
            r.ReceiverAccountHolder, context.Order.Status, context.Order.FinalAmount,
            context.Account?.UserBankAccountId, context.Account?.UpdatedAt, context.Account?.PayoutHoldUntil,
            context.Account?.VerificationStatus, context.Account?.VerifiedAt,
            BankActive = context.Account?.BankCodeNavigation.IsActive,
            Refunds = r.PaymentTransaction.RefundTransactions.OrderBy(x => x.RefundTransactionId)
                .Select(x => new { x.RefundTransactionId, x.Amount, x.RefundStatus })
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
