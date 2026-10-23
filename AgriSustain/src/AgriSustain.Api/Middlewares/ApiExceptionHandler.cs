using AgriSustain.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AgriSustain.Api.Middlewares;

/// <summary>
/// Receives the API logger for centralized technical-failure reporting; expected business
/// failures are already Result values.
/// </summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>
    /// Logs an unexpected failure on the server and returns sanitized ProblemDetails with a trace
    /// ID; database queries, credentials and stack traces are never sent to the client.
    /// </summary>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is OperationCanceledException
            && context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
            return true;
        }

        bool domainFailure = exception is BusinessRuleException;
        bool unavailableDatabase = IsUnavailableDatabase(exception);
        if (!domainFailure)
        {
            logger.LogError(exception, "Three-UC request failed with trace {TraceId}",
                context.TraceIdentifier);
        }

        context.Response.StatusCode = domainFailure ? 409 : unavailableDatabase ? 503 : 500;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = context.Response.StatusCode,
            Title = domainFailure ? exception.Message : unavailableDatabase
                ? "Không kết nối được cơ sở dữ liệu. Kiểm tra SQL Server/LocalDB rồi tải lại."
                : "Không thể xử lý. Vui lòng thử lại.",
            Extensions = { ["code"] = domainFailure ? "BUSINESS_CONFLICT"
                    : unavailableDatabase ? "DATABASE_UNAVAILABLE" : "SERVER_ERROR",
                ["traceId"] = context.TraceIdentifier }
        }, cancellationToken);
        return true;
    }

    /// <summary>
    /// Recognizes SQL connection failures even when EF Core wraps them in a transient-failure
    /// InvalidOperationException or a persistence exception. Other database errors remain 500.
    /// </summary>
    private static bool IsUnavailableDatabase(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is SqlException sql
                && (sql.Class >= 20 || sql.Number is -2 or -1 or 2 or 53 or 4060 or 18456))
            {
                return true;
            }
        }
        return false;
    }
}
