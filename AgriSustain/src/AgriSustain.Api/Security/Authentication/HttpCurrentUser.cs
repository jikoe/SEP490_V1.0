using System.Security.Claims;
using AgriSustain.Application.Interfaces.Identity;

namespace AgriSustain.Api.Security.Authentication;

/// <summary>
/// Reads actor identity only from the authenticated HTTP principal through the request accessor.
/// It does not trust request JSON or perform login; absent identity remains unauthenticated.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor contextAccessor) : ICurrentUser
{
    public long? UserId => long.TryParse(contextAccessor.HttpContext?.User.FindFirstValue(
        ClaimTypes.NameIdentifier), out long id) ? id : null;
}
