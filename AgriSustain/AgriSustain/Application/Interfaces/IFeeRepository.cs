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

    /// <summary>
    /// Policy created by an earlier request with the same idempotency key.
    /// </summary>
    /// <param name="requestKey">Idempotency key.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The policy, or null.</returns>
    Task<FeePolicy?> FindPolicyByRequestKeyAsync(string requestKey, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a fee code is already taken (MSG195).
    /// </summary>
    /// <param name="feeCode">Upper-case fee code.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>True when taken.</returns>
    Task<bool> CodeExistsAsync(string feeCode, CancellationToken cancellationToken);

    /// <summary>True when the fee type has been applied to any order item (BR-FEE-07).</summary>
    Task<bool> IsAppliedAsync(long feeTypeId, CancellationToken cancellationToken);

    /// <summary>Category id → archived flag, for the ids that exist.</summary>
    Task<IReadOnlyDictionary<long, bool>> CategoriesAsync(IReadOnlyCollection<long> ids,
        CancellationToken cancellationToken);

    /// <summary>Approved policies of the order type active on the date, with fee type and categories.</summary>
    Task<IReadOnlyList<FeePolicy>> ActivePoliciesAsync(string orderType, DateOnly date,
        CancellationToken cancellationToken);

    /// <summary>
    /// Category of each product, used to pick category-specific policies.
    /// </summary>
    /// <param name="productIds">Products of the order.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Product id → category id.</returns>
    Task<IReadOnlyDictionary<long, long>> ProductCategoriesAsync(IReadOnlyCollection<long> productIds,
        CancellationToken cancellationToken);

    /// <summary>Tracked fee lines of a seller order.</summary>
    Task<IReadOnlyList<OrderItemFee>> SellerOrderFeesAsync(long sellerOrderId, CancellationToken cancellationToken);

    /// <summary>
    /// Active Administrators, who receive approval requests.
    /// </summary>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>User ids.</returns>
    Task<IReadOnlyList<long>> AdministratorIdsAsync(CancellationToken cancellationToken);

    /// <summary>Categories a policy may target: Active or Inactive, never Archived (BR-FEE-11).</summary>
    Task<IReadOnlyList<FeeCategoryOption>> SelectableCategoriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stages a new fee type (with its policies).
    /// </summary>
    /// <param name="feeType">Fee type to insert.</param>
    void AddType(FeeType feeType);

    /// <summary>
    /// Stages the deletion of a fee type.
    /// </summary>
    /// <param name="feeType">Fee type to delete.</param>
    void RemoveType(FeeType feeType);

    /// <summary>
    /// Stages the deletion of category links removed from a policy.
    /// </summary>
    /// <param name="categories">Links to delete.</param>
    void RemoveCategories(IEnumerable<FeePolicyCategory> categories);

    /// <summary>
    /// Stages an audit row.
    /// </summary>
    /// <param name="audit">Row to insert.</param>
    void AddAudit(AuditLog audit);

    /// <summary>
    /// Stages a notification row.
    /// </summary>
    /// <param name="notification">Row to insert.</param>
    void AddNotification(Notification notification);
}

public interface IPlatformFeeService
{
    /// <summary>
    /// Fee list (screen filters and table). Allowed for Finance Staff and Administrators. Filters by keyword (code or
    /// name), group, order type, category and policy state; each row shows the Active and Scheduled policies and how
    /// many policies wait for approval. Filtering and paging run in memory because the fee catalogue is small.
    /// </summary>
    /// <param name="search">Keyword, Group, OrderType, CategoryId, State, Page and PageSize.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The page; 403 FORBIDDEN for other roles; 400 when Page, PageSize (1..100) or Keyword (100 chars) is invalid.</returns>
    Task<UseCaseResult<Page<FeeTypeRow>>> SearchAsync(FeeSearch search, CancellationToken cancellationToken);
    /// <summary>
    /// Fee detail: the fee type with every policy version, its state, dates, approver and version token.
    /// </summary>
    /// <param name="id">Fee type id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The fee type; 403 FORBIDDEN; 404 NOT_FOUND.</returns>
    Task<UseCaseResult<FeeTypeDto>> GetAsync(long id, CancellationToken cancellationToken);
    /// <summary>
    /// Finance Staff creates a fee type together with its first policy. Steps: 1) role check; 2) idempotency — a
    /// RequestKey already used returns what it created; 3) validate name, description, group, refund mode and code
    /// (3–30 chars A–Z, 0–9, _); 4) inside a transaction: the code must be unique (MSG195) and the policy must pass
    /// <c>CheckPolicyAsync</c>; 5) save the type and policy, write two audit rows, notify Administrators when it is
    /// submitted, commit.
    /// </summary>
    /// <param name="request">Fee type fields, the first policy, Submit flag and RequestKey.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>MSG-FEE-01 (saved as Draft) or MSG193 (sent for approval) with the new fee; or 403, validation or FEE_OVERLAP.</returns>
    Task<UseCaseResult<FeeResultDto>> CreateAsync(CreateFeeRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// Finance Staff edits a fee type. Name and description can always change; group and refund mode are locked
    /// once the type is no longer Draft or has been applied to an order (MSG190).
    /// </summary>
    /// <param name="id">Fee type id.</param>
    /// <param name="request">New name, description, group and refund mode.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The refreshed fee type with a message, or the error to show.</returns>
    Task<UseCaseResult<FeeResultDto>> UpdateTypeAsync(long id, UpdateFeeTypeRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// Finance Staff deletes a fee type. BR-FEE-07: only a Draft, non-mandatory type with no approved policy that was
    /// never applied to an order can be deleted; otherwise FEE_APPLIED (MSG190).
    /// </summary>
    /// <param name="id">Fee type id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Confirmation without a fee, or 403 / 404 / 409.</returns>
    Task<UseCaseResult<FeeResultDto>> DeleteTypeAsync(long id, CancellationToken cancellationToken);
    /// <summary>
    /// Finance Staff adds a new policy version to an existing fee type (same checks and idempotency as create).
    /// The policy code is {FeeCode}-{ORDERTYPE}-V{n}.
    /// </summary>
    /// <param name="feeTypeId">Fee type that receives the policy.</param>
    /// <param name="request">Policy, Submit flag and RequestKey.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The refreshed fee type with a message, or the error to show.</returns>
    Task<UseCaseResult<FeeResultDto>> AddPolicyAsync(long feeTypeId, NewPolicyRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// Finance Staff edits a policy that is still Draft or Rejected (BR-FEE-07). The version token must match the
    /// one the form was opened with (MSG122). Saving clears the review note; Submit sends it for approval.
    /// </summary>
    /// <param name="policyId">Policy id.</param>
    /// <param name="request">New policy values, Submit flag and Version.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The refreshed fee type with a message, or the error to show.</returns>
    Task<UseCaseResult<FeeResultDto>> EditPolicyAsync(long policyId, EditPolicyRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// Finance Staff sends a Draft or Rejected policy for approval. The policy is validated again because dates and
    /// other policies may have changed since it was saved; Administrators are notified in-app.
    /// </summary>
    /// <param name="policyId">Policy id.</param>
    /// <param name="request">Version shown on the screen.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The refreshed fee type with a message, or the error to show.</returns>
    Task<UseCaseResult<FeeResultDto>> SubmitPolicyAsync(long policyId, PolicyActionRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// Administrator approves a pending request. The creator cannot approve their own policy (four-eyes rule).
    /// Two cases: (a) an end-date request on an approved policy — the end date is applied if the fee stays covered
    /// (BR-FEE-07 / BR-FEE-10); (b) a new policy — its start date must not be in the past, it must not overlap another
    /// approved policy, a category-specific policy needs an all-categories policy of the same order type, and the
    /// previous version of the same scope is end-dated the day before. The fee type becomes Active and the creator is
    /// notified (MSG194).
    /// </summary>
    /// <param name="policyId">Policy id.</param>
    /// <param name="request">Version and optional note.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The refreshed fee type with a message, or the error to show.</returns>
    Task<UseCaseResult<FeeResultDto>> ApprovePolicyAsync(long policyId, PolicyActionRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// Administrator rejects a pending request with a required reason (at most 500 characters). A new policy goes
    /// back to Draft; an end-date request is simply dropped. The creator is notified (MSG191).
    /// </summary>
    /// <param name="policyId">Policy id.</param>
    /// <param name="request">Version and the rejection reason in Note.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The refreshed fee type with a message, or the error to show.</returns>
    Task<UseCaseResult<FeeResultDto>> RejectPolicyAsync(long policyId, PolicyActionRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// Finance Staff asks to end an Active or Scheduled policy on a date. The date must be today or later, inside the
    /// current validity, and must not leave a mandatory or all-categories scope uncovered (MSG192). The end date is
    /// stored as PendingEffectiveTo and only takes effect after an Administrator approves it.
    /// </summary>
    /// <param name="policyId">Policy id.</param>
    /// <param name="request">EffectiveTo, Reason and Version.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The refreshed fee type with a message, or the error to show.</returns>
    Task<UseCaseResult<FeeResultDto>> EndDatePolicyAsync(long policyId, EndDatePolicyRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// Calculates the fees of a sample seller order without saving anything. Uses the policies active on the chosen
    /// date; an optional draft policy replaces the policies of the same fee type and scope so its effect can be seen.
    /// </summary>
    /// <param name="request">Order type, 1–20 lines (category, subtotal), delivery fee, date and optional draft policy.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Fees by type, total fees, net amount for the Farmer and tax withheld; 400 for an invalid sample order.</returns>
    Task<UseCaseResult<FeePreviewDto>> PreviewAsync(FeePreviewRequest request, CancellationToken cancellationToken);
    /// <summary>
    /// Categories a policy may target (Active or Inactive, never Archived — BR-FEE-11).
    /// </summary>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Category options; 403 for other roles.</returns>
    Task<UseCaseResult<IReadOnlyList<FeeCategoryOption>>> CategoriesAsync(CancellationToken cancellationToken);
}

/// <summary>Fee lines of orders: recorded at placement (BR-FEE-05) and reversed on cancel or refund (BR-FEE-08).</summary>
public interface IOrderFeeService
{
    /// <summary>
    /// BR-FEE-05: records the fee lines of every seller order when the order is placed.
    /// </summary>
    /// <param name="order">New order with seller orders, items and voucher redemptions.</param>
    /// <param name="nowUtc">Placement time (UTC).</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    Task ApplyAsync(Order order, DateTime nowUtc, CancellationToken cancellationToken);

    /// <summary>
    /// BR-FEE-08: a cancelled seller order carries no fee.
    /// </summary>
    /// <param name="sellerOrderId">Cancelled seller order.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    Task ReverseCancelledAsync(long sellerOrderId, CancellationToken cancellationToken);

    /// <summary>
    /// BR-FEE-08: reverses fees for the refunded share of a seller order.
    /// </summary>
    /// <param name="sellerOrderId">Refunded seller order.</param>
    /// <param name="refundedTotal">Total refunded so far.</param>
    /// <param name="sellerOrderAmount">Final amount of the seller order.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    Task ReverseRefundAsync(long sellerOrderId, decimal refundedTotal, decimal sellerOrderAmount,
        CancellationToken cancellationToken);
}
