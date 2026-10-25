using System.Security.Claims;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Interfaces.Identity;

namespace AgriSustain.Api.Security;

/// <summary>
/// The caller identity for every Application contract. Identity comes only from the validated
/// JWT; request headers are client-controlled.
/// </summary>
public sealed class CurrentActor(IHttpContextAccessor accessor)
    : ICurrentActor, IActorContext, ICurrentUser
{
    public long UserId => AuthenticatedUserId ?? 0;

    public string Role => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role) ?? "";

    public string? TraceId => accessor.HttpContext?.TraceIdentifier;

    long? ICurrentUser.UserId => AuthenticatedUserId;

    private long? AuthenticatedUserId => long.TryParse(
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),
        out long id) ? id : null;
}
