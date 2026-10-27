using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.DTOs.Cart;
using AgriSustain.Application.DTOs.Products;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.Commerce;

[Trait("Layer", "SqlIntegration")]
public sealed partial class CommerceSqlTests
{
    private static async Task<T> Ok<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    private static async Task Error(HttpResponseMessage response, HttpStatusCode status)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.DoesNotMatch(@"MSG\d+", await response.Content.ReadAsStringAsync());
    }

    [CommerceSqlFact, Trait("UseCase", "UC-FAR-03"), Trait("CaseId", "IT-FAR-03-R2-001")]
    public async Task Submit_PersistsPendingImagesAndAudit_WithoutStock()
    {
        await using var f = await CommerceDatabase.Create();
        var value = await Ok<ProductResponse>(await f.Farmer().PostAsJsonAsync("api/farmer/products", f.ProductRequest()));
        await using var db = f.Open();
        var product = await db.Products.Include(x => x.ProductImages).SingleAsync(x => x.ProductId == value.ProductId);
        Assert.Equal("Pending", product.ApprovalStatus); Assert.False(product.IsPublished);
        Assert.Single(product.ProductImages);
        Assert.False(await db.HarvestBatches.AnyAsync(x => x.ProductId == value.ProductId));
        Assert.True(await db.AuditLogs.AnyAsync(x => x.EntityType == "Product" && x.EntityId == value.ProductId));
    }
    [CommerceSqlTheory, Trait("UseCase", "UC-FAR-03")]
    [InlineData("name"), InlineData("category"), InlineData("unit"), InlineData("description")]
    [InlineData("zero-price"), InlineData("negative-price"), InlineData("minimum"), InlineData("wholesale-zero")]
    [InlineData("wholesale-high"), InlineData("no-primary"), InlineData("two-primary"), InlineData("foreign-image")]
    public async Task InvalidSubmission_DoesNotPersistProductOrAudit(string invalid)
    {
        await using var f = await CommerceDatabase.Create();
        var request = f.ProductRequest();
        request = invalid switch
        {
            "name" => request with { ProductName = "" },
            "category" => request with { CategoryId = 0 },
            "unit" => request with { Unit = "" },
            "description" => request with { Description = "" },
            "zero-price" => request with { BaseRetailPrice = 0 },
            "negative-price" => request with { BaseRetailPrice = -1 },
            "minimum" => request with { WholesaleMinQuantity = 1 },
            "wholesale-zero" => request with { WholesalePrice = 0 },
            "wholesale-high" => request with { WholesalePrice = 30001 },
            "no-primary" => request with { Images = [new(f.Image, false, null)] },
            "two-primary" => request with { Images = [new(f.Image, true, null), new(f.Image.Replace(".jpg", "b.jpg"), true, null)] },
            _ => request with { Images = [new("/media/999/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.jpg", true, null)] }
        };
        await Error(await f.Farmer().PostAsJsonAsync("api/farmer/products", request), HttpStatusCode.BadRequest);
        await using var db = f.Open(); Assert.Equal(1, await db.Products.CountAsync()); Assert.Empty(await db.AuditLogs.ToListAsync());
    }
    [CommerceSqlFact, Trait("UseCase", "UC-FAR-03"), Trait("CaseId", "IT-FAR-03-R2-006")]
    public async Task PriceEdit_PreservesApprovalAndCheckoutSnapshots()
    {
        await using var f = await CommerceDatabase.Create();
        var current = await f.Farmer().GetFromJsonAsync<ProductResponse>($"api/farmer/products/{f.ProductId}");
        var result = await Ok<ProductResponse>(await f.Farmer().PutAsJsonAsync($"api/farmer/products/{f.ProductId}",
            f.ProductRequest(current!.Version) with { BaseRetailPrice = 32000 }));
        Assert.Equal("Approved", result.ApprovalStatus); Assert.True(result.IsPublished);
        await using var db = f.Open();
        Assert.Equal(30000, (await db.OrderItems.SingleAsync()).UnitPriceSnapshot);
        Assert.Equal(160000, (await db.SellerOrders.SingleAsync()).FinalAmount);
    }
    [CommerceSqlFact, Trait("UseCase", "UC-FAR-03"), Trait("CaseId", "IT-FAR-03-R2-005")]
    public async Task ContentEdit_ResubmitsSameProductForApproval()
    {
        await using var f = await CommerceDatabase.Create();
        var current = await f.Farmer().GetFromJsonAsync<ProductResponse>($"api/farmer/products/{f.ProductId}");
        var result = await Ok<ProductResponse>(await f.Farmer().PutAsJsonAsync($"api/farmer/products/{f.ProductId}",
            f.ProductRequest(current!.Version) with { Description = "Changed description" }));
        Assert.Equal(f.ProductId, result.ProductId); Assert.Equal("Pending", result.ApprovalStatus); Assert.False(result.IsPublished);
        await using var db = f.Open(); Assert.Equal(1, await db.Products.CountAsync()); Assert.Equal(1, await db.ProductImages.CountAsync());
    }
    [CommerceSqlFact, Trait("UseCase", "UC-FAR-03"), Trait("CaseId", "IT-FAR-03-R2-038")]
    public async Task StaleProductVersion_RejectsSecondWrite()
    {
        await using var f = await CommerceDatabase.Create();
        var current = await f.Farmer().GetFromJsonAsync<ProductResponse>($"api/farmer/products/{f.ProductId}");
        var request = f.ProductRequest(current!.Version);
        await Ok<ProductResponse>(await f.Farmer().PutAsJsonAsync($"api/farmer/products/{f.ProductId}", request with { BaseRetailPrice = 32000 }));
        await Error(await f.Farmer().PutAsJsonAsync($"api/farmer/products/{f.ProductId}", request with { BaseRetailPrice = 33000 }), HttpStatusCode.Conflict);
        await using var db = f.Open(); Assert.Equal(32000, (await db.Products.SingleAsync()).BaseRetailPrice);
        Assert.Equal(1, await db.AuditLogs.CountAsync());
    }
    [CommerceSqlFact, Trait("UseCase", "UC-FAR-03"), Trait("CaseId", "IT-FAR-03-R2-039")]
    public async Task ProductSaveFailure_RollsBackProductImagesAndAudit()
    {
        await using var f = await CommerceDatabase.Create(); f.Failure.Armed = true;
        await Error(await f.Farmer().PostAsJsonAsync("api/farmer/products", f.ProductRequest()), HttpStatusCode.InternalServerError);
        await using var db = f.Open(); Assert.Equal(1, await db.Products.CountAsync());
        Assert.Equal(1, await db.ProductImages.CountAsync()); Assert.Empty(await db.AuditLogs.ToListAsync());
        await Ok<ProductResponse>(await f.Farmer().PostAsJsonAsync("api/farmer/products", f.ProductRequest()));
        Assert.Equal(2, await db.Products.CountAsync());
    }
    [CommerceSqlTheory, Trait("UseCase", "UC-FAR-03")]
    [InlineData("Pending"), InlineData("Rejected")]
    public async Task NonApprovedProduct_CannotBePublished(string status)
    {
        await using var f = await CommerceDatabase.Create();
        await f.Edit(async db => { var p = await db.Products.SingleAsync(); p.ApprovalStatus = status; p.IsPublished = false; });
        var current = await f.Farmer().GetFromJsonAsync<ProductResponse>($"api/farmer/products/{f.ProductId}");
        await Error(await f.Farmer().PutAsJsonAsync($"api/farmer/products/{f.ProductId}/visibility",
            new VisibilityRequest(true, current!.Version)), HttpStatusCode.Conflict);
        await using var check = f.Open(); Assert.False((await check.Products.SingleAsync()).IsPublished);
    }
    [CommerceSqlFact, Trait("UseCase", "UC-FAR-06"), Trait("CaseId", "IT-FAR-06-R2-003")]
    public async Task ConfirmReadyDispatch_ConsumesHeldStockExactlyOnce()
    {
        await using var f = await CommerceDatabase.Create();
        foreach (var operation in new[] { "confirm", "ready", "dispatch" })
            await Ok<SellerOrderResponse>(await f.Farmer().PostAsJsonAsync($"api/farmer/orders/{f.SellerId}/{operation}", await f.Action()));
        await using var db = f.Open();
        Assert.Equal("Shipping", (await db.SellerOrders.SingleAsync()).Status);
        Assert.Equal("Shipping", (await db.Orders.SingleAsync()).OrderStatus);
        var batch = await db.HarvestBatches.SingleAsync(); Assert.Equal(20, batch.AvailableQuantity); Assert.Equal(0, batch.ReservedQuantity);
        Assert.Equal("Consumed", (await db.OrderItemBatchAllocations.SingleAsync()).AllocationStatus);
        Assert.Equal(-5, (await db.InventoryMovements.SingleAsync()).QuantityChange);
        Assert.Equal("InTransit", (await db.Shipments.SingleAsync()).ShipmentStatus);
        Assert.Equal(3, await db.OrderStatusHistories.CountAsync());
        Assert.Equal(3, await db.Notifications.CountAsync(x => x.UserId == f.BuyerId));
    }
    [CommerceSqlFact, Trait("UseCase", "UC-FAR-06"), Trait("CaseId", "IT-FAR-06-R2-012")]
    public async Task DispatchFailure_RollsBackEveryTable_AndRetrySucceeds()
    {
        await using var f = await CommerceDatabase.Create();
        await f.Edit(async db => (await db.SellerOrders.SingleAsync()).Status = "ReadyToShip");
        var action = await f.Action(); f.Failure.Armed = true;
        await Error(await f.Farmer().PostAsJsonAsync($"api/farmer/orders/{f.SellerId}/dispatch", action), HttpStatusCode.InternalServerError);
        await using (var db = f.Open())
        {
            Assert.Equal("ReadyToShip", (await db.SellerOrders.SingleAsync()).Status);
            Assert.Equal(5, (await db.HarvestBatches.SingleAsync()).ReservedQuantity);
            Assert.Equal("Reserved", (await db.OrderItemBatchAllocations.SingleAsync()).AllocationStatus);
            Assert.Empty(await db.Shipments.ToListAsync()); Assert.Empty(await db.InventoryMovements.ToListAsync());
            Assert.Empty(await db.AuditLogs.ToListAsync()); Assert.Empty(await db.OrderStatusHistories.ToListAsync());
            Assert.Empty(await db.Notifications.ToListAsync());
        }
        await Ok<SellerOrderResponse>(await f.Farmer().PostAsJsonAsync($"api/farmer/orders/{f.SellerId}/dispatch", action));
        await using var check = f.Open(); Assert.Equal(1, await check.Shipments.CountAsync()); Assert.Equal(1, await check.InventoryMovements.CountAsync());
    }
    [CommerceSqlFact, Trait("UseCase", "UC-FAR-06"), Trait("CaseId", "IT-FAR-06-R2-034")]
    public async Task ConcurrentDispatch_SameRequestKey_CreatesOneShipmentAndMovement()
    {
        await using var f = await CommerceDatabase.Create();
        await f.Edit(async db => (await db.SellerOrders.SingleAsync()).Status = "ReadyToShip");
        var action = await f.Action(); var client = f.Farmer();
        var results = await Task.WhenAll(Enumerable.Range(0, 2)
            .Select(_ => client.PostAsJsonAsync($"api/farmer/orders/{f.SellerId}/dispatch", action)));
        Assert.Single(results, x => x.IsSuccessStatusCode);
        Assert.Single(results, x => x.StatusCode == HttpStatusCode.Conflict);


        await using var db = f.Open(); Assert.Equal(1, await db.Shipments.CountAsync());
        Assert.Equal(1, await db.InventoryMovements.CountAsync()); Assert.Equal(0, (await db.HarvestBatches.SingleAsync()).ReservedQuantity);
    }
    [CommerceSqlTheory, Trait("UseCase", "UC-FAR-06")]
    [InlineData("Pending", "ready"), InlineData("Confirmed", "dispatch"), InlineData("Shipping", "confirm"), InlineData("Shipping", "reject")]
    public async Task WrongStateAction_DoesNotWriteHistoryOrStock(string state, string operation)
    {
        await using var f = await CommerceDatabase.Create();
        await f.Edit(async db => (await db.SellerOrders.SingleAsync()).Status = state);
        await Error(await f.Farmer().PostAsJsonAsync($"api/farmer/orders/{f.SellerId}/{operation}",
            await f.Action("Shortfall", "Valid explanation with enough characters")), HttpStatusCode.Conflict);
        await using var db = f.Open(); Assert.Equal(state, (await db.SellerOrders.SingleAsync()).Status);
        Assert.Empty(await db.InventoryMovements.ToListAsync()); Assert.Empty(await db.AuditLogs.ToListAsync());
    }
    [CommerceSqlTheory, Trait("UseCase", "UC-FAR-06")]
    [InlineData("Expired"), InlineData("Recalled"), InlineData("Blocked"), InlineData("Unpaid"), InlineData("MissingAllocation")]
    public async Task Dispatch_RevalidatesPaymentAndBatchAtCommit(string invalid)
    {
        await using var f = await CommerceDatabase.Create();
        await f.Edit(async db =>
        {
            (await db.SellerOrders.SingleAsync()).Status = "ReadyToShip";
            if (invalid == "Unpaid") (await db.PaymentTransactions.SingleAsync()).PaymentStatus = "Pending";
            else if (invalid == "MissingAllocation") db.OrderItemBatchAllocations.Remove(await db.OrderItemBatchAllocations.SingleAsync());
            else (await db.HarvestBatches.SingleAsync()).Status = invalid;
        });
        await Error(await f.Farmer().PostAsJsonAsync($"api/farmer/orders/{f.SellerId}/dispatch", await f.Action()), HttpStatusCode.Conflict);
        await using var db = f.Open(); Assert.Empty(await db.Shipments.ToListAsync()); Assert.Empty(await db.InventoryMovements.ToListAsync());
    }
    [CommerceSqlFact, Trait("UseCase", "UC-FAR-06"), Trait("CaseId", "IT-FAR-06-R2-006")]
    public async Task RejectPaidOrder_ReleasesStock_QueuesFinanceWithoutClaimingRefund()
    {
        await using var f = await CommerceDatabase.Create();
        await Ok<SellerOrderResponse>(await f.Farmer().PostAsJsonAsync($"api/farmer/orders/{f.SellerId}/reject",
            await f.Action("Shortfall", "Insufficient harvest for this order")));
        await using var db = f.Open(); Assert.Equal("Cancelled", (await db.SellerOrders.SingleAsync()).Status);
        Assert.Equal("Released", (await db.OrderItemBatchAllocations.SingleAsync()).AllocationStatus);
        Assert.Equal(25, (await db.HarvestBatches.SingleAsync()).AvailableQuantity);
        Assert.Equal(0, (await db.HarvestBatches.SingleAsync()).ReservedQuantity);
        Assert.Empty(await db.RefundTransactions.ToListAsync());
        Assert.True(await db.Notifications.AnyAsync(x => x.UserId == f.BuyerId));
    }
    [CommerceSqlTheory, Trait("UseCase", "UC-FAR-06")]
    [InlineData("DeliveryFailure", "DeliveryFailed"), InlineData("Return", "Returned")]
    public async Task DeliveryReport_PersistsEvidence_AndNotifiesSupport(string reason, string status)
    {
        await using var f = await CommerceDatabase.Create();
        await f.Edit(async db =>
        {
            var seller = await db.SellerOrders.SingleAsync(); seller.Status = "Shipping";
            seller.Shipments.Add(new Shipment { ShipmentStatus = "InTransit", ShippedAt = f.Clock.GetUtcNow().UtcDateTime });
        });
        await Ok<SellerOrderResponse>(await f.Farmer().PostAsJsonAsync($"api/farmer/orders/{f.SellerId}/delivery-report",
            await f.Action(reason, "Delivery incident with sufficient explanation",
                [$"/api/files/evidence/{f.FarmerId}/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.pdf"])));
        await using var db = f.Open(); Assert.Equal(status, (await db.SellerOrders.SingleAsync()).Status);
        Assert.Equal(1, await db.DisputeTickets.CountAsync()); Assert.Equal(1, await db.DisputeEvidences.CountAsync());
        var support = await db.Users.SingleAsync(x => x.Role == "CustomerSupport");
        Assert.True(await db.Notifications.AnyAsync(x => x.UserId == support.UserId));
    }
    [CommerceSqlFact, Trait("UseCase", "UC-FAR-06"), Trait("CaseId", "IT-FAR-06-R2-007")]
    public async Task OrderDetail_ExposesCheckoutFinancialSnapshotsForPrinting()
    {
        await using var f = await CommerceDatabase.Create();
        await f.Edit(async db => (await db.Products.SingleAsync()).BaseRetailPrice = 999999);
        var detail = (await f.Farmer().GetFromJsonAsync<SellerOrderResponse>($"api/farmer/orders/{f.SellerId}"))!;
        Assert.Equal(30000, Assert.Single(detail.Items).UnitPrice);
        Assert.Equal(150000, detail.Subtotal); Assert.Equal(10000, detail.DiscountShare);
        Assert.Equal(20000, detail.DeliveryFee); Assert.Equal(160000, detail.FinalAmount); Assert.Equal("TEST10", detail.VoucherCode);
        // Browser print rendering is a separate UI test; this checks the source values only.
    }
    [CommerceSqlFact, Trait("UseCase", "UC-RC-01"), Trait("CaseId", "IT-RC-01-R2-003")]
    public async Task AddSameOffer_MergesAndPersists_WithoutReservingStock()
    {
        await using var f = await CommerceDatabase.Create();
        var cart = await Ok<CartResponse>(await f.Buyer().PostAsJsonAsync("api/cart/items", new CartItemRequest(f.ProductId, f.BatchId, 3)));
        Assert.Equal(5, Assert.Single(cart.Items).Quantity); Assert.Equal(150000, cart.SelectedSubtotal);
        await using var db = f.Open(); Assert.Equal(1, await db.CartItems.CountAsync());
        Assert.Equal(20, (await db.HarvestBatches.SingleAsync()).AvailableQuantity);
        Assert.Equal(5, (await db.HarvestBatches.SingleAsync()).ReservedQuantity);
    }
    [CommerceSqlTheory, Trait("UseCase", "UC-RC-01")]
    [InlineData(9, 30000, 270000), InlineData(10, 25000, 250000), InlineData(11, 25000, 275000)]
    public async Task QuantityCrossesWholesaleThreshold_UsesCurrentPricing(int quantity, int unitPrice, int total)
    {
        await using var f = await CommerceDatabase.Create();
        var value = await Ok<CartResponse>(await f.Buyer().PutAsJsonAsync($"api/cart/items/{f.CartItemId}/quantity", new QuantityRequest(quantity)));
        Assert.Equal(unitPrice, Assert.Single(value.Items).UnitPrice); Assert.Equal(total, value.SelectedSubtotal);
        await using var db = f.Open(); Assert.Equal(quantity, (await db.CartItems.SingleAsync()).Quantity);
    }
    [CommerceSqlFact, Trait("UseCase", "UC-RC-01"), Trait("CaseId", "IT-RC-01-R2-011")]
    public async Task CheckoutPreparation_StockDeclineRequiresReviewAndRetry()
    {
        await using var f = await CommerceDatabase.Create();
        var original = (await f.Buyer().GetFromJsonAsync<CartResponse>("api/cart"))!;
        await f.Edit(async db => (await db.HarvestBatches.SingleAsync()).AvailableQuantity = 1);
        var first = await Ok<CartResponse>(await f.Buyer().PostAsJsonAsync("api/cart/prepare-checkout", new PrepareCheckoutRequest(original.ReviewToken)));
        Assert.False(first.CanProceedToCheckout); Assert.Equal(1, Assert.Single(first.Items).Quantity);
        var second = await Ok<CartResponse>(await f.Buyer().PostAsJsonAsync("api/cart/prepare-checkout", new PrepareCheckoutRequest(first.ReviewToken)));
        Assert.True(second.CanProceedToCheckout);
        await using var db = f.Open(); Assert.Equal(1, await db.Orders.CountAsync()); Assert.Equal(1, await db.PaymentTransactions.CountAsync());
        Assert.Equal(5, (await db.HarvestBatches.SingleAsync()).ReservedQuantity);
    }
    [CommerceSqlTheory, Trait("UseCase", "UC-RC-01")]
    [InlineData("sold-out"), InlineData("recalled"), InlineData("hidden"), InlineData("category-inactive")]
    public async Task CheckoutPreparation_RevalidatesOfferAndDeselectsUnavailable(string change)
    {
        await using var f = await CommerceDatabase.Create();
        var original = (await f.Buyer().GetFromJsonAsync<CartResponse>("api/cart"))!;
        await f.Edit(async db =>
        {
            if (change == "sold-out") (await db.HarvestBatches.SingleAsync()).AvailableQuantity = 0;
            if (change == "recalled") (await db.HarvestBatches.SingleAsync()).Status = "Recalled";
            if (change == "hidden") (await db.Products.SingleAsync()).IsPublished = false;
            if (change == "category-inactive") (await db.Categories.SingleAsync()).IsActive = false;
        });
        var result = await Ok<CartResponse>(await f.Buyer().PostAsJsonAsync("api/cart/prepare-checkout", new PrepareCheckoutRequest(original.ReviewToken)));
        Assert.False(result.CanProceedToCheckout); Assert.False(Assert.Single(result.Items).IsSelected); Assert.Equal(0, result.SelectedSubtotal);
        await using var db = f.Open(); Assert.False((await db.CartItems.SingleAsync()).IsSelected);
    }
    [CommerceSqlFact, Trait("UseCase", "UC-RC-01"), Trait("CaseId", "IT-RC-01-R2-016")]
    public async Task CartOwnership_BlocksForeignItemAndLeavesBothCartsUntouched()
    {
        await using var f = await CommerceDatabase.Create();
        await Error(await f.As(f.OtherBuyerId, "RetailCustomer").PutAsJsonAsync($"api/cart/items/{f.CartItemId}/quantity", new QuantityRequest(10)), HttpStatusCode.NotFound);
        await using var db = f.Open(); Assert.Equal(2, (await db.CartItems.SingleAsync()).Quantity);
        Assert.Equal(1, await db.Carts.CountAsync());
    }
    [CommerceSqlFact, Trait("UseCase", "UC-RC-01")]
    public async Task CartCommitFailure_RollsBackQuantityAndRetryPersists()
    {
        await using var f = await CommerceDatabase.Create(); f.Failure.Armed = true;
        await Error(await f.Buyer().PutAsJsonAsync($"api/cart/items/{f.CartItemId}/quantity", new QuantityRequest(10)), HttpStatusCode.InternalServerError);
        await using (var db = f.Open()) Assert.Equal(2, (await db.CartItems.SingleAsync()).Quantity);
        await Ok<CartResponse>(await f.Buyer().PutAsJsonAsync($"api/cart/items/{f.CartItemId}/quantity", new QuantityRequest(10)));
        await using var check = f.Open(); Assert.Equal(10, (await check.CartItems.SingleAsync()).Quantity);
    }
}
