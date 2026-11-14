using System.Globalization;
using System.Text.RegularExpressions;
using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces;
using FluentValidation;

namespace AgriSustain.Application.Services.Finance;

// UC-FIN-06 Track B2B Contract Payments (Finance Staff).

public static class ContractLineStatuses
{
    public const string Pending = "Pending";
    public const string PartiallyPaid = "PartiallyPaid";
    public const string Paid = "Paid";
    public const string Overdue = "Overdue";

    /// <summary>Older rows use the database default "Unpaid"; it means Pending.</summary>
    public static string Normalize(string? status) => status switch
    {
        PartiallyPaid or "Partially Paid" or "Partial" => PartiallyPaid,
        Paid or "Completed" => Paid,
        Overdue => Overdue,
        _ => Pending
    };
}

public static class ContractPaymentStatuses
{
    public const string OnTrack = "OnTrack";
    public const string DueSoon = "DueSoon";
    public const string Overdue = "Overdue";
    public const string Completed = "Completed";
    public static readonly string[] All = [OnTrack, DueSoon, Overdue, Completed];
}

public static class ContractPaymentRules
{
    public const int PageSize = 10;
    public const int DueSoonDays = 3;
    public static readonly TimeSpan ReminderCooldown = TimeSpan.FromHours(24);
    public const string PaymentPurpose = "ContractPayment";
    public const string DepositType = "Deposit";
    public const string DepositPurpose = "ContractDeposit";
    public const string ManualGateway = "BankTransfer";
    public static readonly string[] TrackedContractStatuses = ["AwaitingDeposit", "Active"];

    public const string AuditEntity = "ContractMilestone";
    public const string ActionConfirm = "ContractPayment:ConfirmReceived";
    public const string ActionAutoApply = "ContractPayment:GatewayApplied";
    public const string ActionOverdue = "ContractPayment:MarkOverdue";
    public const string ActionReminder = "ContractPayment:Reminder";

    public const string Msg01 = "Không có kết quả tìm kiếm.";
    public const string Msg121 = "Bạn không có quyền truy cập bản ghi hoặc thực hiện thao tác này.";
    public const string Recorded = "Đã ghi nhận thanh toán cho hợp đồng {0}.";
    public const string AmountTooHigh =
        "Số tiền nhận lớn hơn số còn phải thu của đợt này. Khoản thanh toán được chuyển sang đối soát (UC-FIN-02).";
    public const string ReadOnly = "Hợp đồng đã chấm dứt hoặc tạm dừng; lịch thanh toán chỉ được xem.";

    private static readonly Regex s_reference = new(@"CTP(\d{6,12})",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Transfer content shown on each schedule line, e.g. "CTP000123".</summary>
    public static string Reference(long lineId) => "CTP" + lineId.ToString("D6", CultureInfo.InvariantCulture);

    public static long? FindLineId(IEnumerable<string?> texts)
    {
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            var match = s_reference.Match(text.Replace(" ", "").Replace("-", ""));
            if (match.Success && long.TryParse(match.Groups[1].Value, NumberStyles.None,
                    CultureInfo.InvariantCulture, out long id))
            {
                return id;
            }
        }
        return null;
    }

    public static decimal Outstanding(ContractLineRecord line) => Math.Max(0, line.Amount - line.PaidAmount);

    /// <summary>Line status shown on the schedule; an unpaid line past its due date reads as Overdue.</summary>
    public static string LineStatus(ContractLineRecord line, DateOnly today)
    {
        string status = ContractLineStatuses.Normalize(line.PaymentStatus);
        if (status == ContractLineStatuses.Paid || line.PaidAmount >= line.Amount && line.Amount > 0)
        {
            return ContractLineStatuses.Paid;
        }
        return line.DueDate < today ? ContractLineStatuses.Overdue : status;
    }

    /// <summary>Step 2 contract payment status.</summary>
    public static string ContractStatus(IReadOnlyCollection<ContractLineRecord> lines, DateOnly today)
    {
        if (lines.Count > 0 && lines.All(l => LineStatus(l, today) == ContractLineStatuses.Paid))
        {
            return ContractPaymentStatuses.Completed;
        }
        if (lines.Any(l => LineStatus(l, today) == ContractLineStatuses.Overdue))
        {
            return ContractPaymentStatuses.Overdue;
        }
        return lines.Any(l => LineStatus(l, today) != ContractLineStatuses.Paid
                && l.DueDate <= today.AddDays(DueSoonDays))
            ? ContractPaymentStatuses.DueSoon
            : ContractPaymentStatuses.OnTrack;
    }

    /// <summary>Step 6 / AF2: a lower amount leaves the line Partially Paid; a higher amount is refused.</summary>
    public static (decimal PaidAmount, string Status)? Apply(ContractLineRecord line, decimal amount)
    {
        if (amount <= 0 || amount > Outstanding(line)) return null;
        decimal paid = line.PaidAmount + amount;
        return (paid, paid >= line.Amount ? ContractLineStatuses.Paid : ContractLineStatuses.PartiallyPaid);
    }

    public static bool IsTracked(string contractStatus) => TrackedContractStatuses.Contains(contractStatus);

    public static bool IsDeposit(string type) => type.Contains("Deposit", StringComparison.OrdinalIgnoreCase);
}

public sealed record ContractRecord(
    long ContractId,
    string ContractNumber,
    string Status,
    long BuyerUserId,
    string BuyerName,
    string BuyerEmail,
    long FarmerUserId,
    string FarmName,
    string FarmerEmail);

public sealed record ContractLineRecord(
    long LineId,
    string Title,
    string Type,
    DateOnly DueDate,
    decimal Amount,
    decimal PaidAmount,
    string PaymentStatus,
    DateTime? LastPaidAt,
    string? LastReference)
{
    public bool AwaitingInspection { get; init; }
}

public sealed record ContractScheduleRecord(ContractRecord Contract, IReadOnlyList<ContractLineRecord> Lines);

public sealed record ContractHistoryItem(DateTime At, string Kind, string Description, decimal? Amount,
    string? Reference, string? By);

/// <summary>Everything written for one received payment (Step 6) in a single save.</summary>
public sealed record ContractPaymentWrite(
    long ContractId,
    long LineId,
    decimal Amount,
    decimal NewPaidAmount,
    string NewLineStatus,
    string TransactionCode,
    string Gateway,
    string? ProviderReference,
    DateTime PaidAt,
    string? RawPayload,
    long? ActorUserId,
    string AuditAction,
    string? Note,
    bool ActivateContract,
    DateTime Now);

public sealed record ContractPaymentQuery(string? Search = null, string? Status = null, int Page = 1);

public sealed record ContractPaymentListItemDto(
    long ContractId,
    string ContractNumber,
    string ContractStatus,
    string BuyerName,
    string FarmName,
    decimal ContractValue,
    decimal Received,
    decimal Outstanding,
    DateOnly? NextDueDate,
    string PaymentStatus);

public sealed record ContractPaymentListDto(
    IReadOnlyList<ContractPaymentListItemDto> Items, int Total, int Page, int PageSize, string? Message,
    IReadOnlyDictionary<string, int> Counts);

public sealed record ContractLineDto(
    long LineId,
    string Title,
    string Type,
    DateOnly DueDate,
    decimal Amount,
    decimal Received,
    decimal Outstanding,
    DateTime? ReceivedAt,
    string PaymentReference,
    string? LastReference,
    string Status,
    bool CanConfirm,
    bool CanRemind,
    bool CanMarkOverdue);

public sealed record ContractScheduleDto(
    long ContractId,
    string ContractNumber,
    string ContractStatus,
    string BuyerName,
    string FarmName,
    decimal ContractValue,
    decimal Received,
    decimal Outstanding,
    string PaymentStatus,
    bool ReadOnly,
    IReadOnlyList<ContractLineDto> Lines,
    IReadOnlyList<ContractHistoryItem> History);

public sealed record ConfirmReceivedRequest(decimal Amount, string? Reference, string? Note);

public sealed record MarkOverdueRequest(string? Note);

public sealed record ContractActionResultDto(string Message, ContractScheduleDto Schedule);

public sealed class ConfirmReceivedRequestValidator : AbstractValidator<ConfirmReceivedRequest>
{
    public ConfirmReceivedRequestValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("Số tiền nhận phải lớn hơn 0.");
        RuleFor(x => x.Reference).NotEmpty().WithMessage("Vui lòng nhập mã giao dịch trên sao kê.")
            .MaximumLength(100).WithMessage("Mã giao dịch tối đa 100 ký tự.");
        RuleFor(x => x.Note).MaximumLength(500).WithMessage("Ghi chú tối đa 500 ký tự.");
    }
}

public interface IContractPaymentRepository
{
    Task<IReadOnlyList<ContractScheduleRecord>> ListAsync(string? search, CancellationToken cancellationToken);

    Task<ContractScheduleRecord?> GetAsync(long contractId, CancellationToken cancellationToken);

    Task<ContractScheduleRecord?> GetByLineAsync(long lineId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContractHistoryItem>> HistoryAsync(long contractId, CancellationToken cancellationToken);

    Task<bool> TransferRecordedAsync(string transactionCode, CancellationToken cancellationToken);

    Task<bool> ReferenceUsedAsync(string gateway, string reference, CancellationToken cancellationToken);

    Task<bool> RecordPaymentAsync(ContractPaymentWrite write, CancellationToken cancellationToken);

    /// <summary>Gateway credit that cannot be applied (AF2 / AF5); kept for reconciliation (UC-FIN-02).</summary>
    Task RecordUnappliedAsync(IncomingBankTransfer transfer, long? contractId, long? lineId, string status,
        DateTime now, CancellationToken cancellationToken);

    Task SetLineStatusAsync(long lineId, string status, CancellationToken cancellationToken);

    Task AddAuditAsync(long? actorUserId, long lineId, string action, string? oldValue, string? newValue,
        string? reason, DateTime now, CancellationToken cancellationToken);

    Task<DateTime?> LastReminderAtAsync(long lineId, CancellationToken cancellationToken);

    /// <summary>Unpaid lines of tracked contracts whose due date has passed and that are not marked Overdue yet.</summary>
    Task<IReadOnlyList<(ContractRecord Contract, ContractLineRecord Line)>> ListNewlyOverdueAsync(DateOnly today,
        CancellationToken cancellationToken);

    Task AddNotificationAsync(long userId, string title, string content, string type, long contractId, DateTime now,
        CancellationToken cancellationToken);
}

public interface IContractPaymentService
{
    Task<Result<ContractPaymentListDto>> ListAsync(ContractPaymentQuery query, CancellationToken cancellationToken);

    Task<Result<ContractScheduleDto>> GetScheduleAsync(long contractId, CancellationToken cancellationToken);

    Task<Result<ContractActionResultDto>> ConfirmReceivedAsync(long lineId, ConfirmReceivedRequest request,
        CancellationToken cancellationToken);

    Task<Result<ContractActionResultDto>> MarkOverdueAsync(long lineId, MarkOverdueRequest request,
        CancellationToken cancellationToken);

    Task<Result<ContractActionResultDto>> SendReminderAsync(long lineId, CancellationToken cancellationToken);

    /// <summary>AF3 daily job. Returns the number of lines marked Overdue.</summary>
    Task<int> MarkOverdueDailyAsync(string paymentLink, CancellationToken cancellationToken);
}

public sealed class ContractPaymentService(
    IContractPaymentRepository repository,
    IValidator<ConfirmReceivedRequest> validator,
    INotificationGateway notifications,
    ICurrentActor currentActor,
    TimeProvider timeProvider) : IContractPaymentService, IBankTransferHandler
{
    private static readonly string[] s_financeRoles = ["FinanceStaff", "Finance Staff"];

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>Contract dates are calendar days in Vietnam time (UTC+7).</summary>
    private DateOnly Today => DateOnly.FromDateTime(Now.AddHours(7));

    public async Task<Result<ContractPaymentListDto>> ListAsync(ContractPaymentQuery query,
        CancellationToken cancellationToken)
    {
        if (Denied() is { } denied) return Result<ContractPaymentListDto>.FailureResult(denied);

        DateOnly today = Today;
        string? search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var rows = (await repository.ListAsync(search, cancellationToken))
            .Where(r => ContractPaymentRules.IsTracked(r.Contract.Status))
            .Select(r => ListItem(r, today))
            .ToList();

        var counts = ContractPaymentStatuses.All.ToDictionary(s => s, s => rows.Count(r => r.PaymentStatus == s));
        string? status = ContractPaymentStatuses.All.FirstOrDefault(s =>
            string.Equals(s, query.Status?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (status != null) rows = rows.Where(r => r.PaymentStatus == status).ToList();

        // AF3: overdue contracts first, then the nearest due date.
        rows = rows.OrderBy(r => r.PaymentStatus == ContractPaymentStatuses.Overdue ? 0 : 1)
            .ThenBy(r => r.NextDueDate ?? DateOnly.MaxValue)
            .ThenBy(r => r.ContractNumber)
            .ToList();

        int page = query.Page < 1 ? 1 : query.Page;
        var items = rows.Skip((page - 1) * ContractPaymentRules.PageSize).Take(ContractPaymentRules.PageSize).ToList();
        return Result<ContractPaymentListDto>.SuccessResult(new ContractPaymentListDto(items, rows.Count, page,
            ContractPaymentRules.PageSize, rows.Count == 0 ? ContractPaymentRules.Msg01 : null, counts));
    }

    public async Task<Result<ContractScheduleDto>> GetScheduleAsync(long contractId, CancellationToken cancellationToken)
    {
        if (Denied() is { } denied) return Result<ContractScheduleDto>.FailureResult(denied);
        var record = await repository.GetAsync(contractId, cancellationToken);
        return record is null
            ? Result<ContractScheduleDto>.FailureResult(NotFound())
            : Result<ContractScheduleDto>.SuccessResult(await ScheduleAsync(record, cancellationToken));
    }

    public async Task<Result<ContractActionResultDto>> ConfirmReceivedAsync(long lineId,
        ConfirmReceivedRequest request, CancellationToken cancellationToken)
    {
        if (Denied() is { } denied) return Result<ContractActionResultDto>.FailureResult(denied);
        Error? invalid = await RequestValidation.CheckAsync(validator, request, cancellationToken);
        if (invalid != null) return Result<ContractActionResultDto>.FailureResult(invalid);

        var (record, line, error) = await LineAsync(lineId, cancellationToken);
        if (error != null) return Result<ContractActionResultDto>.FailureResult(error);
        if (ContractPaymentRules.IsDeposit(line!.Type) && decimal.Truncate(request.Amount) != request.Amount)
            return Result<ContractActionResultDto>.Fail("Số tiền đặt cọc phải là số nguyên VND.");

        if (ContractPaymentRules.LineStatus(line!, Today) == ContractLineStatuses.Paid)
        {
            return Result<ContractActionResultDto>.FailureResult(new Error("LINE_ALREADY_PAID",
                "Đợt thanh toán này đã được thanh toán đủ.", "Conflict"));
        }

        var applied = ContractPaymentRules.Apply(line!, request.Amount);
        if (applied is null)
        {
            // AF2: a higher amount is not confirmed here.
            return Result<ContractActionResultDto>.FailureResult(new Error("AMOUNT_EXCEEDS_DUE",
                ContractPaymentRules.AmountTooHigh, "Validation",
                new Dictionary<string, string[]> { ["amount"] = [ContractPaymentRules.AmountTooHigh] }));
        }

        string reference = request.Reference!.Trim();
        if (await repository.ReferenceUsedAsync(ContractPaymentRules.ManualGateway, reference, cancellationToken))
        {
            return Result<ContractActionResultDto>.FailureResult(new Error("REFERENCE_USED",
                "Mã giao dịch này đã được ghi nhận trước đó.", "Conflict",
                new Dictionary<string, string[]> { ["reference"] = ["Mã giao dịch này đã được ghi nhận trước đó."] }));
        }

        DateTime now = Now;
        if (!await RecordAsync(record!, line!, request.Amount, applied.Value,
            $"CTM-{lineId}-{Guid.NewGuid():N}", ContractPaymentRules.ManualGateway, reference, now, null,
            currentActor.UserId, ContractPaymentRules.ActionConfirm, request.Note?.Trim(), now, cancellationToken))
            return Result<ContractActionResultDto>.Fail("Đợt thanh toán đã thay đổi. Vui lòng tải lại và đối chiếu giao dịch.", 409);

        return await ActionResultAsync(record!.Contract.ContractId,
            string.Format(ContractPaymentRules.Recorded, record.Contract.ContractNumber), cancellationToken);
    }

    public async Task<Result<ContractActionResultDto>> MarkOverdueAsync(long lineId, MarkOverdueRequest request,
        CancellationToken cancellationToken)
    {
        if (Denied() is { } denied) return Result<ContractActionResultDto>.FailureResult(denied);
        string? note = request.Note?.Trim();
        if (string.IsNullOrEmpty(note) || note.Length > 500)
        {
            const string message = "Vui lòng nhập ghi chú (tối đa 500 ký tự).";
            return Result<ContractActionResultDto>.FailureResult(new Error("INVALID_INPUT", message, "Validation",
                new Dictionary<string, string[]> { ["note"] = [message] }));
        }

        var (record, line, error) = await LineAsync(lineId, cancellationToken);
        if (error != null) return Result<ContractActionResultDto>.FailureResult(error);

        string current = ContractLineStatuses.Normalize(line!.PaymentStatus);
        if (ContractPaymentRules.LineStatus(line, Today) == ContractLineStatuses.Paid || current == ContractLineStatuses.Overdue)
        {
            return Result<ContractActionResultDto>.FailureResult(new Error("INVALID_STATE",
                "Chỉ đánh dấu quá hạn cho đợt chưa thanh toán đủ và chưa bị đánh dấu quá hạn.", "Conflict"));
        }
        if (line.DueDate >= Today)
        {
            return Result<ContractActionResultDto>.FailureResult(new Error("NOT_DUE_YET",
                "Đợt thanh toán này chưa đến hạn.", "Conflict"));
        }

        await MarkLineOverdueAsync(record!.Contract, line, currentActor.UserId, note, null, cancellationToken);
        return await ActionResultAsync(record.Contract.ContractId,
            $"Đã đánh dấu quá hạn đợt \"{line.Title}\" và gửi thông báo cho bên mua.", cancellationToken);
    }

    public async Task<Result<ContractActionResultDto>> SendReminderAsync(long lineId,
        CancellationToken cancellationToken)
    {
        if (Denied() is { } denied) return Result<ContractActionResultDto>.FailureResult(denied);
        var (record, line, error) = await LineAsync(lineId, cancellationToken);
        if (error != null) return Result<ContractActionResultDto>.FailureResult(error);

        if (ContractPaymentRules.LineStatus(line!, Today) == ContractLineStatuses.Paid)
        {
            return Result<ContractActionResultDto>.FailureResult(new Error("INVALID_STATE",
                "Đợt thanh toán này đã được thanh toán đủ.", "Conflict"));
        }

        DateTime now = Now;
        // AF4: at most one reminder per line every 24 hours.
        if (await repository.LastReminderAtAsync(lineId, cancellationToken) is DateTime last
            && now - last < ContractPaymentRules.ReminderCooldown)
        {
            var next = last + ContractPaymentRules.ReminderCooldown;
            return Result<ContractActionResultDto>.FailureResult(new Error("REMINDER_TOO_SOON",
                $"Đợt này đã được nhắc lúc {last.AddHours(7):HH:mm dd/MM}. Có thể nhắc lại sau {next.AddHours(7):HH:mm dd/MM}.",
                "TooManyRequests"));
        }

        var contract = record!.Contract;
        string subject = $"Nhắc thanh toán hợp đồng {contract.ContractNumber}";
        string content = $"Đợt \"{line!.Title}\" của hợp đồng {contract.ContractNumber} cần thanh toán "
            + $"{ContractPaymentRules.Outstanding(line):N0} đ, hạn {line.DueDate:dd/MM/yyyy}. "
            + $"Chuyển khoản qua SePay/VietQR với nội dung {ContractPaymentRules.Reference(line.LineId)}.";
        try
        {
            await notifications.SendEmailAsync(contract.BuyerEmail, subject, content,
                $"CTP-REMIND:{lineId}:{now:yyyyMMddHHmm}", cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            return Result<ContractActionResultDto>.FailureResult(new Error("EMAIL_UNAVAILABLE",
                "Không gửi được email nhắc thanh toán. Vui lòng thử lại sau.", "ServiceUnavailable"));
        }

        await repository.AddNotificationAsync(contract.BuyerUserId, subject, content, "ContractPaymentReminder",
            contract.ContractId, now, cancellationToken);
        await repository.AddAuditAsync(currentActor.UserId, lineId, ContractPaymentRules.ActionReminder, null,
            ContractPaymentRules.Outstanding(line).ToString(CultureInfo.InvariantCulture), null, now, cancellationToken);
        return await ActionResultAsync(contract.ContractId, "Đã gửi email nhắc thanh toán cho bên mua.",
            cancellationToken);
    }

    public async Task<int> MarkOverdueDailyAsync(string paymentLink, CancellationToken cancellationToken)
    {
        int marked = 0;
        foreach (var (contract, line) in await repository.ListNewlyOverdueAsync(Today, cancellationToken))
        {
            await MarkLineOverdueAsync(contract, line, null, "Tự động: quá hạn thanh toán", paymentLink,
                cancellationToken);
            marked++;
        }
        return marked;
    }

    /// <summary>Step 5: a verified SePay credit whose content carries "CTP{lineId}" is applied automatically.</summary>
    public async Task<bool> TryHandleAsync(IncomingBankTransfer transfer, CancellationToken cancellationToken)
    {
        if (ContractPaymentRules.FindLineId(transfer.ReferenceTexts) is not long lineId) return false;
        if (await repository.TransferRecordedAsync(transfer.TransactionCode, cancellationToken)) return true;

        var record = await repository.GetByLineAsync(lineId, cancellationToken);
        var line = record?.Lines.FirstOrDefault(l => l.LineId == lineId);
        if (record is null || line is null) return false;
        if (!string.IsNullOrWhiteSpace(transfer.ProviderReference)
            && await repository.ReferenceUsedAsync(transfer.Gateway, transfer.ProviderReference.Trim(), cancellationToken))
            return true;

        DateTime now = Now;
        if (!ContractPaymentRules.IsTracked(record.Contract.Status) || line.AwaitingInspection)
        {
            // AF5: the schedule of a terminated or suspended contract is read-only.
            await repository.RecordUnappliedAsync(transfer, record.Contract.ContractId, lineId, "Unmatched", now,
                cancellationToken);
            return true;
        }

        var applied = ContractPaymentRules.Apply(line, transfer.Amount);
        if (applied is null || (ContractPaymentRules.IsDeposit(line.Type) && decimal.Truncate(transfer.Amount) != transfer.Amount))
        {
            // AF2: more than the outstanding amount goes to reconciliation.
            await repository.RecordUnappliedAsync(transfer, record.Contract.ContractId, lineId, "AmountMismatch", now,
                cancellationToken);
            return true;
        }

        if (!await RecordAsync(record, line, transfer.Amount, applied.Value, transfer.TransactionCode, transfer.Gateway,
            transfer.ProviderReference, transfer.PaidAt ?? now, transfer.RawPayload, null,
            ContractPaymentRules.ActionAutoApply, null, now, cancellationToken)
            && !await repository.TransferRecordedAsync(transfer.TransactionCode, cancellationToken))
            throw new InvalidOperationException("Contract payment changed while applying the transfer.");
        return true;
    }

    private Error? Denied()
    {
        if (currentActor.UserId <= 0)
        {
            return new Error("UNAUTHORIZED", "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.", "Unauthorized");
        }
        return s_financeRoles.Contains(currentActor.Role)
            ? null
            : new Error("FORBIDDEN", ContractPaymentRules.Msg121, "Forbidden");
    }

    private static Error NotFound() =>
        new("CONTRACT_NOT_FOUND", "Không tìm thấy hợp đồng hoặc đợt thanh toán.", "NotFound");

    /// <summary>Loads a line with its contract and refuses changes on a read-only schedule (AF5).</summary>
    private async Task<(ContractScheduleRecord? Record, ContractLineRecord? Line, Error? Error)> LineAsync(long lineId,
        CancellationToken cancellationToken)
    {
        var record = await repository.GetByLineAsync(lineId, cancellationToken);
        var line = record?.Lines.FirstOrDefault(l => l.LineId == lineId);
        if (record is null || line is null) return (null, null, NotFound());
        if (!ContractPaymentRules.IsTracked(record.Contract.Status))
        {
            return (null, null, new Error("CONTRACT_READ_ONLY", ContractPaymentRules.ReadOnly, "Conflict"));
        }
        if (line.AwaitingInspection)
            return (null, null, new Error("AWAITING_INSPECTION", "Chưa có hóa đơn sau kiểm nhận.", "Conflict"));
        return (record, line, null);
    }

    private async Task<bool> RecordAsync(ContractScheduleRecord record, ContractLineRecord line, decimal amount,
        (decimal PaidAmount, string Status) applied, string transactionCode, string gateway, string? reference,
        DateTime paidAt, string? rawPayload, long? actor, string action, string? note, DateTime now,
        CancellationToken cancellationToken)
    {
        var contract = record.Contract;
        // A contract waiting for its deposit becomes Active once every deposit line is paid.
        bool activate = contract.Status == "AwaitingDeposit"
            && ContractPaymentRules.IsDeposit(line.Type)
            && record.Lines.Where(l => ContractPaymentRules.IsDeposit(l.Type)).All(l => l.LineId == line.LineId
                ? applied.Status == ContractLineStatuses.Paid
                : ContractLineStatuses.Normalize(l.PaymentStatus) == ContractLineStatuses.Paid);

        if (!await repository.RecordPaymentAsync(new ContractPaymentWrite(contract.ContractId, line.LineId, amount,
            applied.PaidAmount, applied.Status, transactionCode, gateway, reference, paidAt, rawPayload, actor, action,
            note, activate, now), cancellationToken)) return false;

        // Step 7: notify both parties.
        string title = $"Đã nhận thanh toán hợp đồng {contract.ContractNumber}";
        string content = $"Đã nhận {amount:N0} đ cho đợt \"{line.Title}\" của hợp đồng {contract.ContractNumber}"
            + (applied.Status == ContractLineStatuses.Paid ? " (đã thanh toán đủ)." :
                $"; còn phải thu {line.Amount - applied.PaidAmount:N0} đ.");
        foreach (var (userId, email) in new[] { (contract.BuyerUserId, contract.BuyerEmail),
                     (contract.FarmerUserId, contract.FarmerEmail) })
        {
            await repository.AddNotificationAsync(userId, title, content, "ContractPaymentReceived",
                contract.ContractId, now, cancellationToken);
            await TrySendAsync(email, title, content, $"CTP-PAID:{transactionCode}:{userId}", cancellationToken);
        }
        return true;
    }

    private async Task MarkLineOverdueAsync(ContractRecord contract, ContractLineRecord line, long? actor, string note,
        string? paymentLink, CancellationToken cancellationToken)
    {
        DateTime now = Now;
        await repository.SetLineStatusAsync(line.LineId, ContractLineStatuses.Overdue, cancellationToken);
        await repository.AddAuditAsync(actor, line.LineId, ContractPaymentRules.ActionOverdue,
            ContractLineStatuses.Normalize(line.PaymentStatus), ContractLineStatuses.Overdue, note, now,
            cancellationToken);

        string title = $"Quá hạn thanh toán hợp đồng {contract.ContractNumber}";
        string content = $"Đợt \"{line.Title}\" ({ContractPaymentRules.Outstanding(line):N0} đ) của hợp đồng "
            + $"{contract.ContractNumber} đã quá hạn từ {line.DueDate:dd/MM/yyyy}. Vui lòng chuyển khoản với nội dung "
            + ContractPaymentRules.Reference(line.LineId) + (paymentLink is null ? "." : $": {paymentLink}");
        await repository.AddNotificationAsync(contract.BuyerUserId, title, content, "ContractPaymentOverdue",
            contract.ContractId, now, cancellationToken);
        await TrySendAsync(contract.BuyerEmail, title, content, $"CTP-OVERDUE:{line.LineId}", cancellationToken);
    }

    private async Task TrySendAsync(string email, string subject, string content, string eventKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email)) return;
        try
        {
            await notifications.SendEmailAsync(email, subject, content, eventKey, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            // The in-app notification is already saved; e-mail delivery is best effort.
        }
    }

    private async Task<Result<ContractActionResultDto>> ActionResultAsync(long contractId, string message,
        CancellationToken cancellationToken)
    {
        var record = await repository.GetAsync(contractId, cancellationToken);
        return record is null
            ? Result<ContractActionResultDto>.FailureResult(NotFound())
            : Result<ContractActionResultDto>.SuccessResult(new ContractActionResultDto(message,
                await ScheduleAsync(record, cancellationToken)));
    }

    private async Task<ContractScheduleDto> ScheduleAsync(ContractScheduleRecord record,
        CancellationToken cancellationToken)
    {
        DateOnly today = Today;
        var contract = record.Contract;
        bool readOnly = !ContractPaymentRules.IsTracked(contract.Status);
        var lines = record.Lines.OrderBy(l => l.DueDate).ThenBy(l => l.LineId).Select(l =>
        {
            string status = ContractPaymentRules.LineStatus(l, today);
            bool open = !readOnly && !l.AwaitingInspection && status != ContractLineStatuses.Paid;
            return new ContractLineDto(l.LineId, l.Title, l.Type, l.DueDate, l.Amount, l.PaidAmount,
                ContractPaymentRules.Outstanding(l), l.LastPaidAt, ContractPaymentRules.Reference(l.LineId),
                l.LastReference, status, open, open,
                open && l.DueDate < today && ContractLineStatuses.Normalize(l.PaymentStatus) != ContractLineStatuses.Overdue);
        }).ToList();

        var item = ListItem(record, today);
        return new ContractScheduleDto(contract.ContractId, contract.ContractNumber, contract.Status,
            contract.BuyerName, contract.FarmName, item.ContractValue, item.Received, item.Outstanding,
            item.PaymentStatus, readOnly, lines, await repository.HistoryAsync(contract.ContractId, cancellationToken));
    }

    private static ContractPaymentListItemDto ListItem(ContractScheduleRecord record, DateOnly today)
    {
        var lines = record.Lines;
        decimal value = lines.Sum(l => l.Amount);
        decimal received = lines.Sum(l => Math.Min(l.PaidAmount, l.Amount));
        DateOnly? next = lines.Where(l => ContractPaymentRules.LineStatus(l, today) != ContractLineStatuses.Paid)
            .Select(l => (DateOnly?)l.DueDate).Min();
        return new ContractPaymentListItemDto(record.Contract.ContractId, record.Contract.ContractNumber,
            record.Contract.Status, record.Contract.BuyerName, record.Contract.FarmName, value, received,
            value - received, next, ContractPaymentRules.ContractStatus(lines, today));
    }

}
