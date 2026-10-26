using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController(IProfileService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetCurrentProfileAsync(cancellationToken), HttpContext);

    [HttpGet("{userId:long}")]
    public async Task<IActionResult> GetProfileById(
        long userId, CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetProfileByIdAsync(userId, cancellationToken), HttpContext);

    [HttpGet("farmer/{userId:long}")]
    [HttpGet("famer/{userId:long}")]
    public async Task<IActionResult> GetFarmerProfile(
        long userId, CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetProfileByIdAsync(userId, cancellationToken), HttpContext);

    [HttpPut("consumer")]
    public async Task<IActionResult> UpdateConsumerProfile(
        [FromBody] UpdateConsumerProfileRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.UpdateConsumerProfileAsync(request, cancellationToken), HttpContext);

    [HttpPut("farmer")]
    public async Task<IActionResult> UpdateFarmerProfile(
        [FromBody] UpdateFarmerProfileRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.UpdateFarmerProfileAsync(request, cancellationToken), HttpContext);

    [HttpPost("farmer/submit-kyc")]
    public async Task<IActionResult> SubmitFarmerKyc(
        [FromBody] SubmitKycRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.SubmitFarmerKycAsync(request, cancellationToken), HttpContext);
}
