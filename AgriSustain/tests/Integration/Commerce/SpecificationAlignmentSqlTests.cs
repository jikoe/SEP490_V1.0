using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.Commerce;
public sealed partial class CommerceSqlTests
{
    [CommerceSqlFact,Trait("UseCase","UC-FAR-06"),Trait("CaseId","IT-FAR-06-REV-003")]
    public async Task PaidRejection_ReplayDoesNotRepeatRefundOrNotifications()
    {
        await using var f=await CommerceDatabase.Create();var action=await f.Action("Quality","x");
        var result=await Ok<SellerOrderResponse>(await f.Farmer().PostAsJsonAsync($"api/farmer/orders/{f.SellerId}/reject",action));
        // The refund waits for Finance (UC-FIN-03), so nothing is refunded yet.
        Assert.False(result.IsRefunded);Assert.True(result.NotificationPending);Assert.Equal(0,result.RefundedAmount);
        await Error(await f.Farmer().PostAsJsonAsync($"api/farmer/orders/{f.SellerId}/reject",action),HttpStatusCode.Conflict);
        await using var db=f.Open();var refund=Assert.Single(await db.RefundTransactions.ToListAsync());
        Assert.Equal(("Pending",160000m),(refund.RefundStatus,refund.Amount));
        Assert.Equal(2,await db.Notifications.CountAsync(n=>n.NotificationType=="FarmerOrderUpdate"));
        Assert.Equal(0,(await db.HarvestBatches.SingleAsync()).ReservedQuantity);
    }

    [CommerceSqlFact,Trait("UseCase","UC-FAR-06"),Trait("CaseId","IT-FAR-06-REV-004")]
    public async Task InactiveReceivingBank_StillRejects_RefundAwaitsAccount()
    {
        await using var f=await CommerceDatabase.Create();
        await f.Edit(async db=>(await db.SupportedBanks.SingleAsync()).IsActive=false);
        await Ok<SellerOrderResponse>(await f.Farmer().PostAsJsonAsync($"api/farmer/orders/{f.SellerId}/reject",await f.Action("Quality","x")));
        await using var db=f.Open();Assert.Equal("Cancelled",(await db.SellerOrders.SingleAsync()).Status);
        Assert.Equal(0,(await db.HarvestBatches.SingleAsync()).ReservedQuantity);
        // No usable account: Finance sees "Awaiting customer account" until the buyer provides one (UC-FIN-08).
        var refund=Assert.Single(await db.RefundTransactions.ToListAsync());
        Assert.Equal("Pending",refund.RefundStatus);Assert.Null(refund.ReceiverAccountNumber);
    }

    [CommerceSqlFact,Trait("UseCase","UC-FAR-06"),Trait("CaseId","IT-FAR-06-REV-005")]
    public async Task PaidRejection_SubtractsAlreadyCommittedRefund()
    {
        await using var f=await CommerceDatabase.Create();
        await f.Edit(async db=> {
            var payment=await db.PaymentTransactions.SingleAsync();
            db.RefundTransactions.Add(new RefundTransaction {PaymentTransactionId=payment.PaymentTransactionId,
                ProcessedByUserId=f.FarmerId,RefundCode="EXISTING-PENDING",Amount=50000,RefundStatus="Pending",RequestKey="existing-pending"});
        });
        var response=await Ok<SellerOrderResponse>(await f.Farmer().PostAsJsonAsync($"api/farmer/orders/{f.SellerId}/reject",await f.Action("Quality","x")));
        Assert.Equal(0,response.RefundedAmount);
        await using var db=f.Open();Assert.Equal(160000,await db.RefundTransactions.SumAsync(r=>r.Amount));
        Assert.Equal(110000,(await db.RefundTransactions.SingleAsync(r=>r.RequestKey.StartsWith("farmer-reject:"))).Amount);
    }

    [CommerceSqlFact,Trait("UseCase","UC-FIN-08"),Trait("CaseId","IT-FIN-08-F3")]
    public async Task FarmerRejectionRefund_ShowsFarmAndRejectionTimeToBuyer()
    {
        await using var f=await CommerceDatabase.Create();
        await Ok<SellerOrderResponse>(await f.Farmer().PostAsJsonAsync($"api/farmer/orders/{f.SellerId}/reject",await f.Action("Quality","x")));
        await using var db=f.Open();
        var refund=await db.RefundTransactions.SingleAsync(r=>r.RequestKey.StartsWith("farmer-reject:"));

        var record=await new AgriSustain.Infrastructure.Repositories.RefundAccountRepository(db).FindMineAsync(f.BuyerId,refund.RefundTransactionId,default);

        Assert.NotNull(record);
        Assert.Equal("Test farm",record!.FarmName);
        Assert.NotNull(record.ApprovedAt);
    }
}

