namespace AgriSustain.Application.Common;

public class Result
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public bool IsSuccess => Success;

    public static Result Ok(string message = "Thao tác thành công", int statusCode = 200) =>
        new() { Success = true, Message = message, StatusCode = statusCode };

    public static Result Fail(string message, int statusCode = 400) =>
        new() { Success = false, Message = message, StatusCode = statusCode };
}

public class Result<T> : Result
{
    public T? Data { get; init; }
    public T? Value => Data;
    public Error? Error { get; init; }

    public static Result<T> Ok(T data, string message = "Thao tác thành công", int statusCode = 200) =>
        new() { Success = true, Message = message, StatusCode = statusCode, Data = data };

    public new static Result<T> Fail(string message, int statusCode = 400) =>
        new() { Success = false, Message = message, StatusCode = statusCode, Data = default, Error = new Error("FAIL", message) };

    public static Result<T> SuccessResult(T value) =>
        Ok(value);

    public static Result<T> FailureResult(Error error) =>
        new()
        {
            Success = false,
            Message = error.Message,
            StatusCode = error.Kind switch
            {
                "Unauthorized" => 401,
                "Forbidden" => 403,
                "NotFound" => 404,
                "Conflict" => 409,
                "TooManyRequests" => 429,
                "ServiceUnavailable" => 503,
                "Error" => 500,
                _ => 400
            },
            Data = default,
            Error = error
        };

    public static Result<T> FailureResult(string message, int statusCode = 400) =>
        Fail(message, statusCode);
}
