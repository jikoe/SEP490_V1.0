using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IUserRepository
{
    Task<User?> FindByUsernameOrEmailAsync(
        string identifier,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
