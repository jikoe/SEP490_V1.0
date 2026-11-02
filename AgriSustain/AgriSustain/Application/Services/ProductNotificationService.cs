using System.Text.Json;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
namespace AgriSustain.Application.Services;

// Queue both channels in the listing transaction. Only the email worker performs network I/O.
public sealed class ProductNotificationService(IProductNotificationRepository repository,
    TimeProvider clock)
{
    public async Task<bool> NotifyModeratorsAsync(Product product, string eventKey,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var recipients = await repository.GetModeratorIdsAsync(cancellationToken);
        foreach (var userId in recipients)
        {
            string content = $"Sản phẩm {product.ProductName} của nông trại "
                + $"{product.FarmerProfile.FarmName} đã được gửi duyệt lúc "
                + $"{now.AddHours(7):dd/MM/yyyy HH:mm}. Vui lòng kiểm tra thông tin sản phẩm.";
            foreach (var channel in new[] { "InApp", "Email" })
                repository.Add(new Notification
                {
                    UserId = userId, Title = "Có sản phẩm cần kiểm duyệt", Content = content,
                    NotificationType = "ProductListingReview", DeliveryChannel = channel,
                    DeliveryStatus = channel == "InApp" ? "Sent" : "Pending", SentAt = now,
                    RelatedRecordType = "Product", RelatedRecordId = product.ProductId,
                    PayloadJson = JsonSerializer.Serialize(new { EventKey = eventKey,
                        ProductId = product.ProductId, SubmittedAt = now,
                        ReviewPath = $"/moderation/products/{product.ProductId}" })
                });
        }
        return recipients.Count > 0;
    }
}
