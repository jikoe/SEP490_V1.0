using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Verification;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class KycVerificationRepository(AgriSustainContext context) : IKycVerificationRepository
{
    public Task<User?> FindActorAsync(long userId, CancellationToken cancellationToken)
    {
        return context.Users.AsNoTracking()
            .SingleOrDefaultAsync(user => user.UserId == userId, cancellationToken);
    }

    public async Task<Page<KycApplicationSummaryDto>> SearchAsync(
        KycApplicationSearch search,
        CancellationToken cancellationToken)
    {
        var query = context.Users.AsNoTracking()
            .Include(u => u.DeliveryAddress)
            .Include(u => u.FarmerProfile)
                .ThenInclude(f => f!.FarmCertificates)
            .Include(u => u.WholesaleBuyerProfile)
            .Where(user => user.Role == "Farmer"
                || user.Role == "WholesaleBuyer"
                || user.Role == "RetailCustomer");

        if (!string.IsNullOrWhiteSpace(search.Role))
        {
            query = query.Where(user => user.Role == search.Role);
        }

        var keyword = !string.IsNullOrWhiteSpace(search.Keyword)
            ? search.Keyword.Trim()
            : search.Search?.Trim();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(user =>
                user.UserId.ToString() == keyword
                || user.Username.Contains(keyword)
                || user.FullName.Contains(keyword)
                || user.Email.Contains(keyword)
                || (user.PhoneNumber != null && user.PhoneNumber.Contains(keyword))
                || (user.FarmerProfile != null && (
                    user.FarmerProfile.FarmName.Contains(keyword)
                    || user.FarmerProfile.RepresentativeName.Contains(keyword)
                    || (user.FarmerProfile.BusinessLicenseNumber != null
                        && user.FarmerProfile.BusinessLicenseNumber.Contains(keyword))))
                || (user.WholesaleBuyerProfile != null && (
                    user.WholesaleBuyerProfile.CompanyName.Contains(keyword)
                    || user.WholesaleBuyerProfile.RepresentativeName.Contains(keyword)
                    || user.WholesaleBuyerProfile.TaxCode.Contains(keyword))));
        }

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .ThenByDescending(u => u.UserId)
            .ToListAsync(cancellationToken);

        var userIds = users.Select(u => u.UserId).ToList();
        var audits = userIds.Count == 0
            ? []
            : await context.AuditLogs.AsNoTracking()
                .Include(log => log.PerformedByUser)
                .Where(log => log.EntityType == "KycApplication" && log.EntityId != null && userIds.Contains(log.EntityId.Value))
                .OrderByDescending(log => log.OccurredAt)
                .ThenByDescending(log => log.AuditLogId)
                .ToListAsync(cancellationToken);

        var latestAuditByUser = audits
            .Where(log => log.EntityId.HasValue)
            .GroupBy(log => log.EntityId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var mapped = users.Select(user =>
        {
            latestAuditByUser.TryGetValue(user.UserId, out var latestAudit);
            var farm = user.FarmerProfile;
            var wholesale = user.WholesaleBuyerProfile;
            var delivery = user.DeliveryAddress;

            string rawStatus = user.Role switch
            {
                "Farmer" => farm?.VerificationStatus ?? VerificationStatuses.PendingApproval,
                "WholesaleBuyer" => wholesale?.VerificationStatus ?? VerificationStatuses.PendingApproval,
                _ => latestAudit is not null
                    ? (latestAudit.Action == "ApproveKyc" ? VerificationStatuses.Approved
                        : latestAudit.Action == "RejectKyc" ? VerificationStatuses.Rejected
                        : VerificationStatuses.AdditionalInfoRequired)
                    : (user.IsEmailVerified ? VerificationStatuses.Approved : VerificationStatuses.PendingApproval)
            };

            string normalizedStatus = NormalizeStatusForClient(rawStatus);

            var accountStatus = user.Status == "Suspended" ? "Suspended"
                : user.Role is "Farmer" or "WholesaleBuyer"
                    ? (normalizedStatus == VerificationStatuses.Approved ? "Active"
                        : normalizedStatus == VerificationStatuses.Rejected ? "Rejected"
                        : "Pending")
                    : user.Status;

            var organizationName = farm?.FarmName ?? wholesale?.CompanyName ?? user.FullName;
            var representativeName = farm?.RepresentativeName ?? wholesale?.RepresentativeName ?? user.FullName;
            var identificationNumber = farm?.BusinessLicenseNumber
                ?? wholesale?.TaxCode
                ?? user.PhoneNumber;
            var address = farm?.Address
                ?? wholesale?.BusinessAddress
                ?? (delivery is null ? null : $"{delivery.AddressDetail}, {delivery.Ward}, {delivery.ProvinceCity}");

            int docCount = farm is not null
                ? Math.Max(1, farm.FarmCertificates.Count)
                : 1;

            var reviewedAt = latestAudit?.OccurredAt
                ?? farm?.ApprovedAt
                ?? wholesale?.ApprovedAt;

            return new KycApplicationSummaryDto(
                user.UserId,
                user.Username,
                user.FullName,
                organizationName,
                organizationName,
                representativeName,
                user.Email,
                user.PhoneNumber,
                user.Role,
                accountStatus,
                normalizedStatus,
                identificationNumber,
                address,
                docCount,
                user.CreatedAt,
                reviewedAt,
                latestAudit?.PerformedByUser?.FullName);
        }).ToList();

        var statusFilter = !string.IsNullOrWhiteSpace(search.Status)
            ? search.Status.Trim()
            : search.VerificationStatus?.Trim();

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            var normalizedFilter = NormalizeStatusForClient(statusFilter);
            mapped = mapped
                .Where(item => string.Equals(item.VerificationStatus, normalizedFilter, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var ordered = mapped
            .OrderBy(item => item.VerificationStatus == "Pending" ? 0
                : item.VerificationStatus == VerificationStatuses.AdditionalInfoRequired ? 1
                : item.VerificationStatus == VerificationStatuses.Rejected ? 2 : 3)
            .ThenByDescending(item => item.SubmittedAt)
            .ThenByDescending(item => item.UserId)
            .ToList();

        var total = ordered.Count;
        var items = ordered
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToList();

        return new Page<KycApplicationSummaryDto>(items, total, search.Page, search.PageSize);
    }

    public async Task<KycApplicationDetailDto?> GetDetailAsync(
        long userId,
        CancellationToken cancellationToken)
    {
        var user = await context.Users.AsNoTracking()
            .Include(u => u.DeliveryAddress)
            .Include(u => u.FarmerProfile)
                .ThenInclude(f => f!.FarmCertificates)
            .Include(u => u.WholesaleBuyerProfile)
            .SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null || user.Role is not ("Farmer" or "WholesaleBuyer" or "RetailCustomer"))
        {
            return null;
        }

        var latestAudit = await context.AuditLogs.AsNoTracking()
            .Include(log => log.PerformedByUser)
            .Where(log => log.EntityType == "KycApplication" && log.EntityId == user.UserId)
            .OrderByDescending(log => log.OccurredAt)
            .ThenByDescending(log => log.AuditLogId)
            .FirstOrDefaultAsync(cancellationToken);

        var farm = user.FarmerProfile;
        var wholesale = user.WholesaleBuyerProfile;
        var delivery = user.DeliveryAddress;

        string rawVerificationStatus = user.Role switch
        {
            "Farmer" => farm?.VerificationStatus ?? VerificationStatuses.PendingApproval,
            "WholesaleBuyer" => wholesale?.VerificationStatus ?? VerificationStatuses.PendingApproval,
            _ => latestAudit is not null
                ? (latestAudit.Action == "ApproveKyc" ? VerificationStatuses.Approved
                    : latestAudit.Action == "RejectKyc" ? VerificationStatuses.Rejected
                    : VerificationStatuses.AdditionalInfoRequired)
                : (user.IsEmailVerified ? VerificationStatuses.Approved : VerificationStatuses.PendingApproval)
        };

        string verificationStatus = NormalizeStatusForClient(rawVerificationStatus);

        string accountStatus = user.Status == "Suspended" ? "Suspended"
            : user.Role is "Farmer" or "WholesaleBuyer"
                ? (verificationStatus == VerificationStatuses.Approved ? "Active"
                    : verificationStatus == VerificationStatuses.Rejected ? "Rejected"
                    : "Pending")
                : user.Status;

        var documents = new List<KycDocumentDto>();
        if (farm is not null && farm.FarmCertificates.Count > 0)
        {
            foreach (var cert in farm.FarmCertificates.OrderByDescending(c => c.IssueDate))
            {
                var certStatus = NormalizeStatusForClient(cert.VerificationStatus);
                documents.Add(new KycDocumentDto(
                    cert.FarmCertificateId,
                    cert.FarmCertificateId,
                    cert.CertificateType,
                    cert.CertificateType,
                    cert.CertificateNumber,
                    cert.CertificateNumber,
                    cert.IssuedBy,
                    cert.IssueDate,
                    cert.ExpiryDate,
                    cert.DocumentUrl,
                    certStatus,
                    certStatus));
            }
        }
        else if (farm is not null)
        {
            var licenseNum = farm.BusinessLicenseNumber ?? $"FARM-{user.UserId}";
            documents.Add(new KycDocumentDto(
                farm.FarmerProfileId,
                farm.FarmerProfileId,
                "Giấy phép ĐKKD / Chứng nhận hộ nông dân",
                "Giấy phép ĐKKD / Chứng nhận hộ nông dân",
                licenseNum,
                licenseNum,
                "Cơ quan đăng ký kinh doanh địa phương",
                null,
                null,
                null,
                verificationStatus,
                verificationStatus));
        }
        else if (wholesale is not null)
        {
            var taxCode = wholesale.TaxCode ?? $"B2B-{user.UserId}";
            documents.Add(new KycDocumentDto(
                wholesale.WholesaleBuyerProfileId,
                wholesale.WholesaleBuyerProfileId,
                "Giấy chứng nhận đăng ký doanh nghiệp (B2B)",
                "Giấy chứng nhận đăng ký doanh nghiệp (B2B)",
                taxCode,
                taxCode,
                "Sở Kế hoạch và Đầu tư / Cơ quan Thuế",
                null,
                null,
                wholesale.DocumentUrl,
                verificationStatus,
                verificationStatus));
        }
        else
        {
            var idNum = user.PhoneNumber ?? user.Email;
            documents.Add(new KycDocumentDto(
                user.UserId,
                user.UserId,
                "Hồ sơ định danh khách hàng cá nhân (CCCD / Hộ chiếu)",
                "Hồ sơ định danh khách hàng cá nhân (CCCD / Hộ chiếu)",
                idNum,
                idNum,
                "Khách hàng kê khai & xác thực",
                null,
                null,
                null,
                verificationStatus,
                verificationStatus));
        }

        string? address = farm?.Address
            ?? wholesale?.BusinessAddress
            ?? (delivery is null
                ? null
                : $"{delivery.AddressDetail}, {delivery.Ward}, {delivery.ProvinceCity}");

        string? provinceCode = farm?.ProvinceCode ?? delivery?.ProvinceCode;
        string identificationType = user.Role switch
        {
            "Farmer" => "Giấy phép ĐKKD / Mã số hộ nông dân",
            "WholesaleBuyer" => "Mã số thuế doanh nghiệp (MST)",
            _ => "Thông tin định danh & Liên hệ cá nhân"
        };
        string? identificationNumber = user.Role switch
        {
            "Farmer" => farm?.BusinessLicenseNumber,
            "WholesaleBuyer" => wholesale?.TaxCode,
            _ => user.PhoneNumber
        };

        DateTime? approvedAt = farm?.ApprovedAt ?? wholesale?.ApprovedAt;
        DateTime? reviewedAt = latestAudit?.OccurredAt ?? approvedAt;
        string? rejectReason = farm?.RejectReason ?? wholesale?.RejectReason ?? latestAudit?.Reason;
        string? entityName = farm?.FarmName ?? wholesale?.CompanyName ?? user.FullName;

        return new KycApplicationDetailDto(
            user.UserId,
            user.Username,
            user.FullName,
            entityName,
            entityName,
            farm?.RepresentativeName ?? wholesale?.RepresentativeName ?? user.FullName,
            user.Email,
            user.PhoneNumber,
            user.Role,
            accountStatus,
            user.IsEmailVerified,
            user.CreatedAt,
            user.CreatedAt,
            address,
            provinceCode,
            wholesale?.BusinessType ?? (user.Role == "Farmer" ? "Nông hộ / Trang trại sản xuất" : "Khách hàng cá nhân"),
            farm?.ProductionScale,
            farm?.CultivationZone,
            farm?.Gpscoordinates,
            identificationType,
            identificationNumber,
            verificationStatus,
            approvedAt,
            reviewedAt,
            latestAudit?.PerformedByUser?.FullName,
            rejectReason,
            documents);
    }

    public Task<User?> GetUserForKycReviewAsync(long userId, CancellationToken cancellationToken)
    {
        return context.Users
            .Include(u => u.FarmerProfile)
                .ThenInclude(f => f!.FarmCertificates)
            .Include(u => u.WholesaleBuyerProfile)
            .SingleOrDefaultAsync(u => u.UserId == userId, cancellationToken);
    }

    public void AddAudit(AuditLog audit)
    {
        context.AuditLogs.Add(audit);
    }

    public void AddNotification(Notification notification)
    {
        context.Notifications.Add(notification);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return context.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeStatusForClient(string? rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus)
            || VerificationStatuses.IsPending(rawStatus)
            || rawStatus == VerificationStatuses.NotSubmitted)
        {
            return "Pending";
        }

        return rawStatus;
    }
}
