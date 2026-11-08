using System.Text.Json;
using System.Text.RegularExpressions;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Payouts;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class FinanceFarmerPayoutService(IFinanceFarmerPayoutRepository repository,
    FarmerPayoutRules rules, IPayoutGateway gateway)
{
    private const string Entity = "FinanceFarmerPayout";
    private static readonly Regex Operation = new(@"(?<![A-Z0-9])FPO[0-9A-F]{12}(?![A-Z0-9])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static FeatureResult<T> Fail<T>(string code) => FeatureResult<T>.Fail(
        code == "MSG173" ? "FORBIDDEN" : code == "MSG174" ? "CONFLICT" : code, FinancePayoutMessages.Text(code));
    private static FeatureResult<PayoutActionResult> Result(FarmerPayout payout, string code) =>
        FeatureResult<PayoutActionResult>.Ok(new(payout.FarmerPayoutId, payout.PayoutStatus, FinancePayoutMessages.Text(code)));

    public async Task<FeatureResult<object>> GetPayoutsAsync(long actor, FarmerPayoutQuery query, CancellationToken ct)
    {
        if (!await repository.AuthorizedAsync(actor, ct)) return Fail<object>("MSG173");
        if (query.Page < 1 || query.Page > 100000 || query.Keyword?.Length > 150 || query.Status?.Length > 50
            || query.FarmerId < 1 || query.To == DateOnly.MaxValue) return Fail<object>("MSG172");
        if (query.From > query.To) return Fail<object>("INVALID_DATES");
        return FeatureResult<object>.Ok(await repository.SearchOwnedAsync(query, ct));
    }

    public async Task<FeatureResult<object>> FarmersAsync(long actor, CancellationToken ct) =>
        await repository.AuthorizedAsync(actor, ct) ? FeatureResult<object>.Ok(await repository.FarmersAsync(ct)) : Fail<object>("MSG173");

    public async Task<FeatureResult<PayoutPreview>> GetCreationContextAsync(long actor, long farmerId, CancellationToken ct)
    {
        if (!await repository.AuthorizedAsync(actor, ct)) return Fail<PayoutPreview>("MSG173");
        var context = await repository.LoadContextAsync(farmerId, null, false, ct);
        if (context == null) return Fail<PayoutPreview>("MSG172");
        var sources = rules.ValidateEligibleProceeds(context, null, DateTime.UtcNow);
        return FeatureResult<PayoutPreview>.Ok(Preview(context, sources, [], false));
    }

    public async Task<FeatureResult<PayoutPreview>> PreviewPayoutAsync(long actor, CreateFarmerPayoutRequest input, CancellationToken ct)
    {
        if (!await repository.AuthorizedAsync(actor, ct)) return Fail<PayoutPreview>("MSG173");
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

    public async Task<FeatureResult<PayoutActionResult>> CreatePayoutAsync(long actor, CreateFarmerPayoutRequest input, CancellationToken ct)
    {
        if (!await repository.AuthorizedAsync(actor, ct)) return Fail<PayoutActionResult>("MSG173");
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
                PayoutStatus = "Prepared",
                RequestKey = requestKey,
                ApprovedByUserId = actor,
                ApprovedAt = DateTime.UtcNow,
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
            Record(payout.FarmerPayoutId, actor, "Create", sources, FarmerPayoutRules.Hash("create:" + requestKey), fingerprint);
            return Result(payout, "MSG169");
        }, ct);
    }

    public async Task<FeatureResult<object>> GetContextAsync(long actor, long id, CancellationToken ct)
    {
        if (!await repository.AuthorizedAsync(actor, ct)) return Fail<object>("MSG173");
        var context = await Load(id, false, ct);
        return context?.Payout == null ? Fail<object>("MSG172") : FeatureResult<object>.Ok(Details(context));
    }

    private async Task<FarmerPayoutContext?> Load(long id, bool update, CancellationToken ct)
    {
        long? farmer = await repository.FarmerForPayoutAsync(id, ct);
        return farmer.HasValue ? await repository.LoadContextAsync(farmer.Value, id, update, ct) : null;
    }

    private static PayoutTransferPlan? Plan(FarmerPayoutContext context) => context.History
        .Where(x => x.Action == "OpenQr").Select(x => Deserialize<PayoutTransferPlan>(x.NewValue)).LastOrDefault();

    private static T? Deserialize<T>(string? json)
    {
        try { return json == null ? default : JsonSerializer.Deserialize<T>(json); }
        catch (JsonException) { return default; }
    }

    private object Details(FarmerPayoutContext context)
    {
        var payout = context.Payout!;
        var plan = Plan(context);
        return new
        {
            id = payout.FarmerPayoutId,
            code = payout.PayoutCode,
            farmerId = payout.FarmerProfileId,
            farmer = context.Farmer.FarmName,
            status = FarmerPayoutRules.Paid(payout) ? "Processed" : payout.PayoutStatus,
            gross = payout.Amount,
            commission = payout.DeductedCommission,
            deduction = payout.Amount - payout.DeductedCommission - payout.NetAmount,
            net = payout.NetAmount,
            recipient = new { bankCode = payout.RecipientBankCode, accountNumber = payout.RecipientAccountNumber, accountHolder = payout.RecipientAccountHolder },
            plan,
            payout.BankTransferRef,
            payout.ProcessedAt,
            payout.ApprovedAt,
            version = rules.Version(context),
            sources = payout.PayoutItems.Select(item => new
            {
                id = item.InvoiceLineId,
                reference = context.Lines.FirstOrDefault(x => x.InvoiceLineId == item.InvoiceLineId)?.Invoice.InvoiceNumber,
                gross = item.GrossAmount,
                commission = item.CommissionAmount,
                deduction = item.GrossAmount - item.CommissionAmount - item.NetAmount,
                net = item.NetAmount,
                item.SourceAmountSettled
            }),
            holdReason = FarmerPayoutRules.Paid(payout) ? null : rules.ValidateBeforeTransfer(context, DateTime.UtcNow) is { } reason
                ? FinancePayoutMessages.Text(reason) : null,
            integrationMessage = plan == null && !FarmerPayoutRules.Paid(payout) && gateway.CreateQr(payout) == null
                ? FinancePayoutMessages.Text("SOURCE_CONFIG") : null,
            history = context.History.Select(x => new { x.Action, x.OccurredAt, x.Reason }),
            notificationFailed = context.NotificationFailed,
            canProcess = payout.PayoutStatus == "Prepared" && plan == null,
            canCancel = payout.PayoutStatus == "Prepared" && plan == null,
            canReconcile = payout.PayoutStatus is "Processing" or "NeedsReconciliation"
        };
    }

    public async Task<FeatureResult<PayoutActionResult>> ProcessPayoutAsync(long actor, long id, ProcessFarmerPayoutRequest input, CancellationToken ct)
    {
        if (!await repository.AuthorizedAsync(actor, ct)) return Fail<PayoutActionResult>("MSG173");
        if (!Guid.TryParse(input.RequestKey, out _) || input.ExpectedVersion?.Length != 64) return Fail<PayoutActionResult>("MSG02");
        var before = await Load(id, false, ct);
        if (before?.Payout == null) return Fail<PayoutActionResult>("MSG172");
        if (before.Payout.PayoutStatus == "Prepared" && Plan(before) == null)
        {
            var ready = gateway.CreateQr(before.Payout);
            if (ready == null) return Fail<PayoutActionResult>("SOURCE_CONFIG");
            if (FarmerPayoutRules.BankKey(ready.SourceBankCode) is "MB" or "MBBANK")
            {
                // Verify the read API can be reached before exposing a payable QR for MB.
                var probe = await gateway.GetTransferStatusAsync(ready, ct);
                if (!probe.LookupAvailable) return Fail<PayoutActionResult>("SOURCE_CONFIG");
            }
        }
        return await repository.SaveAtomicAsync(async () =>
        {
            var context = await Load(id, true, ct);
            if (context?.Payout == null) return Fail<PayoutActionResult>("MSG172");
            var payout = context.Payout;
            if (FarmerPayoutRules.Paid(payout)) return Result(payout, "MSG171");
            if (Plan(context) != null) return input.ConfirmedNetAmount == payout.NetAmount
                ? Result(payout, "MSG175") : Fail<PayoutActionResult>("MSG174");
            if (payout.PayoutStatus != "Prepared") return Fail<PayoutActionResult>("MSG174");
            if (rules.Version(context) != input.ExpectedVersion || input.ConfirmedNetAmount != payout.NetAmount) return Fail<PayoutActionResult>("MSG174");
            if (rules.ValidateBeforeTransfer(context, DateTime.UtcNow) is { } blocked) return Fail<PayoutActionResult>(blocked);
            var plan = gateway.CreateQr(payout);
            if (plan == null) return Fail<PayoutActionResult>("SOURCE_CONFIG");
            payout.PayoutStatus = "Processing";
            payout.ApprovedByUserId = actor;
            Record(id, actor, "OpenQr", plan, FarmerPayoutRules.Hash("open:" + id));
            return Result(payout, "QR_READY");
        }, ct);
    }

    public async Task<FeatureResult<PayoutActionResult>> CancelUnsubmittedAsync(long actor, long id, PayoutActionRequest input, CancellationToken ct)
    {
        if (!await repository.AuthorizedAsync(actor, ct)) return Fail<PayoutActionResult>("MSG173");
        if (!Guid.TryParse(input.RequestKey, out _)) return Fail<PayoutActionResult>("MSG02");
        return await repository.SaveAtomicAsync(async () =>
        {
            var context = await Load(id, true, ct);
            if (context?.Payout == null) return Fail<PayoutActionResult>("MSG172");
            var payout = context.Payout;
            if (payout.PayoutStatus == "Cancelled") return Result(payout, "CANCELLED");
            if (payout.PayoutStatus != "Prepared" || Plan(context) != null || input.ExpectedVersion != rules.Version(context))
                return Fail<PayoutActionResult>("MSG174");
            payout.PayoutStatus = "Cancelled";
            Record(id, actor, "CancelUnsubmitted", new { payout.PayoutCode });
            return Result(payout, "CANCELLED");
        }, ct);
    }

    public async Task<FeatureResult<PayoutActionResult>> ReconcilePayoutAsync(long actor, long id, ReconcileFarmerPayoutRequest input, CancellationToken ct)
    {
        if (!await repository.AuthorizedAsync(actor, ct)) return Fail<PayoutActionResult>("MSG173");
        return await CheckExisting(id, input.ExistingOperationKey, ct);
    }

    public Task<FeatureResult<PayoutActionResult>> ReconcileSystemAsync(long id, CancellationToken ct) => CheckExisting(id, null, ct);

    private async Task<FeatureResult<PayoutActionResult>> CheckExisting(long id, string? expectedOperation, CancellationToken ct)
    {
        var context = await Load(id, false, ct);
        if (context?.Payout == null) return Fail<PayoutActionResult>("MSG172");
        if (FarmerPayoutRules.Paid(context.Payout)) return Result(context.Payout, "MSG171");
        var plan = Plan(context);
        if (plan == null || (expectedOperation != null && expectedOperation != plan.OperationKey)) return Fail<PayoutActionResult>("MSG174");
        var lookup = await gateway.GetTransferStatusAsync(plan, ct);
        // A query can only consume observed transactions. Absence never means failure or permission to resend.
        foreach (var transfer in lookup.Transfers.Where(x => ContainsOperation(x.Content, plan.OperationKey)))
            await RecordOutgoingAsync(transfer, ct);
        var fresh = await Load(id, false, ct);
        return Result(fresh!.Payout!, FarmerPayoutRules.Paid(fresh.Payout!) ? "MSG171" : "MSG175");
    }

    public bool AuthenticateWebhook(string authorization, string alternateKey) => gateway.AuthenticateWebhook(authorization, alternateKey);

    public async Task<bool> RecordOutgoingAsync(PayoutOutgoingTransfer transfer, CancellationToken ct)
    {
        var matches = Operation.Matches(transfer.Content).Select(x => x.Value.ToUpperInvariant()).Distinct().ToList();
        if (matches.Count != 1 || transfer.EventId.Length is < 1 or > 100 || transfer.RawPayload.Length > 100000) return false;
        long? id = await repository.FindOperationAsync(matches[0], ct);
        if (!id.HasValue) return false;
        return await repository.SaveAtomicAsync(async () =>
        {
            string trace = FarmerPayoutRules.Hash("sepay-out:" + transfer.EventId);
            var previous = await repository.FindTraceAsync(trace, ct);
            if (previous != null)
            {
                // Conflicting replay does not replace the original bank evidence or create another settlement.
                return previous.EntityId == id && previous.OldValue == FarmerPayoutRules.Hash(transfer.RawPayload);
            }
            var context = await Load(id.Value, true, ct);
            if (context?.Payout == null) return false;
            var payout = context.Payout;
            var plan = Plan(context);
            if (plan == null) return false;
            bool match = payout.PayoutStatus is "Processing" or "NeedsReconciliation"
                && FarmerPayoutRules.Digits(transfer.SourceAccountNumber) == FarmerPayoutRules.Digits(plan.SourceAccountNumber)
                && SameBank(transfer.Gateway, plan.SourceBankCode)
                && transfer.Amount == plan.Amount && transfer.Amount == payout.NetAmount
                && transfer.At >= plan.OpenedAt.AddSeconds(-5) && transfer.At <= DateTime.UtcNow.AddMinutes(5)
                && ContainsOperation(transfer.Content, plan.OperationKey);
            if (FarmerPayoutRules.Paid(payout))
            {
                Record(id.Value, null, "AdditionalOut", transfer, trace, FarmerPayoutRules.Hash(transfer.RawPayload));
                return true;
            }
            Record(id.Value, null, match ? "VerifiedOut" : "MismatchOut", transfer, trace,
                FarmerPayoutRules.Hash(transfer.RawPayload));
            if (!match)
            {
                payout.PayoutStatus = "NeedsReconciliation";
                return true;
            }
            // This means verified debit from the source, not an assertion of beneficiary receipt.
            payout.PayoutStatus = "Processed";
            payout.ProcessedAt = transfer.At;
            payout.BankTransferRef = transfer.EventId;
            foreach (var item in payout.PayoutItems) item.SourceAmountSettled = item.GrossAmount;
            Record(id.Value, null, "Settled", new
            {
                transfer.EventId,
                transfer.BankReference,
                transfer.At,
                transfer.Amount,
                Basis = "VerifiedSourceDebit",
                Recipient = payout.RecipientAccountNumber
            });
            QueueSuccess(context, transfer);
            return true;
        }, ct);
    }

    private static bool ContainsOperation(string content, string operation) => Operation.Matches(content)
        .Any(x => string.Equals(x.Value, operation, StringComparison.OrdinalIgnoreCase));

    private static bool SameBank(string actual, string expected)
    {
        string Canonical(string text) => FarmerPayoutRules.BankKey(text) switch
        {
            "MB" or "MBBANK" or "MILITARYBANK" => "MB",
            "TPB" or "TPBANK" => "TPB",
            "ICB" or "VIETINBANK" or "CTG" => "ICB",
            "STB" or "SACOMBANK" => "STB",
            var value => value
        };
        return Canonical(actual) == Canonical(expected);
    }

    private void Record(long id, long? actor, string action, object payload, string? trace = null, string? fingerprint = null) =>
        repository.Record(new AuditLog
        {
            EntityType = Entity,
            EntityId = id,
            PerformedByUserId = actor,
            Action = action,
            OldValue = fingerprint,
            NewValue = JsonSerializer.Serialize(payload),
            TraceId = trace,
            OccurredAt = DateTime.UtcNow
        });

    private void QueueSuccess(FarmerPayoutContext context, PayoutOutgoingTransfer transfer)
    {
        var payout = context.Payout!;
        string content = $"Khoản chi {payout.PayoutCode}: {payout.NetAmount:N0} VND, ngân hàng {payout.RecipientBankCode}, "
            + $"tài khoản {payout.RecipientAccountNumber}. Đã xác minh tiền ra từ tài khoản nguồn qua SePay. "
            + $"Tham chiếu {transfer.BankReference ?? transfer.EventId}.";
        repository.Queue(new[] { "InApp", "Email" }.Select(channel => new Notification
        {
            UserId = context.Farmer.UserId,
            Title = "Kết quả chi trả " + payout.PayoutCode,
            Content = content,
            NotificationType = Entity,
            DeliveryChannel = channel,
            DeliveryStatus = channel == "InApp" ? "Sent" : "Pending",
            SentAt = DateTime.UtcNow,
            RelatedRecordType = "FarmerPayout",
            RelatedRecordId = payout.FarmerPayoutId,
            PayloadJson = JsonSerializer.Serialize(new { EventKey = transfer.EventId, payout.PayoutCode })
        }));
    }
}
