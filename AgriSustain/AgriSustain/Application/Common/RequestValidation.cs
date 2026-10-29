using FluentValidation;

namespace AgriSustain.Application.Common;

public static class RequestValidation
{
    public static async Task<Error?> CheckAsync<T>(
        IValidator<T> validator, T request, CancellationToken cancellationToken)
    {
        var result = await validator.ValidateAsync(request, cancellationToken);
        if (result.IsValid) return null;

        // Group errors by property name for field-level display
        var fieldErrors = result.Errors
            .GroupBy(e => ToCamelCase(e.PropertyName))
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray());

        string summary = string.Join(" ", result.Errors.Select(e => e.ErrorMessage));

        return new Error("INVALID_INPUT", summary, "Validation", fieldErrors);
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}