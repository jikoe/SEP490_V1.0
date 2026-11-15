using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.Refunds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AgriSustain.Api.Controllers;

[Authorize(Roles = "FinanceStaff,Finance Staff")]
[Route("api/finance/rejected-order-refunds")]
[ServiceFilter(typeof(RejectedOrderRefundInputFilter), Order = int.MinValue)]
public sealed class FinanceRejectedOrderRefundController(FinanceRejectedOrderRefundService service, ICurrentActor actor)
    : FeatureControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] RejectedOrderRefundQuery query, CancellationToken ct) =>
        Respond(await service.SearchAsync(actor.UserId, query, ct));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken ct) =>
        Respond(await service.DetailAsync(actor.UserId, id, ct));

    [HttpGet("{id:long}/qr-preview")]
    public async Task<IActionResult> Preview(long id, CancellationToken ct) =>
        Respond(await service.PreviewAsync(actor.UserId, id, ct));

    [HttpPost("{id:long}/open-qr")]
    public async Task<IActionResult> Open(long id, OpenRejectedOrderRefundRequest request, CancellationToken ct) =>
        Respond(await service.OpenQrAsync(actor.UserId, id, request, ct));

    [HttpPost("{id:long}/reconcile")]
    public async Task<IActionResult> Reconcile(long id, CancellationToken ct) =>
        Respond(await service.ReconcileAsync(actor.UserId, id, ct));
}

public sealed class RejectedOrderRefundInputFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.ModelState.IsValid)
            context.Result = new BadRequestObjectResult(new ProblemDetails
            {
                Status = 400, Title = RejectedOrderRefundMessages.Invalid
            });
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
