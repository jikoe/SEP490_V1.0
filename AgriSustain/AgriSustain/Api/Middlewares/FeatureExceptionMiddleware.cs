using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Api.Middlewares;

public sealed class FeatureExceptionMiddleware(RequestDelegate next,
    ILogger<FeatureExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
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
            bool conflict = exception is DbUpdateConcurrencyException
                || exception is SqlException { Number: 1205 or 2601 or 2627 }
                || exception.InnerException is SqlException { Number: 1205 or 2601 or 2627 };
            logger.LogError(exception, "Request failed: {TraceId}", context.TraceIdentifier);
            context.Response.StatusCode = conflict ? 409 : 500;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = conflict ? "Dữ liệu thay đổi, tải lại và thử lại (MSG122)."
                    : HttpMethods.IsGet(context.Request.Method)
                        ? "Không thể tải dữ liệu. Vui lòng thử lại (MSG126)."
                        : "Không thể lưu dữ liệu. Vui lòng thử lại (MSG123).",
                Extensions = { ["code"] = conflict ? "CONFLICT" : "SERVER_ERROR",
                    ["traceId"] = context.TraceIdentifier }
            }, context.RequestAborted);
        }
    }
}

