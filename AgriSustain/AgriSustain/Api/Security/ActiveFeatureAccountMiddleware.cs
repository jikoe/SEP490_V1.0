using System.Security.Claims;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Api.Security;

public sealed class ActiveFeatureAccountMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AgriSustainContext database)
    {
        bool feature = context.Request.Path.StartsWithSegments("/api/farmer")
            || context.Request.Path.StartsWithSegments("/api/finance")
            || context.Request.Path.StartsWithSegments("/api/cart")
            || context.Request.Path.StartsWithSegments("/api/files")
            || context.Request.Path.StartsWithSegments("/api/profile")
            || context.Request.Path.StartsWithSegments("/api/orders")
            || context.Request.Path.StartsWithSegments("/api/moderation")
            || context.Request.Path.StartsWithSegments("/api/auth/change-password");
        if (feature && context.User.Identity?.IsAuthenticated == true)
        {
            string? identifier = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            string? role = context.User.FindFirstValue(ClaimTypes.Role);
            if (!long.TryParse(identifier, out long id)
                || !await database.Users.AnyAsync(user => user.UserId == id
                    && user.Status == "Active" && user.IsEmailVerified && user.Role == role,
                    context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new
                {
                    title = "Tài khoản không còn đủ điều kiện truy cập.", code = "UNAUTHORIZED"
                }, context.RequestAborted);
                return;
            }
        }

        await next(context);
    }
}

