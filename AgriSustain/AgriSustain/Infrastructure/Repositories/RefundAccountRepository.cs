using System.Text.Json;
using AgriSustain.Application.Services.Profiles;
using AgriSustain.Application.Services.Refunds;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

/// <summary>
/// UC-FIN-08 over dbo.RefundTransaction. The request belongs to the user who submitted the
/// complaint, or to the buyer of the paid order. Transfer failures are read from the
/// "RefundTransferFailed" audit rows written by UC-FIN-03.
/// </summary>
public sealed class RefundAccountRepository(AgriSustainContext db) : IRefundAccountRepository
{
    private const string EntityType = "RefundTransaction";

    public async Task<IReadOnlyList<RefundRequestRecord>> ListMineAsync(long userId, CancellationToken cancellationToken)
    {
        var ids = await Mine(userId).OrderByDescending(r => r.RefundTransactionId)
            .Select(r => r.RefundTransactionId).ToListAsync(cancellationToken);
        var list = new List<RefundRequestRecord>();
        foreach (long id in ids)
        {
            if (await FindMineAsync(userId, id, cancellationToken) is { } record) list.Add(record);
        }

        return list;
    }

    public async Task<RefundRequestRecord?> FindMineAsync(long userId, long refundId, CancellationToken cancellationToken)
    {
        var row = await Mine(userId).AsNoTracking()
            .Where(r => r.RefundTransactionId == refundId)
            .Select(r => new
            {
                r.RefundTransactionId,
                r.RefundCode,
                OrderCode = r.PaymentTransaction.Order != null ? r.PaymentTransaction.Order.OrderCode : null,
                FarmName = r.DisputeTicket != null && r.DisputeTicket.SellerOrder != null
                    ? r.DisputeTicket.SellerOrder.FarmerProfile.FarmName : null,
                r.Amount,
                r.Reason,
                r.RefundStatus,
                ApprovedAt = r.DisputeTicket != null ? r.DisputeTicket.DecisionAt : null,
                r.ReceiverBankCode,
                ReceiverBankShortName = r.ReceiverBankCodeNavigation != null ? r.ReceiverBankCodeNavigation.ShortName : null,
                r.ReceiverAccountNumber,
                r.ReceiverAccountHolder,
                r.ReceiverAccountProvidedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (row is null) return null;

        var failure = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == EntityType && a.EntityId == refundId && a.Action == "RefundTransferFailed")
            .OrderByDescending(a => a.OccurredAt)
            .Select(a => new { a.Reason, a.OccurredAt })
            .FirstOrDefaultAsync(cancellationToken);

        return new RefundRequestRecord(row.RefundTransactionId, userId, row.RefundCode, row.OrderCode, row.FarmName,
            row.Amount, row.Reason, row.RefundStatus, row.ApprovedAt, row.ReceiverBankCode, row.ReceiverBankShortName,
            row.ReceiverAccountNumber, row.ReceiverAccountHolder, row.ReceiverAccountProvidedAt,
            failure?.Reason, failure?.OccurredAt);
    }

    public async Task<IReadOnlyList<RefundHistoryDto>> HistoryAsync(long refundId, CancellationToken cancellationToken)
    {
        var rows = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == EntityType && a.EntityId == refundId)
            .OrderBy(a => a.OccurredAt)
            .Select(a => new
            {
                a.Action, a.Reason, a.NewValue, a.OccurredAt,
                Role = a.PerformedByUser != null ? a.PerformedByUser.Role : null
            })
            .ToListAsync(cancellationToken);

        return rows.Select(a => new RefundHistoryDto(Label(a.Action), ActorLabel(a.Role),
            a.Action is "RefundAccountProvided" or "RefundAccountChanged" ? MaskedFrom(a.NewValue) : a.Reason,
            a.OccurredAt)).ToList();
    }

    public async Task<bool> SaveReceiverAsync(long refundId, string bankCode, string accountNumber,
        string accountHolder, long userId, DateTime now, CancellationToken cancellationToken)
    {
        var refund = await db.RefundTransactions.FirstOrDefaultAsync(r => r.RefundTransactionId == refundId
            && r.RefundStatus == RefundTransaction.StatusPending, cancellationToken);
        if (refund is null) return false;

        bool changed = refund.ReceiverAccountNumber is not null;
        string? before = refund.ReceiverAccountNumber is null ? null
            : $"{refund.ReceiverBankCode} {BankAccountService.Mask(refund.ReceiverAccountNumber)}";
        refund.ReceiverBankCode = bankCode;
        refund.ReceiverAccountNumber = accountNumber;
        refund.ReceiverAccountHolder = accountHolder;
        refund.ReceiverAccountProvidedAt = now;

        db.AuditLogs.Add(new AuditLog
        {
            PerformedByUserId = userId,
            EntityType = EntityType,
            EntityId = refundId,
            Action = changed ? "RefundAccountChanged" : "RefundAccountProvided",
            OldValue = before is null ? null : JsonSerializer.Serialize(new { Account = before }),
            NewValue = JsonSerializer.Serialize(new
            {
                Account = $"{bankCode} {BankAccountService.Mask(accountNumber)}", AccountHolder = accountHolder
            }),
            OccurredAt = now
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }

    private IQueryable<RefundTransaction> Mine(long userId) =>
        db.RefundTransactions.Where(r =>
            (r.DisputeTicket != null && r.DisputeTicket.SubmittedByUserId == userId)
            || (r.DisputeTicket == null && r.PaymentTransaction.Order != null
                && r.PaymentTransaction.Order.BuyerUserId == userId));

    private static string Label(string action) => action switch
    {
        "RefundAccountProvided" => "Cung cấp tài khoản nhận tiền",
        "RefundAccountChanged" => "Đổi tài khoản nhận tiền",
        "RefundTransferFailed" => "Chuyển khoản thất bại",
        "CompleteRefund" => "Đã chuyển khoản hoàn tiền",
        _ => action
    };

    private static string ActorLabel(string? role) => role switch
    {
        "RetailCustomer" or "WholesaleBuyer" => "Khách hàng",
        "FinanceStaff" or "Finance Staff" => "Nhân viên tài chính",
        "CustomerSupport" or "Customer Support" => "Bộ phận hỗ trợ",
        null => "Hệ thống",
        _ => role
    };

    private static string? MaskedFrom(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("Account", out var account) ? account.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
