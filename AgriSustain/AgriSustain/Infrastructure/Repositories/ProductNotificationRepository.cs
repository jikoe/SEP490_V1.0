using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
namespace AgriSustain.Infrastructure.Repositories;

public sealed class ProductNotificationRepository(AgriSustainContext context)
    : IProductNotificationRepository
{
    public async Task<IReadOnlyList<long>> GetModeratorIdsAsync(CancellationToken cancellationToken) =>
        await context.Users.Where(user => user.Status == "Active"
            && (user.Role == "ContentModerator" || user.Role == "Content Moderator"))
            .Select(user => user.UserId).ToListAsync(cancellationToken);
    public void Add(Notification notification) => context.Notifications.Add(notification);
}
