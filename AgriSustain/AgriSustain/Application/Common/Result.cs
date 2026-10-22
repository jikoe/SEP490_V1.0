namespace AgriSustain.Application.Common
{
    public class Result
    {
        public bool Success { get; init; }
        public string Message { get; init; } = string.Empty;
        public int StatusCode { get; init; }

        public static Result Ok(string message = "Thao tác thành công", int statusCode = 200) =>
            new() { Success = true, Message = message, StatusCode = statusCode };

        public static Result Fail(string message, int statusCode = 400) =>
            new() { Success = false, Message = message, StatusCode = statusCode };
    }

    public class Result<T> : Result
    {
        public T? Data { get; init; }

        public static Result<T> Ok(T data, string message = "Thao tác thành công", int statusCode = 200) =>
            new() { Success = true, Message = message, StatusCode = statusCode, Data = data };

        public new static Result<T> Fail(string message, int statusCode = 400) =>
            new() { Success = false, Message = message, StatusCode = statusCode, Data = default };
    }
}
