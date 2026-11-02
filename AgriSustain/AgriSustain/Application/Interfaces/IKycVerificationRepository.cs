using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Verification;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Interfaces;

public interface IKycVerificationRepository
{
    Task<User?> FindActorAsync(long userId, CancellationToken cancellationToken);

    Task<Page<KycApplicationSummaryDto>> SearchAsync(
        KycApplicationSearch search,
        CancellationToken cancellationToken);

    Task<KycApplicationDetailDto?> GetDetailAsync(
        long userId,
        CancellationToken cancellationToken);

    Task<User?> GetUserForKycReviewAsync(
        long userId,
        CancellationToken cancellationToken);

    void AddAudit(AuditLog audit);

    void AddNotification(Notification notification);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

