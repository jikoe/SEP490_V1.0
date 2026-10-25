using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.Validators.Administration;
using AgriSustain.Application.Validators.Checkout;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.UnitTests;

public sealed class BusinessRuleTests
{
    [Theory]
    [InlineData("suspend", "", true, false)]
    [InlineData("suspend", "reason", false, false)]
    [InlineData("activate", "reason", true, true)]
    [InlineData("unlock", null, true, true)]
    [InlineData("delete", "reason", true, false)]
    public async Task ValidateAccountAction_ConfirmationAndReason_EnforcesContract(
        string action, string? reason, bool confirmed, bool expected)
    {
        var result = await new AccountActionValidator().ValidateAsync(
            new AccountActionRequest(action, reason, confirmed));
        Assert.Equal(expected, result.IsValid);
    }

    [Theory]
    [InlineData("Active", true)]
    [InlineData("Suspended", false)]
    public void CanBeManagedBy_ActorStatus_DeniesInactiveAdministrator(string status, bool expected)
    {
        var target = new User { UserId = 2 };
        var actor = new User { UserId = 1, Role = "SystemAdmin", Status = status };
        Assert.Equal(expected, target.CanBeManagedBy(actor));
        Assert.False(actor.CanBeManagedBy(actor));
    }

    [Theory]
    [InlineData("Farmer", true)]
    [InlineData("WholesaleBuyer", true)]
    [InlineData("RetailCustomer", true)]
    [InlineData("SystemAdmin", false)]
    [InlineData("VerificationOfficer", false)]
    public void Suspend_Role_OnlyMarketplaceAccounts(string role, bool expected)
    {
        var user = new User { Role = role, Status = "Active" };
        Assert.Equal(expected, user.CanBeSuspended);
        if (expected)
        {
            user.Suspend("reason");
            Assert.Equal("Suspended", user.Status);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => user.Suspend("reason"));
            Assert.Equal("Active", user.Status);
        }
    }

    [Fact]
    public void Activate_PendingAccount_DoesNotBypassApproval()
    {
        var user = new User { Status = "Pending" };
        Assert.Throws<InvalidOperationException>(() => user.Activate("reason"));
        Assert.Equal("Pending", user.Status);
    }

    [Theory]
    [InlineData("Available", 2, true)]
    [InlineData("Recalled", 2, false)]
    [InlineData("Expired", 2, false)]
    [InlineData("Available", 11, false)]
    [InlineData("Available", 0, false)]
    public void Reserve_BatchStateAndQuantity_ProtectsInventoryInvariant(
        string status, decimal quantity, bool expected)
    {
        var today = new DateOnly(2026, 10, 22);
        var batch = new HarvestBatch { Status = status, ActualHarvestDate = today,
            StockEnteredAt = today.ToDateTime(TimeOnly.MinValue), AvailableQuantity = 10,
            ExpirationDate = today.AddDays(2) };
        if (!expected)
        {
            Assert.Throws<InvalidOperationException>(() => batch.Reserve(quantity, today));
            Assert.Equal(10, batch.AvailableQuantity);
            Assert.Equal(0, batch.ReservedQuantity);
            return;
        }
        batch.Reserve(quantity, today);
        Assert.Equal(10, batch.AvailableQuantity + batch.ReservedQuantity);
        batch.ReleaseReservation(quantity);
        Assert.Equal(10, batch.AvailableQuantity);
        Assert.Throws<InvalidOperationException>(() => batch.ReleaseReservation(quantity));
    }

    [Theory]
    [InlineData(1, 10000)]
    [InlineData(9.999, 10000)]
    [InlineData(10, 8000)]
    public void UnitPrice_WholesaleThreshold_UsesCurrentPolicy(decimal quantity, decimal expected)
    {
        var product = new Product { BaseRetailPrice = 10000,
            WholesaleMinQuantity = 10, WholesalePrice = 8000 };
        Assert.Equal(expected, RetailPricing.UnitPrice(product, quantity));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(99999)]
    public void Split_RoundingRemainder_PreservesExactTotal(decimal total)
    {
        var shares = RetailPricing.Split(total, [1m, 2m, 3m]);
        Assert.Equal(total, shares.Sum());
        Assert.All(shares, share => Assert.True(share >= 0));
    }

    [Theory]
    [InlineData("0912345678", true)]
    [InlineData("+84912345678", true)]
    [InlineData("12345", false)]
    [InlineData("09123456789", false)]
    public async Task ValidateCheckout_Phone_EnforcesVietnameseMobileFormat(string phone, bool expected)
    {
        var input = new CheckoutRequest(new CheckoutAddress("Recipient", phone, "TEST",
            "Province", "D", "District", "W", "Ward", "Test address"), "Standard", "PayOS", null, 0, null);
        Assert.Equal(expected, (await new CheckoutValidator().ValidateAsync(input)).IsValid);
    }

    [Theory]
    [InlineData("Name", 501, false)]
    [InlineData("", 0, false)]
    [InlineData("Name", 500, true)]
    public async Task ValidateCategory_NameAndDescription_EnforcesLimits(string name,
        int descriptionLength, bool expected)
    {
        var input = new CategoryRequest(name, new string('a', descriptionLength), true, false);
        Assert.Equal(expected, (await new CategoryValidator().ValidateAsync(input)).IsValid);
    }

    [Fact]
    public void Model_SQLServerScript_HasDurableIdempotencyAndCategoryUniqueness()
    {
        using var context = new AgriSustainContext(new DbContextOptionsBuilder<AgriSustainContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ModelOnly;Integrated Security=True")
            .Options);
        var script = context.Database.GenerateCreateScript();
        Assert.Contains("UQ_Order_CheckoutRequest", script);
        Assert.Contains("UX_Order_Quote", script);
        Assert.Contains("UX_Category_Parent_Name", script);
        Assert.Contains("CREATE TABLE [CategorySettings]", script);
        Assert.DoesNotContain("ImageBytes", script);
        Assert.Contains("AvailableDelta", script);
        Assert.True(context.Model.FindEntityType(typeof(HarvestBatch))!
            .FindProperty(nameof(HarvestBatch.AvailableQuantity))!.IsConcurrencyToken);
    }
}
