using System.Data;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Application.Services.Finance;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

/// <summary>
/// UC-FIN-06 persistence. The payment schedule is the ContractMilestone rows of the contract's current version;
/// each received payment is a PaymentTransaction with PaymentPurpose = ContractPayment.
/// </summary>
public sealed class ContractPaymentRepository(AgriSustainContext db) : IContractPaymentRepository
{
    private static readonly string[] s_unpaid = ["Pending", "Unpaid", "PartiallyPaid", "Partially Paid", "Partial"];

    public async Task<IReadOnlyList<ContractScheduleRecord>> ListAsync(string? search,
        CancellationToken cancellationToken)
    {
        var contracts = db.B2bcontracts.AsNoTracking()
            .Where(c => ContractPaymentRules.TrackedContractStatuses.Contains(c.Status));
        if (search is { } text)
        {
            contracts = contracts.Where(c => c.ContractNumber.Contains(text)
                || c.WholesaleBuyerProfile.CompanyName.Contains(text)
                || c.FarmerProfile.FarmName.Contains(text));
        }
        return await LoadAsync(contracts, cancellationToken);
    }

    public async Task<ContractScheduleRecord?> GetAsync(long contractId, CancellationToken cancellationToken) =>
        (await LoadAsync(db.B2bcontracts.AsNoTracking().Where(c => c.B2bcontractId == contractId),
            cancellationToken)).FirstOrDefault();

    public async Task<ContractScheduleRecord?> GetByLineAsync(long lineId, CancellationToken cancellationToken)
    {
        long? contractId = await db.ContractMilestones.AsNoTracking()
            .Where(m => m.ContractMilestoneId == lineId)
            .Select(m => (long?)m.B2bcontractId)
            .FirstOrDefaultAsync(cancellationToken);
        return contractId is long id ? await GetAsync(id, cancellationToken) : null;
    }

    public async Task<IReadOnlyList<ContractHistoryItem>> HistoryAsync(long contractId,
        CancellationToken cancellationToken)
    {
        var payments = await db.PaymentTransactions.AsNoTracking()
            .Where(p => p.B2bcontractId == contractId
                || (p.ContractMilestoneId != null && p.ContractMilestone!.B2bcontractId == contractId))
            .Select(p => new
            {
                At = p.PaidAt ?? p.RequestedAt,
                p.PaymentStatus,
                p.Amount,
                p.GatewayProvider,
                Reference = p.ProviderTransactionReference ?? p.TransactionCode,
                Line = p.ContractMilestone != null ? p.ContractMilestone.MilestoneTitle : null
            })
            .ToListAsync(cancellationToken);

        var lineIds = await db.ContractMilestones.AsNoTracking()
            .Where(m => m.B2bcontractId == contractId)
            .Select(m => new { m.ContractMilestoneId, m.MilestoneTitle })
            .ToListAsync(cancellationToken);
        var ids = lineIds.Select(l => (long?)l.ContractMilestoneId).ToList();
        var titles = lineIds.ToDictionary(l => l.ContractMilestoneId, l => l.MilestoneTitle);

        var audits = await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == ContractPaymentRules.AuditEntity && ids.Contains(a.EntityId)
                && a.Action.StartsWith("ContractPayment:"))
            .Select(a => new
            {
                a.OccurredAt,
                a.Action,
                a.EntityId,
                a.Reason,
                a.NewValue,
                By = a.PerformedByUser != null ? a.PerformedByUser.FullName : null
            })
            .ToListAsync(cancellationToken);

        var items = payments.Select(p => new ContractHistoryItem(p.At, "Payment",
                $"{PaymentLabel(p.PaymentStatus)}{(p.Line is null ? "" : $" – {p.Line}")} ({p.GatewayProvider})",
                p.Amount, p.Reference, null))
            .Concat(audits.Where(a => a.Action != ContractPaymentRules.ActionConfirm
                    && a.Action != ContractPaymentRules.ActionAutoApply)
                .Select(a => new ContractHistoryItem(a.OccurredAt, a.Action.Replace("ContractPayment:", ""),
                    $"{AuditLabel(a.Action)} – {(a.EntityId is long id && titles.TryGetValue(id, out var t) ? t : "")}"
                    + (string.IsNullOrWhiteSpace(a.Reason) ? "" : $": {a.Reason}"),
                    null, null, a.By ?? "Hệ thống")))
            .Concat(audits.Where(a => a.Action == ContractPaymentRules.ActionConfirm)
                .Select(a => new ContractHistoryItem(a.OccurredAt, "Confirm",
                    "Xác nhận đã nhận tiền" + (string.IsNullOrWhiteSpace(a.Reason) ? "" : $": {a.Reason}"),
                    null, null, a.By)))
            .OrderByDescending(i => i.At)
            .ToList();
        return items;
    }

    public Task<bool> TransferRecordedAsync(string transactionCode, CancellationToken cancellationToken) =>
        db.PaymentTransactions.AsNoTracking().AnyAsync(p => p.TransactionCode == transactionCode, cancellationToken);

    public Task<bool> ReferenceUsedAsync(string gateway, string reference, CancellationToken cancellationToken) =>
        db.PaymentTransactions.AsNoTracking().AnyAsync(p => p.ProviderTransactionReference == reference
            && (p.GatewayProvider == gateway || p.PaymentStatus == "Success"), cancellationToken);

    public async Task<bool> RecordPaymentAsync(ContractPaymentWrite write, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var contract = await db.B2bcontracts.FromSqlInterpolated(
            $"SELECT * FROM dbo.B2BContract WITH (UPDLOCK,HOLDLOCK) WHERE B2BContractId = {write.ContractId}")
            .SingleOrDefaultAsync(cancellationToken);
        if (contract is null || !ContractPaymentRules.IsTracked(contract.Status)) return false;
        var line = await db.ContractMilestones.SingleOrDefaultAsync(m => m.ContractMilestoneId == write.LineId
            && m.B2bcontractId == write.ContractId && m.ContractVersion.VersionNo == contract.CurrentVersionNo,
            cancellationToken);
        if (line is null || line.PaidAmount != write.NewPaidAmount - write.Amount || write.NewPaidAmount > line.Amount
            || ContractLineStatuses.Normalize(line.PaymentStatus) == ContractLineStatuses.Paid)
            return false;
        if (line.MilestoneType == "DeliveryAndPayment" && line.DeliveryQuantity > 0
            && !await db.Invoices.AnyAsync(i => i.ContractMilestoneId == write.LineId && i.InvoiceStatus != "Cancelled",
                cancellationToken)) return false;
        if (await db.PaymentTransactions.AnyAsync(p => p.TransactionCode == write.TransactionCode
            || (write.ProviderReference != null && p.ProviderTransactionReference == write.ProviderReference
                && (p.GatewayProvider == write.Gateway || p.PaymentStatus == "Success")), cancellationToken))
            return false;

        var isDeposit = ContractPaymentRules.IsDeposit(line.MilestoneType);
        string oldStatus = line.PaymentStatus;
        line.PaidAmount = write.NewPaidAmount;
        line.PaymentStatus = write.NewLineStatus;

        var payment = new PaymentTransaction
        {
            B2bcontractId = write.ContractId,
            ContractMilestoneId = write.LineId,
            TransactionCode = write.TransactionCode.Length > 100 ? write.TransactionCode[..100] : write.TransactionCode,
            GatewayProvider = write.Gateway,
            Amount = write.Amount,
            PaymentType = "Transfer",
            PaymentStatus = "Success",
            WebhookPayload = write.RawPayload,
            PaidAt = write.PaidAt,
            PaymentPurpose = isDeposit ? ContractPaymentRules.DepositPurpose : ContractPaymentRules.PaymentPurpose,
            RequestedAt = write.Now,
            ReconciledAt = write.ActorUserId is null ? null : write.Now,
            RequestKey = Guid.NewGuid().ToString("N"),
            ProviderTransactionReference = string.IsNullOrWhiteSpace(write.ProviderReference)
                ? null : write.ProviderReference.Trim()
        };
        db.PaymentTransactions.Add(payment);

        if (isDeposit && line.MilestoneTitle == OfftakeContractRules.DepositTitle)
        {
            var delivery = await db.ContractMilestones.SingleOrDefaultAsync(m => m.ContractVersionId == line.ContractVersionId
                && m.MilestoneType == "DeliveryAndPayment", cancellationToken);
            if (delivery is not null)
                payment.ContractDepositAllocations.Add(new ContractDepositAllocation
                {
                    ContractMilestone = delivery, Amount = write.Amount, CreatedAt = write.Now,
                    RequestKey = Guid.NewGuid().ToString("N")
                });
        }

        // DBR-48: the invoice of the line follows its payment status.
        var invoices = await db.Invoices.Where(i => i.ContractMilestoneId == write.LineId)
            .ToListAsync(cancellationToken);
        foreach (var invoice in invoices)
        {
            if (invoice.InvoiceNumber.StartsWith("OFT-INV-", StringComparison.Ordinal))
                payment.PaymentInvoiceAllocations.Add(new PaymentInvoiceAllocation
                {
                    Invoice = invoice, Amount = write.Amount, CreatedAt = write.Now,
                    RequestKey = Guid.NewGuid().ToString("N")
                });
            invoice.InvoiceStatus = write.NewLineStatus == ContractLineStatuses.Paid ? "Paid" : "PartiallyPaid";
        }

        if (write.ActivateContract && contract.Status == "AwaitingDeposit")
        {
            contract.Status = "Active";
        }

        db.AuditLogs.Add(new AuditLog
        {
            PerformedByUserId = write.ActorUserId,
            EntityType = ContractPaymentRules.AuditEntity,
            EntityId = write.LineId,
            Action = write.AuditAction,
            OldValue = $"{{\"PaymentStatus\":\"{oldStatus}\"}}",
            NewValue = $"{{\"PaymentStatus\":\"{write.NewLineStatus}\",\"PaidAmount\":{write.NewPaidAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"Amount\":{write.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}",
            Reason = write.Note,
            OccurredAt = write.Now
        });

        // The line update, payment, invoice, contract and audit entry are saved together.
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.ChangeTracker.Clear();
        return true;
    }

    public async Task RecordUnappliedAsync(IncomingBankTransfer transfer, long? contractId, long? lineId, string status,
        DateTime now, CancellationToken cancellationToken)
    {
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            B2bcontractId = contractId,
            TransactionCode = transfer.TransactionCode.Length > 100 ? transfer.TransactionCode[..100] : transfer.TransactionCode,
            GatewayProvider = transfer.Gateway,
            Amount = transfer.Amount,
            PaymentType = "Transfer",
            PaymentStatus = status,
            WebhookPayload = transfer.RawPayload,
            PaidAt = transfer.PaidAt ?? now,
            PaymentPurpose = ContractPaymentRules.PaymentPurpose,
            RequestedAt = now,
            RequestKey = Guid.NewGuid().ToString("N"),
            ProviderTransactionReference = string.IsNullOrWhiteSpace(transfer.ProviderReference)
                ? null : transfer.ProviderReference.Trim()
        });
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    public Task SetLineStatusAsync(long lineId, string status, CancellationToken cancellationToken) =>
        db.ContractMilestones.Where(m => m.ContractMilestoneId == lineId)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.PaymentStatus, status), cancellationToken);

    public async Task AddAuditAsync(long? actorUserId, long lineId, string action, string? oldValue, string? newValue,
        string? reason, DateTime now, CancellationToken cancellationToken)
    {
        db.AuditLogs.Add(new AuditLog
        {
            PerformedByUserId = actorUserId,
            EntityType = ContractPaymentRules.AuditEntity,
            EntityId = lineId,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
            Reason = reason,
            OccurredAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    public async Task<DateTime?> LastReminderAtAsync(long lineId, CancellationToken cancellationToken) =>
        await db.AuditLogs.AsNoTracking()
            .Where(a => a.EntityType == ContractPaymentRules.AuditEntity && a.EntityId == lineId
                && a.Action == ContractPaymentRules.ActionReminder)
            .OrderByDescending(a => a.OccurredAt)
            .Select(a => (DateTime?)a.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<(ContractRecord Contract, ContractLineRecord Line)>> ListNewlyOverdueAsync(
        DateOnly today, CancellationToken cancellationToken)
    {
        var contractIds = await CurrentLines(db.B2bcontracts.AsNoTracking()
                .Where(c => ContractPaymentRules.TrackedContractStatuses.Contains(c.Status)))
            .Where(m => m.DueDate < today && s_unpaid.Contains(m.PaymentStatus) && m.PaidAmount < m.Amount)
            .Select(m => m.B2bcontractId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (contractIds.Count == 0) return [];

        var records = await LoadAsync(db.B2bcontracts.AsNoTracking().Where(c => contractIds.Contains(c.B2bcontractId)),
            cancellationToken);
        return records.SelectMany(r => r.Lines
                .Where(l => l.DueDate < today && s_unpaid.Contains(l.PaymentStatus) && l.PaidAmount < l.Amount)
                .Select(l => (r.Contract, l)))
            .ToList();
    }

    public async Task AddNotificationAsync(long userId, string title, string content, string type, long contractId,
        DateTime now, CancellationToken cancellationToken)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title.Length > 200 ? title[..200] : title,
            Content = content,
            NotificationType = type,
            DeliveryChannel = "InApp",
            DeliveryStatus = "Sent",
            SentAt = now,
            RelatedRecordType = "B2BContract",
            RelatedRecordId = contractId
        });
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    /// <summary>Milestones of the version the contract currently uses.</summary>
    private IQueryable<ContractMilestone> CurrentLines(IQueryable<B2bcontract> contracts) =>
        from c in contracts
        join m in db.ContractMilestones.AsNoTracking() on c.B2bcontractId equals m.B2bcontractId
        where m.ContractVersion.VersionNo == c.CurrentVersionNo
        select m;

    private async Task<IReadOnlyList<ContractScheduleRecord>> LoadAsync(IQueryable<B2bcontract> contracts,
        CancellationToken cancellationToken)
    {
        var heads = await contracts
            .Select(c => new ContractRecord(c.B2bcontractId, c.ContractNumber, c.Status,
                c.WholesaleBuyerProfile.UserId, c.WholesaleBuyerProfile.CompanyName,
                c.WholesaleBuyerProfile.User.Email, c.FarmerProfile.UserId, c.FarmerProfile.FarmName,
                c.FarmerProfile.User.Email))
            .ToListAsync(cancellationToken);
        if (heads.Count == 0) return [];

        var ids = heads.Select(h => h.ContractId).ToList();
        var lines = await CurrentLines(db.B2bcontracts.AsNoTracking().Where(c => ids.Contains(c.B2bcontractId)))
            .Select(m => new
            {
                m.B2bcontractId,
                Line = new ContractLineRecord(m.ContractMilestoneId, m.MilestoneTitle, m.MilestoneType, m.DueDate,
                    m.Amount, m.PaidAmount, m.PaymentStatus,
                    m.PaymentTransactions.Where(p => (p.PaymentPurpose == ContractPaymentRules.PaymentPurpose
                            || p.PaymentPurpose == ContractPaymentRules.DepositPurpose)
                            && p.PaymentStatus == "Success")
                        .OrderByDescending(p => p.PaidAt).Select(p => p.PaidAt).FirstOrDefault(),
                    m.PaymentTransactions.Where(p => (p.PaymentPurpose == ContractPaymentRules.PaymentPurpose
                            || p.PaymentPurpose == ContractPaymentRules.DepositPurpose)
                            && p.PaymentStatus == "Success")
                        .OrderByDescending(p => p.PaidAt)
                        .Select(p => p.ProviderTransactionReference ?? p.TransactionCode).FirstOrDefault())
                {
                    AwaitingInspection = m.MilestoneType == "DeliveryAndPayment" && m.DeliveryQuantity > 0
                        && !m.Invoices.Any(i => i.InvoiceStatus != "Cancelled")
                }
            })
            .ToListAsync(cancellationToken);

        return heads.Select(h => new ContractScheduleRecord(h,
                lines.Where(l => l.B2bcontractId == h.ContractId).Select(l => l.Line).ToList()))
            .ToList();
    }

    private static string PaymentLabel(string status) => status switch
    {
        "Success" => "Đã nhận tiền",
        "AmountMismatch" => "Chuyển thừa – chờ đối soát",
        "Unmatched" => "Chưa áp dụng – chờ đối soát",
        _ => status
    };

    private static string AuditLabel(string action) => action switch
    {
        ContractPaymentRules.ActionOverdue => "Đánh dấu quá hạn",
        ContractPaymentRules.ActionReminder => "Gửi nhắc thanh toán",
        _ => action
    };
}
