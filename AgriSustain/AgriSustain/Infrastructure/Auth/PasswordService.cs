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
        if (!user.PasswordHash.StartsWith("AQAAAA", StringComparison.Ordinal))
        {
            var valid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
            return new PasswordVerification(valid, valid);
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return new PasswordVerification(
            result != PasswordVerificationResult.Failed,
            result == PasswordVerificationResult.SuccessRehashNeeded);
    }

    public string Hash(User user, string password)
    {
        return _passwordHasher.HashPassword(user, password);
    }
}
