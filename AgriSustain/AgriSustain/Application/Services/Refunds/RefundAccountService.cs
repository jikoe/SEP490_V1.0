using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Services.Profiles;
using ICurrentActor = AgriSustain.Application.Interfaces.ICurrentActor;

namespace AgriSustain.Application.Services.Refunds;

// UC-FIN-08 Provide Refund Account (RetailCustomer, WholesaleBuyer).

public static class RefundAccountStates
{
    public const string AwaitingAccount = "AwaitingAccount";
    public const string ReadyForTransfer = "ReadyForTransfer";
    public const string TransferFailed = "TransferFailed";
    public const string Completed = "Completed";
}

/// <summary>Masked snapshot of the account stored on the refund request.</summary>
public sealed record RefundReceiverDto(
    string BankCode,
    string BankShortName,
    string MaskedAccountNumber,
    string AccountHolder,
    DateTime? ProvidedAt);

public sealed record RefundHistoryDto(string Action, string Actor, string? Detail, DateTime OccurredAt);

public sealed record MyRefundListItemDto(
    long RefundId,
    string RefundCode,
    string? OrderCode,
    decimal Amount,
    string RefundStatus,
    string AccountState,
    string? MaskedAccount);

public sealed record MyRefundDetailDto(
    long RefundId,
    string RefundCode,
    string? OrderCode,
    string? FarmName,
    decimal Amount,
    string? Reason,
    string RefundStatus,
    string AccountState,
    bool CanEdit,
    string? LastFailureReason,
    DateTime? ApprovedAt,
    RefundReceiverDto? Receiver,
    BankAccountDto? DefaultAccount,
    IReadOnlyList<SupportedBankDto> Banks,
    IReadOnlyList<RefundHistoryDto> History);

/// <summary>
/// Step 4-5. UseDefaultAccount copies the saved default account (UC-AUTH-03); otherwise the bank
/// and number are looked up. SaveAsDefault also replaces the default account and needs the OTP.
/// </summary>
public sealed record ProvideRefundAccountRequest(
    bool UseDefaultAccount,
    string? BankCode,
    string? AccountNumber,
    string? AccountHolder,
    bool ConfirmRecipient,
    bool SaveAsDefault,
    string? OtpCode);

/// <summary>Refund request data owned by the current user.</summary>
public sealed record RefundRequestRecord(
    long RefundId,
    long OwnerUserId,
    string RefundCode,
    string? OrderCode,
    string? FarmName,
    decimal Amount,
    string? Reason,
    string RefundStatus,
    DateTime? ApprovedAt,
    string? ReceiverBankCode,
    string? ReceiverBankShortName,
    string? ReceiverAccountNumber,
    string? ReceiverAccountHolder,
    DateTime? ReceiverAccountProvidedAt,
    string? LastFailureReason,
    DateTime? LastFailureAt);

public interface IRefundAccountRepository
{
    Task<IReadOnlyList<RefundRequestRecord>> ListMineAsync(long userId, CancellationToken cancellationToken);

    /// <summary>The refund request when it belongs to the user (dispute submitter or order buyer), else null.</summary>
    Task<RefundRequestRecord?> FindMineAsync(long userId, long refundId, CancellationToken cancellationToken);

    Task<IReadOnlyList<RefundHistoryDto>> HistoryAsync(long refundId, CancellationToken cancellationToken);

    /// <summary>Stores the receiver snapshot (Step 7) with an audit row holding the masked account.</summary>
    Task<bool> SaveReceiverAsync(long refundId, string bankCode, string accountNumber, string accountHolder,
        long userId, DateTime now, CancellationToken cancellationToken);
}

public interface IRefundAccountService
{
    Task<Result<IReadOnlyList<MyRefundListItemDto>>> ListMineAsync(CancellationToken cancellationToken);

    Task<Result<MyRefundDetailDto>> GetAsync(long refundId, CancellationToken cancellationToken);

    Task<Result<string>> RequestOtpAsync(CancellationToken cancellationToken);

    Task<Result<MyRefundDetailDto>> ProvideAccountAsync(long refundId, ProvideRefundAccountRequest request,
        CancellationToken cancellationToken);
}

public sealed class RefundAccountService(
    IRefundAccountRepository repository,
    IBankAccountRepository bankAccounts,
    IBankAccountLookup bankLookup,
    IEmailOtpVerifier otpVerifier,
    ICurrentActor currentActor,
    TimeProvider timeProvider) : IRefundAccountService
{
    // MSG-RF-11
    public const string NotFoundMessage = "Không tìm thấy yêu cầu hoàn tiền này trong tài khoản của bạn.";

    // MSG-RF-12
    public const string SaveFailedMessage = "Không thể lưu tài khoản nhận hoàn tiền. Vui lòng thử lại.";

    private static readonly string[] s_roles = ["RetailCustomer", "WholesaleBuyer"];

    public async Task<Result<IReadOnlyList<MyRefundListItemDto>>> ListMineAsync(CancellationToken cancellationToken)
    {
        if (Caller() is { } error) return Result<IReadOnlyList<MyRefundListItemDto>>.FailureResult(error);
        var rows = await repository.ListMineAsync(currentActor.UserId, cancellationToken);
        return Result<IReadOnlyList<MyRefundListItemDto>>.SuccessResult(rows.Select(r => new MyRefundListItemDto(
            r.RefundId, r.RefundCode, r.OrderCode, r.Amount, r.RefundStatus, State(r),
            r.ReceiverAccountNumber is null ? null
                : $"{r.ReceiverBankShortName ?? r.ReceiverBankCode} {BankAccountService.Mask(r.ReceiverAccountNumber)}"))
            .ToList());
    }

    public async Task<Result<MyRefundDetailDto>> GetAsync(long refundId, CancellationToken cancellationToken)
    {
        if (Caller() is { } error) return Result<MyRefundDetailDto>.FailureResult(error);
        var refund = await repository.FindMineAsync(currentActor.UserId, refundId, cancellationToken);
        return refund is null
            ? Result<MyRefundDetailDto>.FailureResult(NotFound())
            : Result<MyRefundDetailDto>.SuccessResult(await DetailAsync(refund, cancellationToken));
    }

    public async Task<Result<string>> RequestOtpAsync(CancellationToken cancellationToken)
    {
        if (Caller() is { } error) return Result<string>.FailureResult(error);
        var owner = await bankAccounts.GetOwnerAsync(currentActor.UserId, cancellationToken);
        if (owner is null) return Result<string>.FailureResult(NotFound());
        return await otpVerifier.IssueAsync(BankAccountService.OtpPurpose, owner.Email, cancellationToken);
    }

    public async Task<Result<MyRefundDetailDto>> ProvideAccountAsync(long refundId,
        ProvideRefundAccountRequest request, CancellationToken cancellationToken)
    {
        if (Caller() is { } callerError) return Result<MyRefundDetailDto>.FailureResult(callerError);

        var refund = await repository.FindMineAsync(currentActor.UserId, refundId, cancellationToken);
        if (refund is null) return Result<MyRefundDetailDto>.FailureResult(NotFound());

        // Step 6 / MSG-RF-10: only a request that is still waiting for transfer can change account.
        if (refund.RefundStatus != "Pending")
        {
            return Result<MyRefundDetailDto>.FailureResult(new Error("REFUND_LOCKED",
                $"Yêu cầu hoàn tiền đang ở trạng thái {refund.RefundStatus}. Không thể thay đổi tài khoản nhận nữa.",
                "Conflict"));
        }

        if (!request.ConfirmRecipient)
        {
            return Result<MyRefundDetailDto>.FailureResult(Field("confirmRecipient",
                "Vui lòng xác nhận đây là đúng tài khoản nhận tiền."));
        }

        string bankCode;
        string accountNumber;
        string holder;
        string verificationStatus = BankAccountService.Verified;
        if (request.UseDefaultAccount)
        {
            var key = await bankAccounts.GetDefaultAccountKeyAsync(currentActor.UserId, cancellationToken);
            var saved = await bankAccounts.GetDefaultAccountAsync(currentActor.UserId, cancellationToken);
            if (key is null || saved is null)
            {
                return Result<MyRefundDetailDto>.FailureResult(Field("useDefaultAccount",
                    "Bạn chưa có tài khoản ngân hàng mặc định. Vui lòng nhập tài khoản khác."));
            }

            (bankCode, accountNumber, holder) = (key.Value.BankCode, key.Value.AccountNumber, saved.AccountHolder);
        }
        else
        {
            var checkedAccount = await CheckNewAccountAsync(request, cancellationToken);
            if (checkedAccount.Error is { } accountError) return Result<MyRefundDetailDto>.FailureResult(accountError);
            (bankCode, accountNumber, holder) = (checkedAccount.BankCode!, checkedAccount.Number!, checkedAccount.Holder!);
            verificationStatus = checkedAccount.Verified ? BankAccountService.Verified : BankAccountService.Unverified;

            if (request.SaveAsDefault)
            {
                // BR-BA-04: replacing the default account needs the e-mail OTP.
                var owner = await bankAccounts.GetOwnerAsync(currentActor.UserId, cancellationToken);
                var verified = owner is null || string.IsNullOrWhiteSpace(request.OtpCode)
                    ? null
                    : await otpVerifier.VerifyAsync(BankAccountService.OtpPurpose, owner.Email,
                        request.OtpCode.Trim(), cancellationToken);
                if (verified is null || !verified.IsSuccess)
                {
                    string message = verified?.Error?.Message ?? "Vui lòng nhập mã OTP gửi tới email để lưu làm tài khoản mặc định.";
                    return Result<MyRefundDetailDto>.FailureResult(Field("otpCode", message));
                }
            }
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        if (!await repository.SaveReceiverAsync(refundId, bankCode, accountNumber, holder,
                currentActor.UserId, now, cancellationToken))
        {
            return Result<MyRefundDetailDto>.FailureResult(new Error("SAVE_FAILED", SaveFailedMessage, "Conflict"));
        }

        if (!request.UseDefaultAccount && request.SaveAsDefault)
        {
            await bankAccounts.SaveDefaultAccountAsync(currentActor.UserId, bankCode, accountNumber, holder,
                verificationStatus, now, null, cancellationToken);
        }

        var updated = await repository.FindMineAsync(currentActor.UserId, refundId, cancellationToken);
        return Result<MyRefundDetailDto>.SuccessResult(await DetailAsync(updated!, cancellationToken));
    }

    private async Task<(string? BankCode, string? Number, string? Holder, bool Verified, Error? Error)> CheckNewAccountAsync(
        ProvideRefundAccountRequest request, CancellationToken cancellationToken)
    {
        string bankCode = request.BankCode?.Trim() ?? string.Empty;
        string number = request.AccountNumber?.Trim() ?? string.Empty;
        if (bankCode.Length == 0) return (null, null, null, false, Field("bankCode", "Trường này là bắt buộc."));
        if (number.Length == 0) return (null, null, null, false, Field("accountNumber", "Trường này là bắt buộc."));
        if (number.Length is < 6 or > 19 || !number.All(char.IsAsciiDigit))
        {
            // MSG-RF-02
            return (null, null, null, false, Field("accountNumber",
                "Vui lòng sửa Số tài khoản: số tài khoản chỉ gồm chữ số, dài từ 6 đến 19 ký tự."));
        }

        var banks = await bankAccounts.GetActiveBanksAsync(cancellationToken);
        if (banks.All(b => b.BankCode != bankCode))
        {
            return (null, null, null, false, Field("bankCode", "Ngân hàng này hiện không được hỗ trợ."));
        }

        BankLookupResponse lookup;
        try
        {
            lookup = await bankLookup.LookupAsync(bankCode, number, cancellationToken);
        }
        catch (Exception exception) when ((exception is HttpRequestException or TaskCanceledException
            or TimeoutException) && !cancellationToken.IsCancellationRequested)
        {
            lookup = new BankLookupResponse(BankLookupStatuses.Unavailable, null);
        }

        switch (lookup.Status)
        {
            case BankLookupStatuses.Found when !string.IsNullOrWhiteSpace(lookup.AccountHolder):
                return (bankCode, number, lookup.AccountHolder, true, null);
            case BankLookupStatuses.Unavailable:
                string typed = request.AccountHolder?.Trim() ?? string.Empty;
                if (typed.Length is 0 or > 100)
                {
                    return (null, null, null, false, Field("accountHolder",
                        "Không tra cứu được ngân hàng. Vui lòng nhập tên chủ tài khoản (tối đa 100 ký tự)."));
                }
                return (bankCode, number, typed.ToUpperInvariant(), false, null);
            default:
                // MSG-BA-04
                return (null, null, null, false, Field("accountNumber",
                    "Không tìm thấy tài khoản này tại ngân hàng đã chọn. Vui lòng kiểm tra lại ngân hàng và số tài khoản."));
        }
    }

    private async Task<MyRefundDetailDto> DetailAsync(RefundRequestRecord r, CancellationToken cancellationToken)
    {
        var history = await repository.HistoryAsync(r.RefundId, cancellationToken);
        var defaultAccount = await bankAccounts.GetDefaultAccountAsync(currentActor.UserId, cancellationToken);
        var banks = await bankAccounts.GetActiveBanksAsync(cancellationToken);
        string state = State(r);
        return new MyRefundDetailDto(
            r.RefundId, r.RefundCode, r.OrderCode, r.FarmName, r.Amount, r.Reason, r.RefundStatus, state,
            CanEdit: r.RefundStatus == "Pending",
            LastFailureReason: state == RefundAccountStates.TransferFailed ? r.LastFailureReason : null,
            r.ApprovedAt,
            r.ReceiverAccountNumber is null ? null : new RefundReceiverDto(
                r.ReceiverBankCode!, r.ReceiverBankShortName ?? r.ReceiverBankCode!,
                BankAccountService.Mask(r.ReceiverAccountNumber), r.ReceiverAccountHolder ?? string.Empty,
                r.ReceiverAccountProvidedAt),
            defaultAccount,
            banks,
            history);
    }

    /// <summary>A failure recorded after the account was provided means the user must correct it.</summary>
    public static string State(RefundRequestRecord r) =>
        r.RefundStatus != "Pending" ? RefundAccountStates.Completed
        : r.ReceiverAccountNumber is null ? RefundAccountStates.AwaitingAccount
        : r.LastFailureAt is { } failed && (r.ReceiverAccountProvidedAt is null || failed > r.ReceiverAccountProvidedAt)
            ? RefundAccountStates.TransferFailed
            : RefundAccountStates.ReadyForTransfer;

    private Error? Caller() =>
        currentActor.UserId <= 0
            ? new Error("UNAUTHORIZED", "Vui lòng đăng nhập để sử dụng chức năng này.", "Unauthorized")
            : s_roles.Contains(currentActor.Role) || string.IsNullOrEmpty(currentActor.Role)
                ? null
                : new Error("FORBIDDEN", "Chỉ khách hàng mới cung cấp tài khoản nhận hoàn tiền.", "Forbidden");

    private static Error NotFound() => new("REFUND_NOT_FOUND", NotFoundMessage, "NotFound");

    private static Error Field(string field, string message) =>
        new("INVALID_INPUT", message, "Validation", new Dictionary<string, string[]> { [field] = [message] });
}
