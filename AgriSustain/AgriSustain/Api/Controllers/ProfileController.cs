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
public sealed class ProfileController(
    IProfileService service, IAddressCatalog addresses, IFileStorageService storage) : ControllerBase
{
    private const int MaximumAvatarBytes = 5 * 1024 * 1024;

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

    /// <summary>Upload avatar / farm banner: PNG or JPG, max 5 MB (FR 6.2.1 (1), FR 3.2 (1)).</summary>
    [HttpPost("avatar")]
    [RequestSizeLimit(MaximumAvatarBytes + 65536)]
    public async Task<IActionResult> UploadAvatar(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length is <= 0 or > MaximumAvatarBytes)
        {
            return Problem("Vui lòng chọn ảnh PNG hoặc JPG có dung lượng tối đa 5 MB.", statusCode: 400);
        }

        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg"))
        {
            return Problem("Chỉ hỗ trợ ảnh định dạng .png, .jpg, .jpeg.", statusCode: 400);
        }

        // Check the real file signature, not only the extension.
        await using var stream = file.OpenReadStream();
        var header = new byte[8];
        int read = await stream.ReadAsync(header, cancellationToken);
        bool png = read == 8 && header.AsSpan().SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        bool jpeg = read >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255;
        if (!(extension == ".png" ? png : jpeg))
        {
            return Problem("Nội dung tệp không đúng định dạng ảnh.", statusCode: 400);
        }

        stream.Position = 0;
        string url = await storage.SaveImageAsync(stream, extension, cancellationToken);
        return ApiResults.From(await service.UpdateAvatarAsync(url, cancellationToken), HttpContext);
    }

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
