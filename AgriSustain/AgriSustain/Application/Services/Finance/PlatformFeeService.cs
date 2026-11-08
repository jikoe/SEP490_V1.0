using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.Finance;

/// <summary>
/// UC-FIN-05 Manage Platform Fees. Finance Staff define fee types and submit versioned policies;
/// an Administrator approves them (never the author). Scheduled / Active / Expired follow the approved
/// dates, so a policy applies from its effective date without a background job.
/// </summary>
public sealed class PlatformFeeService(
    IFeeRepository repository,
    IAdministrationRepository accounts,
    IUseCaseUnitOfWork unitOfWork,
    IActorContext actor,
    TimeProvider clock) : IPlatformFeeService
{
    public const string AuditFeeType = "FeeType";
    public const string AuditFeePolicy = "FeePolicy";
    private static readonly string[] s_financeRoles = ["FinanceStaff", "Finance Staff"];
    private static readonly string[] s_adminRoles = ["SystemAdmin", "Administrator", "Admin"];

    public async Task<UseCaseResult<Page<FeeTypeRow>>> SearchAsync(FeeSearch search, CancellationToken cancellationToken)
    {
        if (await RoleAsync(cancellationToken) is null)
        {
            return Denied<Page<FeeTypeRow>>();
        }
        if (search.Page <= 0 || search.PageSize is <= 0 or > 100 || search.Keyword?.Length > 100)
        {
            return Invalid<Page<FeeTypeRow>>("Bộ lọc không hợp lệ.");
        }
        var today = Today();
        var rows = (await repository.AllTypesAsync(cancellationToken))
            .Where(type => string.IsNullOrWhiteSpace(search.Keyword)
                || type.FeeCode.Contains(search.Keyword.Trim(), StringComparison.OrdinalIgnoreCase)
                || type.FeeName.Contains(search.Keyword.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(type => string.IsNullOrWhiteSpace(search.Group) || type.FeeGroup == search.Group)
            .Where(type => string.IsNullOrWhiteSpace(search.OrderType)
                || type.Policies.Any(policy => policy.OrderType == search.OrderType))
            .Where(type => search.CategoryId is null
                || type.Policies.Any(policy => policy.AppliesToAllCategories || policy.Covers(search.CategoryId)))
            .Where(type => string.IsNullOrWhiteSpace(search.State)
                || type.Policies.Any(policy => policy.State(today) == search.State))
            .OrderBy(type => type.FeeGroup).ThenBy(type => type.FeeCode)
            .ToList();
        var page = rows.Skip((search.Page - 1) * search.PageSize).Take(search.PageSize).Select(type =>
        {
            var current = type.Policies.Where(policy => policy.State(today) is FeePolicy.StateActive)
                .Concat(type.Policies.Where(policy => policy.State(today) is FeePolicy.StateScheduled))
                .OrderBy(policy => policy.OrderType).ThenByDescending(policy => policy.AppliesToAllCategories)
                .Select(policy => ToDto(policy, today)).ToList();
            int pending = type.Policies.Count(policy => policy.ApprovalStatus == FeePolicy.ApprovalPending
                || policy.PendingEffectiveTo is not null);
            return new FeeTypeRow(type.FeeTypeId, type.FeeCode, type.FeeName, type.FeeGroup, type.RefundMode,
                type.IsMandatory, type.Status, current, pending);
        }).ToList();
        return UseCaseResult<Page<FeeTypeRow>>.Success(new Page<FeeTypeRow>(page, rows.Count, search.Page,
            search.PageSize));
    }

    public async Task<UseCaseResult<FeeTypeDto>> GetAsync(long id, CancellationToken cancellationToken)
    {
        if (await RoleAsync(cancellationToken) is null)
        {
            return Denied<FeeTypeDto>();
        }
        var type = await repository.FindTypeAsync(id, cancellationToken);
        return type is null ? Missing<FeeTypeDto>()
            : UseCaseResult<FeeTypeDto>.Success(await ToDtoAsync(type, cancellationToken));
    }

    public async Task<UseCaseResult<FeeResultDto>> CreateAsync(CreateFeeRequest request, CancellationToken cancellationToken)
    {
        if (await RoleAsync(cancellationToken) != Role.Finance)
        {
            return Denied<FeeResultDto>();
        }
        string requestKey = RequestKeyOf(request.RequestKey);
        if (await repository.FindPolicyByRequestKeyAsync(requestKey, cancellationToken) is { } existing)
        {
            // Same request sent again: return what it created (UC-FIN-05 Steps 7–8 duplicate).
            return await ResultAsync(existing.FeeTypeId, "Yêu cầu này đã được xử lý.", cancellationToken);
        }
        string code = (request.FeeCode ?? "").Trim().ToUpperInvariant();
        var fields = new Dictionary<string, string[]>();
        CheckType(fields, request.FeeName, request.Description, request.FeeGroup, request.RefundMode);
        if (!FeeType.IsValidCode(code))
        {
            fields["FeeCode"] = ["Mã phí gồm 3–30 ký tự A–Z, 0–9 hoặc dấu gạch dưới."];
        }
        if (fields.Count > 0)
        {
            return UseCaseResult<FeeResultDto>.Validation(fields);
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (await repository.CodeExistsAsync(code, cancellationToken))
        {
            // MSG195
            return UseCaseResult<FeeResultDto>.Validation(new Dictionary<string, string[]>
            {
                ["FeeCode"] = [$"Mã phí {code} đã tồn tại."]
            });
        }
        var type = new FeeType
        {
            FeeCode = code, FeeName = request.FeeName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            FeeGroup = request.FeeGroup, RefundMode = request.RefundMode,
            IsMandatory = FeeType.IsMandatoryGroup(request.FeeGroup), Status = FeeType.StatusDraft,
            CreatedAt = Now()
        };
        var check = await CheckPolicyAsync(type, request.Policy, null, cancellationToken);
        if (check is not null)
        {
            return check;
        }
        var policy = NewPolicy(type, request.Policy, request.Submit, requestKey);
        type.Policies.Add(policy);
        repository.AddType(type);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        Audit(AuditFeeType, type.FeeTypeId, "CreateFeeType", null, Snapshot(type));
        Audit(AuditFeePolicy, policy.FeePolicyId, request.Submit ? "SubmitFeePolicy" : "CreateFeePolicy", null,
            Snapshot(policy));
        if (request.Submit)
        {
            await NotifyAdministratorsAsync(type, policy, cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ResultAsync(type.FeeTypeId, SavedMessage(type, policy), cancellationToken);
    }

    public async Task<UseCaseResult<FeeResultDto>> UpdateTypeAsync(long id, UpdateFeeTypeRequest request,
        CancellationToken cancellationToken)
    {
        if (await RoleAsync(cancellationToken) != Role.Finance)
        {
            return Denied<FeeResultDto>();
        }
        var fields = new Dictionary<string, string[]>();
        CheckType(fields, request.FeeName, request.Description, request.FeeGroup, request.RefundMode);
        if (fields.Count > 0)
        {
            return UseCaseResult<FeeResultDto>.Validation(fields);
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var type = await repository.FindTypeAsync(id, cancellationToken);
        if (type is null)
        {
            return Missing<FeeResultDto>();
        }
        bool locked = type.Status != FeeType.StatusDraft || await repository.IsAppliedAsync(id, cancellationToken);
        if (locked && (type.FeeGroup != request.FeeGroup || type.RefundMode != request.RefundMode))
        {
            // MSG190
            return Applied<FeeResultDto>(type.FeeCode);
        }
        string before = Snapshot(type);
        type.FeeName = request.FeeName.Trim();
        type.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        type.FeeGroup = request.FeeGroup;
        type.RefundMode = request.RefundMode;
        type.IsMandatory = FeeType.IsMandatoryGroup(request.FeeGroup);
        Audit(AuditFeeType, type.FeeTypeId, "UpdateFeeType", before, Snapshot(type));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ResultAsync(id, $"Đã lưu phí {type.FeeCode}.", cancellationToken);
    }

    public async Task<UseCaseResult<FeeResultDto>> DeleteTypeAsync(long id, CancellationToken cancellationToken)
    {
        if (await RoleAsync(cancellationToken) != Role.Finance)
        {
            return Denied<FeeResultDto>();
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var type = await repository.FindTypeAsync(id, cancellationToken);
        if (type is null)
        {
            return Missing<FeeResultDto>();
        }
        // BR-FEE-07: only a Draft fee type that was never approved or applied can be deleted.
        if (type.Status != FeeType.StatusDraft || type.IsMandatory
            || type.Policies.Any(policy => policy.ApprovalStatus == FeePolicy.ApprovalApproved)
            || await repository.IsAppliedAsync(id, cancellationToken))
        {
            return Applied<FeeResultDto>(type.FeeCode);
        }
        Audit(AuditFeeType, type.FeeTypeId, "DeleteFeeType", Snapshot(type), null);
        repository.RemoveType(type);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UseCaseResult<FeeResultDto>.Success(new FeeResultDto($"Đã xóa phí {type.FeeCode}.", null));
    }

    public async Task<UseCaseResult<FeeResultDto>> AddPolicyAsync(long feeTypeId, NewPolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (await RoleAsync(cancellationToken) != Role.Finance)
        {
            return Denied<FeeResultDto>();
        }
        string requestKey = RequestKeyOf(request.RequestKey);
        if (await repository.FindPolicyByRequestKeyAsync(requestKey, cancellationToken) is { } existing)
        {
            return await ResultAsync(existing.FeeTypeId, "Yêu cầu này đã được xử lý.", cancellationToken);
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var type = await repository.FindTypeAsync(feeTypeId, cancellationToken);
        if (type is null)
        {
            return Missing<FeeResultDto>();
        }
        var check = await CheckPolicyAsync(type, request.Policy, null, cancellationToken);
        if (check is not null)
        {
            return check;
        }
        var policy = NewPolicy(type, request.Policy, request.Submit, requestKey);
        type.Policies.Add(policy);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        Audit(AuditFeePolicy, policy.FeePolicyId, request.Submit ? "SubmitFeePolicy" : "CreateFeePolicy", null,
            Snapshot(policy));
        if (request.Submit)
        {
            await NotifyAdministratorsAsync(type, policy, cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ResultAsync(type.FeeTypeId, SavedMessage(type, policy), cancellationToken);
    }

    public async Task<UseCaseResult<FeeResultDto>> EditPolicyAsync(long policyId, EditPolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (await RoleAsync(cancellationToken) != Role.Finance)
        {
            return Denied<FeeResultDto>();
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var policy = await repository.FindPolicyAsync(policyId, cancellationToken);
        if (policy is null)
        {
            return Missing<FeeResultDto>();
        }
        if (!SameVersion(policy, request.Version))
        {
            return Changed<FeeResultDto>();
        }
        // BR-FEE-07: only a policy not yet approved can be edited.
        if (policy.ApprovalStatus is not (FeePolicy.ApprovalDraft or FeePolicy.ApprovalRejected))
        {
            return Applied<FeeResultDto>(policy.FeeType.FeeCode);
        }
        var check = await CheckPolicyAsync(policy.FeeType, request.Policy, policy.FeePolicyId, cancellationToken);
        if (check is not null)
        {
            return check;
        }
        string before = Snapshot(policy);
        Apply(policy, request.Policy);
        policy.ApprovalStatus = request.Submit ? FeePolicy.ApprovalPending : FeePolicy.ApprovalDraft;
        policy.ReviewNote = null;
        Audit(AuditFeePolicy, policy.FeePolicyId, request.Submit ? "SubmitFeePolicy" : "UpdateFeePolicy", before,
            Snapshot(policy));
        if (request.Submit)
        {
            await NotifyAdministratorsAsync(policy.FeeType, policy, cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ResultAsync(policy.FeeTypeId, SavedMessage(policy.FeeType, policy), cancellationToken);
    }

    public async Task<UseCaseResult<FeeResultDto>> SubmitPolicyAsync(long policyId, PolicyActionRequest request,
        CancellationToken cancellationToken)
    {
        if (await RoleAsync(cancellationToken) != Role.Finance)
        {
            return Denied<FeeResultDto>();
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var policy = await repository.FindPolicyAsync(policyId, cancellationToken);
        if (policy is null)
        {
            return Missing<FeeResultDto>();
        }
        if (!SameVersion(policy, request.Version)
            || policy.ApprovalStatus is not (FeePolicy.ApprovalDraft or FeePolicy.ApprovalRejected))
        {
            return Changed<FeeResultDto>();
        }
        var check = await CheckPolicyAsync(policy.FeeType, InputOf(policy), policy.FeePolicyId, cancellationToken);
        if (check is not null)
        {
            return check;
        }
        policy.ApprovalStatus = FeePolicy.ApprovalPending;
        policy.ReviewNote = null;
        Audit(AuditFeePolicy, policy.FeePolicyId, "SubmitFeePolicy", null, Snapshot(policy));
        await NotifyAdministratorsAsync(policy.FeeType, policy, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ResultAsync(policy.FeeTypeId, SavedMessage(policy.FeeType, policy), cancellationToken);
    }

    public async Task<UseCaseResult<FeeResultDto>> ApprovePolicyAsync(long policyId, PolicyActionRequest request,
        CancellationToken cancellationToken)
    {
        if (await RoleAsync(cancellationToken) != Role.Administrator)
        {
            return Denied<FeeResultDto>();
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var policy = await repository.FindPolicyAsync(policyId, cancellationToken);
        if (policy is null)
        {
            return Missing<FeeResultDto>();
        }
        if (!SameVersion(policy, request.Version))
        {
            return Changed<FeeResultDto>();
        }
        if (policy.CreatedByUserId == actor.UserId)
        {
            return Denied<FeeResultDto>();
        }
        var type = policy.FeeType;
        string before = Snapshot(policy);
        string action;
        string message;
        if (policy.PendingEffectiveTo is { } endDate && policy.ApprovalStatus == FeePolicy.ApprovalApproved)
        {
            // Approving an end-date request.
            if (MissingRequiredCover(type, policy, endDate) is { } scope)
            {
                return RequiredPolicy<FeeResultDto>(type.FeeCode, scope);
            }
            policy.EffectiveTo = endDate;
            policy.PendingEffectiveTo = null;
            action = "ApproveFeePolicyEndDate";
            message = $"Đã duyệt kết thúc chính sách {policy.PolicyCode} từ sau ngày {endDate:dd/MM/yyyy}.";
        }
        else if (policy.ApprovalStatus == FeePolicy.ApprovalPending)
        {
            var today = Today();
            if (policy.EffectiveFrom < today)
            {
                return Invalid<FeeResultDto>("Ngày hiệu lực đã qua. Hãy trả lại để Finance chọn ngày mới.");
            }
            if (Conflict(type, policy, InputOf(policy), policy.FeePolicyId, approving: true) is { } conflict)
            {
                return Overlap<FeeResultDto>(conflict, type.FeeCode);
            }
            // BR-FEE-10: a category-specific rate needs an all-categories policy of the same order type.
            if (!policy.AppliesToAllCategories && !type.Policies.Any(other => other.AppliesToAllCategories
                    && other.OrderType == policy.OrderType && other.ApprovalStatus == FeePolicy.ApprovalApproved
                    && other.State(today) is FeePolicy.StateActive or FeePolicy.StateScheduled))
            {
                return RequiredPolicy<FeeResultDto>(type.FeeCode, "tất cả danh mục");
            }
            // The previous version of the same scope ends the day before this one starts.
            foreach (var previous in Previous(type, policy))
            {
                previous.EffectiveTo = policy.EffectiveFrom.AddDays(-1);
                Audit(AuditFeePolicy, previous.FeePolicyId, "EndPreviousFeePolicy", null, Snapshot(previous));
            }
            policy.ApprovalStatus = FeePolicy.ApprovalApproved;
            policy.ApprovedByUserId = actor.UserId;
            policy.ApprovedAt = Now();
            type.Status = FeeType.StatusActive;
            action = "ApproveFeePolicy";
            // MSG194
            message = $"Chính sách phí {policy.PolicyCode} đã được duyệt và áp dụng từ {policy.EffectiveFrom:dd/MM/yyyy}.";
        }
        else
        {
            return Changed<FeeResultDto>();
        }
        policy.ReviewNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        Audit(AuditFeePolicy, policy.FeePolicyId, action, before, Snapshot(policy));
        Notify(policy.CreatedByUserId, "Chính sách phí đã được duyệt", message, policy);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ResultAsync(type.FeeTypeId, message, cancellationToken);
    }

    public async Task<UseCaseResult<FeeResultDto>> RejectPolicyAsync(long policyId, PolicyActionRequest request,
        CancellationToken cancellationToken)
    {
        if (await RoleAsync(cancellationToken) != Role.Administrator)
        {
            return Denied<FeeResultDto>();
        }
        if (string.IsNullOrWhiteSpace(request.Note) || request.Note.Trim().Length > 500)
        {
            return UseCaseResult<FeeResultDto>.Validation(new Dictionary<string, string[]>
            {
                ["Note"] = [string.IsNullOrWhiteSpace(request.Note) ? "Trường Lý do là bắt buộc."
                    : "Vượt quá độ dài tối đa 500 ký tự."]
            });
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var policy = await repository.FindPolicyAsync(policyId, cancellationToken);
        if (policy is null)
        {
            return Missing<FeeResultDto>();
        }
        if (!SameVersion(policy, request.Version))
        {
            return Changed<FeeResultDto>();
        }
        string before = Snapshot(policy);
        if (policy.PendingEffectiveTo is not null && policy.ApprovalStatus == FeePolicy.ApprovalApproved)
        {
            policy.PendingEffectiveTo = null;
        }
        else if (policy.ApprovalStatus == FeePolicy.ApprovalPending)
        {
            policy.ApprovalStatus = FeePolicy.ApprovalDraft;
        }
        else
        {
            return Changed<FeeResultDto>();
        }
        policy.ReviewNote = request.Note.Trim();
        Audit(AuditFeePolicy, policy.FeePolicyId, "RejectFeePolicy", before, Snapshot(policy));
        // MSG191
        string message = $"Chính sách phí {policy.PolicyCode} đã bị Quản trị viên từ chối. Lý do: {policy.ReviewNote}. "
            + "Hãy sửa và gửi duyệt lại.";
        Notify(policy.CreatedByUserId, "Chính sách phí bị từ chối", message, policy);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ResultAsync(policy.FeeTypeId, $"Đã từ chối chính sách {policy.PolicyCode}.", cancellationToken);
    }

    public async Task<UseCaseResult<FeeResultDto>> EndDatePolicyAsync(long policyId, EndDatePolicyRequest request,
        CancellationToken cancellationToken)
    {
        if (await RoleAsync(cancellationToken) != Role.Finance)
        {
            return Denied<FeeResultDto>();
        }
        var today = Today();
        var fields = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            fields["Reason"] = ["Trường Lý do là bắt buộc."];
        }
        else if (request.Reason.Trim().Length > 500)
        {
            fields["Reason"] = ["Vượt quá độ dài tối đa 500 ký tự."];
        }
        if (request.EffectiveTo < today)
        {
            fields["EffectiveTo"] = ["Ngày kết thúc không được ở quá khứ."];
        }
        if (fields.Count > 0)
        {
            return UseCaseResult<FeeResultDto>.Validation(fields);
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        var policy = await repository.FindPolicyAsync(policyId, cancellationToken);
        if (policy is null)
        {
            return Missing<FeeResultDto>();
        }
        if (!SameVersion(policy, request.Version))
        {
            return Changed<FeeResultDto>();
        }
        if (policy.State(today) is not (FeePolicy.StateActive or FeePolicy.StateScheduled)
            || policy.PendingEffectiveTo is not null)
        {
            // MSG131: the status change is not allowed now.
            return Invalid<FeeResultDto>("Chỉ kết thúc được chính sách đang hoặc sắp áp dụng và chưa có yêu cầu kết thúc khác.");
        }
        if (request.EffectiveTo < policy.EffectiveFrom || policy.EffectiveTo is { } current && request.EffectiveTo >= current)
        {
            return UseCaseResult<FeeResultDto>.Validation(new Dictionary<string, string[]>
            {
                ["EffectiveTo"] = ["Ngày kết thúc phải trong thời gian hiệu lực hiện tại của chính sách."]
            });
        }
        if (MissingRequiredCover(policy.FeeType, policy, request.EffectiveTo) is { } scope)
        {
            // MSG192
            return RequiredPolicy<FeeResultDto>(policy.FeeType.FeeCode, scope);
        }
        string before = Snapshot(policy);
        policy.PendingEffectiveTo = request.EffectiveTo;
        policy.ReviewNote = null;
        var audit = NewAudit(AuditFeePolicy, policy.FeePolicyId, "RequestFeePolicyEndDate", before, Snapshot(policy));
        audit.Reason = request.Reason.Trim();
        repository.AddAudit(audit);
        await NotifyAdministratorsAsync(policy.FeeType, policy, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await ResultAsync(policy.FeeTypeId,
            $"Đã gửi duyệt việc kết thúc chính sách {policy.PolicyCode} sau ngày {request.EffectiveTo:dd/MM/yyyy}.",
            cancellationToken);
    }

    public async Task<UseCaseResult<FeePreviewDto>> PreviewAsync(FeePreviewRequest request, CancellationToken cancellationToken)
    {
        if (await RoleAsync(cancellationToken) is null)
        {
            return Denied<FeePreviewDto>();
        }
        if (request.Lines is null || request.Lines.Count is 0 or > 20 || request.Lines.Any(line => line.Subtotal < 0)
            || request.DeliveryFee < 0 || !FeePolicy.OrderTypes.Contains(request.OrderType))
        {
            return Invalid<FeePreviewDto>("Đơn mẫu không hợp lệ.");
        }
        var date = request.Date ?? Today();
        var policies = (await repository.ActivePoliciesAsync(request.OrderType, date, cancellationToken)).ToList();
        FeePolicy? draft = null;
        if (request.DraftPolicy is not null)
        {
            var type = request.FeeTypeId is long id ? await repository.FindTypeAsync(id, cancellationToken) : null;
            type ??= new FeeType { FeeTypeId = -1, FeeCode = "NEW", FeeName = "Phí đang soạn",
                FeeGroup = request.FeeGroup ?? FeeType.GroupOther, RefundMode = FeeType.RefundProportional };
            draft = new FeePolicy { FeeTypeId = type.FeeTypeId, FeeType = type, PolicyCode = "DRAFT" };
            Apply(draft, request.DraftPolicy with { OrderType = request.OrderType });
            // The draft replaces the policies of the same fee type and scope in the preview.
            policies.RemoveAll(policy => policy.FeeTypeId == type.FeeTypeId
                && policy.AppliesToAllCategories == draft.AppliesToAllCategories);
            policies.Add(draft);
        }
        var inputs = request.Lines.Select((line, index) => new FeeItemInput(index, line.CategoryId, line.Subtotal)).ToList();
        decimal itemsTotal = inputs.Sum(item => item.Subtotal);
        decimal sellerTotal = itemsTotal + request.DeliveryFee;
        var lines = FeeCalculator.Calculate(inputs, sellerTotal, policies);
        var fees = lines.GroupBy(line => line.Policy.FeeType)
            .Select(group => new FeePreviewFee(group.Key.FeeCode, group.Key.FeeName, group.Key.FeeGroup,
                group.Sum(line => line.Amount), group.Any(line => ReferenceEquals(line.Policy, draft))))
            .OrderBy(fee => fee.FeeGroup).ToList();
        decimal total = fees.Sum(fee => fee.Amount);
        decimal tax = fees.Where(fee => fee.FeeGroup == FeeType.GroupTaxWithholding).Sum(fee => fee.Amount);
        return UseCaseResult<FeePreviewDto>.Success(new FeePreviewDto(date, itemsTotal, sellerTotal, fees, total,
            sellerTotal - total, tax));
    }

    public async Task<UseCaseResult<IReadOnlyList<FeeCategoryOption>>> CategoriesAsync(CancellationToken cancellationToken)
    {
        if (await RoleAsync(cancellationToken) is null)
        {
            return Denied<IReadOnlyList<FeeCategoryOption>>();
        }
        return UseCaseResult<IReadOnlyList<FeeCategoryOption>>.Success(
            await repository.SelectableCategoriesAsync(cancellationToken));
    }

    // ---- rules ------------------------------------------------------------------------------

    private static void CheckType(Dictionary<string, string[]> fields, string? name, string? description,
        string? group, string? refundMode)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            fields["FeeName"] = ["Trường Tên phí là bắt buộc."];
        }
        else if (name.Trim().Length > 100)
        {
            fields["FeeName"] = ["Vượt quá độ dài tối đa 100 ký tự."];
        }
        if (description?.Trim().Length > 500)
        {
            fields["Description"] = ["Vượt quá độ dài tối đa 500 ký tự."];
        }
        if (group is null || !FeeType.Groups.Contains(group))
        {
            fields["FeeGroup"] = ["Nhóm phí không hợp lệ."];
        }
        if (refundMode is null || !FeeType.RefundModes.Contains(refundMode))
        {
            fields["RefundMode"] = ["Cách xử lý khi hoàn tiền không hợp lệ."];
        }
    }

    /// <summary>BR-FEE-02, BR-FEE-03, BR-FEE-04, BR-FEE-11 and the overlap check (MSG189).</summary>
    private async Task<UseCaseResult<FeeResultDto>?> CheckPolicyAsync(FeeType type, FeePolicyInput? input,
        long? exceptPolicyId, CancellationToken cancellationToken)
    {
        if (input is null)
        {
            return Invalid<FeeResultDto>("Thiếu thông tin chính sách phí.");
        }
        var fields = new Dictionary<string, string[]>();
        if (!FeePolicy.Methods.Contains(input.Method))
        {
            fields["Method"] = ["Cách tính không hợp lệ."];
        }
        if (!FeePolicy.Bases.Contains(input.CalculationBase))
        {
            fields["CalculationBase"] = ["Cơ sở tính không hợp lệ."];
        }
        if (!FeePolicy.OrderTypes.Contains(input.OrderType))
        {
            fields["OrderType"] = ["Loại đơn không hợp lệ."];
        }
        if (input.Method == FeePolicy.MethodPercentage)
        {
            if (input.Rate is not > 0 || input.Rate > 100)
            {
                fields["Rate"] = ["Tỷ lệ phải lớn hơn 0 và không quá 100%."];
            }
        }
        else if (input.FixedAmount is not > 0 || decimal.Round(input.FixedAmount.Value, 0) != input.FixedAmount)
        {
            fields["FixedAmount"] = ["Số tiền cố định phải là số nguyên VND lớn hơn 0."];
        }
        if (input.MinAmount < 0 || input.MaxAmount < 0)
        {
            fields["MinAmount"] = ["Mức tối thiểu và tối đa không được âm."];
        }
        else if (input.MinAmount is { } min && input.MaxAmount is { } max && min > max)
        {
            fields["MinAmount"] = ["Mức tối thiểu không được lớn hơn mức tối đa."];
        }
        if (input.EffectiveFrom < Today())
        {
            fields["EffectiveFrom"] = ["Ngày bắt đầu phải từ hôm nay trở đi."];
        }
        if (input.EffectiveTo is { } to && to <= input.EffectiveFrom)
        {
            fields["EffectiveTo"] = ["Ngày kết thúc phải sau ngày bắt đầu."];
        }
        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            fields["Reason"] = ["Trường Lý do thay đổi là bắt buộc."];
        }
        else if (input.Reason.Trim().Length > 500)
        {
            fields["Reason"] = ["Vượt quá độ dài tối đa 500 ký tự."];
        }
        var categoryIds = (input.CategoryIds ?? []).Distinct().ToList();
        if (!input.AppliesToAllCategories)
        {
            if (!type.AllowsCategoryScope)
            {
                fields["CategoryIds"] = ["Chỉ phí hoa hồng và phí dịch vụ được áp dụng riêng theo danh mục."];
            }
            else if (input.Method == FeePolicy.MethodFixedPerOrder)
            {
                // BR-FEE-03 / AF6: a fixed per-order fee applies to all categories.
                fields["CategoryIds"] = ["Phí cố định mỗi đơn trang trại áp dụng cho tất cả danh mục."];
            }
            else if (categoryIds.Count == 0)
            {
                fields["CategoryIds"] = ["Hãy chọn ít nhất một danh mục."];
            }
            else
            {
                var categories = await repository.CategoriesAsync(categoryIds, cancellationToken);
                if (categories.Count != categoryIds.Count || categories.Values.Any(archived => archived))
                {
                    fields["CategoryIds"] = ["Không thể chọn danh mục đã lưu trữ hoặc không tồn tại."];
                }
            }
        }
        if (fields.Count > 0)
        {
            return UseCaseResult<FeeResultDto>.Validation(fields);
        }
        return Conflict(type, null, input, exceptPolicyId, approving: false) is { } conflict
            ? Overlap<FeeResultDto>(conflict, type.FeeCode) : null;
    }

    /// <summary>
    /// A policy of the same fee type, order type and overlapping scope whose period overlaps. An approved
    /// policy that starts earlier is the previous version: it is end-dated on approval, not a conflict.
    /// </summary>
    private static FeePolicy? Conflict(FeeType type, FeePolicy? self, FeePolicyInput input, long? exceptPolicyId,
        bool approving)
    {
        var categoryIds = (input.CategoryIds ?? []).ToHashSet();
        foreach (var other in type.Policies)
        {
            if (other == self || other.FeePolicyId == exceptPolicyId && exceptPolicyId is not null
                || other.OrderType != input.OrderType
                || other.ApprovalStatus is not (FeePolicy.ApprovalApproved or FeePolicy.ApprovalPending)
                || approving && other.ApprovalStatus == FeePolicy.ApprovalPending)
            {
                continue;
            }
            bool sameScope = other.AppliesToAllCategories && input.AppliesToAllCategories
                || !other.AppliesToAllCategories && !input.AppliesToAllCategories
                    && other.Categories.Any(category => categoryIds.Contains(category.CategoryId));
            if (!sameScope || !other.Overlaps(input.EffectiveFrom, input.EffectiveTo))
            {
                continue;
            }
            bool previousVersion = other.ApprovalStatus == FeePolicy.ApprovalApproved
                && other.EffectiveFrom < input.EffectiveFrom;
            if (!previousVersion)
            {
                return other;
            }
        }
        return null;
    }

    private static IEnumerable<FeePolicy> Previous(FeeType type, FeePolicy policy) => type.Policies
        .Where(other => other != policy && other.ApprovalStatus == FeePolicy.ApprovalApproved
            && other.OrderType == policy.OrderType && other.EffectiveFrom < policy.EffectiveFrom
            && other.Overlaps(policy.EffectiveFrom, policy.EffectiveTo)
            && (other.AppliesToAllCategories && policy.AppliesToAllCategories
                || !other.AppliesToAllCategories && !policy.AppliesToAllCategories
                    && other.Categories.Select(c => c.CategoryId).Intersect(policy.Categories.Select(c => c.CategoryId)).Any()))
        .ToList();

    /// <summary>
    /// BR-FEE-07 / BR-FEE-10: a mandatory fee, or the all-categories policy of a category-scoped fee,
    /// must be followed by another approved policy starting no later than the day after it ends.
    /// Returns the scope that would be left uncovered.
    /// </summary>
    private static string? MissingRequiredCover(FeeType type, FeePolicy policy, DateOnly endDate)
    {
        bool required = type.IsMandatory || type.AllowsCategoryScope && policy.AppliesToAllCategories;
        if (!required)
        {
            return null;
        }
        bool covered = type.Policies.Any(other => other != policy && other.ApprovalStatus == FeePolicy.ApprovalApproved
            && other.OrderType == policy.OrderType && other.AppliesToAllCategories == policy.AppliesToAllCategories
            && other.EffectiveFrom <= endDate.AddDays(1) && (other.EffectiveTo is null || other.EffectiveTo > endDate));
        return covered ? null : policy.AppliesToAllCategories ? "tất cả danh mục" : "danh mục đã chọn";
    }

    // ---- mapping and helpers ------------------------------------------------------------------

    private FeePolicy NewPolicy(FeeType type, FeePolicyInput input, bool submit, string requestKey)
    {
        int version = type.Policies.Count(policy => policy.OrderType == input.OrderType) + 1;
        var policy = new FeePolicy
        {
            FeeType = type,
            PolicyCode = $"{type.FeeCode}-{input.OrderType.ToUpperInvariant()}-V{version}",
            ApprovalStatus = submit ? FeePolicy.ApprovalPending : FeePolicy.ApprovalDraft,
            CreatedByUserId = actor.UserId, CreatedAt = Now(), RequestKey = requestKey
        };
        Apply(policy, input);
        return policy;
    }

    private void Apply(FeePolicy policy, FeePolicyInput input)
    {
        policy.Method = input.Method;
        policy.CalculationBase = input.CalculationBase;
        policy.Rate = input.Method == FeePolicy.MethodPercentage ? input.Rate : null;
        policy.FixedAmount = input.Method == FeePolicy.MethodPercentage ? null : input.FixedAmount;
        policy.MinAmount = input.MinAmount;
        policy.MaxAmount = input.MaxAmount;
        policy.OrderType = input.OrderType;
        policy.AppliesToAllCategories = input.AppliesToAllCategories;
        policy.EffectiveFrom = input.EffectiveFrom;
        policy.EffectiveTo = input.EffectiveTo;
        policy.Reason = input.Reason?.Trim() ?? "";
        var wanted = input.AppliesToAllCategories ? [] : (input.CategoryIds ?? []).Distinct().ToHashSet();
        var removed = policy.Categories.Where(category => !wanted.Contains(category.CategoryId)).ToList();
        if (removed.Count > 0 && policy.FeePolicyId > 0)
        {
            repository.RemoveCategories(removed);
        }
        foreach (var category in removed)
        {
            policy.Categories.Remove(category);
        }
        foreach (long id in wanted.Where(id => policy.Categories.All(category => category.CategoryId != id)))
        {
            policy.Categories.Add(new FeePolicyCategory { CategoryId = id, FeePolicy = policy });
        }
    }

    private static FeePolicyInput InputOf(FeePolicy policy) => new(policy.Method, policy.CalculationBase, policy.Rate,
        policy.FixedAmount, policy.MinAmount, policy.MaxAmount, policy.OrderType, policy.AppliesToAllCategories,
        policy.Categories.Select(category => category.CategoryId).ToList(), policy.EffectiveFrom, policy.EffectiveTo,
        policy.Reason);

    private async Task<UseCaseResult<FeeResultDto>> ResultAsync(long feeTypeId, string message,
        CancellationToken cancellationToken)
    {
        var type = await repository.FindTypeAsync(feeTypeId, cancellationToken);
        return UseCaseResult<FeeResultDto>.Success(new FeeResultDto(message,
            type is null ? null : await ToDtoAsync(type, cancellationToken)));
    }

    private async Task<FeeTypeDto> ToDtoAsync(FeeType type, CancellationToken cancellationToken)
    {
        var today = Today();
        return new FeeTypeDto(type.FeeTypeId, type.FeeCode, type.FeeName, type.Description, type.FeeGroup,
            type.RefundMode, type.IsMandatory, type.Status, await repository.IsAppliedAsync(type.FeeTypeId, cancellationToken),
            type.Policies.OrderBy(policy => policy.OrderType).ThenByDescending(policy => policy.EffectiveFrom)
                .ThenByDescending(policy => policy.FeePolicyId).Select(policy => ToDto(policy, today)).ToList());
    }

    public static FeePolicyDto ToDto(FeePolicy policy, DateOnly today) => new(policy.FeePolicyId, policy.PolicyCode,
        policy.Method, policy.CalculationBase, policy.Rate, policy.FixedAmount, policy.MinAmount, policy.MaxAmount,
        policy.OrderType, policy.AppliesToAllCategories, policy.Categories.Select(c => c.CategoryId).OrderBy(id => id).ToList(),
        policy.EffectiveFrom, policy.EffectiveTo, policy.PendingEffectiveTo, policy.State(today), policy.Reason,
        policy.ReviewNote, policy.CreatedByUserId, policy.ApprovedByUserId, policy.CreatedAt, policy.ApprovedAt,
        VersionOf(policy));

    public static string VersionOf(FeePolicy policy) => policy.RowVersion is { Length: > 0 } rowVersion
        ? Convert.ToBase64String(rowVersion)
        : $"{policy.ApprovalStatus}:{policy.EffectiveFrom:yyyyMMdd}:{policy.EffectiveTo:yyyyMMdd}:{policy.PendingEffectiveTo:yyyyMMdd}";

    private static bool SameVersion(FeePolicy policy, string? version) =>
        !string.IsNullOrWhiteSpace(version) && version == VersionOf(policy);

    private static string SavedMessage(FeeType type, FeePolicy policy) => policy.ApprovalStatus == FeePolicy.ApprovalPending
        // MSG193
        ? $"Chính sách phí {policy.PolicyCode} đã được gửi duyệt. Chính sách chỉ áp dụng sau khi được duyệt và tới ngày hiệu lực."
        // MSG-FEE-01
        : $"Đã lưu phí {type.FeeCode}. Trạng thái: Nháp.";

    private async Task NotifyAdministratorsAsync(FeeType type, FeePolicy policy, CancellationToken cancellationToken)
    {
        foreach (long admin in await repository.AdministratorIdsAsync(cancellationToken))
        {
            Notify(admin, "Chính sách phí chờ duyệt",
                $"Chính sách {policy.PolicyCode} của phí {type.FeeCode} đang chờ duyệt.", policy);
        }
    }

    private void Notify(long userId, string title, string content, FeePolicy policy) => repository.AddNotification(
        new Notification
        {
            UserId = userId, Title = title, Content = content, NotificationType = "PlatformFee",
            DeliveryChannel = "InApp", DeliveryStatus = "Sent", SentAt = Now(),
            RelatedRecordType = AuditFeePolicy, RelatedRecordId = policy.FeePolicyId
        });

    private void Audit(string type, long id, string action, string? before, string? after) =>
        repository.AddAudit(NewAudit(type, id, action, before, after));

    private AuditLog NewAudit(string type, long id, string action, string? before, string? after) => new()
    {
        EntityType = type, EntityId = id, Action = action, OldValue = before, NewValue = after,
        PerformedByUserId = actor.UserId, OccurredAt = Now(), TraceId = actor.TraceId
    };

    private static string Snapshot(FeeType type) => JsonSerializer.Serialize(new
    {
        type.FeeCode, type.FeeName, type.Description, type.FeeGroup, type.RefundMode, type.IsMandatory, type.Status
    });

    private static string Snapshot(FeePolicy policy) => JsonSerializer.Serialize(new
    {
        policy.PolicyCode, policy.Method, policy.CalculationBase, policy.Rate, policy.FixedAmount, policy.MinAmount,
        policy.MaxAmount, policy.OrderType, policy.AppliesToAllCategories,
        Categories = policy.Categories.Select(c => c.CategoryId), policy.EffectiveFrom, policy.EffectiveTo,
        policy.PendingEffectiveTo, policy.ApprovalStatus, policy.Reason
    });

    private static string RequestKeyOf(string? key) =>
        string.IsNullOrWhiteSpace(key) ? Guid.NewGuid().ToString("N") : key.Trim()[..Math.Min(100, key.Trim().Length)];

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;

    private DateOnly Today() => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime.AddHours(7));

    private enum Role { Finance, Administrator }

    private async Task<Role?> RoleAsync(CancellationToken cancellationToken)
    {
        var user = await accounts.FindActorAsync(actor.UserId, cancellationToken);
        if (user is null || user.Status != "Active" || user.IsTemporarilyLocked(clock.GetUtcNow()))
        {
            return null;
        }
        return s_financeRoles.Contains(user.Role) ? Role.Finance
            : s_adminRoles.Contains(user.Role) ? Role.Administrator : null;
    }

    private static UseCaseResult<T> Denied<T>() => UseCaseResult<T>.Failure("FORBIDDEN",
        "Bạn không có quyền thực hiện thao tác này.", "Forbidden");

    private static UseCaseResult<T> Missing<T>() => UseCaseResult<T>.Failure("NOT_FOUND",
        "Không tìm thấy phí hoặc chính sách phí.", "NotFound");

    private static UseCaseResult<T> Invalid<T>(string message) => UseCaseResult<T>.Failure("VALIDATION", message);

    // MSG122
    private static UseCaseResult<T> Changed<T>() => UseCaseResult<T>.Failure("FEE_CHANGED",
        "Chính sách phí đã thay đổi hoặc đã được xử lý. Hãy tải lại để xem thông tin hiện tại.", "Conflict");

    // MSG189
    private static UseCaseResult<T> Overlap<T>(FeePolicy other, string feeCode) => UseCaseResult<T>.Failure("FEE_OVERLAP",
        $"Thời gian hiệu lực trùng với chính sách {other.PolicyCode} ({other.EffectiveFrom:dd/MM/yyyy} – "
            + $"{(other.EffectiveTo is { } to ? to.ToString("dd/MM/yyyy") : "không thời hạn")}) của phí {feeCode} cho cùng "
            + "loại đơn và danh mục. Hãy chỉnh thời gian hoặc tạo phiên bản mới.", "Conflict");

    // MSG190
    private static UseCaseResult<T> Applied<T>(string feeCode) => UseCaseResult<T>.Failure("FEE_APPLIED",
        $"Phí {feeCode} đã được áp dụng hoặc đã duyệt nên không thể sửa hoặc xóa. Hãy kết thúc hoặc tạo phiên bản mới.",
        "Conflict");

    // MSG192
    private static UseCaseResult<T> RequiredPolicy<T>(string feeCode, string scope) => UseCaseResult<T>.Failure(
        "FEE_POLICY_REQUIRED",
        $"Phí {feeCode} luôn phải có chính sách đang áp dụng cho {scope}. Hãy tạo chính sách thay thế trước khi kết thúc.",
        "Conflict");
}
