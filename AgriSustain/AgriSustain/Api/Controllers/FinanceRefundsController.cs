using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>UC-FIN-03 Process Refund.</summary>
[ApiController]
[Authorize(Roles = "FinanceStaff,Finance Staff")]
[Route("api/finance/refunds")]
public sealed class FinanceRefundsController : ControllerBase
{
    private readonly IRefundProcessingService _refundService;

    /// <summary>
    /// Receives the refund service from dependency injection.
    /// </summary>
    /// <param name="refundService">UC-FIN-03 application service that holds all business rules.</param>
    public FinanceRefundsController(IRefundProcessingService refundService)
    {
        _refundService = refundService;
    }

    /// <summary>
    /// GET api/finance/refunds — screen fields (1)–(5).
    /// One page of refund requests filtered by keyword, status, method and creation date range.
    /// </summary>
    /// <param name="query">Search, Status, RefundMethod, FromDate, ToDate, Page and PageSize from the query string.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200 with <see cref="RefundListDto"/>, or 400 INVALID_INPUT when FromDate is after ToDate.</returns>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] RefundQuery query,
        CancellationToken cancellationToken) =>
        ApiResults.From(await _refundService.SearchAsync(query, cancellationToken), HttpContext);

    /// <summary>
    /// GET api/finance/refunds/{refundId} — screen fields (6)–(17).
    /// The refund with its source, seller order, customer, receiving account, balances and transfer history.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200 with <see cref="RefundDetailDto"/>, or 404 REFUND_NOT_FOUND.</returns>
    [HttpGet("{refundId:long}")]
    public async Task<IActionResult> Detail(
        long refundId,
        CancellationToken cancellationToken) =>
        ApiResults.From(await _refundService.GetDetailAsync(refundId, cancellationToken), HttpContext);

    /// <summary>
    /// POST api/finance/refunds/{refundId}/start-transfer — button (16) Start Transfer / Transfer Again.
    /// Only marks the request Processing and opens the VietQR view; the money is sent by Finance outside the platform.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="request">ExpectedStatus: the status the screen showed, used to detect a stale page.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>
    /// 200 with status Processing; 404 when missing; 409 REFUND_NOT_PENDING (MSG-RF-04), REFUND_ACCOUNT_MISSING,
    /// REFUND_ACCOUNT_NOT_RECHECKED or REFUND_BALANCE_EXCEEDED.
    /// </returns>
    [HttpPost("{refundId:long}/start-transfer")]
    public async Task<IActionResult> StartTransfer(
        long refundId,
        [FromBody] RefundTransferRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _refundService.StartTransferAsync(refundId, request, cancellationToken),
            HttpContext);

    /// <summary>
    /// POST api/finance/refunds/{refundId}/remind-account — button "Nhắc khách hàng".
    /// UC-FIN-03 AF (Step 3): remind the customer to provide or check the account (UC-FIN-08).
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>
    /// 200 when the reminder was queued; 404 when missing; 409 REFUND_ACCOUNT_NOT_NEEDED,
    /// REFUND_REMINDER_TOO_SOON (once per 24 hours) or REFUND_CUSTOMER_UNKNOWN.
    /// </returns>
    [HttpPost("{refundId:long}/remind-account")]
    public async Task<IActionResult> RemindAccount(
        long refundId,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _refundService.RemindAccountAsync(refundId, cancellationToken),
            HttpContext);

    /// <summary>
    /// POST api/finance/refunds/{refundId}/cancel-transfer — button (23) Cancel Transfer.
    /// Returns a Processing request to Pending when no money was sent.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="request">ExpectedStatus shown on the screen.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200 with status Pending; 404 when missing; 409 REFUND_NOT_PENDING when it is no longer Processing.</returns>
    [HttpPost("{refundId:long}/cancel-transfer")]
    public async Task<IActionResult> CancelTransfer(
        long refundId,
        [FromBody] RefundTransferRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _refundService.CancelTransferAsync(refundId, request, cancellationToken),
            HttpContext);

    /// <summary>
    /// POST api/finance/refunds/{refundId}/complete — button (20) Record Success.
    /// Records the bank transaction ID, closes the complaint, updates the seller order and notifies the parties.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="request">RefundMethod, TransferReference (bank transaction ID) and optional Note.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>
    /// 200 with status Completed and MSG-RF-01; 400 for invalid input (MSG-RF-02); 404 when missing;
    /// 409 REFUND_NOT_PENDING or TRANSFER_REFERENCE_USED (MSG-RF-03).
    /// </returns>
    [HttpPost("{refundId:long}/complete")]
    public async Task<IActionResult> Complete(
        long refundId,
        [FromBody] CompleteRefundRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _refundService.CompleteAsync(refundId, request, cancellationToken),
            HttpContext);

    /// <summary>
    /// POST api/finance/refunds/{refundId}/transfer-failed — button (21) Record Failure.
    /// Marks the transfer Failed with a reason and asks the customer to check the receiving account.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="request">Required failure reason, at most 500 characters.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200 with status Failed; 400 for a missing or too long reason; 404; 409 REFUND_NOT_PENDING.</returns>
    [HttpPost("{refundId:long}/transfer-failed")]
    public async Task<IActionResult> TransferFailed(
        long refundId,
        [FromBody] RefundTransferFailedRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _refundService.RecordTransferFailureAsync(refundId, request, cancellationToken),
            HttpContext);

    /// <summary>
    /// POST api/finance/refunds/{refundId}/needs-reconciliation — button (22) Needs Reconciliation.
    /// Used when the bank result is unknown: the money stays held until Finance checks the statement.
    /// </summary>
    /// <param name="refundId">Refund request (RefundTransaction) id.</param>
    /// <param name="request">Required reconciliation note, at most 500 characters.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200 with status NeedsReconciliation; 400 for an invalid note; 404; 409 REFUND_NOT_PENDING.</returns>
    [HttpPost("{refundId:long}/needs-reconciliation")]
    public async Task<IActionResult> NeedsReconciliation(
        long refundId,
        [FromBody] RefundReconciliationRequest request,
        CancellationToken cancellationToken) =>
        ApiResults.From(
            await _refundService.MarkNeedsReconciliationAsync(refundId, request, cancellationToken),
            HttpContext);
}
