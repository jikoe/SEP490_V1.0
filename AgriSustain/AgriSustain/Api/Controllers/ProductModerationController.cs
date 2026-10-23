    using System.Security.Claims;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Authorize(Roles = "ContentModerator,Content Moderator")]
[Route("api/moderation/products")]
public sealed class ProductModerationController : ControllerBase
{
    private readonly AgriSustainContext _context;

    public ProductModerationController(AgriSustainContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? keyword,
        [FromQuery] string status = "Pending",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        if (status is not ("Pending" or "Reviewed"))
            return BadRequest(new { message = "Trạng thái chỉ nhận Pending hoặc Reviewed." });

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var products = _context.Products.AsNoTracking()
            .Where(product => status == "Pending"
                ? product.ApprovalStatus == "Pending"
                : product.ApprovalStatus == "Approved" || product.ApprovalStatus == "Rejected");

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var search = keyword.Trim();
            products = products.Where(product =>
                product.ProductName.Contains(search)
                || product.FarmerProfile.FarmName.Contains(search)
                || product.FarmerProfile.User.FullName.Contains(search));
        }

        var total = await products.CountAsync(cancellationToken);
        var items = await products.OrderBy(product => product.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(product => new
            {
                id = product.ProductId,
                name = product.ProductName,
                status = product.ApprovalStatus,
                category = product.Category.CategoryName,
                farmerName = product.FarmerProfile.User.FullName,
                farmName = product.FarmerProfile.FarmName,
                submittedAt = product.CreatedAt,
                imageUrl = product.ProductImages.OrderByDescending(image => image.IsPrimary).Select(image => image.ImageUrl).FirstOrDefault()
            })
            .ToListAsync(cancellationToken);

        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{productId:long}")]
    public async Task<IActionResult> Detail(long productId, CancellationToken cancellationToken)
    {
        var product = await _context.Products.AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.ProductImages)
            .Include(item => item.FarmerProfile).ThenInclude(farmer => farmer.User)
            .Include(item => item.HarvestBatches).ThenInclude(batch => batch.CropSeason)
            .Include(item => item.HarvestBatches).ThenInclude(batch => batch.TraceabilityQrcode)
            .SingleOrDefaultAsync(item => item.ProductId == productId, cancellationToken);

        if (product is null)
            return NotFound();

        var certificates = await _context.FarmCertificates.AsNoTracking()
            .Where(certificate => certificate.FarmerProfileId == product.FarmerProfileId)
            .Select(certificate => new
            {
                certificate.FarmCertificateId,
                certificate.CertificateType,
                certificate.CertificateNumber,
                certificate.IssuedBy,
                certificate.ExpiryDate,
                certificate.DocumentUrl,
                certificate.VerificationStatus
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            id = product.ProductId,
            moderationStatus = product.ApprovalStatus,
            rejectionReason = product.RejectionReason,
            name = product.ProductName,
            description = product.Description,
            category = product.Category.CategoryName,
            images = product.ProductImages.OrderBy(image => image.DisplayOrder)
                .Select(image => new { image.ImageUrl, image.AltText }),
            retailPrice = product.BaseRetailPrice,
            wholesalePrice = product.WholesalePrice,
            wholesaleMinQuantity = product.WholesaleMinQuantity,
            unit = product.Unit,
            quantity = product.HarvestBatches.Sum(batch => batch.AvailableQuantity),
            farmer = new
            {
                product.FarmerProfile.User.FullName,
                product.FarmerProfile.User.Email,
                product.FarmerProfile.User.PhoneNumber
            },
            farm = new
            {
                product.FarmerProfile.FarmName,
                product.FarmerProfile.Address,
                product.FarmerProfile.ProductionScale,
                product.FarmerProfile.ProvinceCode,
                product.FarmerProfile.VerificationStatus
            },
            harvest = product.HarvestBatches.Select(batch => new
            {
                batch.BatchCode,
                batch.HarvestYield,
                batch.AvailableQuantity,
                batch.EstimatedHarvestDate,
                batch.ActualHarvestDate,
                batch.ExpirationDate,
                batch.QualityNotes,
                season = batch.CropSeason.SeasonName,
                cropVariety = batch.CropSeason.CropVariety
            }),
            traceability = product.HarvestBatches
                .Where(batch => batch.TraceabilityQrcode != null)
                .Select(batch => new
                {
                    batch.BatchCode,
                    batch.TraceabilityQrcode!.TargetUrl,
                    batch.TraceabilityQrcode.QrcontentString,
                    batch.TraceabilityQrcode.IsRevoked
                }),
            certificates
        });
    }

    [HttpPost("{productId:long}/approve")]
    public Task<IActionResult> Approve(long productId, CancellationToken cancellationToken) =>
        Moderate(productId, approved: true, reason: null, cancellationToken);

    [HttpPost("{productId:long}/reject")]
    public Task<IActionResult> Reject(
        long productId,
        [FromBody] RejectProductRequest request,
        CancellationToken cancellationToken)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason))
            return Task.FromResult<IActionResult>(BadRequest(new { message = "Vui lòng nhập lý do từ chối." }));
        if (reason.Length > 500)
            return Task.FromResult<IActionResult>(BadRequest(new { message = "Lý do từ chối tối đa 500 ký tự." }));

        return Moderate(productId, approved: false, reason, cancellationToken);
    }

    private async Task<IActionResult> Moderate(
        long productId,
        bool approved,
        string? reason,
        CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .Include(item => item.FarmerProfile)
            .SingleOrDefaultAsync(item => item.ProductId == productId, cancellationToken);
        if (product is null)
            return NotFound();
        if (product.ApprovalStatus != "Pending")
            return Conflict(new { message = "Sản phẩm đã được xử lý trước đó." });

        if (string.IsNullOrWhiteSpace(User.FindFirstValue(ClaimTypes.NameIdentifier)))
            return Unauthorized();

        product.ApprovalStatus = approved ? "Approved" : "Rejected";
        product.IsPublished = approved;
        product.RejectionReason = approved ? null : reason;
        _context.Notifications.Add(new Notification
        {
            UserId = product.FarmerProfile.UserId,
            Title = approved ? "Sản phẩm đã được duyệt" : "Sản phẩm chưa được duyệt",
            Content = approved
                ? $"Sản phẩm “{product.ProductName}” đã được duyệt và có thể hiển thị trên sàn."
                : $"Sản phẩm “{product.ProductName}” bị từ chối. Lý do: {reason}",
            NotificationType = "ProductModeration",
            IsRead = false,
            SentAt = DateTime.UtcNow,
            DeliveryChannel = "InApp",
            DeliveryStatus = "Sent",
            RelatedRecordType = "Product"
        });

        await _context.SaveChangesAsync(cancellationToken);
        return Ok(new { id = product.ProductId, status = product.ApprovalStatus });
    }

    public sealed record RejectProductRequest(string? Reason);
}

