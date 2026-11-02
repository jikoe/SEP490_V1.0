using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
[ServiceFilter(typeof(FarmInputFilter), Order = int.MinValue)]
[Authorize(Roles = "Farmer")]
[Route("api/farmer/evidence")]
public sealed class FarmerEvidenceController(
    IFarmerEvidenceStorage storage,
    IFarmerCropSeasonRepository repo,
    ICurrentActor actor) : ControllerBase
{
    private async Task<bool> Allowed(CancellationToken ct)
    {
        var f = await repo.GetFarmerAsync(actor.UserId, false, ct);
        return f is not null && f.User.Status == "Active" && f.VerificationStatus == "Approved";
    }


    [HttpPost]
    [RequestSizeLimit(105000000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 105000000)]
    public async Task<IActionResult> Upload(
        [FromForm] List<IFormFile> files,
        [FromQuery] string feature = "log",
        CancellationToken ct = default)
    {
        if (!await Allowed(ct))
            return Problem(FarmManagementMessages.MSG121, statusCode: 403);
        try
        {
            return Ok(new { evidenceUrls = await storage.SaveImagesAsync(actor.UserId, files, feature, ct) });
        }
        catch (FarmRuleException e)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]> { { "evidenceUrls", [e.Message] } }) { Title = e.Message, Status = 400 });
        }
    }


    [HttpGet("{key}")]
    public async Task<IActionResult> Evidence(string key, CancellationToken ct)
    {
        if (!await Allowed(ct))
            return Problem(FarmManagementMessages.MSG121, statusCode: 403);
        var path = storage.ResolvePath(actor.UserId, "/api/farmer/evidence/" + key);
        if (path is null)
            return NotFound(new { message = FarmManagementMessages.MSG126 });
        Response.Headers.CacheControl = "private, no-store";
        return PhysicalFile(path, Path.GetExtension(path) == ".png" ? "image/png" : "image/jpeg");
    }
}
