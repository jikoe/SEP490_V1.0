using System.Text.RegularExpressions;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgriSustain.Infrastructure.Payments;

/// <summary>An incoming bank credit reported by a gateway webhook (VietQR, SePay).</summary>
/// <param name="ReferenceTexts">Fields that may carry the order reference, in priority order.</param>
public sealed record BankTransfer(
    string Gateway,
    string TransactionCode,
    decimal Amount,
    DateTime? PaidAt,
    string? ProviderReference,
    string RawPayload,
    IReadOnlyList<string?> ReferenceTexts);

public enum BankTransferOutcome { Duplicate, Success, AmountMismatch, Unmatched }

public sealed record BankTransferResult(BankTransferOutcome Outcome, string TransactionCode, Order? Order, decimal Amount);

/// <summary>
/// Records each bank credit once (keyed by TransactionCode) and confirms the matching retail order
/// when it is still awaiting payment and the full amount arrived.
/// </summary>
public sealed class BankTransferReconciler(AgriSustainContext db, ILogger<BankTransferReconciler> logger)
{
    // Transfer content is "RC" + the first 16 hex digits of the order code (see QrPaymentScreen);
    // banks may drop the dash or wrap the text with their own prefixes/suffixes.
    private static readonly Regex RetailReference =
        new(@"RC-?([0-9A-F]{16})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex LegacyReference =
        new(@"ORD-?([A-Z0-9]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public async Task<BankTransferResult> RecordAsync(BankTransfer transfer, CancellationToken cancellationToken)
    {
        var code = transfer.TransactionCode.Length > 100 ? transfer.TransactionCode[..100] : transfer.TransactionCode;
        if (await IsRecordedAsync(code, cancellationToken))
        {
            logger.LogInformation("{Gateway} transfer {Code} was already processed.", transfer.Gateway, code);
            return new(BankTransferOutcome.Duplicate, code, null, transfer.Amount);
        }

        var order = await FindOrderAsync(transfer, cancellationToken);
        var outcome = order is null || order.OrderStatus != "PendingPayment" ? BankTransferOutcome.Unmatched
            : transfer.Amount < order.FinalAmount ? BankTransferOutcome.AmountMismatch
            : BankTransferOutcome.Success;
        var now = DateTime.UtcNow;

        db.PaymentTransactions.Add(new PaymentTransaction
        {
            OrderId = order?.OrderId,
            TransactionCode = code,
            GatewayProvider = transfer.Gateway,
            Amount = transfer.Amount,
            PaymentType = "Transfer",
            PaymentStatus = outcome.ToString(),
            WebhookPayload = transfer.RawPayload,
            PaidAt = transfer.PaidAt ?? now,
            PaymentPurpose = "RetailOrderPayment",
            RequestedAt = now,
            RequestKey = Guid.NewGuid().ToString("N"),
            ProviderTransactionReference = string.IsNullOrWhiteSpace(transfer.ProviderReference)
                ? null : transfer.ProviderReference.Trim()
        });

        if (outcome == BankTransferOutcome.Success)
        {
            ConfirmOrder(order!, transfer, now);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent retry of the same webhook may have committed first.
            if (!await IsRecordedAsync(code, cancellationToken))
            {
                throw;
            }
            return new(BankTransferOutcome.Duplicate, code, null, transfer.Amount);
        }

        logger.LogInformation("{Gateway} transfer {Code}: order={OrderCode}, amount={Amount}, outcome={Outcome}",
            transfer.Gateway, code, order?.OrderCode, transfer.Amount, outcome);
        return new(outcome, code, order, transfer.Amount);
    }

    private void ConfirmOrder(Order order, BankTransfer transfer, DateTime now)
    {
        order.OrderStatus = "Confirmed";
        foreach (var sellerOrder in order.SellerOrders)
        {
            sellerOrder.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OldStatus = sellerOrder.Status,
                NewStatus = "Confirmed",
                OccurredAt = now,
                Reason = $"Thanh toán thành công qua {transfer.Gateway}"
            });
            sellerOrder.Status = "Confirmed";
        }

        db.Notifications.Add(new Notification
        {
            UserId = order.BuyerUserId,
            Title = "Thanh toán chuyển khoản thành công",
            Content = $"Đơn hàng #{order.OrderCode} đã thanh toán thành công {transfer.Amount:N0} đ qua {transfer.Gateway}.",
            NotificationType = "OrderPayment",
            DeliveryChannel = "InApp",
            DeliveryStatus = "Sent",
            SentAt = now,
            RelatedRecordType = "Order",
            RelatedRecordId = order.OrderId
        });
    }

    private Task<bool> IsRecordedAsync(string transactionCode, CancellationToken cancellationToken) =>
        db.PaymentTransactions.AsNoTracking()
            .AnyAsync(p => p.TransactionCode == transactionCode, cancellationToken);

    private async Task<Order?> FindOrderAsync(BankTransfer transfer, CancellationToken cancellationToken)
    {
        foreach (var text in transfer.ReferenceTexts)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var retail = RetailReference.Match(text);
            if (retail.Success)
            {
                var prefix = "RC-" + retail.Groups[1].Value.ToLowerInvariant();
                var order = await db.Orders.Include(o => o.SellerOrders)
                    .Where(o => o.OrderCode.StartsWith(prefix))
                    .OrderByDescending(o => o.OrderId)
                    .FirstOrDefaultAsync(cancellationToken);
                if (order is not null)
                {
                    return order;
                }
            }

            var legacy = LegacyReference.Match(text);
            if (legacy.Success)
            {
                var code = "ORD-" + legacy.Groups[1].Value.ToUpperInvariant();
                var order = await db.Orders.Include(o => o.SellerOrders)
                    .FirstOrDefaultAsync(o => o.OrderCode == code, cancellationToken);
                if (order is not null)
                {
                    return order;
                }
            }
        }

        logger.LogWarning("No order matches {Gateway} transfer {Code}: {Texts}",
            transfer.Gateway, transfer.TransactionCode, string.Join(" | ", transfer.ReferenceTexts));
        return null;
    }
}
