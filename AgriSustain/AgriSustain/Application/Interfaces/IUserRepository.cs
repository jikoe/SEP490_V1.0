using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> FindByUsernameOrEmailAsync(
        string identifier,
        CancellationToken cancellationToken);

    Task<User?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken);

    void AddAudit(AuditLog audit);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
