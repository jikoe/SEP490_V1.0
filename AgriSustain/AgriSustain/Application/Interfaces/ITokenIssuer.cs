using AgriSustain.Application.DTOs.Auth;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface ITokenIssuer
{
    IssuedToken Issue(User user, DateTimeOffset issuedAt);
}
