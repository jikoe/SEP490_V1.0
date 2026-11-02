using AgriSustain.Application.DTOs.Orders;

namespace AgriSustain.Application.Interfaces;

public interface IOrderFeedbackStorage
{
    Task<string> SaveAsync(long userId, FeedbackFile file,
        CancellationToken cancellationToken);
    void Delete(long userId, IEnumerable<string> urls);
    FeedbackDownload? OpenEvidence(long userId, string key);
}
