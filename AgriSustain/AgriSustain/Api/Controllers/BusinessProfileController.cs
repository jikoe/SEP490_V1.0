using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Services.BusinessProfiles;
using AgriSustain.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>UC-AUTH-02 (part 2): WholesaleBuyer submits the company dossier for KYC from the Profile screen.</summary>
[ApiController]
[Authorize(Roles = "WholesaleBuyer")]
[Route("api/profile/business")]
public sealed class BusinessProfileController(
    IBusinessProfileService service, IDossierStorage dossiers) : ControllerBase
{
    private static readonly string[] s_licenseExtensions = [".pdf", ".jpg", ".jpeg"];

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        ApiResults.From(await service.GetMineAsync(cancellationToken), HttpContext);

    /// <summary>multipart/form-data: companyName, taxCode, representativeName, businessType, businessAddress, file.</summary>
    [HttpPost]
    [RequestSizeLimit(DocumentFileCheck.MaximumBytes + 65536)]
    public async Task<IActionResult> Submit([FromForm] SubmitBusinessProfileForm form,
        CancellationToken cancellationToken)
    {
        // AF3: a corrupt, oversized or non PDF/JPG file is rejected before anything is stored.
        if (form.File is not null
            && await DocumentFileCheck.ValidateAsync(form.File, s_licenseExtensions, cancellationToken) is { } fileError)
        {
            return new ObjectResult(new ProblemDetails
            {
                Status = 400, Title = fileError,
                Extensions = { ["code"] = "FILE_INVALID", ["errors"] = new Dictionary<string, string[]> { ["file"] = [fileError] } }
            }) { StatusCode = 400 };
        }

        var request = new SubmitBusinessProfileRequest(form.CompanyName ?? "", form.TaxCode ?? "",
            form.RepresentativeName ?? "", form.BusinessType, form.BusinessAddress ?? "");
        Func<long, Task<string>>? save = form.File is null ? null : async userId =>
        {
            await using var stream = form.File.OpenReadStream();
            return await dossiers.SaveAsync(stream, Path.GetExtension(form.File.FileName),
                BusinessProfileRules.DocumentFolder, userId, cancellationToken);
        };

        var result = await service.SubmitAsync(request, save, cancellationToken);
        return result.IsSuccess
            ? Ok(new { message = BusinessProfileService.SuccessMessage, profile = result.Value })
            : ApiResults.From(result, HttpContext);
    }
}

public sealed class SubmitBusinessProfileForm
{
    public string? CompanyName { get; set; }
    public string? TaxCode { get; set; }
    public string? RepresentativeName { get; set; }
    public string? BusinessType { get; set; }
    public string? BusinessAddress { get; set; }
    public IFormFile? File { get; set; }
}
