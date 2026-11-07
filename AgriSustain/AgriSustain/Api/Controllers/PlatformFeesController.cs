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
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] FeeSearch search, CancellationToken cancellationToken) =>
        Respond(await service.SearchAsync(search, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken) =>
        Respond(await service.GetAsync(id, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(CreateFeeRequest request, CancellationToken cancellationToken) =>
        Respond(await service.CreateAsync(request, cancellationToken));

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Update(long id, UpdateFeeTypeRequest request, CancellationToken cancellationToken) =>
        Respond(await service.UpdateTypeAsync(id, request, cancellationToken));

    [HttpPost("{id:long}/delete")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken) =>
        Respond(await service.DeleteTypeAsync(id, cancellationToken));

    [HttpPost("{id:long}/policies")]
    public async Task<IActionResult> AddPolicy(long id, NewPolicyRequest request, CancellationToken cancellationToken) =>
        Respond(await service.AddPolicyAsync(id, request, cancellationToken));

    [HttpPut("policies/{policyId:long}")]
    public async Task<IActionResult> EditPolicy(long policyId, EditPolicyRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.EditPolicyAsync(policyId, request, cancellationToken));

    [HttpPost("policies/{policyId:long}/submit")]
    public async Task<IActionResult> Submit(long policyId, PolicyActionRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.SubmitPolicyAsync(policyId, request, cancellationToken));

    [HttpPost("policies/{policyId:long}/approve")]
    public async Task<IActionResult> Approve(long policyId, PolicyActionRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.ApprovePolicyAsync(policyId, request, cancellationToken));

    [HttpPost("policies/{policyId:long}/reject")]
    public async Task<IActionResult> Reject(long policyId, PolicyActionRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.RejectPolicyAsync(policyId, request, cancellationToken));

    [HttpPost("policies/{policyId:long}/end-date")]
    public async Task<IActionResult> EndDate(long policyId, EndDatePolicyRequest request,
        CancellationToken cancellationToken) =>
        Respond(await service.EndDatePolicyAsync(policyId, request, cancellationToken));

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken cancellationToken) =>
        Respond(await service.CategoriesAsync(cancellationToken));

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(FeePreviewRequest request, CancellationToken cancellationToken) =>
        Respond(await service.PreviewAsync(request, cancellationToken));
}
