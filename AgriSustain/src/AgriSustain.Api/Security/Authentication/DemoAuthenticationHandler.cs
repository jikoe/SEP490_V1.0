using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using AgriSustain.Application.Interfaces.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace AgriSustain.Api.Security.Authentication;

// DEMO-AUTH (owner: authentication team): reads public fixture selectors such as
// "Authorization: Demo admin". This does NOT validate passwords, JWTs or refresh tokens.
// It is registered only behind the Development + explicit Demo guard and rejects remote
// socket peers. Do not deploy this scheme, proxy it publicly or describe it as real auth.
// Replacement: JWT bearer middleware + trusted claims + current account/session checks;
// suspend must revoke refresh sessions and invalidate cached permissions in that module.
/// <summary>
/// Receives ASP.NET authentication dependencies and the isolated identity reader. Registration is
/// gated to explicit Development use; replace it with the team JWT/session adapter.
/// </summary>
public sealed class DemoAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IDemoIdentityReader identities)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>
    /// Accepts only local socket peers and a valid public demo selector, then emits the verified
    /// fixture ID/role claims. It rejects revoked fixtures and does not validate passwords, JWT
    /// signatures or refresh tokens.
    /// </summary>
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        IPAddress? remote = Context.Connection.RemoteIpAddress;
        if (remote != null && !IPAddress.IsLoopback(remote))
        {
            return AuthenticateResult.Fail("Demo accepts loopback connections only.");
        }

        string header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Demo ", StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var identity = await identities.FindAsync(header[5..], Context.RequestAborted);
        if (identity == null)
        {
            return AuthenticateResult.Fail("Invalid or revoked demo fixture session.");
        }

        // UI self-action visibility uses the verified actor, never a presumed SQL IDENTITY.
        Response.Headers["X-Demo-Actor-Id"] = identity.Value.Id.ToString();
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, identity.Value.Id.ToString()),
            new(ClaimTypes.Role, identity.Value.Role)
        ];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }
}
