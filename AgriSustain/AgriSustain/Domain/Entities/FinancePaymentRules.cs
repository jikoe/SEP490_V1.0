using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
        payment.ContractMilestoneId == null && payment.B2bcontractId == null
        && (payment.PaymentPurpose is "OrderPayment" or "RetailOrderPayment"
                && payment.OrderId != null && payment.B2bsubscriptionId == null
            || payment.OrderId == null && SubscriptionReference(payment) != null);

    public static string? SubscriptionReference(PaymentTransaction payment)
    {
        if (payment.PaymentPurpose != "SubscriptionPayment" || payment.GatewayProvider != "SePay")
            return null;

        var parts = payment.RequestKey?.Split('|');
        return parts is [var reference, var buyer, var package, "0" or "1"]
            && reference.Length == 13 && reference.StartsWith("SUB", StringComparison.Ordinal)
            && reference.AsSpan(3).IndexOfAnyExcept("0123456789ABCDEF") < 0
            && long.TryParse(buyer, out var buyerId) && buyerId > 0
            && long.TryParse(package, out var packageId) && packageId > 0
            ? reference : null;
    }

    public static string OrderReference(string orderCode)
    {
        var code = string.Concat(orderCode.Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();
        return code[..Math.Min(18, code.Length)];
    }

    public static string? TransferReference(PaymentTransaction payment) =>
        payment.Order is { } order ? OrderReference(order.OrderCode) : SubscriptionReference(payment);

    public static bool MatchesReference(IEnumerable<string?> texts, string reference)
    {
        if (reference.Length is 0 or > 18 || !reference.All(char.IsAsciiLetterOrDigit))
            return false;

        string pattern = "(?<![A-Z0-9])" + string.Join("-?", reference.ToCharArray()) + "(?![A-Z0-9])";
        return texts.Any(text => text != null && Regex.IsMatch(text, pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    }

    public static void AddSePayRequest(Order order)
    {
        if (order.PaymentMethod != "SePay" || order.FinalAmount <= 0)
            return;

        string key = "ORDER-" + order.OrderCode;
        order.PaymentTransactions.Add(new PaymentTransaction
        {
            TransactionCode = key,
            RequestKey = key,
            GatewayProvider = "SePay",
            Amount = order.FinalAmount,
            PaymentType = "Transfer",
            PaymentStatus = "Pending",
            PaymentPurpose = "RetailOrderPayment",
            RequestedAt = order.OrderDate,
            ExpiresAt = order.ReservationExpiresAt
        });
    }

    public static void ExpireSePayRequest(Order order)
    {
        foreach (var request in order.PaymentTransactions.Where(p => p.GatewayProvider == "SePay"
                     && p.RequestKey == "ORDER-" + order.OrderCode && p.PaymentStatus == "Pending"))
            request.PaymentStatus = "Expired";
    }

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
            payment.RequestKey,
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
        decimal total = payment.Order?.FinalAmount ?? payment.B2bsubscription?.PriceSnapshot
            ?? (SubscriptionReference(payment) != null ? payment.Amount : 0);
        return Math.Max(0, total - payments.Where(IsCaptured).Sum(p => p.Amount));
    }
}
