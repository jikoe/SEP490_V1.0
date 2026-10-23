namespace AgriSustain.Application.Common;

public sealed class Result<T>
{
    /// <summary>
    /// Creates a success/error outcome through the named factories, preventing callers from
    /// inventing an inconsistent value/error pair.
    /// </summary>
    private Result(T? value, Error? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value
    {
        get;
    }
    public Error? Error
    {
        get;
    }
    public bool IsSuccess => Error == null;

    /// <summary>
    /// Wraps the completed application result without adding transport-specific HTTP behavior.
    /// </summary>
    public static Result<T> Success(T value) => new(value, null);
    /// <summary>
    /// Carries a stable expected application error with no successful value, allowing the unit of
    /// work to discard staged effects.
    /// </summary>
    public static Result<T> Failure(Error error) => new(default, error);
}
