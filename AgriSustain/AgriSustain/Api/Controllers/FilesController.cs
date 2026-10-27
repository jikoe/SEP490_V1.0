using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Common;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Authorize(Roles = "Farmer")]
[Route("api/files")]
public sealed class FilesController(IFileStorageService storage, ICurrentActor actor,
    IWebHostEnvironment environment, AgriSustainContext context) : ControllerBase
{
    private const int MaximumBytes = 5 * 1024 * 1024;

    [HttpPost("images")]
    [RequestSizeLimit(MaximumBytes + 65536)]
    public Task<IActionResult> UploadImage(IFormFile file, CancellationToken cancellationToken) =>
        Upload(file, false, cancellationToken);

    [HttpPost("evidence")]
    [RequestSizeLimit(MaximumBytes + 65536)]
    public Task<IActionResult> UploadEvidence(IFormFile file,
        CancellationToken cancellationToken) => Upload(file, true, cancellationToken);

    private async Task<IActionResult> Upload(IFormFile? file, bool evidence,
        CancellationToken cancellationToken)
    {
        if (!await context.FarmerProfiles.AnyAsync(x => x.UserId == actor.UserId
            && x.VerificationStatus == "Approved", cancellationToken))
        {
            return evidence ? Problem("Bạn chưa có quyền tải bằng chứng. Hồ sơ nông trại phải được duyệt trước khi thực hiện thao tác.", statusCode: 403)
                : Problem(ProductListingMessages.MSG75, statusCode: 403);
        }

        if (file is null || file.Length is <= 0 or > MaximumBytes)
        {
            return Problem(evidence ? "Chọn tệp tối đa 5 MB." : ProductListingMessages.MSG78, statusCode: 400);
        }

        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png")
            && !(evidence && extension == ".pdf"))
        {
            return Problem(evidence ? "Định dạng tệp không được hỗ trợ." : ProductListingMessages.MSG78, statusCode: 400);
        }

        await using var stream = file.OpenReadStream();
        var header = new byte[24];
        int read = await stream.ReadAsync(header, cancellationToken);
        bool png = read == 24 && header.AsSpan(0, 8).SequenceEqual(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        bool jpeg = read >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255;
        bool pdf = read >= 5 && System.Text.Encoding.ASCII.GetString(header, 0, 5) == "%PDF-";
        if (!(extension == ".png" ? png : extension == ".pdf" ? pdf : jpeg))
        {
            return Problem(evidence ? "Nội dung tệp không đúng định dạng." : ProductListingMessages.MSG78, statusCode: 400);
        }

        stream.Position = 0;
        if (!evidence)
        {
            return Ok(new { imageUrl = await storage.SaveImageAsync(
                stream, extension, cancellationToken) });
        }

        string directory = Path.Combine(environment.ContentRootPath,
            "private-files", actor.UserId.ToString());
        Directory.CreateDirectory(directory);
        string key = Guid.NewGuid().ToString("N") + extension;
        await using var output = new FileStream(Path.Combine(directory, key), FileMode.CreateNew);
        await stream.CopyToAsync(output, cancellationToken);
        return Ok(new { fileUrl = $"/api/files/evidence/{actor.UserId}/{key}" });
    }

    [HttpGet("evidence/{owner:long}/{key}")]
    public IActionResult Download(long owner, string key)
    {
        if (owner != actor.UserId || key != Path.GetFileName(key)
            || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(key), "N", out _)
            || Path.GetExtension(key) is not (".jpg" or ".jpeg" or ".png" or ".pdf"))
        {
            return NotFound();
        }

        string file = Path.Combine(environment.ContentRootPath, "private-files",
            owner.ToString(), key);
        if (!System.IO.File.Exists(file))
        {
            return NotFound();
        }

        return PhysicalFile(file, "application/octet-stream", key);
    }
}

