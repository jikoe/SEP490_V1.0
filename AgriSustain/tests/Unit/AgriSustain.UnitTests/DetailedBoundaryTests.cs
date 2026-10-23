using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Validators.Administration;
using AgriSustain.Domain.Entities.Accounts;
using AgriSustain.Domain.Entities.Ordering;
using AgriSustain.Domain.Exceptions;
using Xunit;

namespace AgriSustain.UnitTests;

/// <summary>Covers adjacent numeric boundaries and invalid state transitions independently of persistence.</summary>
public sealed class DetailedBoundaryTests
{
    /// <summary>Checks exact stock and its adjacent values at the persisted three-decimal precision.</summary>
    [Theory]
    [InlineData("4.999", true)]
    [InlineData("5.000", true)]
    [InlineData("5.001", false)]
    public void Reserve_AdjacentStockBoundary_PreservesConservation(string text, bool allowed)
    {
        decimal quantity = decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
        var batch = new HarvestBatch(5) { Id = 1, ProductId = 2, Code = "BOUNDARY" };
        if (allowed)
        {
            batch.Reserve(quantity, new DateOnly(2026, 10, 19));
            Assert.Equal(5 - quantity, batch.AvailableQuantity);
            Assert.Equal(quantity, batch.ReservedQuantity);
        }
        else
        {
            Assert.Throws<BusinessRuleException>(() => batch.Reserve(quantity, new DateOnly(2026, 10, 19)));
            Assert.Equal(5, batch.AvailableQuantity);
            Assert.Equal(0, batch.ReservedQuantity);
        }
    }

    /// <summary>Verifies that a lock stops applying exactly at its timestamp without changing business status.</summary>
    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, false)]
    [InlineData(1, false)]
    public void TemporaryLock_ExactExpiration_UsesStrictComparison(int seconds, bool locked)
    {
        var until = DateTimeOffset.Parse("2026-10-19T10:00:00Z");
        var user = UserAccount.RestoreLock(new UserAccount
        { Id = 1, Name = "Fixture", Email = "fixture@example.test", Role = "RetailCustomer" }, until, 5);
        Assert.Equal(locked, user.IsTemporarilyLocked(until.AddSeconds(seconds)));
        Assert.Equal("Active", user.Status);
    }

    /// <summary>Checks valid and invalid page-size endpoints without querying any account store.</summary>
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public async Task UserPageSize_AdjacentLimits_ValidatesIndependently(int size, bool expected)
    {
        var result = await new UserQueryValidator().ValidateAsync(new UserQuery(PageSize: size));
        Assert.Equal(expected, result.IsValid);
    }

    /// <summary>Invalid transitions must preserve both status and the mock session version.</summary>
    [Fact]
    public void Activate_AlreadyActive_RejectsWithoutMutation()
    {
        var user = new UserAccount { Id = 1, Name = "Fixture", Email = "fixture@example.test", Role = "RetailCustomer" };
        Assert.Throws<BusinessRuleException>(() => user.Activate("Reviewed"));
        Assert.Equal("Active", user.Status);
        Assert.Equal(0, user.SessionVersion);
    }
}
