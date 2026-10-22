namespace AgriSustain.Application.Common;

public sealed record FeatureResult<T>(T? Value, FeatureError? Error)
{
    public bool IsSuccess => Error is null;

    public static FeatureResult<T> Ok(T value) => new(value, null);

    public static FeatureResult<T> Fail(string code, string message) =>
        new(default, new FeatureError(code, message));
}

