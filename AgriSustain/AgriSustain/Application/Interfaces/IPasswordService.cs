using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IPasswordService
{
    PasswordVerification Verify(User user, string password);

    string Hash(User user, string password);
}

public sealed record PasswordVerification(bool IsValid, bool RequiresRehash);
