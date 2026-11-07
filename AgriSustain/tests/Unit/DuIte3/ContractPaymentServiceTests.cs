using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Finance;
using AgriSustain.UnitTests.DuIte1;
using Xunit;

namespace AgriSustain.UnitTests.DuIte3;

/// <summary>UC-FIN-06 Track B2B Contract Payments.</summary>
public sealed class ContractPaymentServiceTests
{
    // 03:00 UTC = 10:00 in Vietnam, so "today" is 08/11/2026 for the service.
    private static readonly DateTime Now = new(2026, 11, 8, 3, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = new(2026, 11, 8);
    private readonly FakeContractPaymentRepository _repo = new();
    private readonly FakeEmail _email = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(Now));

    private ContractPaymentService Service(string role = "FinanceStaff", long userId = 7) =>
        new(_repo, new ConfirmReceivedRequestValidator(), _email, new FakeCurrentUser(userId, role), _clock);

    private static ContractLineRecord Line(long id, int dueInDays, decimal amount, decimal paid = 0,
        string status = "Pending", string type = "Installment") =>
        new(id, $"Đợt {id}", type, Today.AddDays(dueInDays), amount, paid, status, null, null);

    private void Contract(long id, string status, params ContractLineRecord[] lines) =>
        _repo.Add(new ContractRecord(id, $"HD-{id}", status, 50, $"Buyer {id}", "buyer@agri.vn", 60, $"Farm {id}",
            "farm@agri.vn"), lines);

    // ---------- Rules ----------

    [Fact(DisplayName = "UTC-FIN06-01 Contract status: Completed, Overdue, Due Soon (3 days), On Track")]
    public void ContractStatus()
    {
        Assert.Equal(ContractPaymentStatuses.Completed,
            ContractPaymentRules.ContractStatus([Line(1, -5, 100, 100, "Paid")], Today));
        Assert.Equal(ContractPaymentStatuses.Overdue,
            ContractPaymentRules.ContractStatus([Line(1, -1, 100)], Today));
        Assert.Equal(ContractPaymentStatuses.DueSoon,
            ContractPaymentRules.ContractStatus([Line(1, 3, 100)], Today));
        Assert.Equal(ContractPaymentStatuses.OnTrack,
            ContractPaymentRules.ContractStatus([Line(1, 4, 100)], Today));
    }

    [Theory(DisplayName = "UTC-FIN06-02 Received amount: lower is Partially Paid, equal is Paid, higher is refused (AF2)")]
    [InlineData(40, "PartiallyPaid")]
    [InlineData(100, "Paid")]
    [InlineData(101, null)]
    [InlineData(0, null)]
    public void Apply(decimal amount, string? expected) =>
        Assert.Equal(expected, ContractPaymentRules.Apply(Line(1, 5, 100), amount)?.Status);

    [Theory(DisplayName = "UTC-FIN06-03 Line reference CTP{id} is read from the transfer content")]
    [InlineData("TT CTP000123 hop dong", 123L)]
    [InlineData("ctp-000045", 45L)]
    [InlineData("SUB4F9A0C21B7", null)]
    public void FindLineId(string text, long? expected) =>
        Assert.Equal(expected, ContractPaymentRules.FindLineId([text]));

    // ---------- List / schedule ----------

    [Fact(DisplayName = "UTC-FIN06-04 List shows tracked contracts, overdue first, 10 per page, with totals")]
    public async Task List()
    {
        Contract(1, "Active", Line(11, 10, 1_000, 400, "PartiallyPaid"));
        Contract(2, "AwaitingDeposit", Line(21, -2, 500));
        Contract(3, "Terminated", Line(31, 1, 500));

        var list = (await Service().ListAsync(new ContractPaymentQuery(), default)).Value!;

        Assert.Equal(2, list.Total);
        Assert.Equal(10, list.PageSize);
        Assert.Equal("HD-2", list.Items[0].ContractNumber);
        Assert.Equal(ContractPaymentStatuses.Overdue, list.Items[0].PaymentStatus);
        var first = list.Items.Single(i => i.ContractId == 1);
        Assert.Equal(400, first.Received);
        Assert.Equal(600, first.Outstanding);
        Assert.Equal(1, list.Counts[ContractPaymentStatuses.Overdue]);
    }

    [Fact(DisplayName = "UTC-FIN06-05 No matching contract returns MSG01 (AF1)")]
    public async Task List_NoMatch()
    {
        var list = (await Service().ListAsync(new ContractPaymentQuery("zzz"), default)).Value!;

        Assert.Empty(list.Items);
        Assert.Equal(ContractPaymentRules.Msg01, list.Message);
    }

    [Fact(DisplayName = "UTC-FIN06-06 Status filter keeps only that payment status")]
    public async Task List_Filter()
    {
        Contract(1, "Active", Line(11, 10, 1_000));
        Contract(2, "Active", Line(21, -2, 500));

        var list = (await Service().ListAsync(new ContractPaymentQuery(Status: "overdue"), default)).Value!;

        Assert.Equal(new long[] { 2 }, list.Items.Select(i => i.ContractId));
    }

    [Fact(DisplayName = "UTC-FIN06-07 Non-finance caller is denied with MSG121 (AF6)")]
    public async Task Denied()
    {
        var result = await Service(role: "WholesaleBuyer").ListAsync(new ContractPaymentQuery(), default);
        var anonymous = await Service(userId: 0).GetScheduleAsync(1, default);

        Assert.Equal(ContractPaymentRules.Msg121, result.Error!.Message);
        Assert.Equal("Unauthorized", anonymous.Error!.Kind);
    }

    [Fact(DisplayName = "UTC-FIN06-08 Schedule shows each line with reference, status and allowed actions")]
    public async Task Schedule()
    {
        Contract(1, "Active", Line(11, -3, 500), Line(12, 20, 500, 500, "Paid"));

        var s = (await Service().GetScheduleAsync(1, default)).Value!;

        var overdue = s.Lines[0];
        Assert.Equal("CTP000011", overdue.PaymentReference);
        Assert.Equal(ContractLineStatuses.Overdue, overdue.Status);
        Assert.True(overdue.CanConfirm && overdue.CanRemind && overdue.CanMarkOverdue);
        Assert.False(s.Lines[1].CanConfirm);
        Assert.Equal(500, s.Outstanding);
    }

    // ---------- Confirm Received ----------

    [Fact(DisplayName = "UTC-FIN06-09 Confirm Received records the payment, audit and notifies both parties (Step 6-7)")]
    public async Task Confirm()
    {
        Contract(1, "Active", Line(11, 5, 1_000));

        var result = (await Service().ConfirmReceivedAsync(11, new ConfirmReceivedRequest(1_000, " FT123 ", "ok"), default)).Value!;

        var write = Assert.Single(_repo.Writes);
        Assert.Equal(ContractLineStatuses.Paid, write.NewLineStatus);
        Assert.Equal("FT123", write.ProviderReference);
        Assert.Equal(7, write.ActorUserId);
        Assert.Equal(ContractPaymentRules.ActionConfirm, write.AuditAction);
        Assert.Equal("Đã ghi nhận thanh toán cho hợp đồng HD-1.", result.Message);
        Assert.Equal(new long[] { 50, 60 }, _repo.Notified);
        Assert.Equal(ContractPaymentStatuses.Completed, result.Schedule.PaymentStatus);
    }

    [Fact(DisplayName = "UTC-FIN06-10 Lower amount leaves the line Partially Paid with the rest outstanding (AF2)")]
    public async Task Confirm_Partial()
    {
        Contract(1, "Active", Line(11, 5, 1_000));

        var result = (await Service().ConfirmReceivedAsync(11, new ConfirmReceivedRequest(300, "FT1", null), default)).Value!;

        Assert.Equal(ContractLineStatuses.PartiallyPaid, result.Schedule.Lines[0].Status);
        Assert.Equal(700, result.Schedule.Lines[0].Outstanding);
    }

    [Fact(DisplayName = "UTC-FIN06-11 Higher amount is blocked and sent to reconciliation (AF2)")]
    public async Task Confirm_TooHigh()
    {
        Contract(1, "Active", Line(11, 5, 1_000));

        var result = await Service().ConfirmReceivedAsync(11, new ConfirmReceivedRequest(1_500, "FT1", null), default);

        Assert.Equal("AMOUNT_EXCEEDS_DUE", result.Error!.Code);
        Assert.Empty(_repo.Writes);
    }

    [Theory(DisplayName = "UTC-FIN06-12 Amount must be positive and the statement reference is required")]
    [InlineData(0, "FT1", "amount")]
    [InlineData(100, " ", "reference")]
    public async Task Confirm_Invalid(decimal amount, string reference, string field)
    {
        Contract(1, "Active", Line(11, 5, 1_000));

        var result = await Service().ConfirmReceivedAsync(11, new ConfirmReceivedRequest(amount, reference, null), default);

        Assert.True(result.Error!.Fields!.ContainsKey(field));
    }

    [Fact(DisplayName = "UTC-FIN06-13 A statement reference already recorded cannot be used twice")]
    public async Task Confirm_ReferenceUsed()
    {
        Contract(1, "Active", Line(11, 5, 1_000));
        _repo.UsedReferences.Add("FT9");

        Assert.Equal("REFERENCE_USED",
            (await Service().ConfirmReceivedAsync(11, new ConfirmReceivedRequest(100, "FT9", null), default)).Error!.Code);
    }

    [Fact(DisplayName = "UTC-FIN06-14 Terminated contract schedule is read-only (AF5)")]
    public async Task Confirm_ReadOnly()
    {
        Contract(1, "Terminated", Line(11, 5, 1_000));

        var result = await Service().ConfirmReceivedAsync(11, new ConfirmReceivedRequest(100, "FT1", null), default);
        var schedule = (await Service().GetScheduleAsync(1, default)).Value!;

        Assert.Equal("CONTRACT_READ_ONLY", result.Error!.Code);
        Assert.True(schedule.ReadOnly);
        Assert.False(schedule.Lines[0].CanConfirm);
    }

    [Fact(DisplayName = "UTC-FIN06-15 Paying the last deposit line activates an AwaitingDeposit contract")]
    public async Task Confirm_ActivatesContract()
    {
        Contract(1, "AwaitingDeposit", Line(11, 5, 300, type: "Deposit"), Line(12, 30, 700));

        await Service().ConfirmReceivedAsync(11, new ConfirmReceivedRequest(300, "FT1", null), default);

        Assert.True(_repo.Writes.Single().ActivateContract);
    }

    // ---------- Overdue / reminder ----------

    [Fact(DisplayName = "UTC-FIN06-16 Mark Overdue needs a note and a passed due date; e-mails the buyer (AF3)")]
    public async Task MarkOverdue()
    {
        Contract(1, "Active", Line(11, -2, 500), Line(12, 3, 500));

        var noNote = await Service().MarkOverdueAsync(11, new MarkOverdueRequest(""), default);
        var notDue = await Service().MarkOverdueAsync(12, new MarkOverdueRequest("x"), default);
        var ok = await Service().MarkOverdueAsync(11, new MarkOverdueRequest("Đã gọi điện"), default);

        Assert.True(noNote.Error!.Fields!.ContainsKey("note"));
        Assert.Equal("NOT_DUE_YET", notDue.Error!.Code);
        Assert.True(ok.IsSuccess);
        Assert.Equal(ContractLineStatuses.Overdue, _repo.LineStatus(11));
        Assert.Contains(_repo.Audits, a => a.Action == ContractPaymentRules.ActionOverdue && a.Actor == 7);
        Assert.Equal(new[] { "buyer@agri.vn" }, _email.Recipients);
    }

    [Fact(DisplayName = "UTC-FIN06-17 Reminder e-mails the buyer once per line every 24 hours (AF4)")]
    public async Task Reminder()
    {
        Contract(1, "Active", Line(11, 2, 500));

        var first = await Service().SendReminderAsync(11, default);
        _clock.Advance(TimeSpan.FromHours(23));
        var tooSoon = await Service().SendReminderAsync(11, default);
        _clock.Advance(TimeSpan.FromHours(2));
        var again = await Service().SendReminderAsync(11, default);

        Assert.True(first.IsSuccess);
        Assert.Equal("TooManyRequests", tooSoon.Error!.Kind);
        Assert.True(again.IsSuccess);
        Assert.Equal(2, _email.Recipients.Count);
    }

    [Fact(DisplayName = "UTC-FIN06-18 Daily job marks unpaid past-due lines Overdue and notifies the buyer (AF3)")]
    public async Task DailyJob()
    {
        Contract(1, "Active", Line(11, -1, 500), Line(12, 5, 500));

        int marked = await Service(userId: 0).MarkOverdueDailyAsync("http://x", default);

        Assert.Equal(1, marked);
        Assert.Equal(ContractLineStatuses.Overdue, _repo.LineStatus(11));
        Assert.Contains(_repo.Audits, a => a.Action == ContractPaymentRules.ActionOverdue && a.Actor == null);
    }

    // ---------- Gateway (Step 5) ----------

    private static IncomingBankTransfer Transfer(long lineId, decimal amount, long id = 1) =>
        new("SePay", $"SEPAY-{id}", amount, null, $"FT{id}", "{}", [$"TT {ContractPaymentRules.Reference(lineId)}"]);

    [Fact(DisplayName = "UTC-FIN06-19 Verified transfer with a line reference is applied automatically")]
    public async Task Gateway_Applies()
    {
        Contract(1, "Active", Line(11, 5, 500));

        bool handled = await Service(userId: 0).TryHandleAsync(Transfer(11, 500), default);

        Assert.True(handled);
        var write = Assert.Single(_repo.Writes);
        Assert.Equal(ContractLineStatuses.Paid, write.NewLineStatus);
        Assert.Null(write.ActorUserId);
        Assert.Equal("SEPAY-1", write.TransactionCode);
    }

    [Fact(DisplayName = "UTC-FIN06-20 Transfer above the outstanding amount goes to reconciliation (AF2)")]
    public async Task Gateway_TooHigh()
    {
        Contract(1, "Active", Line(11, 5, 500));

        await Service(userId: 0).TryHandleAsync(Transfer(11, 900), default);

        Assert.Empty(_repo.Writes);
        Assert.Equal("AmountMismatch", Assert.Single(_repo.Unapplied));
    }

    [Fact(DisplayName = "UTC-FIN06-21 Webhook retry and transfers without CTP reference are not applied twice")]
    public async Task Gateway_DuplicateAndForeign()
    {
        Contract(1, "Active", Line(11, 5, 500));

        await Service(userId: 0).TryHandleAsync(Transfer(11, 200, id: 5), default);
        bool retry = await Service(userId: 0).TryHandleAsync(Transfer(11, 200, id: 5), default);
        bool foreign = await Service(userId: 0).TryHandleAsync(
            new IncomingBankTransfer("SePay", "SEPAY-9", 10, null, null, "{}", ["RC-0011AABBCCDDEEFF"]), default);

        Assert.True(retry);
        Assert.False(foreign);
        Assert.Single(_repo.Writes);
    }
}

internal sealed class FakeContractPaymentRepository : IContractPaymentRepository
{
    private readonly List<ContractScheduleRecord> _contracts = [];
    private readonly HashSet<string> _codes = [];

    public List<ContractPaymentWrite> Writes { get; } = [];
    public List<string> Unapplied { get; } = [];
    public List<long> Notified { get; } = [];
    public HashSet<string> UsedReferences { get; } = [];
    public List<(long? Actor, long LineId, string Action, DateTime At)> Audits { get; } = [];

    public void Add(ContractRecord contract, ContractLineRecord[] lines) =>
        _contracts.Add(new ContractScheduleRecord(contract, lines.ToList()));

    public string LineStatus(long lineId) =>
        _contracts.SelectMany(c => c.Lines).Single(l => l.LineId == lineId).PaymentStatus;

    public Task<IReadOnlyList<ContractScheduleRecord>> ListAsync(string? search, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ContractScheduleRecord>>(_contracts
            .Where(c => search is null || c.Contract.ContractNumber.Contains(search)).ToList());

    public Task<ContractScheduleRecord?> GetAsync(long contractId, CancellationToken ct) =>
        Task.FromResult(_contracts.FirstOrDefault(c => c.Contract.ContractId == contractId));

    public Task<ContractScheduleRecord?> GetByLineAsync(long lineId, CancellationToken ct) =>
        Task.FromResult(_contracts.FirstOrDefault(c => c.Lines.Any(l => l.LineId == lineId)));

    public Task<IReadOnlyList<ContractHistoryItem>> HistoryAsync(long contractId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ContractHistoryItem>>([]);

    public Task<bool> TransferRecordedAsync(string transactionCode, CancellationToken ct) =>
        Task.FromResult(_codes.Contains(transactionCode));

    public Task<bool> ReferenceUsedAsync(string gateway, string reference, CancellationToken ct) =>
        Task.FromResult(UsedReferences.Contains(reference));

    public Task RecordPaymentAsync(ContractPaymentWrite write, CancellationToken ct)
    {
        Writes.Add(write);
        _codes.Add(write.TransactionCode);
        UpdateLine(write.LineId, l => l with { PaidAmount = write.NewPaidAmount, PaymentStatus = write.NewLineStatus });
        Audits.Add((write.ActorUserId, write.LineId, write.AuditAction, write.Now));
        return Task.CompletedTask;
    }

    public Task RecordUnappliedAsync(IncomingBankTransfer transfer, long? contractId, long? lineId, string status,
        DateTime now, CancellationToken ct)
    {
        Unapplied.Add(status);
        _codes.Add(transfer.TransactionCode);
        return Task.CompletedTask;
    }

    public Task SetLineStatusAsync(long lineId, string status, CancellationToken ct)
    {
        UpdateLine(lineId, l => l with { PaymentStatus = status });
        return Task.CompletedTask;
    }

    public Task AddAuditAsync(long? actorUserId, long lineId, string action, string? oldValue, string? newValue,
        string? reason, DateTime now, CancellationToken ct)
    {
        Audits.Add((actorUserId, lineId, action, now));
        return Task.CompletedTask;
    }

    public Task<DateTime?> LastReminderAtAsync(long lineId, CancellationToken ct) =>
        Task.FromResult(Audits.Where(a => a.LineId == lineId && a.Action == ContractPaymentRules.ActionReminder)
            .Select(a => (DateTime?)a.At).Max());

    public Task<IReadOnlyList<(ContractRecord Contract, ContractLineRecord Line)>> ListNewlyOverdueAsync(
        DateOnly today, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<(ContractRecord, ContractLineRecord)>>(_contracts
            .Where(c => ContractPaymentRules.IsTracked(c.Contract.Status))
            .SelectMany(c => c.Lines.Where(l => l.DueDate < today && l.PaymentStatus is "Pending" or "PartiallyPaid")
                .Select(l => (c.Contract, l)))
            .ToList());

    public Task AddNotificationAsync(long userId, string title, string content, string type, long contractId,
        DateTime now, CancellationToken ct)
    {
        if (type == "ContractPaymentReceived") Notified.Add(userId);
        return Task.CompletedTask;
    }

    private void UpdateLine(long lineId, Func<ContractLineRecord, ContractLineRecord> change)
    {
        for (int i = 0; i < _contracts.Count; i++)
        {
            var lines = _contracts[i].Lines.Select(l => l.LineId == lineId ? change(l) : l).ToList();
            _contracts[i] = _contracts[i] with { Lines = lines };
        }
    }
}
