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
        Assert.True(result.IsRefunded);Assert.True(result.NotificationPending);Assert.Equal(160000,result.RefundedAmount);
        await Error(await f.Farmer().PostAsJsonAsync($"api/farmer/orders/{f.SellerId}/reject",action),HttpStatusCode.Conflict);
        await using var db=f.Open();Assert.Single(await db.RefundTransactions.ToListAsync());
        Assert.Equal(2,await db.Notifications.CountAsync(n=>n.NotificationType=="FarmerOrderUpdate"));
        Assert.Equal(0,(await db.HarvestBatches.SingleAsync()).ReservedQuantity);
    }

    [CommerceSqlFact,Trait("UseCase","UC-FAR-06"),Trait("CaseId","IT-FAR-06-REV-004")]
    public async Task InactiveReceivingBank_DoesNotCancelOrReleaseStock()
    {
        await using var f=await CommerceDatabase.Create();
        await f.Edit(async db=>(await db.SupportedBanks.SingleAsync()).IsActive=false);
        await Error(await f.Farmer().PostAsJsonAsync($"api/farmer/orders/{f.SellerId}/reject",await f.Action("Quality","x")),HttpStatusCode.InternalServerError);
        await using var db=f.Open();Assert.Equal("Pending",(await db.SellerOrders.SingleAsync()).Status);
        Assert.Equal(5,(await db.HarvestBatches.SingleAsync()).ReservedQuantity);
        Assert.Empty(await db.RefundTransactions.ToListAsync());Assert.Empty(await db.Notifications.ToListAsync());
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
        Assert.Equal(110000,response.RefundedAmount);
        await using var db=f.Open();Assert.Equal(160000,await db.RefundTransactions.SumAsync(r=>r.Amount));
    }
}
