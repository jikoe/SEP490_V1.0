using AgriSustain.Application.Common;
using FluentValidation;
using ICurrentActor = AgriSustain.Application.Interfaces.ICurrentActor;

namespace AgriSustain.Application.Services.Certificates;

// UC-FAR-07 Submit Certificate Dossier.

public sealed record FarmCertificateDto(
    long FarmCertificateId,
    string CertificateType,
    string CertificateNumber,
    string IssuedBy,
    DateOnly IssueDate,
    DateOnly? ExpiryDate,
    string VerificationStatus,
    DateTime? ReviewedAt,
    string? RevocationReason,
    string DocumentUrl);

/// <summary>Step 2-3 form fields; the file itself is passed separately by the controller.</summary>
public sealed record SubmitCertificateRequest(
    string CertificateType,
    string CertificateNumber,
    string IssuedBy,
    DateOnly? IssueDate,
    DateOnly? ExpiryDate);

public sealed record NewFarmCertificate(
    long FarmerProfileId,
    string CertificateType,
    string CertificateNumber,
    string IssuedBy,
    DateOnly IssueDate,
    DateOnly? ExpiryDate,
    string DocumentUrl,
    long SubmittedByUserId,
    DateTime SubmittedAt);

public static class FarmCertificateRules
{
    public const string PendingReview = "PendingReview";
    public static readonly string[] Types = ["VietGAP", "GlobalGAP", "Organic", "OCOP"];
    public const string DocumentFolder = "certificates";
}

public interface IFarmCertificateRepository
{
    /// <summary>The Farmer profile of the user and its KYC status, or null.</summary>
    Task<(long FarmerProfileId, string VerificationStatus)?> GetFarmerProfileAsync(
        long userId, CancellationToken cancellationToken);

    Task<bool> NumberExistsAsync(string certificateType, string certificateNumber, CancellationToken cancellationToken);

    Task<IReadOnlyList<FarmCertificateDto>> ListAsync(long farmerProfileId, CancellationToken cancellationToken);

    /// <summary>Stores the dossier with VerificationStatus = PendingReview and writes an audit entry.</summary>
    Task<FarmCertificateDto> AddAsync(NewFarmCertificate certificate, CancellationToken cancellationToken);
}

public interface IFarmCertificateService
{
    Task<Result<IReadOnlyList<FarmCertificateDto>>> ListMineAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Steps 4-7. <paramref name="saveDocument"/> stores the uploaded file for the given user and
    /// returns its private URL; it is called only after every field rule has passed.
    /// </summary>
    Task<Result<FarmCertificateDto>> SubmitAsync(
        SubmitCertificateRequest request, Func<long, Task<string>> saveDocument,
        CancellationToken cancellationToken);
}

public sealed class SubmitCertificateRequestValidator : AbstractValidator<SubmitCertificateRequest>
{
    private const string Required = "Trường này là bắt buộc.";

    public SubmitCertificateRequestValidator(TimeProvider timeProvider)
    {
        DateOnly Today() => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        RuleFor(x => x.CertificateType)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage(Required)
            .Must(v => string.IsNullOrWhiteSpace(v) || FarmCertificateRules.Types.Contains(v))
            .WithMessage("Loại chứng nhận phải là VietGAP, GlobalGAP, Organic hoặc OCOP.");
        RuleFor(x => x.CertificateNumber)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage(Required)
            .MaximumLength(100).WithMessage("Số chứng nhận không quá 100 ký tự.");
        RuleFor(x => x.IssuedBy)
            .Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage(Required)
            .MaximumLength(200).WithMessage("Cơ quan cấp không quá 200 ký tự.");
        RuleFor(x => x.IssueDate)
            .NotNull().WithMessage(Required)
            .Must(d => d is null || d.Value <= Today()).WithMessage("Ngày cấp không được ở tương lai.");
        RuleFor(x => x.ExpiryDate)
            .NotNull().WithMessage(Required)
            .Must((x, d) => d is null || x.IssueDate is null || d.Value > x.IssueDate.Value)
            .WithMessage("Ngày hết hạn phải sau ngày cấp.")
            .Must(d => d is null || d.Value > Today()).WithMessage("Chứng nhận đã hết hạn, không thể nộp.");
    }
}

public sealed class FarmCertificateService(
    IFarmCertificateRepository repository,
    ICurrentActor currentActor,
    IValidator<SubmitCertificateRequest> validator,
    TimeProvider timeProvider) : IFarmCertificateService
{
    // AF2
    public const string DuplicateMessage = "Số chứng nhận này đã được đăng ký trên nền tảng.";

    // Step 7
    public const string SuccessMessage = "Đã nộp hồ sơ chứng nhận thành công! Đang chờ Nhân viên thẩm định duyệt.";

    public async Task<Result<IReadOnlyList<FarmCertificateDto>>> ListMineAsync(CancellationToken cancellationToken)
    {
        var farm = await ApprovedFarmAsync(cancellationToken);
        if (farm.Error is { } error) return Result<IReadOnlyList<FarmCertificateDto>>.FailureResult(error);
        return Result<IReadOnlyList<FarmCertificateDto>>.SuccessResult(
            await repository.ListAsync(farm.ProfileId, cancellationToken));
    }

    public async Task<Result<FarmCertificateDto>> SubmitAsync(
        SubmitCertificateRequest request, Func<long, Task<string>> saveDocument,
        CancellationToken cancellationToken)
    {
        var farm = await ApprovedFarmAsync(cancellationToken);
        if (farm.Error is { } farmError) return Result<FarmCertificateDto>.FailureResult(farmError);

        if (await RequestValidation.CheckAsync(validator, request, cancellationToken) is { } invalid)
        {
            return Result<FarmCertificateDto>.FailureResult(invalid);
        }

        string type = request.CertificateType.Trim();
        string number = request.CertificateNumber.Trim();
        if (await repository.NumberExistsAsync(type, number, cancellationToken))
        {
            return Result<FarmCertificateDto>.FailureResult(new Error("CERTIFICATE_EXISTS", DuplicateMessage,
                "Conflict", new Dictionary<string, string[]> { ["certificateNumber"] = [DuplicateMessage] }));
        }

        string documentUrl = await saveDocument(currentActor.UserId);
        var created = await repository.AddAsync(new NewFarmCertificate(
            farm.ProfileId, type, number, request.IssuedBy.Trim(), request.IssueDate!.Value,
            request.ExpiryDate, documentUrl, currentActor.UserId, timeProvider.GetUtcNow().UtcDateTime),
            cancellationToken);
        return Result<FarmCertificateDto>.SuccessResult(created);
    }

    private async Task<(long ProfileId, Error? Error)> ApprovedFarmAsync(CancellationToken cancellationToken)
    {
        if (currentActor.UserId <= 0)
        {
            return (0, new Error("UNAUTHORIZED", "Vui lòng đăng nhập để sử dụng chức năng này.", "Unauthorized"));
        }

        var farm = await repository.GetFarmerProfileAsync(currentActor.UserId, cancellationToken);
        if (farm is null)
        {
            return (0, new Error("FARMER_PROFILE_NOT_FOUND", "Không tìm thấy hồ sơ nông trại của tài khoản.", "NotFound"));
        }

        // Precondition: only an Approved farm can submit certificate dossiers.
        return farm.Value.VerificationStatus == VerificationStatuses.Approved
            ? (farm.Value.FarmerProfileId, null)
            : (0, new Error("FARM_NOT_APPROVED",
                "Hồ sơ nông trại cần được duyệt KYC trước khi nộp chứng nhận chất lượng.", "Forbidden"));
    }
}
