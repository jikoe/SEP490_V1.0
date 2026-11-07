namespace AgriSustain.Application.DTOs.Finance;

/// <summary>UC-FIN-05 list filters. State: Draft, PendingApproval, Rejected, Scheduled, Active, Expired.</summary>
public sealed record FeeSearch(string? Keyword = null, string? Group = null, string? OrderType = null,
    long? CategoryId = null, string? State = null, int Page = 1, int PageSize = 10);

public sealed record FeePolicyInput(
    string Method,
    string CalculationBase,
    decimal? Rate,
    decimal? FixedAmount,
    decimal? MinAmount,
    decimal? MaxAmount,
    string OrderType,
    bool AppliesToAllCategories,
    IReadOnlyList<long>? CategoryIds,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string Reason);

/// <summary>Step 2–7: a new fee type with its first policy. Submit=false keeps the policy as Draft.</summary>
public sealed record CreateFeeRequest(
    string FeeCode,
    string FeeName,
    string? Description,
    string FeeGroup,
    string RefundMode,
    FeePolicyInput Policy,
    bool Submit = true,
    string? RequestKey = null);

/// <summary>Name and description can always change; group and refund mode only while the fee is a Draft never applied.</summary>
public sealed record UpdateFeeTypeRequest(string FeeName, string? Description, string FeeGroup, string RefundMode);

public sealed record NewPolicyRequest(FeePolicyInput Policy, bool Submit = true, string? RequestKey = null);

public sealed record EditPolicyRequest(FeePolicyInput Policy, bool Submit, string? Version);

public sealed record PolicyActionRequest(string? Version, string? Note = null);

public sealed record EndDatePolicyRequest(DateOnly EffectiveTo, string Reason, string? Version);

public sealed record FeePreviewLine(long? CategoryId, decimal Subtotal);

/// <summary>Step 6 preview: a sample seller order on a date, optionally with a draft policy not yet saved.</summary>
public sealed record FeePreviewRequest(
    string OrderType,
    DateOnly? Date,
    IReadOnlyList<FeePreviewLine> Lines,
    decimal DeliveryFee = 0,
    long? FeeTypeId = null,
    string? FeeGroup = null,
    FeePolicyInput? DraftPolicy = null);

public sealed record FeePreviewFee(string FeeCode, string FeeName, string FeeGroup, decimal Amount, bool FromDraft);

public sealed record FeePreviewDto(DateOnly Date, decimal ItemsTotal, decimal SellerOrderTotal,
    IReadOnlyList<FeePreviewFee> Fees, decimal TotalFees, decimal NetAmount, decimal? TaxWithheld);

public sealed record FeePolicyDto(
    long Id,
    string PolicyCode,
    string Method,
    string CalculationBase,
    decimal? Rate,
    decimal? FixedAmount,
    decimal? MinAmount,
    decimal? MaxAmount,
    string OrderType,
    bool AppliesToAllCategories,
    IReadOnlyList<long> CategoryIds,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    DateOnly? PendingEffectiveTo,
    string State,
    string Reason,
    string? ReviewNote,
    long CreatedByUserId,
    long? ApprovedByUserId,
    DateTime CreatedAt,
    DateTime? ApprovedAt,
    string Version);

public sealed record FeeTypeDto(
    long Id,
    string FeeCode,
    string FeeName,
    string? Description,
    string FeeGroup,
    string RefundMode,
    bool IsMandatory,
    string Status,
    bool Applied,
    IReadOnlyList<FeePolicyDto> Policies);

/// <summary>List row: the policy applying today (or the next scheduled one) and how many wait for review.</summary>
public sealed record FeeTypeRow(
    long Id,
    string FeeCode,
    string FeeName,
    string FeeGroup,
    string RefundMode,
    bool IsMandatory,
    string Status,
    IReadOnlyList<FeePolicyDto> CurrentPolicies,
    int PendingReviews);

public sealed record FeeCategoryOption(long Id, string Name, bool IsActive);

public sealed record FeeResultDto(string Message, FeeTypeDto? Fee);
