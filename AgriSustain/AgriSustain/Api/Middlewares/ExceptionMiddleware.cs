using AgriSustain.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Middlewares;

public sealed class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IPersistenceErrorClassifier classifier)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Abort();
        }
        catch (Exception exception)
        {
            var conflict = classifier.IsConflict(exception);
            logger.Log(conflict ? LogLevel.Warning : LogLevel.Error, exception,
                "Use case request failed with trace {TraceId}", context.TraceIdentifier);
            context.Response.StatusCode = conflict ? 409 : 500;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = conflict ? "Dữ liệu thay đổi đồng thời. Vui lòng tải lại hoặc thử lại."
                    : "Không thể hoàn tất thao tác. Vui lòng thử lại.",
                Extensions =
                {
                    ["code"] = conflict ? "CONCURRENCY_CONFLICT" : "UNEXPECTED_ERROR",
                    ["traceId"] = context.TraceIdentifier
                }
            }, cancellationToken: context.RequestAborted);
        }
    }
}
