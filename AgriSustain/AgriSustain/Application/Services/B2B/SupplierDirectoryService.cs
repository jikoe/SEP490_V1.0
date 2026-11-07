using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces;
using FluentValidation;

namespace AgriSustain.Application.Services.B2B;

// UC-WB-01 Search Suppliers (WholesaleBuyer).

public static class SupplierDirectoryRules
{
    public const int PageSize = 12;
    public const int KeywordMaxLength = 100;
    public const int MainCropCount = 3;

    public const string SortReputation = "Reputation";
    public const string SortNewest = "Newest";
    public const string SortArea = "Area";
    public static readonly string[] Sorts = [SortReputation, SortNewest, SortArea];

    public const string KeywordTooLong = "Từ khóa tối đa 100 ký tự.";
    public const string BuyerNotApproved =
        "Hồ sơ doanh nghiệp của bạn chưa được duyệt. Vui lòng hoàn tất xác minh KYC.";
    public const string NoMatch = "Không tìm thấy nông trại phù hợp.";
    public const string ContactLocked = "Nâng cấp gói hội viên để xem thông tin liên hệ.";

    /// <summary>Unknown or empty sort falls back to the default (highest reputation first).</summary>
    public static string NormalizeSort(string? sort) =>
        Sorts.FirstOrDefault(s => string.Equals(s, sort?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? SortReputation;

    public static int NormalizePage(int page) => page < 1 ? 1 : page;

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>Search criteria of Step 2-3. Empty values mean "no filter".</summary>
public sealed record SupplierSearchQuery(
    string? Keyword = null,
    string? ProvinceCode = null,
    long? CategoryId = null,
    string? CertificateType = null,
    string? ProductionScale = null,
    string? Sort = null,
    int Page = 1);

/// <summary>Normalized filter handed to the repository.</summary>
public sealed record SupplierFilter(
    string? Keyword,
    string? ProvinceCode,
    long? CategoryId,
    string? CertificateType,
    string? ProductionScale,
    string Sort,
    int Page,
    int PageSize);

/// <summary>Raw supplier row read from the database (contact details are always loaded; the service hides them).</summary>
public sealed record SupplierRecord(
    long FarmerProfileId,
    string FarmName,
    string RepresentativeName,
    string? ProvinceCode,
    string? ProductionScale,
    decimal? CultivatedArea,
    decimal ReputationScore,
    IReadOnlyList<string> MainCrops,
    IReadOnlyList<string> Certificates,
    string? Phone,
    string? Email);

public sealed record SupplierCardDto(
    long FarmerProfileId,
    string FarmName,
    string RepresentativeName,
    string? ProvinceCode,
    string? ProvinceName,
    string? ProductionScale,
    decimal? CultivatedArea,
    decimal ReputationScore,
    IReadOnlyList<string> MainCrops,
    IReadOnlyList<string> Certificates,
    bool ContactLocked,
    string? Phone,
    string? Email);

public sealed record SupplierPageDto(
    IReadOnlyList<SupplierCardDto> Items,
    int Total,
    int Page,
    int PageSize,
    string Sort,
    bool ContactLocked,
    string? Message);

public sealed record NamedOption(string Value, string Label);

public sealed record SupplierFilterOptionsDto(
    IReadOnlyList<NamedOption> Provinces,
    IReadOnlyList<NamedOption> Categories,
    IReadOnlyList<string> CertificateTypes,
    IReadOnlyList<string> ProductionScales,
    IReadOnlyList<NamedOption> Sorts);

public sealed class SupplierSearchQueryValidator : AbstractValidator<SupplierSearchQuery>
{
    public SupplierSearchQueryValidator()
    {
        RuleFor(x => x.Keyword)
            .Must(k => k is null || k.Trim().Length <= SupplierDirectoryRules.KeywordMaxLength)
            .WithMessage(SupplierDirectoryRules.KeywordTooLong);
    }
}

public interface ISupplierDirectoryRepository
{
    /// <summary>VerificationStatus of the caller's WholesaleBuyerProfile, or null when there is none.</summary>
    Task<string?> GetBuyerStatusAsync(long userId, CancellationToken cancellationToken);

    /// <summary>True when the buyer has an Active subscription, valid now, whose snapshot unlocks direct contact.</summary>
    Task<bool> HasDirectContactAsync(long userId, DateTime now, CancellationToken cancellationToken);

    /// <summary>Approved farms of active users matching every filter, already sorted and paged.</summary>
    Task<(IReadOnlyList<SupplierRecord> Items, int Total)> SearchAsync(
        SupplierFilter filter, DateOnly today, CancellationToken cancellationToken);

    Task<IReadOnlyList<NamedOption>> GetCategoriesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetCertificateTypesAsync(DateOnly today, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> GetProductionScalesAsync(CancellationToken cancellationToken);
}

public interface ISupplierDirectoryService
{
    Task<Result<SupplierPageDto>> SearchAsync(SupplierSearchQuery query, CancellationToken cancellationToken);

    Task<Result<SupplierFilterOptionsDto>> GetFilterOptionsAsync(CancellationToken cancellationToken);
}

public sealed class SupplierDirectoryService(
    ISupplierDirectoryRepository repository,
    IAddressCatalog addressCatalog,
    IValidator<SupplierSearchQuery> validator,
    ICurrentActor currentActor,
    TimeProvider timeProvider) : ISupplierDirectoryService
{
    private static readonly Error s_signInRequired =
        new("UNAUTHORIZED", "Vui lòng đăng nhập để tiếp tục.", "Unauthorized");

    private static readonly Error s_notApproved =
        new("BUYER_NOT_APPROVED", SupplierDirectoryRules.BuyerNotApproved, "Forbidden");

    public async Task<Result<SupplierPageDto>> SearchAsync(
        SupplierSearchQuery query, CancellationToken cancellationToken)
    {
        Error? gate = await CheckBuyerAsync(cancellationToken);
        if (gate != null) return Result<SupplierPageDto>.FailureResult(gate);

        // AF7: an over-long keyword is rejected before any search runs.
        Error? invalid = await RequestValidation.CheckAsync(validator, query, cancellationToken);
        if (invalid != null)
        {
            return Result<SupplierPageDto>.FailureResult(invalid with { Code = "KEYWORD_TOO_LONG" });
        }

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        var filter = new SupplierFilter(
            SupplierDirectoryRules.Clean(query.Keyword),
            SupplierDirectoryRules.Clean(query.ProvinceCode),
            query.CategoryId is > 0 ? query.CategoryId : null,
            SupplierDirectoryRules.Clean(query.CertificateType),
            SupplierDirectoryRules.Clean(query.ProductionScale),
            SupplierDirectoryRules.NormalizeSort(query.Sort),
            SupplierDirectoryRules.NormalizePage(query.Page),
            SupplierDirectoryRules.PageSize);

        var (rows, total) = await repository.SearchAsync(filter, DateOnly.FromDateTime(now), cancellationToken);

        // AF6: contact details are only returned with the direct-contact benefit (UC-WB-08, DBR-14).
        bool unlocked = await repository.HasDirectContactAsync(currentActor.UserId, now, cancellationToken);
        var provinces = await ProvinceNamesAsync(cancellationToken);

        var items = rows.Select(row => new SupplierCardDto(
            row.FarmerProfileId,
            row.FarmName,
            row.RepresentativeName,
            row.ProvinceCode,
            row.ProvinceCode != null && provinces.TryGetValue(row.ProvinceCode, out var name) ? name : null,
            row.ProductionScale,
            row.CultivatedArea,
            row.ReputationScore,
            row.MainCrops.Take(SupplierDirectoryRules.MainCropCount).ToList(),
            row.Certificates,
            !unlocked,
            unlocked ? row.Phone : null,
            unlocked ? row.Email : null)).ToList();

        // AF1: empty result carries the "no match" message so the screen can offer to clear filters.
        string? message = total == 0 ? SupplierDirectoryRules.NoMatch
            : unlocked ? null : SupplierDirectoryRules.ContactLocked;

        return Result<SupplierPageDto>.SuccessResult(new SupplierPageDto(
            items, total, filter.Page, filter.PageSize, filter.Sort, !unlocked, message));
    }

    public async Task<Result<SupplierFilterOptionsDto>> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        Error? gate = await CheckBuyerAsync(cancellationToken);
        if (gate != null) return Result<SupplierFilterOptionsDto>.FailureResult(gate);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var provinces = await ProvinceNamesAsync(cancellationToken);
        return Result<SupplierFilterOptionsDto>.SuccessResult(new SupplierFilterOptionsDto(
            provinces.Select(p => new NamedOption(p.Key, p.Value)).OrderBy(p => p.Label).ToList(),
            await repository.GetCategoriesAsync(cancellationToken),
            await repository.GetCertificateTypesAsync(today, cancellationToken),
            await repository.GetProductionScalesAsync(cancellationToken),
            [
                new(SupplierDirectoryRules.SortReputation, "Uy tín cao nhất"),
                new(SupplierDirectoryRules.SortNewest, "Mới tham gia"),
                new(SupplierDirectoryRules.SortArea, "Diện tích lớn nhất")
            ]));
    }

    /// <summary>Precondition 1 / AF5: only a signed-in buyer with an Approved business profile can search.</summary>
    private async Task<Error?> CheckBuyerAsync(CancellationToken cancellationToken)
    {
        if (currentActor.UserId <= 0) return s_signInRequired;
        string? status = await repository.GetBuyerStatusAsync(currentActor.UserId, cancellationToken);
        return status == VerificationStatuses.Approved ? null : s_notApproved;
    }

    private async Task<Dictionary<string, string>> ProvinceNamesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var provinces = await addressCatalog.GetProvincesAsync(cancellationToken);
            return provinces.GroupBy(p => p.Code).ToDictionary(g => g.Key, g => g.First().Name);
        }
        catch (Exception)
        {
            // The catalogue is only used for display names; the list still works without it.
            return new Dictionary<string, string>();
        }
    }
}
