using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.DTOs.Payouts;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services;

// Outbox entries are saved with the financial action. The existing worker sends real SMTP email.
public sealed class PayoutNotificationService(IFinanceFarmerPayoutRepository repository,
    IFarmerPaymentRequestRepository access, IConfiguration configuration)
{
    public async Task SubmittedAsync(FarmerPayoutContext context, FarmerPayout payout, CancellationToken ct)
    {
        string link = configuration["App:ClientBaseUrl"] ?? configuration["ClientUrl"] ?? "http://localhost:5173";
        link = link.TrimEnd('/') + "/FinanceStaff/farmer-payouts.html?requestId=" + payout.FarmerPayoutId;
        string content = FinancePayoutMessages.Submission(context.Farmer.FarmName, payout.PayoutCode,
            payout.NetAmount, DateTime.UtcNow, link);
        Queue(await access.FinanceRecipientsAsync(ct), payout, "Submitted",
            FinancePayoutMessages.SubmissionTitle(payout.PayoutCode), content);
    }

    public void Rejected(FarmerPayoutContext context, string reason) => Queue([context.Farmer.UserId], context.Payout!,
        "Rejected", FinancePayoutMessages.RejectionTitle(context.Payout!.PayoutCode),
        FinancePayoutMessages.Rejection(context.Payout.PayoutCode, reason));

    public void Paid(FarmerPayoutContext context, PayoutOutgoingTransfer transfer) => Queue([context.Farmer.UserId],
        context.Payout!, transfer.EventId, FinancePayoutMessages.PaidTitle(context.Payout!.PayoutCode),
        FinancePayoutMessages.Payment(context.Payout, transfer));

    private void Queue(IEnumerable<long> users, FarmerPayout payout, string eventKey, string title, string content)
    {
        repository.Queue(users.Distinct().SelectMany(user => new[] { "InApp", "Email" }.Select(channel => new Notification
        {
            UserId = user, Title = title, Content = content, NotificationType = "FinanceFarmerPayout",
            DeliveryChannel = channel, DeliveryStatus = channel == "InApp" ? "Sent" : "Pending",
            SentAt = DateTime.UtcNow, RelatedRecordType = "FarmerPayout", RelatedRecordId = payout.FarmerPayoutId,
            PayloadJson = JsonSerializer.Serialize(new { EventKey = eventKey, payout.PayoutCode })
        })));
    }
}
