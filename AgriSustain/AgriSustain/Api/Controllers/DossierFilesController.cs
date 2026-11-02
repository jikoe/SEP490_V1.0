using System.Security.Claims;
using AgriSustain.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

/// <summary>
/// Private legal documents (Business License of UC-AUTH-02, certificate dossiers of UC-FAR-07).
/// Only the owner and back-office reviewers may open them (GBR-08).
/// </summary>
[ApiController]
[Authorize]
[Route("api/dossiers")]
public sealed class DossierFilesController(DossierStorage storage) : ControllerBase
{
    private static readonly string[] s_reviewerRoles =
        ["VerificationOfficer", "ContentModerator", "Content Moderator", "SystemAdmin", "Administrator", "Admin"];

    [HttpGet("{folder}/{ownerUserId:long}/{key}")]
    public IActionResult Get(string folder, long ownerUserId, string key)
    {
        bool owner = long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out long userId)
            && userId == ownerUserId;
        if (!owner && !s_reviewerRoles.Any(User.IsInRole))
        {
            return Problem(title: "Bạn không có quyền xem tài liệu này.", statusCode: 403);
        }

        string? path = storage.ResolvePath(folder, ownerUserId, key);
        return path is null
            ? Problem(title: "Không tìm thấy tài liệu.", statusCode: 404)
            : PhysicalFile(path, DossierStorage.ContentType(key));
    }
}
