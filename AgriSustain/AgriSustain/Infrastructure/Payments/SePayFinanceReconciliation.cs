using System.Data;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Application.Services.B2B;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Payments;

public sealed class SePayFinanceReconciliation(
    HttpClient client, IConfiguration configuration, IFinanceRepository repository, AgriSustainContext db,
    BankTransferReconciler orders, BuyerSubscriptionService subscriptions, TimeProvider clock)
    : IFinancePaymentReconciliation
{
    private const int LookupLimit = 5000;
    private string? Token => (configuration["Payment:SePay:ApiToken"] ?? configuration["Payouts:SePayApiToken"])?.Trim();
    private string? Account => configuration["Payment:SePay:AccountNumber"]?.Trim();
    private string? Bank => configuration["Payment:SePay:BankShortName"]?.Trim();

    public bool IsConfigured => !string.IsNullOrEmpty(Token) && !string.IsNullOrEmpty(Account)
        && !string.IsNullOrEmpty(Bank);

    public async Task<Result<FinanceReconciliationOutcome>> CheckAsync(
        PaymentTransaction payment, long staffId, ReconcilePaymentRequest request, CancellationToken ct)
    {
        long id = payment.PaymentTransactionId;
        string trace = "fin02:" + Guid.ParseExact(request.RequestKey, "D").ToString("N");
        if (await FindReplayAsync(id, staffId, trace, request.Version, ct) is { } replay)
            return replay;
        if (FinancePaymentRules.IsCaptured(payment))
            return AlreadyApplied();
        if (!string.Equals(request.Version,
                FinancePaymentRules.Version(payment, await repository.InvoicesAsync(payment, ct)),
                StringComparison.OrdinalIgnoreCase))
            return Changed();

        IReadOnlyList<IncomingBankTransfer>? matches;
        try
        {
            matches = await LookupAsync(payment, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidDataException or IOException
            || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            matches = null;
        }

        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (!await repository.CanAccessAsync(staffId, ct))
                return Result<FinanceReconciliationOutcome>.FailureResult(new(
                    "FINANCE_ACCESS_DENIED", "Tài khoản không còn quyền đối soát giao dịch.", "Forbidden"));
            if (await FindReplayAsync(id, staffId, trace, request.Version, ct) is { } committed)
                return committed;

            var current = await repository.PaymentAsync(id, ct);
            if (current != null && FinancePaymentRules.IsCaptured(current))
                return AlreadyApplied();
            if (current == null || !string.Equals(request.Version,
                    FinancePaymentRules.Version(current, await repository.InvoicesAsync(current, ct)),
                    StringComparison.OrdinalIgnoreCase))
                return Changed();

            var now = clock.GetUtcNow().UtcDateTime;
            var transfer = matches is { Count: 1 } ? matches[0] : null;
            var outcome = await ApplyAsync(current, matches, transfer, now, ct);
            var receipt = transfer == null ? null : new GatewayReceipt("SePay",
                FinancePaymentRules.TransferReference(current)!, transfer.TransactionCode, transfer.Amount,
                "Succeeded", transfer.PaidAt, now,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(transfer.RawPayload))), transfer.ProviderReference);
            var after = await db.PaymentTransactions.Where(p => p.PaymentTransactionId == id)
                .Select(p => p.PaymentStatus).SingleAsync(ct);
            await db.PaymentTransactions.Where(p => p.PaymentTransactionId == id)
                .ExecuteUpdateAsync(set => set.SetProperty(p => p.ReconciledAt, now), ct);
            db.AuditLogs.Add(new AuditLog
            {
                EntityType = "PaymentTransaction",
                EntityId = id,
                Action = FinancePaymentRules.AuditAction,
                PerformedByUserId = staffId,
                OccurredAt = now,
                OldValue = current.PaymentStatus,
                Reason = outcome.Message,
                TraceId = trace,
                NewValue = JsonSerializer.Serialize(new ReconciliationAudit(outcome.Outcome, current.PaymentStatus,
                    after, request.Version, outcome.Outcome is "Mismatch" or "LatePayment"
                        or "VerificationFailed" or "NeedsReview", receipt))
            });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<FinanceReconciliationOutcome>.Ok(outcome);
        }
        catch (SqlException ex) when (IsConcurrencyConflict(ex))
        {
            return Changed();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException sql && IsConcurrencyConflict(sql))
        {
            return Changed();
        }
    }

    private async Task<FinanceReconciliationOutcome> ApplyAsync(PaymentTransaction payment,
        IReadOnlyList<IncomingBankTransfer>? matches, IncomingBankTransfer? transfer, DateTime now, CancellationToken ct)
    {
        if (matches == null)
            return new("VerificationFailed", "Không thể xác minh giao dịch với SePay. Vui lòng thử lại.");
        if (matches.Count > 1)
            return new("NeedsReview", "Có nhiều khoản thu cùng mã chuyển khoản. Cần kiểm tra sao kê.");
        var expiry = payment.ExpiresAt ?? payment.Order?.ReservationExpiresAt;
        if (transfer == null)
            return expiry <= now
                ? new("Expired", "Yêu cầu đã hết hạn và chưa tìm thấy khoản thu khớp mã.")
                : new("Pending", "SePay chưa có khoản thu khớp mã chuyển khoản.");
        if (transfer.PaidAt > expiry || payment.Order?.OrderStatus == "Cancelled" || payment.PaymentStatus == "Expired")
            return new("LatePayment", "Có khoản thu nhưng yêu cầu đã hết hạn hoặc đơn đã hủy. Cần kiểm tra sao kê.");
        if (payment.PaymentStatus is not ("Pending" or "AwaitingPayment" or "Processing"))
            return new("NeedsReview", "Trạng thái hiện tại của yêu cầu cần xử lý thủ công.");
        if (transfer.Amount != payment.Amount || transfer.Amount != FinancePaymentRules.Unpaid(payment))
            return new("Mismatch", "Số tiền SePay xác minh không khớp số tiền cần thu.");

        if (payment.OrderId is long orderId)
        {
            await orders.RecordInTransactionAsync(new BankTransfer(transfer.Gateway, transfer.TransactionCode,
                transfer.Amount, transfer.PaidAt, transfer.ProviderReference, transfer.RawPayload,
                transfer.ReferenceTexts), orderId, payment.PaymentTransactionId, ct);
        }
        else
        {
            await subscriptions.RecordInTransactionAsync(FinancePaymentRules.SubscriptionReference(payment)!,
                transfer, ct);
        }

        return await db.PaymentTransactions.AnyAsync(p => p.PaymentTransactionId == payment.PaymentTransactionId
            && p.TransactionCode == transfer.TransactionCode && FinancePaymentRules.Captured.Contains(p.PaymentStatus)
            && p.PaidAt != null, ct)
            ? new("Succeeded", "Đã xác minh và ghi nhận khoản thu qua SePay.")
            : new("NeedsReview", "Khoản thu đã được ghi nhận ở nơi khác hoặc yêu cầu không còn đủ điều kiện.");
    }

    private async Task<IReadOnlyList<IncomingBankTransfer>> LookupAsync(PaymentTransaction payment, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var start = payment.RequestedAt.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var end = now.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var url = "https://my.sepay.vn/userapi/transactions/list?account_number=" + Uri.EscapeDataString(Account!)
            + "&transaction_date_min=" + Uri.EscapeDataString(start)
            + "&transaction_date_max=" + Uri.EscapeDataString(end) + "&limit=" + LookupLimit;
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
        using var response = await client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(ct));
        if (json.RootElement.ValueKind != JsonValueKind.Object
            || !json.RootElement.TryGetProperty("status", out var status) || status.ToString() != "200"
            || !json.RootElement.TryGetProperty("transactions", out var rows) || rows.ValueKind != JsonValueKind.Array
            || rows.GetArrayLength() >= LookupLimit)
            throw new InvalidDataException("SePay did not return a complete transaction list.");

        string reference = FinancePaymentRules.TransferReference(payment)!;
        var matches = new Dictionary<long, IncomingBankTransfer>();
        foreach (var item in rows.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            string Field(string key) => item.TryGetProperty(key, out var value) ? value.ToString() : "";
            string[] texts = [Field("code"), Field("transaction_content")];
            if (Field("account_number").Trim() != Account || !FarmerPayoutRules.SameBank(Field("bank_brand_name"), Bank!)
                || !FinancePaymentRules.MatchesReference(texts, reference)
                || !long.TryParse(Field("id"), NumberStyles.None, CultureInfo.InvariantCulture, out long id) || id <= 0
                || !decimal.TryParse(Field("amount_in"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount)
                || amount <= 0
                || !decimal.TryParse(Field("amount_out"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var outgoing)
                || outgoing != 0
                || !DateTimeOffset.TryParseExact(Field("transaction_date") + " +07:00", "yyyy-MM-dd HH:mm:ss zzz",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var paidAt)
                || paidAt.UtcDateTime.AddSeconds(1) < payment.RequestedAt || paidAt.UtcDateTime > now)
                continue;

            var providerReference = Field("reference_number").Trim();
            if (providerReference.Length is 0 or > 150)
                continue;
            matches.TryAdd(id, new("SePay", $"SEPAY-{id}", amount, paidAt.UtcDateTime,
                providerReference, item.GetRawText(), texts));
        }
        return matches.Values.ToList();
    }

    private async Task<Result<FinanceReconciliationOutcome>?> FindReplayAsync(
        long id, long staffId, string trace, string version, CancellationToken ct)
    {
        var audit = await db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(a =>
            a.EntityType == "PaymentTransaction" && a.EntityId == id && a.Action == FinancePaymentRules.AuditAction
            && a.TraceId == trace && a.PerformedByUserId == staffId, ct);
        if (audit?.NewValue == null) return null;
        var saved = JsonSerializer.Deserialize<ReconciliationAudit>(audit.NewValue)!;
        return string.Equals(saved.Version, version, StringComparison.OrdinalIgnoreCase)
            ? Result<FinanceReconciliationOutcome>.Ok(new(saved.Outcome, audit.Reason!, saved.Outcome == "Succeeded"))
            : Changed();
    }

    private static bool IsConcurrencyConflict(SqlException ex) => ex.Number is 1205 or 2601 or 2627;

    private static Result<FinanceReconciliationOutcome> Changed() =>
        Result<FinanceReconciliationOutcome>.FailureResult(new(
            "FINANCE_PAYMENT_CHANGED", "Giao dịch đã thay đổi. Hãy tải lại rồi kiểm tra lại.", "Conflict"));

    private static Result<FinanceReconciliationOutcome> AlreadyApplied() =>
        Result<FinanceReconciliationOutcome>.Ok(new("AlreadyApplied", "Khoản thu đã được ghi nhận.", true));
}
