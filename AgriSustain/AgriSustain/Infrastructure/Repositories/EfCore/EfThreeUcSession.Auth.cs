using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

internal sealed partial class EfThreeUcSession
{
    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        return await _db.Users.AnyAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken)
    {
        return await _db.Users.AnyAsync(u => u.PhoneNumber == phone, cancellationToken);
    }

    public Task SavePendingRegistrationAsync(PendingRegistration registration, CancellationToken cancellationToken)
    {
        _dependencies.PendingRegistrations[registration.Email] = registration;
        return Task.CompletedTask;
    }

    public Task<PendingRegistration?> FindPendingRegistrationAsync(string email, CancellationToken cancellationToken)
    {
        _dependencies.PendingRegistrations.TryGetValue(email, out var registration);
        return Task.FromResult(registration);
    }

    public Task UpdatePendingRegistrationAsync(PendingRegistration registration, CancellationToken cancellationToken)
    {
        _dependencies.PendingRegistrations[registration.Email] = registration;
        return Task.CompletedTask;
    }

    public Task RemovePendingRegistrationAsync(string email, CancellationToken cancellationToken)
    {
        _dependencies.PendingRegistrations.TryRemove(email, out _);
        return Task.CompletedTask;
    }

    public async Task<User> CreateUserFromPendingAsync(PendingRegistration registration, CancellationToken cancellationToken)
    {
        var user = new User
        {
            Username = registration.Email.Split('@')[0] + "_" + Random.Shared.Next(100, 999),
            Email = registration.Email,
            PasswordHash = registration.PasswordHash,
            FullName = registration.FullName,
            PhoneNumber = registration.PhoneNumber,
            Role = registration.Role,
            Status = "Active",
            IsEmailVerified = true,
            CreatedAt = DateTime.UtcNow,
            FailedLoginCount = 0
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        if (registration.Role == "Farmer")
        {
            var farmerProfile = new FarmerProfile
            {
                UserId = user.UserId,
                FarmName = "Nông trại " + registration.FullName,
                RepresentativeName = registration.FullName,
                Address = "Chưa cập nhật địa chỉ",
                ProvinceCode = "01",
                VerificationStatus = "Pending",
                ReputationScore = 5.0m
            };
            _db.FarmerProfiles.Add(farmerProfile);
            await _db.SaveChangesAsync(cancellationToken);
        }

        return user;
    }
}
