using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Validators.Administration;
using AgriSustain.Application.Validators.Orders;
using AgriSustain.Domain.ValueObjects;
using Xunit;

namespace AgriSustain.UnitTests;

/// <summary>
/// Covers field partitions and adjacent input boundaries used in the three-UC test report.
/// These isolated validator tests verify the current input contract, not SQL/provider integration.
/// </summary>
public sealed class ValidationCoverageTests
{
    /// <summary>
    /// Checks both valid name limits and their adjacent invalid values with all other fields valid.
    /// This prevents an unrelated field failure from hiding an incorrect name-length rule.
    /// </summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public async Task CategoryName_AdjacentBoundaries_ReturnExpectedValidity(int length, bool valid)
    {
        var result = await new SaveCategoryValidator().ValidateAsync(
            new SaveCategoryRequest(new string('A', length), null, null, 0));
        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Contains(result.Errors, error => error.PropertyName == "Name");
        }
    }

    /// <summary>
    /// Tests the description's maximum length and the first rejected length independently.
    /// </summary>
    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public async Task CategoryDescription_AdjacentLimit_ReturnsExpectedValidity(
        int length, bool valid)
    {
        var result = await new SaveCategoryValidator().ValidateAsync(
            new SaveCategoryRequest("Rau", null, new string('A', length), 0));
        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Contains(result.Errors, error => error.PropertyName == "Description");
        }
    }

    /// <summary>
    /// Rejects an otherwise confirmed suspension with an empty reason before service mutation.
    /// </summary>
    [Fact]
    public async Task AccountAction_MissingSuspensionReason_RejectsReason()
    {
        var result = await new AccountActionValidator().ValidateAsync(
            new AccountActionRequest("suspend", "", true));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Reason");
    }

    /// <summary>
    /// Rejects an unsupported action even when the confirmation and reason are valid.
    /// </summary>
    [Fact]
    public async Task AccountAction_UnknownAction_RejectsAction()
    {
        var result = await new AccountActionValidator().ValidateAsync(
            new AccountActionRequest("delete", "Reviewed", true));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Action");
    }

    /// <summary>
    /// Covers the agreed domestic mobile format, adjacent lengths and a disallowed prefix.
    /// Address names/codes are valid fixture input; this test does not validate catalog ownership.
    /// </summary>
    [Theory]
    [InlineData("0901234567", true)]
    [InlineData("090123456", false)]
    [InlineData("09012345678", false)]
    [InlineData("0401234567", false)]
    public async Task CheckoutPhone_Partitions_ReturnExpectedValidity(string phone, bool valid)
    {
        var result = await new CheckoutInputValidator().ValidateAsync(ValidInput(phone));
        Assert.Equal(valid, result.IsValid);
        if (!valid)
        {
            Assert.Contains(result.Errors, error => error.PropertyName == "Address.Phone");
        }
    }

    /// <summary>
    /// Rejects negative requested points without relying on a mocked or persistent point balance.
    /// Conversion, cap, expiry and reservation policies still require the team's business decision.
    /// </summary>
    [Fact]
    public async Task CheckoutPoints_NegativeRequest_RejectsPoints()
    {
        var result = await new CheckoutInputValidator().ValidateAsync(
            ValidInput("0901234567") with
            {
                Points = -1
            });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "Points");
    }

    /// <summary>
    /// Builds otherwise valid isolated validator input, using real catalog codes without a lookup.
    /// Repository, shipping, authentication and payment behavior are outside these unit tests.
    /// </summary>
    private static CheckoutInput ValidInput(string phone) => new(
        new AddressSnapshot("Test buyer", phone, "Thành phố Hồ Chí Minh", "Xã Thạnh An",
            "123 Test street", "12", "268"));
}
