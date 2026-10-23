namespace AgriSustain.Application.Interfaces.Identity;

// DEMO-AUTH boundary only. Replace this interface/handler with the team's JWT module.
public interface IDemoIdentityReader
{
    /// <summary>
    /// Resolves only an authorized local fixture selector to its current identity/role. Null
    /// means missing, locked, suspended or revoked; the selector is not a credential.
    /// </summary>
    Task<(long Id, string Role)?> FindAsync(string fixture, CancellationToken cancellationToken);
}
