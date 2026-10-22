using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces.Repositories;

public interface IAuthRepository
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
    Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken);
    Task SavePendingRegistrationAsync(PendingRegistration registration, CancellationToken cancellationToken);
    Task<PendingRegistration?> FindPendingRegistrationAsync(string email, CancellationToken cancellationToken);
    Task UpdatePendingRegistrationAsync(PendingRegistration registration, CancellationToken cancellationToken);
    Task RemovePendingRegistrationAsync(string email, CancellationToken cancellationToken);
    Task<User> CreateUserFromPendingAsync(PendingRegistration registration, CancellationToken cancellationToken);
}
