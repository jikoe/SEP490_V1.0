using FluentValidation;

namespace AgriSustain.Application.Common;

public static class RequestValidation
{
    public static async Task<Error?> CheckAsync<T>(
        IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        return result.IsValid ? null : new Error(
            "INVALID_INPUT", string.Join(" ", result.Errors.Select(error => error.ErrorMessage)),
            Fields: result.Errors.GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray()));
    }
}
