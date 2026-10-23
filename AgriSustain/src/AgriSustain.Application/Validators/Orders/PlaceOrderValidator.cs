using AgriSustain.Application.DTOs.Orders;
using FluentValidation;

namespace AgriSustain.Application.Validators.Orders;

public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderRequest>
{
    /// <summary>
    /// Validates quote identity, demo retry key and explicit confirmation before placement.
    /// Retry-key SQL storage remains deferred under DB-06.
    /// </summary>
    public PlaceOrderValidator()
    {
        RuleFor(request => request.QuoteId).NotEmpty();
        RuleFor(request => request.RequestKey).NotEmpty();
        RuleFor(request => request.Confirmed).Equal(true);
    }
}
