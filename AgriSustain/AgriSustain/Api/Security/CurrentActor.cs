using System.Security.Claims;
using AgriSustain.Application.Interfaces;

namespace AgriSustain.Api.Security;

public sealed class CurrentActor(IHttpContextAccessor accessor) : ICurrentActor
{
    public long UserId => long.TryParse(accessor.HttpContext?.User
        .FindFirstValue(ClaimTypes.NameIdentifier), out long id) ? id : 0;

    public string Role => accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role) ?? "";
}

