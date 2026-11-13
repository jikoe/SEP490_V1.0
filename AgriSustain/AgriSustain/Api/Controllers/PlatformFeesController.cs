using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>
/// UC-FIN-05 Manage Platform Fees. Finance Staff create and change fees; an Administrator approves or
/// rejects policies. The service checks which role may do what.
/// </summary>
[Authorize(Roles = "FinanceStaff,Finance Staff,SystemAdmin,Administrator,Admin")]
[Route("api/finance/fees")]
public sealed class PlatformFeesController(IPlatformFeeService service) : UseCaseControllerBase
{
    /// <summary>
    /// GET api/finance/fees — fee list with filters and paging.
    /// </summary>
    /// <param name="search">Filters and paging from the query string.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200 with the page; 400 / 403.</returns>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] FeeSearch search, CancellationToken cancellationToken) =>
        Respond(await service.SearchAsync(search, cancellationToken));

    /// <summary>
    /// GET api/finance/fees/{id} — one fee type with all its policy versions.
    /// </summary>
    /// <param name="id">Fee type id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200 with the fee type; 403 / 404.</returns>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken) =>
        Respond(await service.GetAsync(id, cancellationToken));

    /// <summary>
    /// POST api/finance/fees — create a fee type with its first policy (Finance Staff).
    /// </summary>
    /// <param name="request">Fee type, first policy, Submit and RequestKey.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200 with the new fee; 400 / 403 / 409.</returns>
    [HttpPost]
    public async Task<IActionResult> Create(CreateFeeRequest request, CancellationToken cancellationToken) =>
        Respond(await service.CreateAsync(request, cancellationToken));

    /// <summary>
    /// PUT api/finance/fees/{id} — edit the fee type (Finance Staff).
    /// </summary>
    /// <param name="id">Fee type id.</param>
    /// <param name="request">New fee type values.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200; 400 / 403 / 404 / 409 FEE_APPLIED.</returns>
    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateFeeTypeRequest request, CancellationToken cancellationToken) =>
        Respond(await service.UpdateTypeAsync(id, request, cancellationToken));

    /// <summary>
    /// POST api/finance/fees/{id}/delete — delete a Draft fee type that was never used (Finance Staff).
    /// </summary>
    /// <param name="id">Fee type id.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200; 403 / 404 / 409 FEE_APPLIED.</returns>
    [HttpPost("{id:long}/delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken) =>
        Respond(await service.DeleteTypeAsync(id, cancellationToken));

    /// <summary>
    /// POST api/finance/fees/{id}/policies — add a policy version (Finance Staff).
    /// </summary>
    /// <param name="id">Fee type id.</param>
    /// <param name="request">Policy, Submit and RequestKey.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200; 400 / 403 / 404 / 409 FEE_OVERLAP.</returns>
    [HttpPost("{id:long}/policies")]
    public async Task<IActionResult> AddPolicy(long id, NewPolicyRequest request, CancellationToken cancellationToken) =>
        Respond(await service.AddPolicyAsync(id, request, cancellationToken));

    /// <summary>
    /// PUT api/finance/fees/policies/{policyId} — edit a Draft or Rejected policy (Finance Staff).
    /// </summary>
    /// <param name="policyId">Policy id.</param>
    /// <param name="request">Policy values, Submit and Version.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200; 400 / 403 / 404 / 409.</returns>
    [HttpPut("policies/{policyId:long}")]
    public async Task<IActionResult> EditPolicy(long policyId, EditPolicyRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.EditPolicyAsync(policyId, request, cancellationToken));

    /// <summary>
    /// POST api/finance/fees/policies/{policyId}/submit — send a policy for approval (Finance Staff).
    /// </summary>
    /// <param name="policyId">Policy id.</param>
    /// <param name="request">Version.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200; 403 / 404 / 409 FEE_CHANGED or FEE_OVERLAP.</returns>
    [HttpPost("policies/{policyId:long}/submit")]
    public async Task<IActionResult> Submit(long policyId, PolicyActionRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.SubmitPolicyAsync(policyId, request, cancellationToken));

    /// <summary>
    /// POST api/finance/fees/policies/{policyId}/approve — approve a policy or an end-date request (Administrator).
    /// </summary>
    /// <param name="policyId">Policy id.</param>
    /// <param name="request">Version and optional note.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200; 400 / 403 / 404 / 409.</returns>
    [HttpPost("policies/{policyId:long}/approve")]
    public async Task<IActionResult> Approve(long policyId, PolicyActionRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.ApprovePolicyAsync(policyId, request, cancellationToken));

    /// <summary>
    /// POST api/finance/fees/policies/{policyId}/reject — reject with a reason (Administrator).
    /// </summary>
    /// <param name="policyId">Policy id.</param>
    /// <param name="request">Version and reason.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200; 400 / 403 / 404 / 409.</returns>
    [HttpPost("policies/{policyId:long}/reject")]
    public async Task<IActionResult> Reject(long policyId, PolicyActionRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.RejectPolicyAsync(policyId, request, cancellationToken));

    /// <summary>
    /// POST api/finance/fees/policies/{policyId}/end-date — request an end date (Finance Staff).
    /// </summary>
    /// <param name="policyId">Policy id.</param>
    /// <param name="request">End date, reason and Version.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200; 400 / 403 / 404 / 409.</returns>
    [HttpPost("policies/{policyId:long}/end-date")]
    public async Task<IActionResult> EndDate(long policyId, EndDatePolicyRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.EndDatePolicyAsync(policyId, request, cancellationToken));

    /// <summary>
    /// GET api/finance/fees/categories — categories selectable for a policy.
    /// </summary>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200 with the options; 403.</returns>
    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken) =>
        Respond(await service.CategoriesAsync(cancellationToken));

    /// <summary>
    /// POST api/finance/fees/preview — fees of a sample order; nothing is saved.
    /// </summary>
    /// <param name="request">Sample order and optional draft policy.</param>
    /// <param name="cancellationToken">Cancels the database work when the HTTP request is aborted.</param>
    /// <returns>200 with the preview; 400 / 403.</returns>
    [HttpPost("preview")]
    public async Task<IActionResult> Preview(FeePreviewRequest request, CancellationToken cancellationToken) =>
        Respond(await service.PreviewAsync(request, cancellationToken));
}
