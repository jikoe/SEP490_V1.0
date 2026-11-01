using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Services.Certificates;
using AgriSustain.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>UC-FAR-07 Submit Certificate Dossier (Farmer with an Approved profile).</summary>
[ApiController]
[Authorize(Roles = "Farmer")]
[Route("api/farmer/certificates")]
public sealed class FarmCertificatesController(
    IFarmCertificateService service, IDossierStorage dossiers) : ControllerBase
{
    private static readonly string[] s_extensions = [".pdf", ".jpg", ".jpeg", ".png"];

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        ApiResults.From(await service.ListMineAsync(cancellationToken), HttpContext);

    /// <summary>multipart/form-data: certificateType, certificateNumber, issuedBy, issueDate, expiryDate, file.</summary>
    [HttpPost]
    [RequestSizeLimit(DocumentFileCheck.MaximumBytes + 65536)]
    public async Task<IActionResult> Submit(
        [FromForm] SubmitCertificateForm form, CancellationToken cancellationToken)
    {
        // AF3: an unsupported or oversized file is rejected before anything is stored.
        if (await DocumentFileCheck.ValidateAsync(form.File, s_extensions, cancellationToken) is { } fileError)
        {
            return new ObjectResult(new ProblemDetails
            {
                Status = 400, Title = fileError,
                Extensions = { ["code"] = "FILE_INVALID", ["errors"] = new Dictionary<string, string[]> { ["file"] = [fileError] } }
            }) { StatusCode = 400 };
        }

        var request = new SubmitCertificateRequest(form.CertificateType ?? "", form.CertificateNumber ?? "",
            form.IssuedBy ?? "", form.IssueDate, form.ExpiryDate);
        var result = await service.SubmitAsync(request, async userId =>
        {
            await using var stream = form.File!.OpenReadStream();
            return await dossiers.SaveAsync(stream, Path.GetExtension(form.File.FileName),
                FarmCertificateRules.DocumentFolder, userId, cancellationToken);
        }, cancellationToken);

        return result.IsSuccess
            ? Ok(new { message = FarmCertificateService.SuccessMessage, certificate = result.Value })
            : ApiResults.From(result, HttpContext);
    }
}

public sealed class SubmitCertificateForm
{
    public string? CertificateType { get; set; }
    public string? CertificateNumber { get; set; }
    public string? IssuedBy { get; set; }
    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public IFormFile? File { get; set; }
}
