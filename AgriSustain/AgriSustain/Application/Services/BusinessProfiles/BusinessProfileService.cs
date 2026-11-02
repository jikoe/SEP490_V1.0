using AgriSustain.Application.Common;
using FluentValidation;
using ICurrentActor = AgriSustain.Application.Interfaces.ICurrentActor;

namespace AgriSustain.Application.Services.BusinessProfiles;

// UC-AUTH-02 Register WholesaleBuyer Account, part 2: after signing up as a WholesaleBuyer
// (UC-AUTH-01 + OTP), the buyer submits the company dossier from the Profile screen for KYC review.

/// <summary>Company dossier of the signed-in WholesaleBuyer; fields are null before the first submission.</summary>
public sealed record BusinessProfileDto(
    string? CompanyName,
    string? TaxCode,
    string? RepresentativeName,
    string? BusinessType,
    string? BusinessAddress,
    string? DocumentUrl,
    string VerificationStatus,
    string? RejectReason);

public sealed record SubmitBusinessProfileRequest(
    string CompanyName,
    string TaxCode,
    string RepresentativeName,
    string? BusinessType,
    string BusinessAddress);

public static class BusinessProfileRules
{
    public const string Role = "WholesaleBuyer";
    public const string DocumentFolder = "b2b-documents";

    public static readonly string[] BusinessTypes =
        ["Supermarket", "Restaurant", "Processor", "Exporter", "Distributor", "Other"];

    /// <summary>
    /// Vietnamese tax code: 10 digits for an enterprise, or 10 + 3 digits for a branch
    /// ("0101234567-001"). Spaces are ignored; a 13-digit code is stored with its dash.
    /// Returns null when the format is invalid.
    /// </summary>
    public static string? NormalizeTaxCode(string? taxCode)
    {
        if (string.IsNullOrWhiteSpace(taxCode)) return null;
        string digits = taxCode.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        if (!digits.All(char.IsAsciiDigit)) return null;
        return digits.Length switch
        {
            10 => digits,
            13 => digits[..10] + "-" + digits[10..],
            _ => null
        };
    }
}

public interface IBusinessProfileRepository
{
    /// <summary>The dossier of the user, or null when none has been submitted yet.</summary>
    Task<BusinessProfileDto?> GetAsync(long userId, CancellationToken cancellationToken);

    /// <summary>True when another buyer already uses the tax code.</summary>
    Task<bool> TaxCodeTakenAsync(string taxCode, long userId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates or updates the WholesaleBuyerProfile with VerificationStatus = PendingApproval,
    /// clears the previous reject reason and writes an audit entry. A null document keeps the old one.
    /// </summary>
    Task<BusinessProfileDto> SubmitAsync(long userId, SubmitBusinessProfileRequest request, string? documentUrl,
        DateTime now, CancellationToken cancellationToken);
}

public interface IBusinessProfileService
{
    Task<Result<BusinessProfileDto>> GetMineAsync(CancellationToken cancellationToken);

    /// <summary>
    /// <paramref name="saveDocument"/> stores the uploaded Business License and returns its private
    /// URL; it is null when no file was chosen (the license is optional; the tax code identifies the company). It is called only after every rule has passed.
    /// </summary>
    Task<Result<BusinessProfileDto>> SubmitAsync(SubmitBusinessProfileRequest request,
        Func<long, Task<string>>? saveDocument, CancellationToken cancellationToken);
}

public sealed class SubmitBusinessProfileRequestValidator : AbstractValidator<SubmitBusinessProfileRequest>
{
    private const string Required = "Trường này là bắt buộc.";

    public SubmitBusinessProfileRequestValidator()
    {
        RuleFor(x => x.CompanyName)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage(Required)
            .MaximumLength(255).WithMessage("Tên doanh nghiệp không quá 255 ký tự.");
        RuleFor(x => x.TaxCode)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage(Required)
            .Must(v => string.IsNullOrWhiteSpace(v) || BusinessProfileRules.NormalizeTaxCode(v) is not null)
            .WithMessage("Mã số thuế gồm 10 chữ số, hoặc 13 chữ số với chi nhánh (VD: 0101234567-001).");
        RuleFor(x => x.RepresentativeName)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage(Required)
            .Must(v => v is null || v.Trim().Length is >= 2 and <= 150)
            .WithMessage("Tên người đại diện phải từ 2 đến 150 ký tự.");
        RuleFor(x => x.BusinessType)
            .Must(v => string.IsNullOrWhiteSpace(v) || BusinessProfileRules.BusinessTypes.Contains(v))
            .WithMessage("Loại hình doanh nghiệp không hợp lệ.");
        RuleFor(x => x.BusinessAddress)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage(Required)
            .MaximumLength(500).WithMessage("Địa chỉ không quá 500 ký tự.");
    }
}

public sealed class BusinessProfileService(
    IBusinessProfileRepository repository,
    ICurrentActor currentActor,
    IValidator<SubmitBusinessProfileRequest> validator,
    TimeProvider timeProvider) : IBusinessProfileService
{
    public const string SuccessMessage =
        "Đã gửi hồ sơ doanh nghiệp! Chức năng đặt hàng sỉ và hợp đồng bao tiêu sẽ được mở sau khi "
        + "Nhân viên thẩm định duyệt hồ sơ KYC.";

    public async Task<Result<BusinessProfileDto>> GetMineAsync(CancellationToken cancellationToken)
    {
        if (RoleError() is { } error) return Result<BusinessProfileDto>.FailureResult(error);
        return Result<BusinessProfileDto>.SuccessResult(
            await repository.GetAsync(currentActor.UserId, cancellationToken) ?? Empty);
    }

    public async Task<Result<BusinessProfileDto>> SubmitAsync(SubmitBusinessProfileRequest request,
        Func<long, Task<string>>? saveDocument, CancellationToken cancellationToken)
    {
        if (RoleError() is { } roleError) return Result<BusinessProfileDto>.FailureResult(roleError);

        var current = await repository.GetAsync(currentActor.UserId, cancellationToken) ?? Empty;
        // Only a dossier that was never submitted or was rejected can be (re)submitted.
        if (!VerificationStatuses.CanSubmit(current.VerificationStatus))
        {
            return Result<BusinessProfileDto>.FailureResult(new Error("INVALID_STATE",
                current.VerificationStatus == VerificationStatuses.Approved
                    ? "Hồ sơ doanh nghiệp đã được duyệt."
                    : "Hồ sơ doanh nghiệp đang chờ thẩm định, không thể gửi lại.", "Conflict"));
        }

        if (await RequestValidation.CheckAsync(validator, request, cancellationToken) is { } invalid)
        {
            return Result<BusinessProfileDto>.FailureResult(invalid);
        }

        string taxCode = BusinessProfileRules.NormalizeTaxCode(request.TaxCode)!;
        if (await repository.TaxCodeTakenAsync(taxCode, currentActor.UserId, cancellationToken))
        {
            const string message = "Mã số thuế này đã được đăng ký trên hệ thống.";
            return Result<BusinessProfileDto>.FailureResult(new Error("TAX_CODE_EXISTS", message, "Conflict",
                new Dictionary<string, string[]> { ["taxCode"] = [message] }));
        }

        string? documentUrl = saveDocument is null ? null : await saveDocument(currentActor.UserId);
        var normalized = new SubmitBusinessProfileRequest(
            request.CompanyName.Trim(), taxCode, request.RepresentativeName.Trim(),
            string.IsNullOrWhiteSpace(request.BusinessType) ? null : request.BusinessType.Trim(),
            request.BusinessAddress.Trim());
        return Result<BusinessProfileDto>.SuccessResult(await repository.SubmitAsync(
            currentActor.UserId, normalized, documentUrl, timeProvider.GetUtcNow().UtcDateTime, cancellationToken));
    }

    private static readonly BusinessProfileDto Empty =
        new(null, null, null, null, null, null, VerificationStatuses.NotSubmitted, null);

    private Error? RoleError() =>
        currentActor.UserId <= 0
            ? new Error("UNAUTHORIZED", "Vui lòng đăng nhập để sử dụng chức năng này.", "Unauthorized")
            : currentActor.Role != BusinessProfileRules.Role
                ? new Error("FORBIDDEN", "Chức năng chỉ dành cho tài khoản mua sỉ (WholesaleBuyer).", "Forbidden")
                : null;
}
