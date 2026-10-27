using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Cart;
using AgriSustain.Application.DTOs.Products;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace AgriSustain.IntegrationTests.Commerce;

public sealed partial class CommerceSqlTests
{
    [CommerceSqlTheory, Trait("UseCase", "UC-FAR-06")]
    [InlineData("within"), InlineData("exact"), InlineData("late")]
    [InlineData("foreign"), InlineData("responded"), InlineData("closed")]
    public async Task EvidenceDeadlineAndOwnership_ArePersistedAtomically(string scenario)
    {
        await using var f = await CommerceDatabase.Create();
        long requestId = 0;
        await f.Edit(async db =>
        {
            var now = f.Clock.GetUtcNow();
            var ticket = new DisputeTicket
            {
                SellerOrderId = f.SellerId,
                SubmittedByUserId = f.BuyerId,
                DisputeReason = "Quality",
                TicketStatus = "Open",
                CreatedAt = now.UtcDateTime
            };
            var request = new DisputeEvidenceRequest
            {
                RequestedPartyUserId = scenario == "foreign" ? f.BuyerId : f.FarmerId,
                RequestedByUserId = (await db.Users.SingleAsync(x => x.Role == "CustomerSupport")).UserId,
                RequestedAt = now.AddHours(-48).AddSeconds(scenario == "late" ? -1 : 0).UtcDateTime,
                DueAt = now.AddSeconds(scenario == "late" ? -1 : scenario == "exact" ? 0 : 1).UtcDateTime,

                RequestStatus = scenario == "closed" ? "Closed" : "Pending",
                RespondedAt = scenario == "responded" ? now.AddMinutes(-1).UtcDateTime : null
            };
            ticket.DisputeEvidenceRequests.Add(request); db.DisputeTickets.Add(ticket);
            await db.SaveChangesAsync(); requestId = request.EvidenceRequestId;
        });
        var action = await f.Action(explanation: "Detailed explanation with owned evidence",
            evidence: [$"/api/files/evidence/{f.FarmerId}/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.pdf"]);
        var response = await f.Farmer().PostAsJsonAsync($"api/farmer/orders/{f.SellerId}/evidence",
            action with { EvidenceRequestId = requestId });
        var accepted = scenario is "within" or "exact";
        if (accepted) await Ok<SellerOrderResponse>(response);
        else await Error(response, HttpStatusCode.Conflict);
        await using var db = f.Open();
        Assert.Equal(accepted ? 1 : 0, await db.DisputeEvidences.CountAsync());
        Assert.Equal(accepted ? 1 : 0, await db.AuditLogs.CountAsync());
        var saved = await db.DisputeEvidenceRequests.SingleAsync();
        if (accepted)
        {
            Assert.Equal("Responded", saved.RequestStatus);
            Assert.Equal(f.Clock.GetUtcNow().UtcDateTime, saved.RespondedAt);
            var support = await db.Users.SingleAsync(x => x.Role == "CustomerSupport");
            Assert.True(await db.Notifications.AnyAsync(x => x.UserId == support.UserId));
        }
    }

    [CommerceSqlTheory, Trait("UseCase", "UC-FAR-03")]
    [InlineData(".jpg", 5242880, true)]
    [InlineData(".jpeg", 24, true)]
    [InlineData(".png", 24, true)]
    [InlineData(".gif", 24, false)]
    [InlineData(".jpg", 5242881, false)]
    [InlineData(".jpg", 0, false)]
    [InlineData(".png", 24, false)]
    public async Task UploadImage_ValidatesFiveMbLimitExtensionAndSignature(string extension, int length, bool accepted)
    {
        await using var f = await CommerceDatabase.Create();
        var bytes = new byte[length];
        if (length >= 24)
        {
            // Last PNG vector deliberately has a JPEG signature and must be rejected.
            byte[] header = extension == ".png" && accepted
                ? [137, 80, 78, 71, 13, 10, 26, 10] : [255, 216, 255];
            header.CopyTo(bytes, 0);
        }
        using var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", "image" + extension);
        var response = await f.Farmer().PostAsync("api/files/images", form);
        if (!accepted) { await Error(response, HttpStatusCode.BadRequest); return; }
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var imageUrl = json.RootElement.GetProperty("imageUrl").GetString()!;
        Assert.StartsWith($"/media/{f.FarmerId}/", imageUrl);
        var created = await Ok<ProductResponse>(await f.Farmer().PostAsJsonAsync("api/farmer/products",
            f.ProductRequest() with { Images = [new(imageUrl, true, null)] }));
        Assert.Equal(imageUrl, Assert.Single(created.Images).ImageUrl);
    }

    [CommerceSqlFact, Trait("UseCase", "UC-FAR-03"), Trait("CaseId", "IT-FAR-03-R2-009")]
    public async Task HideAndRepublish_PreservesStockAndHistoricalOrders()
    {
        await using var f = await CommerceDatabase.Create();
        var product = (await f.Farmer().GetFromJsonAsync<ProductResponse>($"api/farmer/products/{f.ProductId}"))!;
        var hidden = await Ok<ProductResponse>(await f.Farmer().PutAsJsonAsync($"api/farmer/products/{f.ProductId}/visibility",
            new VisibilityRequest(false, product.Version)));
        Assert.False(hidden.IsPublished);
        var published = await Ok<ProductResponse>(await f.Farmer().PutAsJsonAsync($"api/farmer/products/{f.ProductId}/visibility",
            new VisibilityRequest(true, hidden.Version)));
        Assert.True(published.IsPublished);
        await using var db = f.Open(); Assert.Equal(20, (await db.HarvestBatches.SingleAsync()).AvailableQuantity);
        Assert.Equal(1, await db.OrderItems.CountAsync()); Assert.Equal(2, await db.AuditLogs.CountAsync());
    }

    [CommerceSqlTheory, Trait("UseCase", "UC-RC-01")]
    [InlineData("retail"), InlineData("wholesale"), InlineData("threshold")]
    public async Task CheckoutPreparation_ChangedPricingRequiresReview(string change)
    {
        await using var f = await CommerceDatabase.Create();
        if (change != "retail")
            await Ok<CartResponse>(await f.Buyer().PutAsJsonAsync($"api/cart/items/{f.CartItemId}/quantity", new QuantityRequest(10)));
        var original = (await f.Buyer().GetFromJsonAsync<CartResponse>("api/cart"))!;
        await f.Edit(async db =>
        {
            var p = await db.Products.SingleAsync();
            if (change == "retail") p.BaseRetailPrice = 32000;
            if (change == "wholesale") p.WholesalePrice = 26000;
            if (change == "threshold") p.WholesaleMinQuantity = 12;
        });
        var revised = await Ok<CartResponse>(await f.Buyer().PostAsJsonAsync("api/cart/prepare-checkout", new PrepareCheckoutRequest(original.ReviewToken)));
        Assert.False(revised.CanProceedToCheckout);
        Assert.Equal(change == "retail" ? 64000 : change == "wholesale" ? 260000 : 300000, revised.SelectedSubtotal);
        var reviewed = await Ok<CartResponse>(await f.Buyer().PostAsJsonAsync("api/cart/prepare-checkout", new PrepareCheckoutRequest(revised.ReviewToken)));
        Assert.True(reviewed.CanProceedToCheckout);
    }

    [CommerceSqlFact, Trait("UseCase", "UC-RC-01")]
    public async Task DifferentBatchCreatesSeparateLine_AndBulkDeletePreservesUnselectedLine()
    {
        await using var f = await CommerceDatabase.Create();
        long otherBatch = 0;
        await f.Edit(async db =>
        {
            var original = await db.HarvestBatches.SingleAsync();
            var batch = new HarvestBatch
            {
                ProductId = f.ProductId,
                CropSeasonId = original.CropSeasonId,
                BatchCode = "TEST-SECOND",
                Status = "Available",
                AvailableQuantity = 10,
                ActualHarvestDate = original.ActualHarvestDate,
                StockEnteredAt = original.StockEnteredAt,
                ExpirationDate = original.ExpirationDate
            };
            db.HarvestBatches.Add(batch); await db.SaveChangesAsync(); otherBatch = batch.HarvestBatchId;
        });
        var cart = await Ok<CartResponse>(await f.Buyer().PostAsJsonAsync("api/cart/items", new CartItemRequest(f.ProductId, otherBatch, 1)));
        Assert.Equal(2, cart.Items.Count);
        var newLine = cart.Items.Single(x => x.HarvestBatchId == otherBatch);
        await Ok<CartResponse>(await f.Buyer().PutAsJsonAsync("api/cart/selection", new CartSelectionRequest([newLine.CartItemId], false)));
        await Ok<CartResponse>(await f.Buyer().PostAsJsonAsync("api/cart/remove-items", new RemoveCartItemsRequest([f.CartItemId])));
        await using var db = f.Open(); Assert.Equal(newLine.CartItemId, (await db.CartItems.SingleAsync()).CartItemId);
        Assert.False((await db.CartItems.SingleAsync()).IsSelected);
    }

    [CommerceSqlFact]
    public async Task SuspendedAccount_IsDeniedByProductionAccountMiddleware()
    {
        await using var f = await CommerceDatabase.Create();
        await f.Edit(async db => (await db.Users.SingleAsync(x => x.UserId == f.BuyerId)).Status = "Suspended");
        await Error(await f.Buyer().GetAsync("api/cart"), HttpStatusCode.Unauthorized);
        await using var db = f.Open(); Assert.Equal(2, (await db.CartItems.SingleAsync()).Quantity);
    }

    [CommerceSqlFact, Trait("UseCase", "UC-FAR-06"), Trait("CaseId", "IT-FAR-06-R2-010")]
    public async Task OrderSearch_QueriesBatchCodeFiltersAndEmptyPage()
    {
        await using var f = await CommerceDatabase.Create();
        var found = await f.Farmer().GetFromJsonAsync<PageResult<SellerOrderResponse>>(
            "api/farmer/orders?Keyword=TEST-BATCH&Status=Pending&OrderType=Retail&PaymentStatus=Paid");
        Assert.Equal(f.SellerId, Assert.Single(found!.Items).SellerOrderId);
        var empty = await f.Farmer().GetFromJsonAsync<PageResult<SellerOrderResponse>>("api/farmer/orders?Keyword=does-not-exist");
        Assert.Empty(empty!.Items); Assert.Equal(0, empty.Total);
    }

    [CommerceSqlTheory]
    [InlineData("api/farmer/products"), InlineData("api/farmer/orders")]
    public async Task ForeignFarmer_CannotReadOrModifyOwnedResource(string route)
    {
        await using var f = await CommerceDatabase.Create();
        long farmerId = 0;
        await f.Edit(async db =>
        {
            var user = new User
            {
                Username = "foreign-farmer",
                Email = "foreign-farmer@example.test",
                FullName = "Foreign farmer",
                PasswordHash = "test-only",
                Role = "Farmer",
                Status = "Active",
                IsEmailVerified = true
            };
            db.Users.Add(user); await db.SaveChangesAsync(); farmerId = user.UserId;
            db.FarmerProfiles.Add(new FarmerProfile
            {
                UserId = farmerId,
                FarmName = "Other farm",
                Address = "Other address",
                ProvinceCode = "TEST",
                RepresentativeName = "Other farmer",
                VerificationStatus = "Approved"
            });
        });
        long id = route.EndsWith("products") ? f.ProductId : f.SellerId;
        var action = await f.Action();
        var client = f.As(farmerId, "Farmer");
        await Error(await client.GetAsync($"{route}/{id}"), HttpStatusCode.NotFound);
        if (route.EndsWith("products"))
            await Error(await client.PutAsJsonAsync($"{route}/{id}", f.ProductRequest(new string('A', 64))), HttpStatusCode.NotFound);
        else await Error(await client.PostAsJsonAsync($"{route}/{id}/confirm", action), HttpStatusCode.NotFound);
        await using var check = f.Open(); Assert.Empty(await check.AuditLogs.ToListAsync());
    }
}
