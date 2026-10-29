using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>UC-AUTH-03 Manage Profile. Users only read and change their own profile.</summary>
[ApiController]
[Authorize]
[Route("api/profile")]
public sealed class ProfileController(IProfileService service, IAddressCatalog addresses) : ControllerBase
{
    private const string BackOfficeRoles = "SystemAdmin,Administrator,Admin,VerificationOfficer";

    [HttpGet]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetCurrentProfileAsync(cancellationToken), HttpContext);

    /// <summary>Legal / contact data of another user is visible to back-office roles only (GBR-08).</summary>
    [HttpGet("{userId:long}")]
    [HttpGet("farmer/{userId:long}")]
    [Authorize(Roles = BackOfficeRoles)]
    public async Task<IActionResult> GetProfileById(
        long userId, CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetProfileByIdAsync(userId, cancellationToken), HttpContext);

    [HttpPut("consumer")]
    public async Task<IActionResult> UpdateConsumerProfile(
        [FromBody] UpdateConsumerProfileRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.UpdateConsumerProfileAsync(request, cancellationToken), HttpContext);

    [HttpPut("farmer")]
    [Authorize(Roles = "Farmer")]
    public async Task<IActionResult> UpdateFarmerProfile(
        [FromBody] UpdateFarmerProfileRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.UpdateFarmerProfileAsync(request, cancellationToken), HttpContext);

    [HttpPost("farmer/submit-kyc")]
    [Authorize(Roles = "Farmer")]
    public async Task<IActionResult> SubmitFarmerKyc(
        [FromBody] SubmitKycRequest? request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.SubmitFarmerKycAsync(
            request ?? new SubmitKycRequest(null), cancellationToken), HttpContext);

    // Address catalog for the default delivery address (same source as checkout).
    [HttpGet("address/provinces")]
    public async Task<IActionResult> Provinces(CancellationToken cancellationToken) =>
        Ok(await addresses.GetProvincesAsync(cancellationToken));

    [HttpGet("address/provinces/{code}/districts")]
    public async Task<IActionResult> Districts(string code, CancellationToken cancellationToken) =>
        Ok(await addresses.GetDistrictsAsync(code, cancellationToken));

    [HttpGet("address/districts/{code}/wards")]
    public async Task<IActionResult> Wards(string code, CancellationToken cancellationToken) =>
        Ok(await addresses.GetWardsAsync(code, cancellationToken));
}
