using AgriSustain.Domain.Entities;
using Xunit;

namespace AgriSustain.UnitTests.Payouts;

/// <summary>BR-UM-07 / BR-UM-08 / BR-CS-05: payout holds derived when Finance pays (UC-FIN-04).</summary>
public sealed class FarmerPayoutEligibilityTests
{
    private static readonly DateTime Now = new(2026, 11, 6, 3, 0, 0, DateTimeKind.Utc);

    private static FarmerPayout Payout(string farmerStatus = "Active", bool openComplaint = false)
    {
        var sellerOrder = new SellerOrder { SellerOrderId = 3, SellerOrderCode = "SO-3" };
        if (openComplaint)
        {
            sellerOrder.DisputeTickets.Add(new DisputeTicket { TicketStatus = DisputeTicket.StatusOpen });
        }
        sellerOrder.DisputeTickets.Add(new DisputeTicket { TicketStatus = DisputeTicket.StatusRejected });
        var payout = new FarmerPayout
        {
            FarmerProfile = new FarmerProfile { User = new User { Status = farmerStatus } }
        };
        payout.PayoutItems.Add(new PayoutItem { OrderItem = new OrderItem { SellerOrder = sellerOrder } });
        return payout;
    }

    [Fact(DisplayName = "[UC-ADM-03 BR-UM-07] An Active Farmer with no other reason can be paid")]
    public void Active_NoBlockers()
    {
        Assert.Empty(Payout().Blockers(new UserBankAccount(), Now));
    }

    [Fact(DisplayName = "[UC-ADM-03 BR-UM-07] A Suspended Farmer cannot be paid")]
    public void Suspended_IsBlocked()
    {
        var block = Assert.Single(Payout("Suspended").Blockers(null, Now));

        Assert.Equal(FarmerPayout.BlockAccountSuspended, block.Reason);
    }

    [Fact(DisplayName = "[UC-ADM-03 BR-UM-08] Reactivate lifts only the suspension; an open complaint still holds the payout")]
    public void Reactivated_ComplaintStillHolds()
    {
        var payout = Payout("Suspended", openComplaint: true);
        Assert.Equal(2, payout.Blockers(null, Now).Count);

        payout.FarmerProfile.User.Status = "Active";
        var block = Assert.Single(payout.Blockers(null, Now));

        Assert.Equal(FarmerPayout.BlockOpenComplaint, block.Reason);
        Assert.Contains("SO-3", block.Message);
    }

    [Theory(DisplayName = "[UC-FIN-04] A bank account changed within 48 hours holds the payout until the hold ends")]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    public void BankAccountChange_HoldsUntilExpiry(int hours, bool blocked)
    {
        var account = new UserBankAccount { PayoutHoldUntil = Now.AddHours(hours) };

        var blocks = Payout().Blockers(account, Now);

        Assert.Equal(blocked, blocks.Any(b => b.Reason == FarmerPayout.BlockBankAccountChanged));
    }
}
