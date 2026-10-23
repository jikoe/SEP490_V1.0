using FluentValidation.Results;

namespace AgriSustain.Application.Common;

public static class ValidationResultExtensions
{
    public static FeatureResult<T> ToFailure<T>(this ValidationResult result) =>
        new(default, new FeatureError("VALIDATION", "Vui lòng kiểm tra các trường đã nhập.",
            result.Errors.GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray())));
}

