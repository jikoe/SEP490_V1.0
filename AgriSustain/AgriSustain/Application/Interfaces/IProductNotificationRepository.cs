using AgriSustain.Domain.Entities;
namespace AgriSustain.Application.Interfaces;

public interface IProductNotificationRepository
{
    Task<IReadOnlyList<long>> GetModeratorIdsAsync(CancellationToken cancellationToken);
    void Add(Notification notification);
}

public interface INotificationGateway
{
    Task SendEmailAsync(string recipient, string subject, string content, string eventKey,
        CancellationToken cancellationToken);
}
