using System.Globalization;
using System.Text.Json;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services.B2B;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

/// <summary>
/// UC-WB-08 persistence over B2BSubscription and PaymentTransaction. A payment request is one PaymentTransaction
/// row (PaymentPurpose = SubscriptionPayment) whose RequestKey is "{reference}|{buyerProfileId}|{packageId}|{autoRenew}";
/// while it is Pending its TransactionCode is the reference, and the SePay credit code once paid.
/// </summary>
public sealed class BuyerSubscriptionRepository(AgriSustainContext db) : IBuyerSubscriptionRepository
{
    private const string Approved = "Approved";

    public Task<BuyerRecord?> GetBuyerAsync(long userId, CancellationToken cancellationToken) =>
        db.WholesaleBuyerProfiles.AsNoTracking()
            .Where(b => b.UserId == userId)
            .Select(b => new BuyerRecord(b.WholesaleBuyerProfileId, b.UserId, b.VerificationStatus,
                b.User.Email, b.User.FullName))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<BuyerRecord?> GetBuyerByProfileAsync(long buyerProfileId, CancellationToken cancellationToken) =>
        db.WholesaleBuyerProfiles.AsNoTracking()
            .Where(b => b.WholesaleBuyerProfileId == buyerProfileId)
            .Select(b => new BuyerRecord(b.WholesaleBuyerProfileId, b.UserId, b.VerificationStatus,
                b.User.Email, b.User.FullName))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<TierRecord>> ListActiveTiersAsync(CancellationToken cancellationToken) =>
        await db.B2bsubscriptionPackages.AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new TierRecord(p.PackageId, p.PackageName, p.PackageDescription, p.DurationDays, p.Price,
                p.UnlocksFarmerDirectContact, p.CanDownloadContractTemplate, p.TransactionLimit, p.IsActive))
            .ToListAsync(cancellationToken);

    public Task<TierRecord?> FindTierAsync(long packageId, CancellationToken cancellationToken) =>
        db.B2bsubscriptionPackages.AsNoTracking()
            .Where(p => p.PackageId == packageId)
            .Select(p => new TierRecord(p.PackageId, p.PackageName, p.PackageDescription, p.DurationDays, p.Price,
                p.UnlocksFarmerDirectContact, p.CanDownloadContractTemplate, p.TransactionLimit, p.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<SubscriptionRecord>> GetSubscriptionsAsync(long buyerProfileId,
        CancellationToken cancellationToken)
    {
        var rows = await db.B2bsubscriptions.AsNoTracking()
            .Where(s => s.WholesaleBuyerProfileId == buyerProfileId)
            .OrderByDescending(s => s.EndDate)
            .Select(s => new SubscriptionRecord(s.B2bsubscriptionId, s.PackageId, s.Package.PackageName,
                s.SubscriptionStatus, s.StartDate, s.EndDate, s.AutoRenew, s.PriceSnapshot, s.DurationDaysSnapshot,
                s.UnlocksFarmerDirectContactSnapshot, s.CanDownloadContractTemplateSnapshot,
                s.TransactionLimitSnapshot))
            .ToListAsync(cancellationToken);

        var current = rows.Where(s => s.Status is SubscriptionStatuses.Active or SubscriptionStatuses.Scheduled);
        var lastEnded = rows.Where(s => s.Status == SubscriptionStatuses.Expired).Take(1);
        return current.Concat(lastEnded).ToList();
    }

    // Status changes run immediately (not through the change tracker) so the one-Active-per-buyer index
    // sees the old row closed before a new Active row is inserted.
    public Task SetStatusAsync(long subscriptionId, string status, CancellationToken cancellationToken) =>
        db.B2bsubscriptions.Where(s => s.B2bsubscriptionId == subscriptionId)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.SubscriptionStatus, status), cancellationToken);

    public async Task<long> AddAsync(NewSubscription subscription, CancellationToken cancellationToken)
    {
        var tier = subscription.Tier;
        var entity = new B2bsubscription
        {
            WholesaleBuyerProfileId = subscription.WholesaleBuyerProfileId,
            PackageId = tier.PackageId,
            StartDate = subscription.StartDate,
            EndDate = subscription.EndDate,
            SubscriptionStatus = subscription.Status,
            AutoRenew = subscription.AutoRenew,
            PriceSnapshot = tier.Price,
            UnlocksFarmerDirectContactSnapshot = tier.UnlocksFarmerDirectContact,
            CanDownloadContractTemplateSnapshot = tier.CanDownloadContractTemplate,
            TransactionLimitSnapshot = tier.TransactionLimit,
            DurationDaysSnapshot = tier.DurationDays
        };
        db.B2bsubscriptions.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        long id = entity.B2bsubscriptionId;
        // Keep the buyer's one-to-one navigation out of later saves in this request.
        db.Entry(entity).State = EntityState.Detached;
        return id;
    }

    public Task ExtendAsync(long subscriptionId, TierRecord tier, DateTime endDate, bool autoRenew, DateTime now,
        CancellationToken cancellationToken) =>
        db.B2bsubscriptions.Where(s => s.B2bsubscriptionId == subscriptionId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(s => s.EndDate, endDate)
                .SetProperty(s => s.AutoRenew, autoRenew)
                .SetProperty(s => s.LastRenewedAt, now)
                .SetProperty(s => s.PriceSnapshot, tier.Price)
                .SetProperty(s => s.DurationDaysSnapshot, tier.DurationDays)
                .SetProperty(s => s.UnlocksFarmerDirectContactSnapshot, tier.UnlocksFarmerDirectContact)
                .SetProperty(s => s.CanDownloadContractTemplateSnapshot, tier.CanDownloadContractTemplate)
                .SetProperty(s => s.TransactionLimitSnapshot, tier.TransactionLimit), cancellationToken);

    public Task SetAutoRenewAsync(long subscriptionId, bool autoRenew, CancellationToken cancellationToken) =>
        db.B2bsubscriptions.Where(s => s.B2bsubscriptionId == subscriptionId)
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.AutoRenew, autoRenew), cancellationToken);

    public async Task<SubscriptionPaymentRecord?> FindOpenPaymentAsync(long buyerProfileId, long packageId,
        DateTime now, CancellationToken cancellationToken)
    {
        string suffix = $"|{buyerProfileId}|{packageId}|";
        var rows = await Payments()
            .Where(p => p.PaymentStatus == SubscriptionPaymentStatuses.Pending && p.ExpiresAt > now
                && p.RequestKey.Contains(suffix))
            .OrderByDescending(p => p.RequestedAt)
            .ToListAsync(cancellationToken);
        return rows.Select(ToRecord).FirstOrDefault(r => r is not null);
    }

    public async Task<SubscriptionPaymentRecord?> FindPaymentAsync(string reference,
        CancellationToken cancellationToken)
    {
        string prefix = reference + "|";
        var row = await Payments()
            .Where(p => p.RequestKey.StartsWith(prefix))
            .OrderByDescending(p => p.PaymentTransactionId)
            .FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : ToRecord(row);
    }

    public async Task AddPaymentAsync(SubscriptionPaymentRecord payment, CancellationToken cancellationToken)
    {
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            TransactionCode = payment.Reference,
            GatewayProvider = SubscriptionRules.Gateway,
            Amount = payment.Amount,
            PaymentType = "Transfer",
            PaymentStatus = payment.Status,
            PaymentPurpose = SubscriptionRules.PaymentPurpose,
            RequestedAt = payment.RequestedAt,
            ExpiresAt = payment.ExpiresAt,
            RequestKey = string.Join('|', payment.Reference, payment.WholesaleBuyerProfileId, payment.PackageId,
                payment.AutoRenew ? "1" : "0")
        });
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    public Task<bool> TransferRecordedAsync(string transactionCode, CancellationToken cancellationToken) =>
        db.PaymentTransactions.AsNoTracking().AnyAsync(p => p.TransactionCode == transactionCode, cancellationToken);

    public async Task CompletePaymentAsync(long paymentTransactionId, string status, IncomingBankTransfer transfer,
        long? subscriptionId, DateTime now, CancellationToken cancellationToken)
    {
        string? providerReference = string.IsNullOrWhiteSpace(transfer.ProviderReference)
            ? null : transfer.ProviderReference.Trim();
        await db.PaymentTransactions.Where(p => p.PaymentTransactionId == paymentTransactionId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(p => p.TransactionCode, Truncate(transfer.TransactionCode, 100))
                .SetProperty(p => p.PaymentStatus, status)
                .SetProperty(p => p.Amount, transfer.Amount)
                .SetProperty(p => p.PaidAt, transfer.PaidAt ?? now)
                .SetProperty(p => p.WebhookPayload, transfer.RawPayload)
                .SetProperty(p => p.ProviderTransactionReference, providerReference)
                .SetProperty(p => p.B2bsubscriptionId, subscriptionId), cancellationToken);
    }

    public async Task AddUnmatchedCreditAsync(IncomingBankTransfer transfer, long? subscriptionId, DateTime now,
        CancellationToken cancellationToken)
    {
        db.PaymentTransactions.Add(new PaymentTransaction
        {
            B2bsubscriptionId = subscriptionId,
            TransactionCode = Truncate(transfer.TransactionCode, 100),
            GatewayProvider = transfer.Gateway,
            Amount = transfer.Amount,
            PaymentType = "Transfer",
            PaymentStatus = SubscriptionPaymentStatuses.Unmatched,
            WebhookPayload = transfer.RawPayload,
            PaidAt = transfer.PaidAt ?? now,
            PaymentPurpose = SubscriptionRules.PaymentPurpose,
            RequestedAt = now,
            RequestKey = Guid.NewGuid().ToString("N"),
            ProviderTransactionReference = string.IsNullOrWhiteSpace(transfer.ProviderReference)
                ? null : transfer.ProviderReference.Trim()
        });
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    public Task ExpirePaymentAsync(long paymentTransactionId, CancellationToken cancellationToken) =>
        db.PaymentTransactions
            .Where(p => p.PaymentTransactionId == paymentTransactionId
                && p.PaymentStatus == SubscriptionPaymentStatuses.Pending)
            .ExecuteUpdateAsync(set => set.SetProperty(p => p.PaymentStatus, SubscriptionPaymentStatuses.Expired),
                cancellationToken);

    public async Task AddInAppNotificationAsync(long userId, string title, string content, string type,
        long? subscriptionId, string? eventKey, DateTime now, CancellationToken cancellationToken)
    {
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Content = content,
            NotificationType = type,
            DeliveryChannel = "InApp",
            DeliveryStatus = "Sent",
            SentAt = now,
            RelatedRecordType = "B2BSubscription",
            RelatedRecordId = subscriptionId,
            PayloadJson = eventKey is null ? null : EventPayload(eventKey)
        });
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
    }

    public async Task<IReadOnlyList<RenewalCandidate>> ListRenewalCandidatesAsync(DateTime from, DateTime to,
        CancellationToken cancellationToken) =>
        await db.B2bsubscriptions.AsNoTracking()
            .Where(s => s.SubscriptionStatus == SubscriptionStatuses.Active && s.AutoRenew
                && s.EndDate > from && s.EndDate <= to
                && s.WholesaleBuyerProfile.VerificationStatus == Approved)
            .Select(s => new RenewalCandidate(s.B2bsubscriptionId, s.WholesaleBuyerProfile.UserId,
                s.WholesaleBuyerProfile.User.Email, s.WholesaleBuyerProfile.User.FullName, s.Package.PackageName,
                s.EndDate))
            .ToListAsync(cancellationToken);

    public Task<bool> NotificationExistsAsync(string eventKey, CancellationToken cancellationToken)
    {
        string payload = EventPayload(eventKey);
        return db.Notifications.AsNoTracking().AnyAsync(n => n.PayloadJson == payload, cancellationToken);
    }

    private IQueryable<PaymentTransaction> Payments() =>
        db.PaymentTransactions.AsNoTracking()
            .Where(p => p.PaymentPurpose == SubscriptionRules.PaymentPurpose && p.GatewayProvider == SubscriptionRules.Gateway);

    private static SubscriptionPaymentRecord? ToRecord(PaymentTransaction p)
    {
        var parts = p.RequestKey.Split('|');
        if (parts.Length != 4
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long buyer)
            || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out long package))
        {
            return null;
        }

        return new SubscriptionPaymentRecord(p.PaymentTransactionId, parts[0], buyer, package, parts[3] == "1",
            p.Amount, p.PaymentStatus, p.RequestedAt, p.ExpiresAt, p.B2bsubscriptionId);
    }

    private static string EventPayload(string eventKey) => JsonSerializer.Serialize(new { EventKey = eventKey });

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
}
