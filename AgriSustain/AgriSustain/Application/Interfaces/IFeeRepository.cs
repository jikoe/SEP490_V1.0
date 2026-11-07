using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

/// <summary>UC-FIN-05 persistence. Saving goes through IUseCaseUnitOfWork.</summary>
public interface IFeeRepository
{
    /// <summary>Every fee type with its policies and categories, read-only.</summary>
    Task<IReadOnlyList<FeeType>> AllTypesAsync(CancellationToken cancellationToken);

    /// <summary>Tracked fee type with policies and categories.</summary>
    Task<FeeType?> FindTypeAsync(long id, CancellationToken cancellationToken);

    /// <summary>Tracked policy with its fee type (and that type's other policies) and categories.</summary>
    Task<FeePolicy?> FindPolicyAsync(long id, CancellationToken cancellationToken);

    Task<FeePolicy?> FindPolicyByRequestKeyAsync(string requestKey, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string feeCode, CancellationToken cancellationToken);

    /// <summary>True when the fee type has been applied to any order item (BR-FEE-07).</summary>
    Task<bool> IsAppliedAsync(long feeTypeId, CancellationToken cancellationToken);

    /// <summary>Category id → archived flag, for the ids that exist.</summary>
    Task<IReadOnlyDictionary<long, bool>> CategoriesAsync(IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken);

    /// <summary>Approved policies of the order type active on the date, with fee type and categories.</summary>
    Task<IReadOnlyList<FeePolicy>> ActivePoliciesAsync(string orderType, DateOnly date,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<long, long>> ProductCategoriesAsync(IReadOnlyCollection<long> productIds,
        CancellationToken cancellationToken);

    /// <summary>Tracked fee lines of a seller order.</summary>
    Task<IReadOnlyList<OrderItemFee>> SellerOrderFeesAsync(long sellerOrderId, CancellationToken cancellationToken);

    Task<IReadOnlyList<long>> AdministratorIdsAsync(CancellationToken cancellationToken);

    /// <summary>Categories a policy may target: Active or Inactive, never Archived (BR-FEE-11).</summary>
    Task<IReadOnlyList<FeeCategoryOption>> SelectableCategoriesAsync(CancellationToken cancellationToken);

    void AddType(FeeType feeType);

    void RemoveType(FeeType feeType);

    void RemoveCategories(IEnumerable<FeePolicyCategory> categories);

    void AddAudit(AuditLog audit);

    void AddNotification(Notification notification);
}

public interface IPlatformFeeService
{
    Task<UseCaseResult<Page<FeeTypeRow>>> SearchAsync(FeeSearch search, CancellationToken cancellationToken);
    Task<UseCaseResult<FeeTypeDto>> GetAsync(long id, CancellationToken cancellationToken);
    Task<UseCaseResult<FeeResultDto>> CreateAsync(CreateFeeRequest request, CancellationToken cancellationToken);
    Task<UseCaseResult<FeeResultDto>> UpdateTypeAsync(long id, UpdateFeeTypeRequest request, CancellationToken cancellationToken);
    Task<UseCaseResult<FeeResultDto>> DeleteTypeAsync(long id, CancellationToken cancellationToken);
    Task<UseCaseResult<FeeResultDto>> AddPolicyAsync(long feeTypeId, NewPolicyRequest request, CancellationToken cancellationToken);
    Task<UseCaseResult<FeeResultDto>> EditPolicyAsync(long policyId, EditPolicyRequest request, CancellationToken cancellationToken);
    Task<UseCaseResult<FeeResultDto>> SubmitPolicyAsync(long policyId, PolicyActionRequest request, CancellationToken cancellationToken);
    Task<UseCaseResult<FeeResultDto>> ApprovePolicyAsync(long policyId, PolicyActionRequest request, CancellationToken cancellationToken);
    Task<UseCaseResult<FeeResultDto>> RejectPolicyAsync(long policyId, PolicyActionRequest request, CancellationToken cancellationToken);
    Task<UseCaseResult<FeeResultDto>> EndDatePolicyAsync(long policyId, EndDatePolicyRequest request, CancellationToken cancellationToken);
    Task<UseCaseResult<FeePreviewDto>> PreviewAsync(FeePreviewRequest request, CancellationToken cancellationToken);
    Task<UseCaseResult<IReadOnlyList<FeeCategoryOption>>> CategoriesAsync(CancellationToken cancellationToken);
}

/// <summary>Fee lines of orders: recorded at placement (BR-FEE-05) and reversed on cancel or refund (BR-FEE-08).</summary>
public interface IOrderFeeService
{
    Task ApplyAsync(Order order, DateTime nowUtc, CancellationToken cancellationToken);

    Task ReverseCancelledAsync(long sellerOrderId, CancellationToken cancellationToken);

    Task ReverseRefundAsync(long sellerOrderId, decimal refundedTotal, decimal sellerOrderAmount,
        CancellationToken cancellationToken);
}
