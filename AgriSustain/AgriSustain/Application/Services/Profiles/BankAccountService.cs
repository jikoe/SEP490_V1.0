using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using FluentValidation;
using ICurrentActor = AgriSustain.Application.Interfaces.ICurrentActor;

namespace AgriSustain.Application.Services.Profiles;

/// <summary>
/// UC-AUTH-03 Manage Profile - Manage Bank Account (BR-BA-01..06).
/// Farmer: the account that receives payouts. RetailCustomer / WholesaleBuyer: the default account
/// that receives refunds. Staff roles have no bank account here and cannot edit users' accounts.
/// </summary>
public sealed class BankAccountService(
    IBankAccountRepository repository,
    ICurrentActor currentActor,
    IEmailOtpVerifier otpVerifier,
    IBankAccountLookup bankLookup,
    IBankAccountEmailSender emailSender,
    TimeProvider timeProvider,
    IValidator<BankLookupRequest> lookupValidator,
    IValidator<SaveBankAccountRequest> saveValidator,
    IValidator<RemoveBankAccountRequest> removeValidator) : IBankAccountService
{
    /// <summary>OTP purpose shared by add, change and remove (BR-BA-04).</summary>
    public const string OtpPurpose = "BankAccount";

    /// <summary>BR-BA-05: payouts are held for 48 hours after an Approved Farmer changes the account.</summary>
    public static readonly TimeSpan PayoutHold = TimeSpan.FromHours(48);

    public const string Verified = "Verified";
    public const string Unverified = "Unverified";

    private static readonly string[] s_accountRoles = ["Farmer", "RetailCustomer", "WholesaleBuyer"];

    private static readonly Error s_signInRequired = new(
        "UNAUTHORIZED", "Vui lòng đăng nhập để sử dụng chức năng này.", "Unauthorized");

    private static readonly Error s_userNotFound = new(
        "USER_NOT_FOUND", "Không tìm thấy thông tin tài khoản người dùng.", "NotFound");

    private static readonly Error s_roleNotAllowed = new(
        "BANK_ACCOUNT_NOT_ALLOWED",
        "Chỉ tài khoản Nông hộ, Khách hàng và Doanh nghiệp mua sỉ mới có tài khoản ngân hàng.", "Forbidden");

    private static readonly Error s_bankNotSupported = new(
        "BANK_NOT_SUPPORTED", "Ngân hàng này hiện không được hỗ trợ. Vui lòng chọn ngân hàng khác.", "Validation",
        new Dictionary<string, string[]> { ["bankCode"] = ["Ngân hàng này hiện không được hỗ trợ."] });

    // MSG-BA-04
    private const string AccountNotFoundMessage =
        "Không tìm thấy tài khoản này tại ngân hàng đã chọn. Vui lòng kiểm tra lại ngân hàng và số tài khoản.";

    // MSG-BA-06
    private const string FarmerCannotRemoveMessage =
        "Nông trại bắt buộc phải có tài khoản nhận tiền. Bạn có thể đổi tài khoản nhưng không thể xóa.";

    private long? SignedInUserId() => currentActor.UserId > 0 ? currentActor.UserId : null;

    public async Task<Result<BankAccountOverview>> GetAsync(CancellationToken cancellationToken)
    {
        var owner = await OwnerAsync(cancellationToken);
        return owner.Error is { } error
            ? Result<BankAccountOverview>.FailureResult(error)
            : Result<BankAccountOverview>.SuccessResult(await OverviewAsync(owner.Value!, cancellationToken));
    }

    public async Task<Result<BankLookupResponse>> LookupAsync(
        BankLookupRequest request, CancellationToken cancellationToken)
    {
        var owner = await OwnerAsync(cancellationToken);
        if (owner.Error is { } ownerError) return Result<BankLookupResponse>.FailureResult(ownerError);

        if (await RequestValidation.CheckAsync(lookupValidator, request, cancellationToken) is { } invalid)
        {
            return Result<BankLookupResponse>.FailureResult(invalid);
        }

        if (!await IsActiveBankAsync(request.BankCode, cancellationToken))
        {
            return Result<BankLookupResponse>.FailureResult(s_bankNotSupported);
        }

        return Result<BankLookupResponse>.SuccessResult(
            await SafeLookupAsync(request.BankCode.Trim(), request.AccountNumber.Trim(), cancellationToken));
    }

    public async Task<Result<string>> RequestOtpAsync(CancellationToken cancellationToken)
    {
        var owner = await OwnerAsync(cancellationToken);
        if (owner.Error is { } error) return Result<string>.FailureResult(error);

        return await otpVerifier.IssueAsync(OtpPurpose, owner.Value!.Email, cancellationToken);
    }

    public async Task<Result<BankAccountChangeResponse>> SaveAsync(
        SaveBankAccountRequest request, CancellationToken cancellationToken)
    {
        var owner = await OwnerAsync(cancellationToken);
        if (owner.Error is { } ownerError) return Result<BankAccountChangeResponse>.FailureResult(ownerError);
        var user = owner.Value!;

        // Step 5: validate the input (BR-BA-01).
        if (await RequestValidation.CheckAsync(saveValidator, request, cancellationToken) is { } invalid)
        {
            return Result<BankAccountChangeResponse>.FailureResult(invalid);
        }

        string bankCode = request.BankCode.Trim();
        string accountNumber = request.AccountNumber.Trim();
        if (!await IsActiveBankAsync(bankCode, cancellationToken))
        {
            return Result<BankAccountChangeResponse>.FailureResult(s_bankNotSupported);
        }

        // The holder name is never taken from the client when the bank can be reached.
        var lookup = await SafeLookupAsync(bankCode, accountNumber, cancellationToken);
        string holder;
        string verificationStatus;
        switch (lookup.Status)
        {
            case BankLookupStatuses.Found:
                holder = lookup.AccountHolder!;
                verificationStatus = Verified;
                break;
            case BankLookupStatuses.Unavailable when !string.IsNullOrWhiteSpace(request.AccountHolder):
                // BR-BA-06: save as Unverified; it cannot receive payouts until a later lookup succeeds.
                holder = request.AccountHolder!.Trim().ToUpperInvariant();
                verificationStatus = Unverified;
                break;
            case BankLookupStatuses.Unavailable:
                return Result<BankAccountChangeResponse>.FailureResult(FieldError(
                    "ACCOUNT_HOLDER_REQUIRED", "accountHolder",
                    "Không tra cứu được ngân hàng. Vui lòng nhập tên chủ tài khoản để lưu ở trạng thái Chưa xác minh."));
            default:
                return Result<BankAccountChangeResponse>.FailureResult(FieldError(
                    "BANK_ACCOUNT_NOT_FOUND", "accountNumber", AccountNotFoundMessage));
        }

        // Step 5: confirm with the e-mail OTP (BR-BA-04). Checked last so a wrong account keeps the code.
        if (await VerifyOtpAsync(user, request.OtpCode, cancellationToken) is { } otpError)
        {
            return Result<BankAccountChangeResponse>.FailureResult(otpError);
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        var previous = await repository.GetDefaultAccountKeyAsync(user.UserId, cancellationToken);
        bool changesAccount = previous is { } old
            && (old.BankCode != bankCode || old.AccountNumber != accountNumber);
        DateTime? payoutHold = user.Role == "Farmer"
            && user.FarmVerificationStatus == VerificationStatuses.Approved
            && changesAccount
                ? now.Add(PayoutHold)
                : null;

        await repository.SaveDefaultAccountAsync(
            user.UserId, bankCode, accountNumber, holder, verificationStatus, now, payoutHold, cancellationToken);

        var overview = await OverviewAsync(user, cancellationToken);
        string last4 = Last4(accountNumber);
        await emailSender.SendBankAccountChangedAsync(
            user.Email, overview.Account?.BankShortName ?? bankCode, last4, now, cancellationToken);

        // MSG-BA-01
        string message = $"Đã lưu tài khoản ngân hàng {overview.Account?.BankShortName ?? bankCode} •••• {last4}.";
        if (payoutHold is { } holdUntil)
        {
            message += $" Các khoản thanh toán sẽ tạm giữ 48 giờ (đến {holdUntil:dd/MM/yyyy HH:mm} UTC).";
        }

        return Result<BankAccountChangeResponse>.SuccessResult(new BankAccountChangeResponse(message, overview));
    }

    public async Task<Result<BankAccountChangeResponse>> RemoveAsync(
        RemoveBankAccountRequest request, CancellationToken cancellationToken)
    {
        var owner = await OwnerAsync(cancellationToken);
        if (owner.Error is { } ownerError) return Result<BankAccountChangeResponse>.FailureResult(ownerError);
        var user = owner.Value!;

        if (user.Role == "Farmer")
        {
            return Result<BankAccountChangeResponse>.FailureResult(new Error(
                "FARMER_ACCOUNT_REQUIRED", FarmerCannotRemoveMessage, "Conflict"));
        }

        if (await RequestValidation.CheckAsync(removeValidator, request, cancellationToken) is { } invalid)
        {
            return Result<BankAccountChangeResponse>.FailureResult(invalid);
        }

        if (await repository.GetDefaultAccountKeyAsync(user.UserId, cancellationToken) is null)
        {
            return Result<BankAccountChangeResponse>.FailureResult(new Error(
                "BANK_ACCOUNT_NOT_FOUND", "Bạn chưa có tài khoản ngân hàng nào để xóa.", "NotFound"));
        }

        if (await VerifyOtpAsync(user, request.OtpCode, cancellationToken) is { } otpError)
        {
            return Result<BankAccountChangeResponse>.FailureResult(otpError);
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        await repository.RemoveDefaultAccountAsync(user.UserId, now, cancellationToken);
        await emailSender.SendBankAccountChangedAsync(user.Email, null, null, now, cancellationToken);

        // MSG-BA-02
        return Result<BankAccountChangeResponse>.SuccessResult(new BankAccountChangeResponse(
            "Đã xóa tài khoản ngân hàng. Các yêu cầu hoàn tiền trước đó vẫn giữ tài khoản bạn đã xác nhận.",
            await OverviewAsync(user, cancellationToken)));
    }

    private async Task<(BankAccountOwner? Value, Error? Error)> OwnerAsync(CancellationToken cancellationToken)
    {
        if (SignedInUserId() is not long userId) return (null, s_signInRequired);

        var owner = await repository.GetOwnerAsync(userId, cancellationToken);
        if (owner is null) return (null, s_userNotFound);
        return s_accountRoles.Contains(owner.Role) ? (owner, null) : (null, s_roleNotAllowed);
    }

    private async Task<BankAccountOverview> OverviewAsync(
        BankAccountOwner owner, CancellationToken cancellationToken)
    {
        bool farmer = owner.Role == "Farmer";
        return new BankAccountOverview(
            owner.Role,
            farmer
                ? "Tài khoản nhận tiền thanh toán (payout) từ AgriSustain cho các đơn hàng của nông trại."
                : "Tài khoản mặc định nhận tiền hoàn khi yêu cầu hoàn tiền của bạn được chấp thuận.",
            CanRemove: !farmer,
            await repository.GetDefaultAccountAsync(owner.UserId, cancellationToken),
            await repository.GetActiveBanksAsync(cancellationToken));
    }

    private async Task<bool> IsActiveBankAsync(string bankCode, CancellationToken cancellationToken) =>
        (await repository.GetActiveBanksAsync(cancellationToken))
            .Any(bank => bank.BankCode == bankCode.Trim());

    /// <summary>A lookup that throws or times out counts as Unavailable (Step 3_Lookup service unavailable).</summary>
    private async Task<BankLookupResponse> SafeLookupAsync(
        string bankCode, string accountNumber, CancellationToken cancellationToken)
    {
        try
        {
            var result = await bankLookup.LookupAsync(bankCode, accountNumber, cancellationToken);
            return result.Status == BankLookupStatuses.Found && string.IsNullOrWhiteSpace(result.AccountHolder)
                ? new BankLookupResponse(BankLookupStatuses.NotFound, null)
                : result;
        }
        catch (Exception exception) when ((exception is HttpRequestException or TaskCanceledException
            or TimeoutException) && !cancellationToken.IsCancellationRequested)
        {
            return new BankLookupResponse(BankLookupStatuses.Unavailable, null);
        }
    }

    private async Task<Error?> VerifyOtpAsync(
        BankAccountOwner user, string otpCode, CancellationToken cancellationToken)
    {
        var verified = await otpVerifier.VerifyAsync(OtpPurpose, user.Email, otpCode.Trim(), cancellationToken);
        if (verified.IsSuccess) return null;

        // MSG-BA-03, keeping the verifier's detail (attempts left, request a new code).
        string detail = verified.Error?.Message ?? "Mã OTP không đúng hoặc đã hết hạn. Vui lòng yêu cầu mã mới.";
        return FieldError(verified.Error?.Code ?? "INVALID_OTP", "otpCode", detail);
    }

    private static Error FieldError(string code, string field, string message) =>
        new(code, message, "Validation", new Dictionary<string, string[]> { [field] = [message] });

    public static string Last4(string accountNumber) =>
        accountNumber.Length <= 4 ? accountNumber : accountNumber[^4..];

    public static string Mask(string accountNumber) => "•••• " + Last4(accountNumber);
}
