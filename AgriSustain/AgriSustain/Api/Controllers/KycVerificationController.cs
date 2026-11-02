using AgriSustain.Application.DTOs.Verification;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[Authorize(Roles = "VerificationOfficer,Verification Officer,SystemAdmin,Administrator,Admin")]
[Route("api/kyc-applications")]
public sealed class KycVerificationController(IKycVerificationService service) : UseCaseControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] KycApplicationSearch search,
        CancellationToken cancellationToken)
    {
        return Respond(await service.SearchAsync(search, cancellationToken));
    }

    [HttpGet("{userId:long}")]
    public async Task<IActionResult> Detail(
        long userId,
        CancellationToken cancellationToken)
    {
        return Respond(await service.GetDetailAsync(userId, cancellationToken));
    }

    [HttpPost("{userId:long}/review")]
    public async Task<IActionResult> Review(
        long userId,
        [FromBody] KycReviewDecisionRequest request,
        CancellationToken cancellationToken)
    {
        return Respond(await service.ReviewAsync(userId, request, cancellationToken));
    }

    [HttpPost("{userId:long}/{actionName:regex(^(approve|reject|request-info)$)}")]
    public async Task<IActionResult> ReviewByActionRoute(
        long userId,
        string actionName,
        [FromBody] KycReviewDecisionRequest? request,
        CancellationToken cancellationToken)
    {
        var merged = new KycReviewDecisionRequest(actionName, request?.Reason);
        return Respond(await service.ReviewAsync(userId, merged, cancellationToken));
    }
}
