using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AgriSustain.Infrastructure.Payments;

/// <summary>An incoming bank credit reported by a gateway webhook (SePay).</summary>
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
public sealed class BankTransferReconciler(
    AgriSustainContext db,
    ILogger<BankTransferReconciler> logger,
    IConfiguration? configuration = null)
{
    private static readonly HttpClient SePayHttpClient = new() { Timeout = TimeSpan.FromSeconds(8) };
    private static DateTime _lastSePayPollUtc = DateTime.MinValue;

    // Transfer content is "RC" + the first 16 hex digits of the order code (see QrPaymentScreen);
    // banks may drop the dash or wrap the text with their own prefixes/suffixes.
    private static readonly Regex RetailReference =
        new(@"RC-?([0-9A-F]{16})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex LegacyReference =
        new(@"ORD-?([A-Z0-9]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Fallback polling against SePay user API when local ngrok/webhook tunnel is offline.
    /// </summary>
    public async Task TrySyncIncomingFromSePayApiAsync(CancellationToken cancellationToken)
    {
        var token = configuration?["Payment:SePay:ApiToken"] ?? configuration?["Payouts:SePayApiToken"];
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var now = DateTime.UtcNow;
        if ((now - _lastSePayPollUtc).TotalSeconds < 2.5)
        {
            return;
        }
        _lastSePayPollUtc = now;

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://my.sepay.vn/userapi/transactions/list?limit=15");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
            using var res = await SePayHttpClient.SendAsync(req, cancellationToken);
            if (!res.IsSuccessStatusCode)
            {
                return;
            }

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(cancellationToken));
            if (!doc.RootElement.TryGetProperty("transactions", out var txArray) || txArray.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var item in txArray.EnumerateArray())
            {
                string GetStr(string prop) => item.TryGetProperty(prop, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : "";

                if (!decimal.TryParse(GetStr("amount_in"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amountIn) || amountIn <= 0)
                {
                    continue;
                }

                var idStr = GetStr("id");
                if (string.IsNullOrWhiteSpace(idStr))
                {
                    continue;
                }

                DateTime? paidAt = DateTime.TryParseExact(
                    GetStr("transaction_date"),
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var vnTime)
                    ? new DateTimeOffset(vnTime, TimeSpan.FromHours(7)).UtcDateTime
                    : null;

                var content = GetStr("transaction_content");
                var code = GetStr("code");
                var refNum = GetStr("reference_number");

                await RecordAsync(
                    new BankTransfer(
                        "SePay",
                        $"SEPAY-{idStr}",
                        amountIn,
                        paidAt,
                        refNum,
                        item.GetRawText(),
                        [code, content]),
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "SePay userapi fallback poll skipped.");
        }
    }

    public async Task<BankTransferResult> RecordAsync(BankTransfer transfer, CancellationToken cancellationToken)
    {
        var code = transfer.TransactionCode.Length > 100 ? transfer.TransactionCode[..100] : transfer.TransactionCode;
        if (await IsRecordedAsync(code, cancellationToken))
        {
            logger.LogInformation("{Gateway} transfer {Code} was already processed.", transfer.Gateway, code);
            return new(BankTransferOutcome.Duplicate, code, null, transfer.Amount);
        }

        var order = await FindOrderAsync(transfer, cancellationToken);
        var outcome = order is null || (order.OrderStatus != "PendingPayment" && order.OrderStatus != "Pending") ? BankTransferOutcome.Unmatched
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

        IReadOnlyList<long> appliedVouchers = [];
        if (outcome == BankTransferOutcome.Success)
        {
            ConfirmOrder(order!, transfer, now);
            appliedVouchers = await ApplyVoucherUsesAsync(order!, now, cancellationToken);
        }

        try
        {
            // The payment, the applied voucher uses and the UsedCount increments commit together.
            await using var transaction = appliedVouchers.Count > 0
                ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
            await db.SaveChangesAsync(cancellationToken);
            foreach (var voucher in appliedVouchers.GroupBy(id => id))
            {
                int uses = voucher.Count();
                // Atomic increment, so two orders paid at the same time cannot lose a use.
                await db.Vouchers.Where(v => v.VoucherId == voucher.Key)
                    .ExecuteUpdateAsync(set => set.SetProperty(v => v.UsedCount, v => v.UsedCount + uses),
                        cancellationToken);
            }
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
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

    /// <summary>
    /// UC-PS-01 / BR-PV-06: once the order is paid, its held platform-voucher use becomes Applied and
    /// counts in the voucher's used count (used / total, Exhausted). Returns the voucher of each use.
    /// </summary>
    private async Task<IReadOnlyList<long>> ApplyVoucherUsesAsync(Order order, DateTime now,
        CancellationToken cancellationToken)
    {
        var held = await db.VoucherRedemptions
            .Where(r => r.OrderId == order.OrderId && r.Status == VoucherRedemptionStatus.Reserved)
            .ToListAsync(cancellationToken);
        foreach (var redemption in held)
        {
            redemption.Status = VoucherRedemptionStatus.Applied;
            redemption.AppliedAt = now;
        }

        return held.Select(r => r.VoucherId).ToList();
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
        var rawTexts = transfer.ReferenceTexts
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!.Trim())
            .ToList();

        if (rawTexts.Count == 0)
        {
            return null;
        }

        // 1. Standard regex matching for Retail ("RC" + 16 hex chars)
        foreach (var text in rawTexts)
        {
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
                var legacyCode = "ORD-" + legacy.Groups[1].Value.ToUpperInvariant();
                var order = await db.Orders.Include(o => o.SellerOrders)
                    .FirstOrDefaultAsync(o => o.OrderCode == legacyCode, cancellationToken);
                if (order is not null)
                {
                    return order;
                }
            }
        }

        // 2. Exact match against database OrderCode
        foreach (var text in rawTexts)
        {
            var order = await db.Orders.Include(o => o.SellerOrders)
                .FirstOrDefaultAsync(o => o.OrderCode == text, cancellationToken);
            if (order is not null)
            {
                return order;
            }
        }

        // 3. Multi-strategy normalized matching across all pending orders
        var normalizedTexts = rawTexts
            .Select(t => Regex.Replace(t, @"[^A-Za-z0-9]", "").ToUpperInvariant())
            .ToList();

        var pendingOrders = await db.Orders.Include(o => o.SellerOrders)
            .Where(o => o.OrderStatus == "PendingPayment" || o.OrderStatus == "Pending")
            .OrderByDescending(o => o.OrderId)
            .Take(100)
            .ToListAsync(cancellationToken);

        foreach (var order in pendingOrders)
        {
            var normOrderCode = Regex.Replace(order.OrderCode, @"[^A-Za-z0-9]", "").ToUpperInvariant();
            var napasTruncated = normOrderCode.Length > 18 ? normOrderCode[..18] : normOrderCode;
            var rc16Prefix = normOrderCode.StartsWith("RC") && normOrderCode.Length >= 18 ? normOrderCode[..18] : null;

            foreach (var normText in normalizedTexts)
            {
                if (normText.Contains(normOrderCode) || normOrderCode.Contains(normText))
                {
                    return order;
                }

                if (normText.Contains(napasTruncated))
                {
                    return order;
                }

                if (rc16Prefix != null && normText.Contains(rc16Prefix))
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
