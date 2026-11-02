using System.Text.Json;
using AgriSustain.Application.Services.Certificates;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

/// <summary>UC-FAR-07 persistence over dbo.FarmCertificate.</summary>
public sealed class FarmCertificateRepository(AgriSustainContext db) : IFarmCertificateRepository
{
    public async Task<(long FarmerProfileId, string VerificationStatus)?> GetFarmerProfileAsync(
        long userId, CancellationToken cancellationToken)
    {
        var farm = await db.FarmerProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new { p.FarmerProfileId, p.VerificationStatus })
            .FirstOrDefaultAsync(cancellationToken);
        return farm is null ? null : (farm.FarmerProfileId, farm.VerificationStatus);
    }

    public Task<bool> NumberExistsAsync(string certificateType, string certificateNumber,
        CancellationToken cancellationToken) =>
        db.FarmCertificates.AnyAsync(c => c.CertificateType == certificateType
            && c.CertificateNumber == certificateNumber, cancellationToken);

    public async Task<IReadOnlyList<FarmCertificateDto>> ListAsync(long farmerProfileId,
        CancellationToken cancellationToken) =>
        await db.FarmCertificates.AsNoTracking()
            .Where(c => c.FarmerProfileId == farmerProfileId)
            .OrderByDescending(c => c.FarmCertificateId)
            .Select(c => new FarmCertificateDto(c.FarmCertificateId, c.CertificateType, c.CertificateNumber,
                c.IssuedBy, c.IssueDate, c.ExpiryDate, c.VerificationStatus, c.ReviewedAt,
                c.RevocationReason, c.DocumentUrl))
            .ToListAsync(cancellationToken);

    public async Task<FarmCertificateDto> AddAsync(NewFarmCertificate certificate, CancellationToken cancellationToken)
    {
        var entity = new FarmCertificate
        {
            FarmerProfileId = certificate.FarmerProfileId,
            CertificateType = certificate.CertificateType,
            CertificateNumber = certificate.CertificateNumber,
            IssuedBy = certificate.IssuedBy,
            IssueDate = certificate.IssueDate,
            ExpiryDate = certificate.ExpiryDate,
            DocumentUrl = certificate.DocumentUrl,
            VerificationStatus = FarmCertificateRules.PendingReview
        };
        db.FarmCertificates.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        db.AuditLogs.Add(new AuditLog
        {
            PerformedByUserId = certificate.SubmittedByUserId,
            EntityType = "FarmCertificate",
            EntityId = entity.FarmCertificateId,
            Action = "SubmitCertificate",
            NewValue = JsonSerializer.Serialize(new
            {
                entity.CertificateType, entity.CertificateNumber, entity.IssuedBy,
                entity.IssueDate, entity.ExpiryDate, entity.VerificationStatus
            }),
            OccurredAt = certificate.SubmittedAt
        });
        await db.SaveChangesAsync(cancellationToken);

        return new FarmCertificateDto(entity.FarmCertificateId, entity.CertificateType, entity.CertificateNumber,
            entity.IssuedBy, entity.IssueDate, entity.ExpiryDate, entity.VerificationStatus, entity.ReviewedAt,
            entity.RevocationReason, entity.DocumentUrl);
    }
}
