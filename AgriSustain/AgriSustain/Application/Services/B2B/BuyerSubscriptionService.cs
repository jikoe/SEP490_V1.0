using System.Security.Cryptography;
using System.Text.RegularExpressions;
using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces;

namespace AgriSustain.Application.Services.B2B;

// UC-WB-08 Manage Buyer Subscription (WholesaleBuyer).

public static class SubscriptionStatuses
{
    public const string Active = "Active";
    /// <summary>A downgrade waiting for the current tier to end (AF13).</summary>
    public const string Scheduled = "Scheduled";
    public const string Expired = "Expired";
    public const string Cancelled = "Cancelled";
    /// <summary>Ended early by an upgrade (Step 10).</summary>
    public const string Replaced = "Replaced";
}

public static class SubscriptionPaymentStatuses
{
    public const string Pending = "Pending";
    public const string Success = "Success";
    public const string AmountMismatch = "AmountMismatch";
    public const string Expired = "Expired";
    /// <summary>Money arrived but cannot be applied (late or repeated payment); left to reconciliation (UC-FIN-02).</summary>
    public const string Unmatched = "Unmatched";
}

public enum SubscriptionChangeKind { New, Renew, Upgrade, Downgrade, SameTier }

public sealed record SubscriptionChange(SubscriptionChangeKind Kind, DateTime StartDate, DateTime EndDate);

public static class SubscriptionRules
{
    public const string PaymentPurpose = "SubscriptionPayment";
    public const string Gateway = "SePay";
    public static readonly TimeSpan PaymentWindow = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan WebhookGrace = TimeSpan.FromMinutes(5);
    /// <summary>Renewal of the current tier is open in the last 7 days (AF7); reminders go out 7 and 1 days before EndDate (AF9).</summary>
    public const int RenewalWindowDays = 7;
    public static readonly int[] ReminderDays = [7, 1];

    public const string Msg36 = "Gói hội viên '{0}' đã được kích hoạt đến {1:dd/MM/yyyy}.";
    public const string Msg37 = "Gói hội viên B2B của bạn đã hết hạn. Vui lòng gia hạn để tiếp tục sử dụng quyền lợi.";
    public const string Msg112 = "Thanh toán thất bại. Vui lòng thử lại.";
    public const string Msg113 = "Phiên thanh toán đã hết hạn. Vui lòng thực hiện lại thanh toán.";
    public const string Msg114 = "Số tiền thanh toán không khớp với số tiền cần trả.";
    public const string Msg115 = "Không thể xác minh kết quả thanh toán. Vui lòng thử lại sau.";
    public const string SameTier = "Bạn đang sử dụng gói này.";
    public const string Scheduled = "Gói mới sẽ có hiệu lực từ {0:dd/MM/yyyy}.";
    public const string NoTier = "Hiện chưa có gói hội viên nào đang mở bán.";
    public const string TierUnavailable = "Gói hội viên này đã ngừng áp dụng. Vui lòng chọn gói khác.";

    private static readonly Regex s_reference = new(@"SUB[0-9A-F]{10}",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Step 10 date rules. First or post-expiry subscription starts now; same tier renews from the current
    /// EndDate (only inside the renewal window unless it is already paid); a higher price replaces the current
    /// tier now without proration; a lower price is scheduled from the current EndDate.
    /// </summary>
    public static SubscriptionChange Decide(SubscriptionRecord? active, TierRecord tier, DateTime now,
        bool paid = false)
    {
        var duration = TimeSpan.FromDays(tier.DurationDays);
        if (active is null || active.EndDate <= now)
        {
            return new(SubscriptionChangeKind.New, now, now + duration);
        }

        if (active.PackageId == tier.PackageId)
        {
            bool due = (active.EndDate - now).TotalDays <= RenewalWindowDays;
            return due || paid
                ? new(SubscriptionChangeKind.Renew, active.EndDate, active.EndDate + duration)
                : new(SubscriptionChangeKind.SameTier, active.StartDate, active.EndDate);
        }

        return tier.Price < active.PriceSnapshot
            ? new(SubscriptionChangeKind.Downgrade, active.EndDate, active.EndDate + duration)
            : new(SubscriptionChangeKind.Upgrade, now, now + duration);
    }

    /// <summary>Transfer content the buyer must keep, e.g. "SUB4F9A0C21B7".</summary>
    public static string NewReference() =>
        "SUB" + Convert.ToHexString(RandomNumberGenerator.GetBytes(5));

    public static string? FindReference(IEnumerable<string?> texts)
    {
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            var match = s_reference.Match(text.Replace(" ", "").Replace("-", ""));
            if (match.Success) return match.Value.ToUpperInvariant();
        }
        return null;
    }

    /// <summary>Whole days between today and EndDate, used for the 7-day and 1-day reminders.</summary>
    public static int DaysLeft(DateTime endDate, DateTime now) => (endDate.Date - now.Date).Days;
}

public sealed record BuyerRecord(long WholesaleBuyerProfileId, long UserId, string VerificationStatus,
    string Email, string FullName);

public sealed record TierRecord(
    long PackageId,
    string PackageName,
    string? PackageDescription,
    int DurationDays,
    decimal Price,
    bool UnlocksFarmerDirectContact,
    bool CanDownloadContractTemplate,
    int? TransactionLimit,
    bool IsActive);

public sealed record SubscriptionRecord(
    long SubscriptionId,
    long PackageId,
    string PackageName,
    string Status,
    DateTime StartDate,
    DateTime EndDate,
    bool AutoRenew,
    decimal PriceSnapshot,
    int DurationDays,
    bool UnlocksFarmerDirectContact,
    bool CanDownloadContractTemplate,
    int? TransactionLimit);

public sealed record SubscriptionPaymentRecord(
    long PaymentTransactionId,
    string Reference,
    long WholesaleBuyerProfileId,
    long PackageId,
    bool AutoRenew,
    decimal Amount,
    string Status,
    DateTime RequestedAt,
    DateTime? ExpiresAt,
    long? SubscriptionId);

public sealed record NewSubscription(
    long WholesaleBuyerProfileId,
    TierRecord Tier,
    string Status,
    DateTime StartDate,
    DateTime EndDate,
    bool AutoRenew);

public sealed record RenewalCandidate(long SubscriptionId, long UserId, string Email, string FullName,
    string PackageName, DateTime EndDate);

public sealed record TierDto(
    long PackageId,
    string PackageName,
    string? PackageDescription,
    int DurationDays,
    decimal Price,
    bool UnlocksFarmerDirectContact,
    bool CanDownloadContractTemplate,
    int? TransactionLimit,
    string Action);

public sealed record SubscriptionDto(
    long SubscriptionId,
    long PackageId,
    string PackageName,
    string Status,
    DateTime StartDate,
    DateTime EndDate,
    bool AutoRenew,
    int DaysLeft,
    decimal PriceSnapshot,
    int DurationDays,
    bool UnlocksFarmerDirectContact,
    bool CanDownloadContractTemplate,
    int? TransactionLimit);

public sealed record MembershipOverviewDto(
    SubscriptionDto? Current,
    SubscriptionDto? Scheduled,
    bool IsExpired,
    string? Message,
    IReadOnlyList<TierDto> Tiers,
    string? EmptyMessage);

public sealed record PurchaseSubscriptionRequest(long PackageId, bool AutoRenew = false, bool ConfirmDowngrade = false);

public sealed record AutoRenewRequest(bool AutoRenew);

public sealed record PurchaseResultDto(string Status, string Message, SubscriptionDto? Subscription, PaymentQr? Payment);

public sealed record SubscriptionPaymentDto(string Reference, string Status, string? Message, decimal Amount,
    DateTime? ExpiresAt, SubscriptionDto? Subscription);

public interface IBuyerSubscriptionRepository
{
    Task<BuyerRecord?> GetBuyerAsync(long userId, CancellationToken cancellationToken);

    Task<BuyerRecord?> GetBuyerByProfileAsync(long buyerProfileId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TierRecord>> ListActiveTiersAsync(CancellationToken cancellationToken);

    Task<TierRecord?> FindTierAsync(long packageId, CancellationToken cancellationToken);

    /// <summary>Active and Scheduled subscriptions of the buyer plus the latest ended one.</summary>
    Task<IReadOnlyList<SubscriptionRecord>> GetSubscriptionsAsync(long buyerProfileId,
        CancellationToken cancellationToken);

    Task SetStatusAsync(long subscriptionId, string status, CancellationToken cancellationToken);

    Task<long> AddAsync(NewSubscription subscription, CancellationToken cancellationToken);

    /// <summary>Renewal of the same tier: moves EndDate and refreshes the snapshots (BR-ST-03).</summary>
    Task ExtendAsync(long subscriptionId, TierRecord tier, DateTime endDate, bool autoRenew, DateTime now,
        CancellationToken cancellationToken);

    Task SetAutoRenewAsync(long subscriptionId, bool autoRenew, CancellationToken cancellationToken);

    Task<SubscriptionPaymentRecord?> FindOpenPaymentAsync(long buyerProfileId, long packageId, DateTime now,
        CancellationToken cancellationToken);

    Task<SubscriptionPaymentRecord?> FindPaymentAsync(string reference, CancellationToken cancellationToken);

    Task AddPaymentAsync(SubscriptionPaymentRecord payment, CancellationToken cancellationToken);

    /// <summary>True when this gateway credit was already recorded (webhook retry).</summary>
    Task<bool> TransferRecordedAsync(string transactionCode, CancellationToken cancellationToken);

    /// <summary>Writes the gateway result onto the payment request (TransactionCode becomes the credit code).</summary>
    Task CompletePaymentAsync(long paymentTransactionId, string status, IncomingBankTransfer transfer,
        long? subscriptionId, DateTime now, CancellationToken cancellationToken);

    /// <summary>A second credit for an already closed request is kept for reconciliation.</summary>
    Task AddUnmatchedCreditAsync(IncomingBankTransfer transfer, long? subscriptionId, DateTime now,
        CancellationToken cancellationToken);

    Task ExpirePaymentAsync(long paymentTransactionId, CancellationToken cancellationToken);

    Task AddInAppNotificationAsync(long userId, string title, string content, string type, long? subscriptionId,
        string? eventKey, DateTime now, CancellationToken cancellationToken);

    Task<IReadOnlyList<RenewalCandidate>> ListRenewalCandidatesAsync(DateTime from, DateTime to,
        CancellationToken cancellationToken);

    Task<bool> NotificationExistsAsync(string eventKey, CancellationToken cancellationToken);
}

public interface IBuyerSubscriptionService
{
    Task<Result<MembershipOverviewDto>> GetOverviewAsync(CancellationToken cancellationToken);

    Task<Result<PurchaseResultDto>> PurchaseAsync(PurchaseSubscriptionRequest request,
        CancellationToken cancellationToken);

    Task<Result<SubscriptionPaymentDto>> GetPaymentAsync(string reference, CancellationToken cancellationToken);

    Task<Result<SubscriptionDto>> SetAutoRenewAsync(AutoRenewRequest request, CancellationToken cancellationToken);

    Task<Result<MembershipOverviewDto>> CancelScheduledAsync(CancellationToken cancellationToken);

    /// <summary>AF9: e-mails a renewal reminder 7 and 1 days before EndDate while AutoRenew is on. Returns the number sent.</summary>
    Task<int> SendRenewalRemindersAsync(string membershipLink, CancellationToken cancellationToken);
}

public sealed class BuyerSubscriptionService(
    IBuyerSubscriptionRepository repository,
    IPaymentQrProvider paymentQr,
    INotificationGateway notifications,
    IUseCaseUnitOfWork unitOfWork,
    ICurrentActor currentActor,
    TimeProvider timeProvider) : IBuyerSubscriptionService, IBankTransferHandler
{
    private static readonly Error s_signInRequired =
        new("UNAUTHORIZED", "Vui lòng đăng nhập để tiếp tục.", "Unauthorized");

    private static readonly Error s_notApproved =
        new("BUYER_NOT_APPROVED", SupplierDirectoryRules.BuyerNotApproved, "Forbidden");

    private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<Result<MembershipOverviewDto>> GetOverviewAsync(CancellationToken cancellationToken)
    {
        var (buyer, error) = await BuyerAsync(cancellationToken);
        if (buyer is null) return Result<MembershipOverviewDto>.FailureResult(error!);
        return Result<MembershipOverviewDto>.SuccessResult(await OverviewAsync(buyer, cancellationToken));
    }

    public async Task<Result<PurchaseResultDto>> PurchaseAsync(PurchaseSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var (buyer, error) = await BuyerAsync(cancellationToken);
        if (buyer is null) return Result<PurchaseResultDto>.FailureResult(error!);

        DateTime now = Now;
        var subscriptions = await RollOverAsync(buyer.WholesaleBuyerProfileId, now, cancellationToken);
        var active = subscriptions.FirstOrDefault(s => s.Status == SubscriptionStatuses.Active);

        // Step 5 / AF2: only an Active tier with its current configuration can be bought (BR-ST-02).
        var tier = request.PackageId > 0 ? await repository.FindTierAsync(request.PackageId, cancellationToken) : null;
        if (tier is null || !tier.IsActive)
        {
            return Result<PurchaseResultDto>.FailureResult(
                new Error("TIER_UNAVAILABLE", SubscriptionRules.TierUnavailable, "Conflict"));
        }

        var change = SubscriptionRules.Decide(active, tier, now);
        if (change.Kind == SubscriptionChangeKind.SameTier)
        {
            // AF7: no duplicate subscription or payment request.
            return Result<PurchaseResultDto>.FailureResult(
                new Error("SAME_TIER", SubscriptionRules.SameTier, "Conflict"));
        }

        if (change.Kind == SubscriptionChangeKind.Downgrade && !request.ConfirmDowngrade)
        {
            // AF13: the buyer confirms that the lower tier starts only at the current EndDate.
            return Result<PurchaseResultDto>.FailureResult(new Error("DOWNGRADE_CONFIRM_REQUIRED",
                string.Format(SubscriptionRules.Scheduled, ToVietnam(change.StartDate)), "Conflict"));
        }

        if (tier.Price <= 0)
        {
            // Step 6: a free tier is activated without a payment request.
            await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
            var applied = await ApplyAsync(buyer, tier, request.AutoRenew, now, null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PurchaseResultDto>.SuccessResult(new PurchaseResultDto(
                applied.Status == SubscriptionStatuses.Scheduled ? "Scheduled" : "Activated",
                ResultMessage(applied), applied, null));
        }

        // Step 7: reuse an open request for the same tier instead of issuing a second QR.
        var open = await repository.FindOpenPaymentAsync(buyer.WholesaleBuyerProfileId, tier.PackageId, now,
            cancellationToken);
        string reference = open?.Reference ?? SubscriptionRules.NewReference();
        DateTime expiresAt = open?.ExpiresAt ?? now + SubscriptionRules.PaymentWindow;
        decimal amount = open?.Amount ?? tier.Price;

        var qr = paymentQr.Create(reference, amount, expiresAt);
        if (qr is null)
        {
            // AF12: the current subscription stays unchanged.
            return Result<PurchaseResultDto>.FailureResult(
                new Error("PAYMENT_UNAVAILABLE", SubscriptionRules.Msg115, "ServiceUnavailable"));
        }

        if (open is null)
        {
            await repository.AddPaymentAsync(new SubscriptionPaymentRecord(0, reference,
                buyer.WholesaleBuyerProfileId, tier.PackageId, request.AutoRenew, amount,
                SubscriptionPaymentStatuses.Pending, now, expiresAt, null), cancellationToken);
        }

        string message = change.Kind == SubscriptionChangeKind.Downgrade
            ? string.Format(SubscriptionRules.Scheduled, ToVietnam(change.StartDate))
            : $"Quét mã QR để thanh toán {amount:N0} đ cho gói {tier.PackageName} ({tier.DurationDays} ngày).";
        return Result<PurchaseResultDto>.SuccessResult(new PurchaseResultDto("PaymentRequired", message, null, qr));
    }

    public async Task<Result<SubscriptionPaymentDto>> GetPaymentAsync(string reference,
        CancellationToken cancellationToken)
    {
        var (buyer, error) = await BuyerAsync(cancellationToken);
        if (buyer is null) return Result<SubscriptionPaymentDto>.FailureResult(error!);

        var payment = string.IsNullOrWhiteSpace(reference) ? null
            : await repository.FindPaymentAsync(reference.Trim().ToUpperInvariant(), cancellationToken);
        if (payment is null || payment.WholesaleBuyerProfileId != buyer.WholesaleBuyerProfileId)
        {
            return Result<SubscriptionPaymentDto>.FailureResult(
                new Error("PAYMENT_NOT_FOUND", "Không tìm thấy yêu cầu thanh toán.", "NotFound"));
        }

        string status = payment.Status;
        if (status == SubscriptionPaymentStatuses.Pending && payment.ExpiresAt <= Now)
        {
            // AF4: an expired session never activates the tier.
            await repository.ExpirePaymentAsync(payment.PaymentTransactionId, cancellationToken);
            status = SubscriptionPaymentStatuses.Expired;
        }

        SubscriptionDto? subscription = null;
        if (status == SubscriptionPaymentStatuses.Success && payment.SubscriptionId is long id)
        {
            var record = (await repository.GetSubscriptionsAsync(buyer.WholesaleBuyerProfileId, cancellationToken))
                .FirstOrDefault(s => s.SubscriptionId == id);
            subscription = record is null ? null : ToDto(record, Now);
        }

        string? message = status switch
        {
            SubscriptionPaymentStatuses.Success => subscription is null ? "Thanh toán thành công."
                : ResultMessage(subscription),
            SubscriptionPaymentStatuses.Expired => SubscriptionRules.Msg113,
            SubscriptionPaymentStatuses.AmountMismatch => SubscriptionRules.Msg114,
            SubscriptionPaymentStatuses.Unmatched => SubscriptionRules.Msg115,
            SubscriptionPaymentStatuses.Pending => null,
            _ => SubscriptionRules.Msg112
        };
        return Result<SubscriptionPaymentDto>.SuccessResult(new SubscriptionPaymentDto(
            payment.Reference, status, message, payment.Amount, payment.ExpiresAt, subscription));
    }

    public async Task<Result<SubscriptionDto>> SetAutoRenewAsync(AutoRenewRequest request,
        CancellationToken cancellationToken)
    {
        var (buyer, error) = await BuyerAsync(cancellationToken);
        if (buyer is null) return Result<SubscriptionDto>.FailureResult(error!);

        DateTime now = Now;
        var active = (await RollOverAsync(buyer.WholesaleBuyerProfileId, now, cancellationToken))
            .FirstOrDefault(s => s.Status == SubscriptionStatuses.Active);
        if (active is null)
        {
            return Result<SubscriptionDto>.FailureResult(new Error("NO_ACTIVE_SUBSCRIPTION",
                "Bạn chưa có gói hội viên đang hiệu lực.", "NotFound"));
        }

        // Step 12 / AF8 / AF9: only the setting changes; tier and validity period stay as they are.
        await repository.SetAutoRenewAsync(active.SubscriptionId, request.AutoRenew, cancellationToken);
        return Result<SubscriptionDto>.SuccessResult(ToDto(active with { AutoRenew = request.AutoRenew }, now));
    }

    public async Task<Result<MembershipOverviewDto>> CancelScheduledAsync(CancellationToken cancellationToken)
    {
        var (buyer, error) = await BuyerAsync(cancellationToken);
        if (buyer is null) return Result<MembershipOverviewDto>.FailureResult(error!);

        var scheduled = (await RollOverAsync(buyer.WholesaleBuyerProfileId, Now, cancellationToken))
            .FirstOrDefault(s => s.Status == SubscriptionStatuses.Scheduled);
        if (scheduled is null)
        {
            return Result<MembershipOverviewDto>.FailureResult(new Error("NO_SCHEDULED_CHANGE",
                "Không có gói nào đang chờ hiệu lực.", "NotFound"));
        }

        // AF13: the scheduled change can be cancelled before it starts.
        await repository.SetStatusAsync(scheduled.SubscriptionId, SubscriptionStatuses.Cancelled, cancellationToken);
        return Result<MembershipOverviewDto>.SuccessResult(await OverviewAsync(buyer, cancellationToken));
    }

    /// <summary>Step 9-10: SePay webhook credit carrying a "SUB..." reference.</summary>
    public async Task<bool> TryHandleAsync(IncomingBankTransfer transfer, CancellationToken cancellationToken)
    {
        string? reference = SubscriptionRules.FindReference(transfer.ReferenceTexts);
        if (reference is null) return false;
        if (await repository.TransferRecordedAsync(transfer.TransactionCode, cancellationToken)) return true;

        var payment = await repository.FindPaymentAsync(reference, cancellationToken);
        if (payment is null) return false;

        DateTime now = Now;
        if (payment.Status != SubscriptionPaymentStatuses.Pending)
        {
            await repository.AddUnmatchedCreditAsync(transfer, payment.SubscriptionId, now, cancellationToken);
            return true;
        }

        // A short grace period covers the delay between the bank transfer and the webhook.
        if (payment.ExpiresAt is DateTime expires && now > expires + SubscriptionRules.WebhookGrace)
        {
            // AF4: a payment after the session expired does not activate the tier.
            await repository.CompletePaymentAsync(payment.PaymentTransactionId,
                SubscriptionPaymentStatuses.Unmatched, transfer, null, now, cancellationToken);
            return true;
        }

        if (transfer.Amount != payment.Amount)
        {
            // AF5: wrong amount is not a successful payment; reconciliation handles it (UC-FIN-02).
            await repository.CompletePaymentAsync(payment.PaymentTransactionId,
                SubscriptionPaymentStatuses.AmountMismatch, transfer, null, now, cancellationToken);
            return true;
        }

        var buyer = await repository.GetBuyerByProfileAsync(payment.WholesaleBuyerProfileId, cancellationToken);
        var tier = await repository.FindTierAsync(payment.PackageId, cancellationToken);
        if (buyer is null || tier is null)
        {
            await repository.CompletePaymentAsync(payment.PaymentTransactionId,
                SubscriptionPaymentStatuses.Unmatched, transfer, null, now, cancellationToken);
            return true;
        }

        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        await ApplyAsync(buyer, tier, payment.AutoRenew, now, transfer, cancellationToken, payment.PaymentTransactionId);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task<int> SendRenewalRemindersAsync(string membershipLink, CancellationToken cancellationToken)
    {
        DateTime now = Now;
        int maxDays = SubscriptionRules.ReminderDays.Max();
        var candidates = await repository.ListRenewalCandidatesAsync(now, now.Date.AddDays(maxDays + 1),
            cancellationToken);
        int sent = 0;
        foreach (var candidate in candidates)
        {
            int days = SubscriptionRules.DaysLeft(candidate.EndDate, now);
            if (!SubscriptionRules.ReminderDays.Contains(days)) continue;

            string eventKey = $"SUB-REMIND:{candidate.SubscriptionId}:{candidate.EndDate:yyyyMMdd}:{days}";
            if (await repository.NotificationExistsAsync(eventKey, cancellationToken)) continue;

            string title = $"Gói hội viên {candidate.PackageName} sẽ hết hạn sau {days} ngày";
            string content = $"Gói hội viên B2B '{candidate.PackageName}' của bạn hết hạn ngày "
                + $"{ToVietnam(candidate.EndDate):dd/MM/yyyy}. Thanh toán gia hạn qua chuyển khoản SePay/VietQR "
                + $"để tiếp tục sử dụng quyền lợi: {membershipLink}";
            try
            {
                await notifications.SendEmailAsync(candidate.Email, title, content, eventKey, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception)
            {
                // The next run retries: nothing is recorded for this reminder yet.
                continue;
            }

            await repository.AddInAppNotificationAsync(candidate.UserId, title, content,
                "SubscriptionRenewalReminder", candidate.SubscriptionId, eventKey, now, cancellationToken);
            sent++;
        }
        return sent;
    }

    private async Task<(BuyerRecord? Buyer, Error? Error)> BuyerAsync(CancellationToken cancellationToken)
    {
        if (currentActor.UserId <= 0) return (null, s_signInRequired);
        var buyer = await repository.GetBuyerAsync(currentActor.UserId, cancellationToken);
        return buyer is null || buyer.VerificationStatus != VerificationStatuses.Approved
            ? (null, s_notApproved)
            : (buyer, null);
    }

    /// <summary>
    /// Ends Active rows past their EndDate (AF11) and starts a Scheduled downgrade whose StartDate has come (AF13).
    /// The expired row is closed first so the "one Active per buyer" index is never violated.
    /// </summary>
    private async Task<IReadOnlyList<SubscriptionRecord>> RollOverAsync(long buyerProfileId, DateTime now,
        CancellationToken cancellationToken)
    {
        var subscriptions = await repository.GetSubscriptionsAsync(buyerProfileId, cancellationToken);
        bool changed = false;
        foreach (var ended in subscriptions.Where(s => s.Status == SubscriptionStatuses.Active && s.EndDate <= now))
        {
            await repository.SetStatusAsync(ended.SubscriptionId, SubscriptionStatuses.Expired, cancellationToken);
            changed = true;
        }

        bool hasActive = subscriptions.Any(s => s.Status == SubscriptionStatuses.Active && s.EndDate > now);
        foreach (var due in subscriptions.Where(s => s.Status == SubscriptionStatuses.Scheduled && s.StartDate <= now)
                     .OrderBy(s => s.StartDate))
        {
            string status = !hasActive && due.EndDate > now ? SubscriptionStatuses.Active : SubscriptionStatuses.Expired;
            await repository.SetStatusAsync(due.SubscriptionId, status, cancellationToken);
            hasActive |= status == SubscriptionStatuses.Active;
            changed = true;
        }

        return changed ? await repository.GetSubscriptionsAsync(buyerProfileId, cancellationToken) : subscriptions;
    }

    /// <summary>Step 10: creates or updates the subscription and stores the snapshots (DBR-14, BR-ST-03).</summary>
    private async Task<SubscriptionDto> ApplyAsync(BuyerRecord buyer, TierRecord tier, bool autoRenew, DateTime now,
        IncomingBankTransfer? transfer, CancellationToken cancellationToken, long? paymentTransactionId = null)
    {
        var subscriptions = await RollOverAsync(buyer.WholesaleBuyerProfileId, now, cancellationToken);
        var active = subscriptions.FirstOrDefault(s => s.Status == SubscriptionStatuses.Active);
        var scheduled = subscriptions.FirstOrDefault(s => s.Status == SubscriptionStatuses.Scheduled);
        var change = SubscriptionRules.Decide(active, tier, now, paid: transfer is not null);

        long subscriptionId;
        string status = SubscriptionStatuses.Active;
        switch (change.Kind)
        {
            case SubscriptionChangeKind.Renew:
                await repository.ExtendAsync(active!.SubscriptionId, tier, change.EndDate, autoRenew, now,
                    cancellationToken);
                subscriptionId = active.SubscriptionId;
                break;
            case SubscriptionChangeKind.Upgrade:
                // No refund or proration of the unused period.
                await repository.SetStatusAsync(active!.SubscriptionId, SubscriptionStatuses.Replaced, cancellationToken);
                if (scheduled is not null)
                {
                    await repository.SetStatusAsync(scheduled.SubscriptionId, SubscriptionStatuses.Cancelled,
                        cancellationToken);
                }
                subscriptionId = await repository.AddAsync(new NewSubscription(buyer.WholesaleBuyerProfileId, tier,
                    status, change.StartDate, change.EndDate, autoRenew), cancellationToken);
                break;
            case SubscriptionChangeKind.Downgrade:
                if (scheduled is not null)
                {
                    await repository.SetStatusAsync(scheduled.SubscriptionId, SubscriptionStatuses.Cancelled,
                        cancellationToken);
                }
                status = SubscriptionStatuses.Scheduled;
                subscriptionId = await repository.AddAsync(new NewSubscription(buyer.WholesaleBuyerProfileId, tier,
                    status, change.StartDate, change.EndDate, autoRenew), cancellationToken);
                break;
            default:
                subscriptionId = await repository.AddAsync(new NewSubscription(buyer.WholesaleBuyerProfileId, tier,
                    status, change.StartDate, change.EndDate, autoRenew), cancellationToken);
                break;
        }

        if (transfer is not null && paymentTransactionId is long paymentId)
        {
            await repository.CompletePaymentAsync(paymentId, SubscriptionPaymentStatuses.Success, transfer,
                subscriptionId, now, cancellationToken);
        }

        var dto = new SubscriptionDto(subscriptionId, tier.PackageId, tier.PackageName, status, change.StartDate,
            change.EndDate, autoRenew, Math.Max(0, SubscriptionRules.DaysLeft(change.EndDate, now)), tier.Price,
            tier.DurationDays, tier.UnlocksFarmerDirectContact, tier.CanDownloadContractTemplate, tier.TransactionLimit);

        // Step 11: MSG36 with the plan name and expiration date.
        await repository.AddInAppNotificationAsync(buyer.UserId, "Kích hoạt gói hội viên B2B", ResultMessage(dto),
            "SubscriptionActivated", subscriptionId, null, now, cancellationToken);
        return dto;
    }

    private async Task<MembershipOverviewDto> OverviewAsync(BuyerRecord buyer, CancellationToken cancellationToken)
    {
        DateTime now = Now;
        var subscriptions = await RollOverAsync(buyer.WholesaleBuyerProfileId, now, cancellationToken);
        var active = subscriptions.FirstOrDefault(s => s.Status == SubscriptionStatuses.Active);
        var scheduled = subscriptions.FirstOrDefault(s => s.Status == SubscriptionStatuses.Scheduled);
        bool expired = active is null && subscriptions.Any(s => s.Status == SubscriptionStatuses.Expired);

        var tiers = (await repository.ListActiveTiersAsync(cancellationToken))
            .OrderBy(t => t.Price).ThenBy(t => t.PackageName)
            .Select(t => new TierDto(t.PackageId, t.PackageName, t.PackageDescription, t.DurationDays, t.Price,
                t.UnlocksFarmerDirectContact, t.CanDownloadContractTemplate, t.TransactionLimit,
                ActionFor(active, scheduled, t, now)))
            .ToList();

        return new MembershipOverviewDto(
            active is null ? null : ToDto(active, now),
            scheduled is null ? null : ToDto(scheduled, now),
            expired,
            expired ? SubscriptionRules.Msg37 : null,
            tiers,
            tiers.Count == 0 ? SubscriptionRules.NoTier : null);
    }

    private static string ActionFor(SubscriptionRecord? active, SubscriptionRecord? scheduled, TierRecord tier,
        DateTime now)
    {
        if (scheduled?.PackageId == tier.PackageId) return "Scheduled";
        return SubscriptionRules.Decide(active, tier, now).Kind switch
        {
            SubscriptionChangeKind.SameTier => "Current",
            SubscriptionChangeKind.Renew => "Renew",
            SubscriptionChangeKind.Upgrade => "Upgrade",
            SubscriptionChangeKind.Downgrade => "Downgrade",
            _ => "Subscribe"
        };
    }

    private static SubscriptionDto ToDto(SubscriptionRecord s, DateTime now) =>
        new(s.SubscriptionId, s.PackageId, s.PackageName, s.Status, s.StartDate, s.EndDate, s.AutoRenew,
            Math.Max(0, SubscriptionRules.DaysLeft(s.EndDate, now)), s.PriceSnapshot, s.DurationDays,
            s.UnlocksFarmerDirectContact, s.CanDownloadContractTemplate, s.TransactionLimit);

    private static string ResultMessage(SubscriptionDto s) =>
        s.Status == SubscriptionStatuses.Scheduled
            ? string.Format(SubscriptionRules.Scheduled, ToVietnam(s.StartDate))
            : string.Format(SubscriptionRules.Msg36, s.PackageName, ToVietnam(s.EndDate));

    /// <summary>Dates are stored in UTC and shown in Vietnam time (UTC+7).</summary>
    private static DateTime ToVietnam(DateTime utc) => utc.AddHours(7);
}
