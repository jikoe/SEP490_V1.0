using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly AgriSustainContext _context;

    public UserRepository(AgriSustainContext context)
    {
        _context = context;
    }

    public async Task<User?> FindByUsernameOrEmailAsync(
        string identifier,
        CancellationToken cancellationToken)
    {
        var user = await _context.Users.SingleOrDefaultAsync(
            candidate => candidate.Username == identifier,
            cancellationToken);

        return user ?? await _context.Users.SingleOrDefaultAsync(
            candidate => candidate.Email == identifier,
            cancellationToken);
    }

    public Task<User?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        return _context.Users.SingleOrDefaultAsync(
            candidate => candidate.Email == email,
            cancellationToken);
    }

    public void AddAudit(AuditLog audit)
    {
        _context.AuditLogs.Add(audit);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
