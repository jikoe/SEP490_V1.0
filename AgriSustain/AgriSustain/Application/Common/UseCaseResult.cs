namespace AgriSustain.Application.Common;

public sealed record UseCaseResult<T>(T? Value, Error? Error)
{
    public bool IsSuccess => Error is null;

    public static UseCaseResult<T> Success(T value) => new(value, null);

    public static UseCaseResult<T> Failure(string code, string message, string kind = "Validation")
    {
        return new(default, new Error(code, message, kind));
    }

    public static UseCaseResult<T> Validation(IReadOnlyDictionary<string, string[]> fields)
    {
        return new(default, new Error("VALIDATION", "Vui lòng kiểm tra các trường được chỉ rõ.",
            "Validation", fields));
    }
}
