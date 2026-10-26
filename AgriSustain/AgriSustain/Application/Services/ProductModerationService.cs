using AgriSustain.Application.DTOs.Moderation;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

public sealed class ProductModerationService : IProductModerationService
{
    private const int MaximumPageSize = 100;
    private const int MaximumRejectionReasonLength = 500;
    private readonly IProductModerationRepository _repository;
    private readonly TimeProvider _timeProvider;

    public ProductModerationService(
        IProductModerationRepository repository,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<ProductModerationListDto?> GetListAsync(
        string? keyword,
        string status,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (status is not ("Pending" or "Reviewed"))
        {
            return null;
        }

        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, MaximumPageSize);
        var (total, items) = await _repository.GetListAsync(
            status,
            keyword?.Trim(),
            normalizedPage,
            normalizedPageSize,
            cancellationToken);

        return new ProductModerationListDto(
            items,
            total,
            normalizedPage,
            normalizedPageSize);
    }

    public Task<ProductModerationDetailDto?> GetDetailAsync(
        long productId,
        CancellationToken cancellationToken)
    {
        return _repository.GetDetailAsync(productId, cancellationToken);
    }

    public async Task<ProductModerationOutcome> ModerateAsync(
        long productId,
        bool approved,
        string? reason,
        CancellationToken cancellationToken)
    {
        var normalizedReason = reason?.Trim();
        if (!approved
            && (string.IsNullOrWhiteSpace(normalizedReason)
                || normalizedReason.Length > MaximumRejectionReasonLength))
        {
            return ProductModerationOutcome.InvalidReason;
        }

        var product = await _repository.GetForModerationAsync(
            productId,
            cancellationToken);
        if (product is null)
        {
            return ProductModerationOutcome.NotFound;
        }

        if (product.ApprovalStatus != "Pending")
        {
            return ProductModerationOutcome.Conflict;
        }

        product.ApprovalStatus = approved ? "Approved" : "Rejected";
        product.IsPublished = approved;
        product.RejectionReason = approved ? null : normalizedReason;
        _repository.AddNotification(new Notification
        {
            UserId = product.FarmerProfile.UserId,
            Title = approved
                ? "Sản phẩm đã được duyệt"
                : "Sản phẩm chưa được duyệt",
            Content = approved
                ? $"Sản phẩm “{product.ProductName}” đã được duyệt và có thể "
                    + "hiển thị trên sàn."
                : $"Sản phẩm “{product.ProductName}” bị từ chối. Lý do: "
                    + normalizedReason,
            NotificationType = "ProductModeration",
            IsRead = false,
            SentAt = _timeProvider.GetUtcNow().UtcDateTime,
            DeliveryChannel = "InApp",
            DeliveryStatus = "Sent",
            RelatedRecordType = "Product"
        });

        await _repository.SaveChangesAsync(cancellationToken);
        return ProductModerationOutcome.Success;
    }
}
