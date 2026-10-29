using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data.Demo;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class AuthRepository(AgriSustainContext db, SqlDemoDependencies demoStore) : IAuthRepository
{
    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        await db.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public async Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken) =>
        await db.Users.AnyAsync(u => u.PhoneNumber == phone, cancellationToken);

    public Task SavePendingRegistrationAsync(PendingRegistration registration, CancellationToken cancellationToken)
    {
        demoStore.PendingRegistrations[registration.Email] = registration;
        return Task.CompletedTask;
    }

    public Task<PendingRegistration?> FindPendingRegistrationAsync(string email, CancellationToken cancellationToken)
    {
        demoStore.PendingRegistrations.TryGetValue(email, out var registration);
        return Task.FromResult(registration);
    }

    public Task UpdatePendingRegistrationAsync(PendingRegistration registration, CancellationToken cancellationToken)
    {
        demoStore.PendingRegistrations[registration.Email] = registration;
        return Task.CompletedTask;
    }

    public Task RemovePendingRegistrationAsync(string email, CancellationToken cancellationToken)
    {
        demoStore.PendingRegistrations.TryRemove(email, out _);
        return Task.CompletedTask;
    }

    public async Task<User> CreateUserFromPendingAsync(PendingRegistration registration, CancellationToken cancellationToken)
    {
        var user = new User
        {
            Username = await UniqueUsernameAsync(registration.Email, cancellationToken),
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

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        if (registration.Role == "Farmer")
        {
            // Farm details are completed later on the profile screen (UC-AUTH-03) before KYC submission.
            db.FarmerProfiles.Add(new FarmerProfile
            {
                UserId = user.UserId,
                FarmName = "",
                RepresentativeName = registration.FullName,
                Address = "",
                ProvinceCode = "",
                VerificationStatus = VerificationStatuses.NotSubmitted,
                ReputationScore = 5.00m
            });
        }

        db.AuditLogs.Add(new AuditLog
        {
            PerformedByUserId = user.UserId,
            EntityType = "User",
            EntityId = user.UserId,
            Action = "Register",
            NewValue = JsonSerializer.Serialize(new
            {
                user.Email, user.PhoneNumber, user.Role, user.Status, user.IsEmailVerified
            }),
            OccurredAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        return user;
    }

    private async Task<string> UniqueUsernameAsync(string email, CancellationToken cancellationToken)
    {
        string baseName = email.Split('@')[0];
        if (baseName.Length > 80) baseName = baseName[..80];
        string candidate = baseName;
        while (await db.Users.AnyAsync(u => u.Username == candidate, cancellationToken))
        {
            candidate = $"{baseName}_{Random.Shared.Next(1000, 99999)}";
        }

        return candidate;
    }
}
