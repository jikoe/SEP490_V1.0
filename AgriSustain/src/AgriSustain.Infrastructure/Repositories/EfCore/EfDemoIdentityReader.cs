using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Infrastructure.Data.Contexts;
using AgriSustain.Infrastructure.Data.Demo;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

/// <summary>
/// Resolves explicitly permitted local fixture selectors using real SQL account state through EF.
/// MOCK-AUTH: this is not login/JWT authentication. Replace with the authentication module's
/// verified principal and durable session revocation; keep Development/loopback restrictions.
/// </summary>
public sealed class EfDemoIdentityReader(AgriSustainDbContext db,
    SqlDemoDependencies mocks, TimeProvider clock) : IDemoIdentityReader
{
    /// <summary>Reads only ID/role for a known active, unlocked seed identity; never reads its hash.</summary>
    public async Task<(long Id, string Role)?> FindAsync(string fixture,
        CancellationToken cancellationToken)
    {
        string? username = fixture switch
        {
            "admin" => "demo3uc_admin",
            "support" => "demo3uc_support",
            "retail-customer" => "demo3uc_retail",
            "other-retail-customer" => "demo3uc_retail_other",
            _ => null
        };
        if (username is null)
        {
            return null;
        }
        var now = clock.GetUtcNow().UtcDateTime;
        var user = await db.User.AsNoTracking()
            .Where(row => row.Username == username && row.Status == "Active"
                && (row.LockedUntil == null || row.LockedUntil <= now))
            .Select(row => new { row.UserId, row.Role }).SingleOrDefaultAsync(cancellationToken);
        return user is not null && mocks.SessionVersions.GetValueOrDefault(user.UserId) == 0
            ? (user.UserId, user.Role) : null;
    }
}
