using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Validators.Administration;
using AgriSustain.Domain.Entities.Accounts;
using AgriSustain.Domain.Entities.Catalog;
using AgriSustain.Domain.Entities.Ordering;
using AgriSustain.Domain.Exceptions;
using Xunit;

namespace AgriSustain.UnitTests;

public sealed class BusinessRulesTests
{
    /// <summary>
    /// Uses an isolated fixture to verify that suspend/activate transitions keep the old fixture
    /// session revoked. This is not evidence of real JWT/payment integration or durable
    /// multi-process SQL behavior.
    /// </summary>
    [Fact]
    public void Suspend_ActiveAccount_InvalidatesExistingSession()
    {
        var user = new UserAccount
        {
            Id = 1,
            Name = "Test",
            Email = "test@example.test",
            Role = "RetailCustomer"
        };
        user.Suspend("Test reason");
        user.Activate("Reviewed");
        Assert.Equal("Active", user.Status);
        Assert.Equal(1, user.SessionVersion);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that unlock clears login lock metadata while preserving
    /// business status. This is not evidence of real JWT/payment integration or durable
    /// multi-process SQL behavior.
    /// </summary>
    [Fact]
    public void Unlock_TemporaryLock_ClearsCounterWithoutChangingStatus()
    {
        DateTimeOffset now = DateTimeOffset.Parse("2026-10-18T03:00:00Z");
        var user = UserAccount.RestoreLock(new UserAccount
        {
            Id = 1,
            Name = "Test",
            Email = "test@example.test",
            Role = "RetailCustomer"
        }, now.AddHours(1), 5);
        user.Unlock(now);
        Assert.Null(user.LockedUntil);
        Assert.Equal(0, user.FailedLoginCount);
        Assert.Equal("Active", user.Status);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that nonpositive or excessive demand throws before
    /// changing either inventory counter. This is not evidence of real JWT/payment integration or
    /// durable multi-process SQL behavior.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(6)]
    public void Reserve_InvalidQuantity_PreservesInventory(decimal quantity)
    {
        var batch = new HarvestBatch(5) { Id = 1, ProductId = 2, Code = "TEST" };
        Assert.Throws<BusinessRuleException>(() =>
            batch.Reserve(quantity, new DateOnly(2026, 10, 18)));
        Assert.Equal(5, batch.AvailableQuantity);
        Assert.Equal(0, batch.ReservedQuantity);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that reserving the exact available quantity yields zero
    /// available and the correct reserved amount. This is not evidence of real JWT/payment
    /// integration or durable multi-process SQL behavior.
    /// </summary>
    [Fact]
    public void Reserve_ExactStock_AllowsZeroAvailable()
    {
        var batch = new HarvestBatch(5) { Id = 1, ProductId = 2, Code = "TEST" };
        batch.Reserve(5, new DateOnly(2026, 10, 18));
        Assert.Equal(0, batch.AvailableQuantity);
        Assert.Equal(5, batch.ReservedQuantity);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that the approved hidden-category state can be
    /// activated without archive metadata. This is not evidence of real JWT/payment integration
    /// or durable multi-process SQL behavior.
    /// </summary>
    [Fact]
    public void SetActive_HiddenCategory_CanBeActivatedWithoutArchiveState()
    {
        var category = new Category { Id = 1 };
        category.Update("Rau", null, null, 0, false);
        category.SetActive(true);
        Assert.True(category.IsActive);
    }

    /// <summary>
    /// Uses an isolated fixture to verify that invalid category name/priority input is rejected
    /// before mutation. This is not evidence of real JWT/payment integration or durable
    /// multi-process SQL behavior.
    /// </summary>
    [Theory]
    [InlineData("A", -1)]
    [InlineData("  ", 0)]
    public async Task ValidateAsync_InvalidCategory_RejectsInput(string name, int priority)
    {
        var validator = new SaveCategoryValidator();
        var result = await validator.ValidateAsync(new SaveCategoryRequest(
            name, null, null, priority));
        Assert.False(result.IsValid);
    }
}
