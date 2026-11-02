using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgriSustain.Domain.Entities;

public static class FinancePaymentRules
{
    public static readonly string[] Captured =
        ["Succeeded", "Success", "Paid", "Completed", "PartiallyRefunded", "Refunded"];
    public static readonly string[] Refunded = ["Succeeded", "Success", "Completed"];
    public static readonly string[] PaidPayouts = ["Paid", "Succeeded", "Completed"];
    public static readonly string[] PendingPayouts = ["Pending", "Approved", "Processing"];
    public const string AuditAction = "ReconcilePayment";

    public static bool IsCaptured(PaymentTransaction payment) =>
        Captured.Contains(payment.PaymentStatus) && payment.PaidAt != null;

    public static bool IsSupported(PaymentTransaction payment) =>
        payment.PaymentPurpose is "OrderPayment" or "SubscriptionPayment"
        && (payment.OrderId != null) != (payment.B2bsubscriptionId != null)
        && payment.ContractMilestoneId == null && payment.B2bcontractId == null;

    public static string Version(PaymentTransaction payment, IEnumerable<Invoice> invoices) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            payment.PaymentTransactionId,
            payment.PaymentStatus,
            payment.Amount,
            payment.ProviderTransactionReference,
            payment.GatewayProvider,
            payment.PaymentPurpose,
            payment.PaidAt,
            payment.ReconciledAt,
            payment.ExpiresAt,
            OrderStatus = payment.Order?.OrderStatus,
            ReservationExpiresAt = payment.Order?.ReservationExpiresAt,
            OrderAmount = payment.Order?.FinalAmount,
            OtherPayments = payment.Order?.PaymentTransactions.OrderBy(p => p.PaymentTransactionId)
                .Select(p => new { p.PaymentTransactionId, p.PaymentStatus, p.Amount, p.PaidAt }),
            SubscriptionStatus = payment.B2bsubscription?.SubscriptionStatus,
            SubscriptionPrice = payment.B2bsubscription?.PriceSnapshot,
            SubscriptionDays = payment.B2bsubscription?.DurationDaysSnapshot,
            Invoices = invoices.OrderBy(i => i.InvoiceId).Select(i => new
            {
                i.InvoiceId,
                i.InvoiceStatus,
                i.TotalAmount,
                Allocations = i.PaymentInvoiceAllocations.OrderBy(a => a.PaymentInvoiceAllocationId)
                    .Select(a => new { a.PaymentInvoiceAllocationId, a.Amount })
            })
        }))));

    public static decimal Unpaid(PaymentTransaction payment)
    {
        var payments = payment.Order?.PaymentTransactions
            ?? payment.B2bsubscription?.PaymentTransactions ?? [];
        decimal total = payment.Order?.FinalAmount ?? payment.B2bsubscription?.PriceSnapshot ?? 0;
        return Math.Max(0, total - payments.Where(IsCaptured).Sum(p => p.Amount));
    }
}
