using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.DTOs.Products;
using Xunit;

namespace AgriSustain.IntegrationTests.Commerce;
public sealed partial class CommerceApiTests
{
    [Fact,Trait("UseCase","UC-FAR-03"),Trait("CaseId","IT-FAR-03-REV-001")]
    public async Task DirectApi_UnsupportedUnitCannotBypassDropdown()
    {
        await using var api=await Api.Create();
        await Error(await api.Client.PostAsJsonAsync("api/farmer/products",api.Data.Request with {Unit="bag"}),HttpStatusCode.BadRequest,"VALIDATION");
        Assert.Equal(0,api.Data.Products.Adds);Assert.Equal(0,api.Data.Work.Commits);
    }

    [Theory,Trait("UseCase","UC-FAR-06"),Trait("CaseId","IT-FAR-06-REV-001")]
    [InlineData("x"),InlineData("  x  "),InlineData("short reason")]
    public async Task RejectApi_UnpaidShortReasonIsAccepted(string reason)
    {
        await using var api=await Api.Create();api.Data.Seller.Order.PaymentTransactions.Clear();
        var response=await Read<SellerOrderResponse>(await api.Client.PostAsJsonAsync("api/farmer/orders/501/reject",api.Data.Action("Quality",reason)));
        Assert.Equal("Cancelled",response.Status);Assert.Empty(api.Data.Seller.Order.PaymentTransactions);
        Assert.Equal(0,api.Data.Batch.ReservedQuantity);Assert.Single(api.Data.Orders.Audits);
    }

    [Fact,Trait("UseCase","UC-FAR-06"),Trait("CaseId","IT-FAR-06-REV-002")]
    public async Task PackingSlipApi_UsesCoordinatorAndCheckoutSnapshots()
    {
        await using var api=await Api.Create();
        var slip=await Read<PackingSlipResponse>(await api.Client.GetAsync("api/farmer/orders/501/packing-slip"));
        Assert.Equal(api.Data.Seller.FinalAmount,slip.FinalAmount);Assert.Equal(api.Data.Seller.DeliveryFee,slip.DeliveryFee);
        Assert.Equal(api.Data.Seller.DiscountShare,slip.DiscountShare);Assert.Empty(api.Data.Orders.Audits);
        Assert.Equal("Pending",api.Data.Seller.Status);
    }
}
