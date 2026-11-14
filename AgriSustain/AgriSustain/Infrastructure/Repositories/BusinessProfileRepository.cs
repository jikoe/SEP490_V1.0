using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.Services.BusinessProfiles;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

/// <summary>UC-AUTH-02 (part 2) persistence over dbo.WholesaleBuyerProfile.</summary>
public sealed class BusinessProfileRepository(AgriSustainContext db) : IBusinessProfileRepository
{
    public Task<BusinessProfileDto?> GetAsync(long userId, CancellationToken cancellationToken) =>
        db.WholesaleBuyerProfiles.AsNoTracking()
            .Where(p => p.UserId == userId)
            .Select(p => new BusinessProfileDto(p.CompanyName, p.TaxCode, p.RepresentativeName, p.BusinessType,
                p.BusinessAddress, p.DocumentUrl, p.VerificationStatus, p.RejectReason))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> TaxCodeTakenAsync(string taxCode, long userId, CancellationToken cancellationToken) =>
        db.WholesaleBuyerProfiles.AnyAsync(p => p.TaxCode == taxCode && p.UserId != userId, cancellationToken);

    public async Task<BusinessProfileDto> SubmitAsync(long userId, SubmitBusinessProfileRequest request,
        string? documentUrl, DateTime now, CancellationToken cancellationToken)
    {
        var profile = await db.WholesaleBuyerProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        string oldStatus = profile?.VerificationStatus ?? VerificationStatuses.NotSubmitted;
        if (profile is null)
        {
            profile = new WholesaleBuyerProfile { UserId = userId };
            db.WholesaleBuyerProfiles.Add(profile);
        }

        profile.CompanyName = request.CompanyName;
        profile.TaxCode = request.TaxCode;
        profile.RepresentativeName = request.RepresentativeName;
        profile.BusinessType = request.BusinessType;
        profile.BusinessAddress = request.BusinessAddress;
        if (documentUrl is not null) profile.DocumentUrl = documentUrl;
        profile.VerificationStatus = VerificationStatuses.PendingApproval;
        profile.RejectReason = null;
        await db.SaveChangesAsync(cancellationToken);

        db.AuditLogs.Add(new AuditLog
        {
            PerformedByUserId = userId,
            EntityType = "WholesaleBuyerProfile",
            EntityId = profile.WholesaleBuyerProfileId,
            Action = AuditActions.SubmitForVerification,
            OldValue = JsonSerializer.Serialize(new { VerificationStatus = oldStatus }),
            NewValue = JsonSerializer.Serialize(new
            {
                profile.CompanyName, profile.TaxCode, profile.VerificationStatus
            }),
            OccurredAt = now
        });
        await db.SaveChangesAsync(cancellationToken);

        return new BusinessProfileDto(profile.CompanyName, profile.TaxCode, profile.RepresentativeName,
            profile.BusinessType, profile.BusinessAddress, profile.DocumentUrl, profile.VerificationStatus, null);
    }
}
