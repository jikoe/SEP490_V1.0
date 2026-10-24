using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace AgriSustain.Api.Security;

public sealed class ActiveAccountHandler(IAdministrationRepository repository,
    IActorContext actor, TimeProvider clock) : AuthorizationHandler<ActiveAccountRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context,
        ActiveAccountRequirement requirement)
    {
        var cancellationToken = context.Resource is HttpContext httpContext
            ? httpContext.RequestAborted : CancellationToken.None;
        var user = await repository.FindActorAsync(actor.UserId, cancellationToken);
        if (user is not null && user.Status == "Active"
            && !user.IsTemporarilyLocked(clock.GetUtcNow()))
        {
            context.Succeed(requirement);
        }
    }
}
