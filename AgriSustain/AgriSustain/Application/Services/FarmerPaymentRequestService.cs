using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Payouts;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class FarmerPaymentRequestService(IFinanceFarmerPayoutRepository repository,
    IFarmerPaymentRequestRepository access, FarmerPayoutRules rules, PayoutNotificationService notices)
{
    private const string Entity = "FinanceFarmerPayout";
    private static FeatureResult<T> Fail<T>(string code) => FeatureResult<T>.Fail(
        code is "MSG173" or "MSG121" ? "FORBIDDEN" : code == "MSG174" ? "CONFLICT" : code, FinancePayoutMessages.Text(code));
    private static FeatureResult<PayoutActionResult> Result(FarmerPayout payout, string code) =>
        FeatureResult<PayoutActionResult>.Ok(new(payout.FarmerPayoutId, payout.PayoutStatus,
            code == "MSG169" ? FinancePayoutMessages.SubmittedResult(payout.PayoutCode, payout.NetAmount)
                : FinancePayoutMessages.Text(code)));

    private async Task<FeatureResult<PayoutPreview>> GetCreationContextAsync(long actor, long farmerId, CancellationToken ct)
    {
        if (await access.FarmerIdAsync(actor, ct) != farmerId) return Fail<PayoutPreview>("MSG121");
        var context = await repository.LoadContextAsync(farmerId, null, false, ct);
        if (context == null) return Fail<PayoutPreview>("MSG172");
        var sources = rules.ValidateEligibleProceeds(context, null, DateTime.UtcNow);
        return FeatureResult<PayoutPreview>.Ok(Preview(context, sources, [], false));
    }

    private async Task<FeatureResult<PayoutPreview>> PreviewPayoutAsync(long actor, CreateFarmerPayoutRequest input, CancellationToken ct)
    {
        if (await access.FarmerIdAsync(actor, ct) != input.FarmerId) return Fail<PayoutPreview>("MSG121");
        if (!ValidSelection(input)) return Fail<PayoutPreview>("MSG02");
        var context = await repository.LoadContextAsync(input.FarmerId, null, false, ct);
        if (context == null) return Fail<PayoutPreview>("MSG172");
        var all = rules.ValidateEligibleProceeds(context, null, DateTime.UtcNow);
        var selected = all.Where(x => input.SelectedSourceIds.Contains(x.Id)).ToList();
        if (selected.Count != input.SelectedSourceIds.Count || selected.Any(x => !x.Eligible)) return Fail<PayoutPreview>("MSG172");
        return FeatureResult<PayoutPreview>.Ok(Preview(context, all, selected, true));
    }

    private PayoutPreview Preview(FarmerPayoutContext context, IReadOnlyList<PayoutSource> all,
        IReadOnlyList<PayoutSource> selected, bool allowCreate) => new(context.Farmer.FarmerProfileId,
        context.Farmer.FarmName, rules.Recipient(context), all,
        selected.Sum(x => x.Gross), selected.Sum(x => x.Commission), selected.Sum(x => x.Deduction), selected.Sum(x => x.Net),
        rules.BalanceVersion(context, selected), allowCreate && selected.Count > 0 && selected.All(x => x.Eligible)
            && selected.Sum(x => x.Net) > 0, rules.ValidateVerifiedRecipient(context, DateTime.UtcNow) is { } blocked
                ? FinancePayoutMessages.Text(blocked) : null);

    private static bool ValidSelection(CreateFarmerPayoutRequest input) => input.FarmerId > 0
        && input.SelectedSourceIds is { Count: > 0 and <= 100 }
        && input.SelectedSourceIds.All(x => x > 0) && input.SelectedSourceIds.Distinct().Count() == input.SelectedSourceIds.Count;

    private async Task<FeatureResult<PayoutActionResult>> CreatePayoutAsync(long actor, CreateFarmerPayoutRequest input, CancellationToken ct)
    {
        if (await access.FarmerIdAsync(actor, ct) != input.FarmerId) return Fail<PayoutActionResult>("MSG121");
        if (!ValidSelection(input) || !Guid.TryParse(input.RequestKey, out var key) || input.ExpectedBalance?.Length != 64)
            return Fail<PayoutActionResult>("MSG02");
        string requestKey = actor + ":" + key.ToString("N");
        string fingerprint = FarmerPayoutRules.Hash(JsonSerializer.Serialize(new
        {
            input.FarmerId,
            Sources = input.SelectedSourceIds.Order(),
            input.ExpectedBalance
        }));
        return await repository.SaveAtomicAsync(async () =>
        {
            var previous = await repository.FindRequestAsync(requestKey, ct);
            if (previous != null)
            {
                var audit = await repository.FindTraceAsync(FarmerPayoutRules.Hash("create:" + requestKey), ct);
                return audit?.OldValue == fingerprint ? Result(previous, "MSG169") : Fail<PayoutActionResult>("MSG174");
            }
            if (await access.FarmerIdAsync(actor, ct) != input.FarmerId) return Fail<PayoutActionResult>("MSG121");
            var context = await repository.LoadContextAsync(input.FarmerId, null, true, ct);
            if (context == null) return Fail<PayoutActionResult>("MSG172");
            var sources = rules.ValidateEligibleProceeds(context, null, DateTime.UtcNow)
                .Where(x => input.SelectedSourceIds.Contains(x.Id)).ToList();
            if (sources.Count != input.SelectedSourceIds.Count || sources.Any(x => !x.Eligible) || sources.Sum(x => x.Net) <= 0)
                return Fail<PayoutActionResult>("MSG172");
            if (rules.BalanceVersion(context, sources) != input.ExpectedBalance) return Fail<PayoutActionResult>("MSG174");
            var bank = context.Bank!;
            var payout = new FarmerPayout
            {
                FarmerProfileId = input.FarmerId,
                PayoutCode = "FPO" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant(),
                Amount = sources.Sum(x => x.Gross),
                DeductedCommission = sources.Sum(x => x.Commission),
                NetAmount = sources.Sum(x => x.Net),
                PayoutStatus = "Pending",
                RequestKey = requestKey,
                ApprovedByUserId = null,
                ApprovedAt = null,
                EligibleAt = DateTime.UtcNow,
                RecipientBankCode = bank.BankCode,
                RecipientAccountNumber = bank.AccountNumber,
                RecipientAccountHolder = bank.AccountHolder
            };
            foreach (var source in sources)
            {
                var line = context.Lines.Single(x => x.InvoiceLineId == source.Id);
                payout.PayoutItems.Add(new PayoutItem
                {
                    InvoiceLineId = source.Id,
                    OrderItemId = line.OrderItemId ?? line.ShipmentItem?.OrderItemBatchAllocation.OrderItemId,
                    ContractMilestoneId = line.Invoice.ContractMilestoneId,
                    CommissionPolicyId = source.CommissionPolicyId,
                    GrossAmount = source.Gross,
                    CommissionAmount = source.Commission,
                    CommissionRateSnapshot = source.CommissionRate,
                    NetAmount = source.Net,
                    SourceAmountSettled = 0
                });
            }
            repository.Add(payout);
            await repository.FlushAsync(ct);
            Record(payout.FarmerPayoutId, actor, "Submitted", sources, FarmerPayoutRules.Hash("create:" + requestKey), fingerprint);
            await notices.SubmittedAsync(context, payout, ct);
            return Result(payout, "MSG169");
        }, ct);
    }


    public async Task<FeatureResult<object>> ListAsync(long actor, FarmerPayoutQuery query, CancellationToken ct)
    {
        long? farmerId = await access.FarmerIdAsync(actor, ct);
        if (!farmerId.HasValue) return Fail<object>("MSG121");
        if (query.Page is < 1 or > 100000 || query.Keyword?.Length > 150
            || query.Status?.Length > 50 || (query.To == DateOnly.MaxValue || query.From == DateOnly.MinValue)) return Fail<object>("MSG172");
        if (query.From > query.To) return Fail<object>("INVALID_DATES");
        query.FarmerId = farmerId.Value;
        return FeatureResult<object>.Ok(await repository.SearchOwnedAsync(query, ct));
    }

    public async Task<FeatureResult<PayoutPreview>> ContextAsync(long actor, CancellationToken ct)
    {
        var farmerId = await access.FarmerIdAsync(actor, ct);
        return farmerId.HasValue ? await GetCreationContextAsync(actor, farmerId.Value, ct) : Fail<PayoutPreview>("MSG121");
    }

    public async Task<FeatureResult<PayoutPreview>> PreviewAsync(long actor, CreateFarmerPayoutRequest input, CancellationToken ct)
    {
        input.FarmerId = await access.FarmerIdAsync(actor, ct) ?? 0;
        return await PreviewPayoutAsync(actor, input, ct);
    }

    public async Task<FeatureResult<PayoutActionResult>> SubmitAsync(long actor, CreateFarmerPayoutRequest input, CancellationToken ct)
    {
        input.FarmerId = await access.FarmerIdAsync(actor, ct) ?? 0;
        return await CreatePayoutAsync(actor, input, ct);
    }

    public async Task<FeatureResult<object>> DetailAsync(long actor, long id, CancellationToken ct)
    {
        var farmerId = await access.FarmerIdAsync(actor, ct);
        if (!farmerId.HasValue) return Fail<object>("MSG121");
        if (await repository.FarmerForPayoutAsync(id, ct) != farmerId) return Fail<object>("MSG121");
        var context = await repository.LoadContextAsync(farmerId.Value, id, false, ct);
        if (context?.Payout == null || !context.History.Any(x => x.Action == "Submitted")) return Fail<object>("MSG172");
        var payout = context.Payout;
        return FeatureResult<object>.Ok(new
        {
            id = payout.FarmerPayoutId, code = payout.PayoutCode,
            status = FarmerPayoutRules.Paid(payout) ? "Processed" : payout.PayoutStatus,
            submittedAt = context.History.First(x => x.Action == "Submitted").OccurredAt,
            gross = payout.Amount, commission = payout.DeductedCommission,
            deduction = payout.Amount - payout.DeductedCommission - payout.NetAmount, net = payout.NetAmount,
            recipient = new { bankCode = payout.RecipientBankCode, accountNumber = payout.RecipientAccountNumber, accountHolder = payout.RecipientAccountHolder },
            payout.BankTransferRef, payout.ProcessedAt,
            rejectionReason = context.History.LastOrDefault(x => x.Action == "Rejected")?.Reason,
            sources = payout.PayoutItems.Select(x => new
            {
                reference = context.Lines.FirstOrDefault(l => l.InvoiceLineId == x.InvoiceLineId)?.Invoice.InvoiceNumber,
                gross = x.GrossAmount, commission = x.CommissionAmount,
                deduction = x.GrossAmount - x.CommissionAmount - x.NetAmount, net = x.NetAmount
            }),
            history = context.History.Select(x => new { x.Action, x.OccurredAt, x.Reason }),
            notificationFailed = context.NotificationFailed
        });
    }

    private void Record(long id, long actor, string action, object payload, string trace, string fingerprint) =>
        repository.Record(new AuditLog
        {
            EntityType = Entity, EntityId = id, PerformedByUserId = actor, Action = action,
            NewValue = JsonSerializer.Serialize(payload), OldValue = fingerprint, TraceId = trace, OccurredAt = DateTime.UtcNow
        });
}
