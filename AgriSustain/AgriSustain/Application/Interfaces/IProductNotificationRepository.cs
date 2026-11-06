using AgriSustain.Domain.Entities;
namespace AgriSustain.Application.Interfaces;

public interface IProductNotificationRepository
{
    Task<IReadOnlyList<long>> GetModeratorIdsAsync(CancellationToken cancellationToken);
    void Add(Notification notification);
    void SaveNotificationsAsync(Notification notification) => Add(notification);

    /// <summary>Sets failed e-mails of the event back to Pending once the retry delay has passed.</summary>
    Task RequeueFailedAsync(string eventKey, DateTime failedBefore, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}

public interface INotificationGateway
{
    Task SendEmailAsync(string recipient, string subject, string content, string eventKey,
        CancellationToken cancellationToken);
}
