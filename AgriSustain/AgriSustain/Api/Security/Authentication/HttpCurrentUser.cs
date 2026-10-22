using System.Security.Claims;
using AgriSustain.Application.Interfaces.Identity;

namespace AgriSustain.Api.Security.Authentication;

public sealed class HttpCurrentUser(IHttpContextAccessor contextAccessor) : ICurrentUser
{
    public long? UserId
    {
        get
        {
            var context = contextAccessor.HttpContext;
            if (context == null) return null;

            if (long.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out long id))
            {
                return id;
            }

            // Fallback: Check custom header or query for easy testing
            if (context.Request.Headers.TryGetValue("X-User-Id", out var headerVal) &&
                long.TryParse(headerVal, out long headerId))
            {
                return headerId;
            }

            return null;
        }
    }
}
