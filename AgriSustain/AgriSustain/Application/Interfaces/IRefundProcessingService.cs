using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Refunds;

namespace AgriSustain.Application.Interfaces;

/// <summary>UC-FIN-03 Process Refund for Finance Staff.</summary>
public interface IRefundProcessingService
{
    /// <summary>
    /// Lists refund requests for the Refund Processing screen.
    /// </summary>
    /// <param name="query">Filters and paging.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The page and total, or INVALID_INPUT for an inverted date range.</returns>
    Task<Result<RefundListDto>> SearchAsync(
        RefundQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads one refund request with its history.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>The detail, or REFUND_NOT_FOUND.</returns>
    Task<Result<RefundDetailDto>> GetDetailAsync(
        long refundId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Start Transfer from Pending, or Transfer Again from Failed.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="request">Status the screen expects.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Status Processing, or the conflict that blocks the transfer.</returns>
    Task<Result<RefundActionResponse>> StartTransferAsync(
        long refundId,
        RefundTransferRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// UC-FIN-03 AF (Step 3): reminds the customer in-app and by email (MSG-RF-07) to provide or
    /// check the receiving account (UC-FIN-08); at most once every 24 hours per request.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Confirmation, or why no reminder was sent.</returns>
    Task<Result<RefundActionResponse>> RemindAccountAsync(
        long refundId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Cancels a started transfer before any money was sent (Processing → Pending).
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="request">Status the screen expects.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Status Pending, or REFUND_NOT_PENDING.</returns>
    Task<Result<RefundActionResponse>> CancelTransferAsync(
        long refundId,
        RefundTransferRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a successful transfer and applies all its consequences (BR-RF-03, BR-RF-07, BR-FEE-08).
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="request">Method, bank transaction ID and note.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Status Completed, or the validation / conflict error.</returns>
    Task<Result<RefundActionResponse>> CompleteAsync(
        long refundId,
        CompleteRefundRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Records a failed transfer and notifies the customer (MSG-RF-06 / MSG-RF-08).
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="request">Failure reason.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Status Failed, or the validation / conflict error.</returns>
    Task<Result<RefundActionResponse>> RecordTransferFailureAsync(
        long refundId,
        RefundTransferFailedRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Parks a Processing transfer whose bank result is unknown.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="request">Reconciliation note.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>Status NeedsReconciliation, or the validation / conflict error.</returns>
    Task<Result<RefundActionResponse>> MarkNeedsReconciliationAsync(
        long refundId,
        RefundReconciliationRequest request,
        CancellationToken cancellationToken);
}
