using AgriSustain.Application.Common;
using AgriSustain.Domain.Entities;
using Xunit;
namespace AgriSustain.UnitTests.Commerce;
[Trait("UseCase", "UC-RC-01")]
public sealed class ShoppingCartTests
{
    [Fact, Trait("CaseId", "UT-RC-01-R2-001")]

    public async Task Add_NewLine_UsesServerPriceWithoutReservation()
    {
        var f = new CommerceFixture();
        f.Carts.Cart = null;
        var result = await f.CartService.AddItemAsync(102, new(301, 401, 2), default);
        Assert.True(result.IsSuccess); var line = Assert.Single(result.Value!.Items);
        Assert.Equal(2, line.Quantity); Assert.Equal(30000, line.UnitPrice); Assert.Equal(60000, line.Subtotal);
        Assert.Equal(1, f.Carts.Adds); Assert.Equal(1, f.Work.Commits); Assert.Equal(20, f.Batch.AvailableQuantity); Assert.Equal(5, f.Batch.ReservedQuantity);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-002")]
    public async Task Add_SameProductBatch_CombinesQuantity()
    {
        var f = new CommerceFixture();
        f.AddCartLine(quantity: 2); var result = await f.CartService.AddItemAsync(102, new(301, 401, 3), default);
        Assert.True(result.IsSuccess); Assert.Equal(5, Assert.Single(result.Value!.Items).Quantity); Assert.Single(f.Cart.CartItems);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-003")]
    public async Task Add_DifferentBatch_KeepsSeparateLines()
    {
        var f = new CommerceFixture();
        f.AddCartLine(quantity: 2); f.Carts.Offers[402] = f.NewBatch(402);
        var result = await f.CartService.AddItemAsync(102, new(301, 402, 3), default);
        Assert.True(result.IsSuccess); Assert.Equal(2, result.Value!.Items.Single(x => x.HarvestBatchId == 401).Quantity);
        Assert.Equal(3, result.Value.Items.Single(x => x.HarvestBatchId == 402).Quantity);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-004")]
    public async Task Add_CombinedOverflow_AdjustsToStock()
    {
        var f = new CommerceFixture();
        f.AddCartLine(quantity: 4); f.Batch.AvailableQuantity = 5;
        var result = await f.CartService.AddItemAsync(102, new(301, 401, 2), default);
        Assert.Equal(5, Assert.Single(result.Value!.Items).Quantity); Assert.Contains("5 kg", Assert.Single(result.Value.Messages));
        Assert.Equal(5, f.Batch.AvailableQuantity);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-005")]
    public async Task Add_UnavailableBatch_IsRejected()
    {
        var f = new CommerceFixture();
        f.Batch.AvailableQuantity = 0; var result = await f.CartService.AddItemAsync(102, new(301, 401, 1), default);
        Assert.Equal("CONFLICT", result.Error!.Code); Assert.Empty(f.Cart.CartItems); Assert.Equal(0, f.Work.Commits);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-006")]
    public async Task Add_CommitFailure_Propagates()
    {
        var f = new CommerceFixture();
        f.Work.FailCommit = true;
        await Assert.ThrowsAsync<IOException>(() => f.CartService.AddItemAsync(102, new(301, 401, 2), default)); Assert.Equal(0, f.Work.Commits);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-007")]
    public async Task Quantity_BelowWholesale_RestoresRetail()
    {
        var f = new CommerceFixture();
        f.AddCartLine(quantity: 10); var result = await f.CartService.UpdateQuantityAsync(102, 701, 9, default);
        Assert.Equal(30000, Assert.Single(result.Value!.Items).UnitPrice); Assert.Equal(270000, result.Value.SelectedSubtotal);
        Assert.Contains(CartMessages.RetailRestored, result.Value.Messages);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-008")]
    public async Task Quantity_AtWholesale_AppliesWholesale()
    {
        var f = new CommerceFixture();
        f.AddCartLine(quantity: 9); var result = await f.CartService.UpdateQuantityAsync(102, 701, 10, default);
        Assert.Equal(25000, Assert.Single(result.Value!.Items).UnitPrice); Assert.Equal(250000, result.Value.SelectedSubtotal);
        Assert.Contains(CartMessages.WholesaleApplied, result.Value.Messages);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-009")]
    public async Task Quantity_AboveWholesale_UsesConfiguredPrice()
    {
        var f = new CommerceFixture();
        f.AddCartLine(quantity: 10); var result = await f.CartService.UpdateQuantityAsync(102, 701, 11, default);
        Assert.Equal(25000, Assert.Single(result.Value!.Items).UnitPrice); Assert.Equal(275000, result.Value.SelectedSubtotal); Assert.Equal(1, f.Work.Commits);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-010")]
    public async Task Quantity_NoWholesalePair_KeepsRetail()
    {
        var f = new CommerceFixture();
        f.Product.WholesaleMinQuantity = null; f.Product.WholesalePrice = null; f.AddCartLine();
        var result = await f.CartService.UpdateQuantityAsync(102, 701, 10, default); Assert.Equal(30000, Assert.Single(result.Value!.Items).UnitPrice);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-011")]
    public async Task Quantity_Zero_KeepsSavedValue()
    {
        var f = new CommerceFixture();
        var item = f.AddCartLine(quantity: 3); var result = await f.CartService.UpdateQuantityAsync(102, 701, 0m, default);
        Assert.Equal("VALIDATION", result.Error!.Code); Assert.Equal(3, item.Quantity); Assert.Equal(0, f.Work.Commits);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-012")]
    public async Task Quantity_Negative_KeepsSavedValue()
    {
        var f = new CommerceFixture();
        var item = f.AddCartLine(quantity: 3); var result = await f.CartService.UpdateQuantityAsync(102, 701, -1m, default);
        Assert.Equal("VALIDATION", result.Error!.Code); Assert.Equal(3, item.Quantity); Assert.Equal(0, f.Work.Commits);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-013")]
    public async Task Quantity_WrongStep_KeepsSavedValue()
    {
        var f = new CommerceFixture();
        var item = f.AddCartLine(quantity: 3); var result = await f.CartService.UpdateQuantityAsync(102, 701, 1.5m, default);
        Assert.Equal("VALIDATION", result.Error!.Code); Assert.Equal(3, item.Quantity); Assert.Equal(0, f.Work.Commits);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-014")]
    public async Task Quantity_Overflow_RespectsIntegerSellingStep()
    {
        var f = new CommerceFixture();
        f.AddCartLine(quantity: 1); f.Batch.AvailableQuantity = 2.4m;
        var result = await f.CartService.UpdateQuantityAsync(102, 701, 3, default); Assert.Equal(2, Assert.Single(result.Value!.Items).Quantity);
        Assert.Contains("2,4 kg", Assert.Single(result.Value.Messages)); Assert.Equal(60000, result.Value.SelectedSubtotal);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-015")]
    public async Task Quantity_LessThanOneSellingUnit_Deselects()
    {
        var f = new CommerceFixture();
        var item = f.AddCartLine(quantity: 1); f.Batch.AvailableQuantity = 0.5m;
        var result = await f.CartService.UpdateQuantityAsync(102, 701, 1, default); Assert.False(item.IsSelected);
        Assert.False(Assert.Single(result.Value!.Items).IsAvailable); Assert.Equal(0, result.Value.SelectedCount); Assert.True(item.Quantity > 0);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-016")]
    public async Task Quantity_CommitFailure_Propagates()
    {
        var f = new CommerceFixture();
        f.AddCartLine(); f.Work.FailCommit = true;
        await Assert.ThrowsAsync<IOException>(() => f.CartService.UpdateQuantityAsync(102, 701, 9, default)); Assert.Equal(0, f.Work.Commits);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-017")]
    public async Task Selection_SubtotalIncludesSelectedOnly()
    {
        var f = new CommerceFixture();
        f.AddCartLine(quantity: 2, selected: false); Second(f);
        var result = await f.CartService.SetSelectionAsync(102, new([701], true), false, default);
        Assert.Equal(60000, result.Value!.SelectedSubtotal); Assert.Equal(1, result.Value.SelectedCount); Assert.False(result.Value.Items.Single(x => x.CartItemId == 702).IsSelected);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-018")]
    public async Task Selection_Deselect_RetainsLine()
    {
        var f = new CommerceFixture();
        var item = f.AddCartLine(); var result = await f.CartService.SetSelectionAsync(102, new([701], false), false, default);
        Assert.False(item.IsSelected); Assert.Equal(0, result.Value!.SelectedSubtotal); Assert.Single(f.Cart.CartItems);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-019")]
    public async Task Selection_UnavailableLine_RemainsDeselected()
    {
        var f = new CommerceFixture();
        var item = f.AddCartLine(); f.Batch.Status = "Recalled"; Second(f);
        var result = await f.CartService.SetSelectionAsync(102, new([701, 702], true), false, default);
        Assert.False(item.IsSelected); Assert.Equal(1, result.Value!.SelectedCount); Assert.Equal(20000, result.Value.SelectedSubtotal);
        Assert.False(result.Value.Items.Single(x => x.CartItemId == 701).IsAvailable);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-020")]
    public async Task Selection_ForeignLine_IsDenied()
    {
        var f = new CommerceFixture();
        var item = f.AddCartLine(selected: false);
        var result = await f.CartService.SetSelectionAsync(102, new([999], true), false, default);
        Assert.Equal("NOT_FOUND", result.Error!.Code); Assert.False(item.IsSelected); Assert.Equal(0, f.Work.Commits);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-021")]
    public async Task Selection_CommitFailure_Propagates()
    {
        var f = new CommerceFixture();
        f.AddCartLine(); f.Work.FailCommit = true;
        await Assert.ThrowsAsync<IOException>(() => f.CartService.SetSelectionAsync(102, new([701], false), false, default)); Assert.Equal(0, f.Work.Commits);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-022")]
    public async Task Prepare_UnchangedSelection_HasNoPurchaseSideEffects()
    {
        var f = new CommerceFixture();
        f.AddCartLine(quantity: 2); Second(f); var token = f.Pricing.Calculate(f.Cart).ReviewToken;
        var result = await f.CartService.PrepareCheckoutAsync(102, token, default);
        Assert.True(result.Value!.CanProceedToCheckout); Assert.Equal(701, Assert.Single(result.Value.Items, x => x.IsSelected).CartItemId);
        Assert.Equal(2, f.Cart.CartItems.Count); Assert.Equal(20, f.Batch.AvailableQuantity); Assert.Equal(5, f.Batch.ReservedQuantity); Assert.Empty(f.Orders.Movements);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-023")]
    public async Task Prepare_EmptyCart_ReportsNoSelection()
    {
        var f = new CommerceFixture();
        var result = await f.CartService.PrepareCheckoutAsync(102, f.Pricing.Calculate(f.Cart).ReviewToken, default);
        Assert.False(result.Value!.CanProceedToCheckout); Assert.Contains(CartMessages.NoSelection, result.Value.Messages); Assert.Empty(result.Value.Items);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-024")]
    public async Task Prepare_UnselectedCart_ReportsNoSelection()
    {
        var f = new CommerceFixture();
        f.AddCartLine(selected: false);
        var result = await f.CartService.PrepareCheckoutAsync(102, f.Pricing.Calculate(f.Cart).ReviewToken, default);
        Assert.False(result.Value!.CanProceedToCheckout); Assert.Contains(CartMessages.NoSelection, result.Value.Messages); Assert.Single(f.Cart.CartItems);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-025")]
    public async Task Prepare_StockDecline_RequiresReviewAndRetry()
    {
        var f = new CommerceFixture();
        f.AddCartLine(quantity: 5); var token = f.Pricing.Calculate(f.Cart).ReviewToken; f.Batch.AvailableQuantity = 3;
        var first = await f.CartService.PrepareCheckoutAsync(102, token, default);
        Assert.False(first.Value!.CanProceedToCheckout); Assert.Equal(3, Assert.Single(first.Value.Items).Quantity); Assert.Single(first.Value.Messages);
        var retry = await f.CartService.PrepareCheckoutAsync(102, first.Value.ReviewToken, default); Assert.True(retry.Value!.CanProceedToCheckout);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-026")]
    public async Task Prepare_RecalledSelectedLine_LeavesOtherLineUsable()
    {
        var f = new CommerceFixture();
        var item = f.AddCartLine(); var second = Second(f); second.IsSelected = true; var token = f.Pricing.Calculate(f.Cart).ReviewToken; f.Batch.Status = "Recalled";
        var first = await f.CartService.PrepareCheckoutAsync(102, token, default);
        Assert.False(first.Value!.CanProceedToCheckout); Assert.False(item.IsSelected); Assert.Single(first.Value.Items, x => x.IsSelected);
        Assert.True((await f.CartService.PrepareCheckoutAsync(102, first.Value.ReviewToken, default)).Value!.CanProceedToCheckout); Assert.Equal(2, f.Cart.CartItems.Count);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-027")]
    public async Task Prepare_ChangedServerPrice_RequiresReviewAndRetry()
    {
        var f = new CommerceFixture();
        f.AddCartLine(quantity: 2); var token = f.Pricing.Calculate(f.Cart).ReviewToken; f.Product.BaseRetailPrice = 32000;
        var first = await f.CartService.PrepareCheckoutAsync(102, token, default);
        Assert.False(first.Value!.CanProceedToCheckout); Assert.Equal(64000, first.Value.SelectedSubtotal); Assert.Contains(CartMessages.PriceChanged, first.Value.Messages);
        Assert.True((await f.CartService.PrepareCheckoutAsync(102, first.Value.ReviewToken, default)).Value!.CanProceedToCheckout);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-028")]
    public async Task Prepare_UnselectedUnavailableLine_DoesNotBlockCheckout()
    {
        var f = new CommerceFixture();
        f.AddCartLine(selected: false); f.Batch.Status = "Recalled"; Second(f).IsSelected = true;
        var result = await f.CartService.PrepareCheckoutAsync(102, f.Pricing.Calculate(f.Cart).ReviewToken, default);
        Assert.True(result.Value!.CanProceedToCheckout); Assert.Equal(702, Assert.Single(result.Value.Items, x => x.IsSelected).CartItemId); Assert.Equal(20000, result.Value.SelectedSubtotal);
    }

    [Fact, Trait("CaseId", "UT-RC-01-R2-029")]
    public async Task Prepare_CommitFailure_Propagates()
    {
        var f = new CommerceFixture();
        f.AddCartLine(); var token = f.Pricing.Calculate(f.Cart).ReviewToken; f.Work.FailCommit = true;
        await Assert.ThrowsAsync<IOException>(() => f.CartService.PrepareCheckoutAsync(102, token, default)); Assert.Equal(0, f.Work.Commits);
    }

    private static CartItem Second(CommerceFixture f)
    {
        var p = new Product
        {
            ProductId = 302,
            FarmerProfile = f.Farmer,
            FarmerProfileId = 1,
            Category = f.Product.Category,
            ProductName = "Carrot",
            Unit = "kg",
            BaseRetailPrice = 20000,
            ApprovalStatus = "Approved",
            IsPublished = true
        };
        var b = f.NewBatch(402); b.Product = p; b.ProductId = 302;
        return f.AddCartLine(702, 1, false, b, p);
    }
}


