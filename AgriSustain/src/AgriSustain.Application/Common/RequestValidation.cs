using FluentValidation;

namespace AgriSustain.Application.Common;

public static class RequestValidation
{
    /// <summary>
    /// Runs FluentValidation asynchronously with cancellation and translates input failures into
    /// the shared application error contract before any mutation.
    /// </summary>
    public static async Task<Error?> CheckAsync<T>(
        IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        return result.IsValid ? null : new Error(
            "INVALID_INPUT", string.Join(" ", result.Errors.Select(error => error.ErrorMessage)));
    }
}
