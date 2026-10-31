using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.Application.Validators;
using AgriSustain.Infrastructure.Payments;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace AgriSustain.UnitTests.Commerce;

public sealed class SpecificationAlignmentTests
{
    [Theory, Trait("UseCase", "UC-FAR-03"), Trait("CaseId", "UT-FAR-03-REV-001")]
    [InlineData("bag"), InlineData("unknown"), InlineData(" kg ")]
    public async Task UnsupportedUnit_IsRejectedWithoutSaving(string unit)
    {
        var f = new CommerceFixture();
        var result = await f.FarmerProductService.SaveProductAsync(101,null,f.Request with { Unit=unit },default);
        Assert.False(result.IsSuccess); Assert.Equal(0,f.Work.Commits); Assert.Equal(0,f.Products.Adds);
    }

    [Theory, Trait("UseCase", "UC-FAR-03"), Trait("CaseId", "UT-FAR-03-REV-002")]
    [InlineData("g"),InlineData("kg"),InlineData("tạ"),InlineData("tấn"),InlineData("ml"),InlineData("lít")]
    [InlineData("cái"),InlineData("quả"),InlineData("củ"),InlineData("bắp"),InlineData("bó"),InlineData("chùm")]
    [InlineData("buồng"),InlineData("cây"),InlineData("cành"),InlineData("bông"),InlineData("con")]
    [InlineData("gói"),InlineData("túi"),InlineData("hộp"),InlineData("khay"),InlineData("vỉ"),InlineData("chai")]
    [InlineData("lọ"),InlineData("hũ"),InlineData("lon"),InlineData("can"),InlineData("bao"),InlineData("thùng"),InlineData("sọt"),InlineData("rổ")]
    public async Task DropdownUnit_IsAcceptedByValidator(string unit)
    {
        var f=new CommerceFixture();
        var result=await new ProductRequestValidator().ValidateAsync(f.Request with {Unit=unit});
        Assert.DoesNotContain(result.Errors,e=>e.PropertyName=="Unit");
    }

    [Theory, Trait("UseCase", "UC-FAR-06"), Trait("CaseId", "UT-FAR-06-REV-001")]
    [InlineData(""),InlineData(" "),InlineData("\t\r\n")]
    public async Task RejectWhitespace_LeavesOrderAndStockUnchanged(string explanation)
    {
        var f=new CommerceFixture();
        var result=await f.OrderService.ActAsync(101,501,"reject",f.Action() with {Explanation=explanation},default);
        Assert.False(result.IsSuccess);Assert.Equal("Pending",f.Seller.Status);
        Assert.Equal(5,f.Batch.ReservedQuantity);Assert.Equal(0,f.Work.Commits);Assert.Empty(f.Orders.Notifications);
    }

    [Theory, Trait("UseCase", "UC-FAR-06"), Trait("CaseId", "UT-FAR-06-REV-002")]
    [InlineData("x"),InlineData("  x  "),InlineData("short reason")]
    public async Task RejectUnpaidShortReason_CancelsWithoutRefund(string explanation)
    {
        var f=new CommerceFixture();f.Seller.Order.PaymentTransactions.Clear();
        var result=await f.OrderService.ActAsync(101,501,"reject",f.Action() with {Explanation=explanation},default);
        Assert.True(result.IsSuccess,result.Error?.Message);Assert.Equal("Cancelled",f.Seller.Status);
        Assert.Equal(0,f.Batch.ReservedQuantity);Assert.Equal(1,f.Work.Commits);
    }

    [Fact, Trait("UseCase", "UC-FAR-06"), Trait("CaseId", "UT-FAR-06-REV-003")]
    public async Task DemoGateway_ReplayKeepsSameTransferReference()
    {
        var gateway=new PaymentGatewayProxy(new DemoEnvironment());
        var request=new RefundTransferRequest(10,100,"VCB","0123456789","TEST BUYER");
        var first=await gateway.RefundAsync(request,"order-1-payment-10",default);
        var second=await gateway.RefundAsync(request,"order-1-payment-10",default);
        Assert.Equal(first,second);Assert.StartsWith("DEMO-",first.Reference);Assert.Equal(100,first.Amount);
    }

    [Theory, Trait("UseCase", "UC-FAR-06"), Trait("CaseId", "UT-FAR-06-REV-004")]
    [InlineData(0,"VCB","0123456789","BUYER")]
    [InlineData(100,"","0123456789","BUYER")]
    [InlineData(100,"VCB","abc123456","BUYER")]
    [InlineData(100,"VCB","12345","BUYER")]
    [InlineData(100,"VCB","0123456789"," ")]
    public async Task DemoGateway_InvalidDestinationOrAmountDoesNotConfirm(int amount,string bank,string number,string holder)
    {
        var gateway=new PaymentGatewayProxy(new DemoEnvironment());
        await Assert.ThrowsAsync<InvalidOperationException>(()=>gateway.RefundAsync(new(10,amount,bank,number,holder),"key",default));
    }

    [Fact, Trait("UseCase", "UC-FAR-06"), Trait("CaseId", "UT-FAR-06-REV-005")]
    public async Task DemoGateway_IsDisabledInProduction()
    {
        var gateway=new PaymentGatewayProxy(new DemoEnvironment{EnvironmentName="Production"});
        await Assert.ThrowsAsync<InvalidOperationException>(()=>gateway.RefundAsync(new(10,100,"VCB","0123456789","BUYER"),"key",default));
    }

    [Fact,Trait("UseCase","UC-FAR-03"),Trait("CaseId","UT-FAR-03-REV-003")]
    public async Task SubmissionNotification_QueuesTwoChannelsForEachModerator()
    {
        var f=new CommerceFixture();var repo=new NotificationDouble {Recipients=[201,202]};
        var service=new ProductNotificationService(repo,f.Clock);
        Assert.True(await service.NotifyModeratorsAsync(f.Product,"event-1",default));
        Assert.Equal(4,repo.Rows.Count);
        foreach(var id in repo.Recipients) {
            var rows=repo.Rows.Where(n=>n.UserId==id).ToList();Assert.Equal(2,rows.Count);
            Assert.Equal("Sent",Assert.Single(rows,n=>n.DeliveryChannel=="InApp").DeliveryStatus);
            Assert.Equal("Pending",Assert.Single(rows,n=>n.DeliveryChannel=="Email").DeliveryStatus);
            Assert.All(rows,n=> {Assert.Equal(f.Product.ProductId,n.RelatedRecordId);Assert.Contains("event-1",n.PayloadJson);Assert.Contains("ReviewPath",n.PayloadJson);});
        }
    }
    [Fact,Trait("UseCase","UC-FAR-03"),Trait("CaseId","UT-FAR-03-REV-004")]
    public async Task SubmissionNotification_NoModeratorDoesNotInventRecipient()
    {
        var f=new CommerceFixture();var repo=new NotificationDouble();
        Assert.False(await new ProductNotificationService(repo,f.Clock).NotifyModeratorsAsync(f.Product,"event-1",default));
        Assert.Empty(repo.Rows);
    }
    private sealed class NotificationDouble:IProductNotificationRepository
    {
        public IReadOnlyList<long> Recipients=[];
        public List<Notification> Rows=[];
        public Task<IReadOnlyList<long>> GetModeratorIdsAsync(CancellationToken ct)=>Task.FromResult(Recipients);
        public void Add(Notification row)=>Rows.Add(row);
    }

    private sealed class DemoEnvironment:IHostEnvironment
    {
        public string EnvironmentName {get;set;}="Development";
        public string ApplicationName {get;set;}="UnitTests";
        public string ContentRootPath {get;set;}=".";
        public IFileProvider ContentRootFileProvider {get;set;}=new NullFileProvider();
    }
}
