using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.Validators.Checkout;
using Xunit;

namespace AgriSustain.UnitTests.Checkout;

/// <summary>UC-RC-02 Step 3: SePay is the only payment gateway (Report 3, API-01).</summary>
public sealed class CheckoutPaymentMethodTests
{
    private static CheckoutRequest Input(string paymentMethod) =>
        new(new CheckoutAddress("Test buyer", "0901234567", "79", "TP HCM", "760", "Quận 1", "26734", "Bến Nghé",
            "123 Test street"), "Standard", paymentMethod, null, 0, null);

    [Theory(DisplayName = "[UC-RC-02] Only SePay is accepted as the payment method")]
    [InlineData("SePay", true)]
    [InlineData("PayOS", false)]
    [InlineData("", false)]
    public void PaymentMethod_OnlySePay(string method, bool valid)
    {
        var result = new CheckoutValidator().Validate(Input(method));

        Assert.Equal(valid, result.IsValid);
    }
}
