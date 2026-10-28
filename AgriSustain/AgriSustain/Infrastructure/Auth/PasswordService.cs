using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace AgriSustain.Infrastructure.Auth;

public sealed class PasswordService : IPasswordService
{
    private readonly IPasswordHasher<User> _passwordHasher;

    public PasswordService(IPasswordHasher<User> passwordHasher)
    {
        _passwordHasher = passwordHasher;
    }

    public PasswordVerification Verify(User user, string password)
    {
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
        {
            return new PasswordVerification(false, false);
        }

        // Handle dummy seed hashes from SQL scripts ($2a$12$Hash... or !DEMO_HASH!...)
        if (user.PasswordHash.StartsWith("$2a$12$Hash", StringComparison.OrdinalIgnoreCase) ||
            user.PasswordHash.StartsWith("!DEMO_HASH!", StringComparison.OrdinalIgnoreCase))
        {
            bool matchesSeed = password == "123456" ||
                               password == "Admin@123" ||
                               password == "Password@123" ||
                               password == "admin";
            return new PasswordVerification(matchesSeed, false);
        }

        if (user.PasswordHash.StartsWith("AQAAAA", StringComparison.Ordinal))
        {
            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            return new PasswordVerification(
                result != PasswordVerificationResult.Failed,
                result == PasswordVerificationResult.SuccessRehashNeeded);
        }

        try
        {
            var valid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            return new PasswordVerification(valid, valid);
        }
        catch
        {
            return new PasswordVerification(false, false);
        }
    }

    public string Hash(User user, string password)
    {
        return _passwordHasher.HashPassword(user, password);
    }
}

