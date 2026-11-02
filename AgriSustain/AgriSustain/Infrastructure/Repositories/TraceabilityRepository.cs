using AgriSustain.Application.Services.Traceability;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

/// <summary>UC-TRC-01: reads HarvestBatch, Product, FarmerProfile, CultivationLog and FarmCertificate.</summary>
public sealed class TraceabilityRepository(AgriSustainContext db) : ITraceabilityRepository
{
    public async Task<TraceabilityDto?> FindAndCountScanAsync(string code, DateOnly today,
        CancellationToken cancellationToken)
    {
        string urlSuffix = "/trace/" + code;
        long? batchId = await db.HarvestBatches.AsNoTracking()
            .Where(b => b.BatchCode == code
                || (b.TraceabilityQrcode != null
                    && (b.TraceabilityQrcode.QrcontentString == code
                        || b.TraceabilityQrcode.TargetUrl == code
                        || b.TraceabilityQrcode.TargetUrl.EndsWith(urlSuffix))))
            .Select(b => (long?)b.HarvestBatchId)
            .FirstOrDefaultAsync(cancellationToken);
        if (batchId is null) return null;

        var batch = await db.HarvestBatches
            .Include(b => b.TraceabilityQrcode)
            .Include(b => b.Product).ThenInclude(p => p.ProductImages)
            .Include(b => b.Product).ThenInclude(p => p.FarmerProfile)
            .Include(b => b.CropSeason).ThenInclude(s => s.CultivationLogs).ThenInclude(l => l.InverseCorrectsLog)
            .AsSplitQuery()
            .FirstAsync(b => b.HarvestBatchId == batchId.Value, cancellationToken);

        var qr = batch.TraceabilityQrcode;
        bool revoked = qr?.IsRevoked == true;
        if (qr is not null && !revoked)
        {
            // Step 4: count the view on the QR record.
            qr.ScanCount += 1;
            await db.SaveChangesAsync(cancellationToken);
        }

        var farm = batch.Product.FarmerProfile;
        var certificates = await db.FarmCertificates.AsNoTracking()
            .Where(c => c.FarmerProfileId == farm.FarmerProfileId)
            .Where(MarketplaceRules.ValidCertificate(today))
            .OrderBy(c => c.CertificateType)
            .Select(c => new TraceCertificateDto(c.FarmCertificateId, c.CertificateType, c.CertificateNumber,
                c.IssuedBy, c.IssueDate, c.ExpiryDate, c.ReviewedAt))
            .ToListAsync(cancellationToken);

        // A log that was later corrected is replaced by its correction (CorrectsLogId).
        var logs = batch.CropSeason.CultivationLogs
            .Where(l => l.InverseCorrectsLog.Count == 0)
            .OrderBy(l => l.ActionDate)
            .Select(l => new TraceCultivationLogDto(l.ActionDate, l.ActionType, l.DetailDescription,
                l.InputMaterial, l.Dosage, l.PerformedBy, l.ImageUrl))
            .ToList();

        var image = batch.Product.ProductImages
            .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder)
            .Select(i => i.ImageUrl).FirstOrDefault();

        return new TraceabilityDto(
            batch.BatchCode,
            revoked,
            null,
            batch.ProductId,
            batch.Product.ProductName,
            image,
            batch.Product.Unit,
            batch.Product.OriginProvince,
            batch.ActualHarvestDate ?? batch.EstimatedHarvestDate,
            batch.ActualHarvestDate is null,
            batch.ExpirationDate,
            batch.QualityNotes,
            farm.FarmName,
            farm.CultivationZone,
            farm.Address,
            farm.ReputationScore,
            batch.CropSeason.SeasonName,
            batch.CropSeason.CropVariety,
            batch.CropSeason.PlantingDate,
            qr?.ScanCount ?? 0,
            logs,
            certificates);
    }
}
