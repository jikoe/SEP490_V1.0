using Microsoft.AspNetCore.Mvc;
using AgriSustain.Application.Common;
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
            var sqlEx = (exception as SqlException) ?? (exception.InnerException as SqlException) ?? (exception.GetBaseException() as SqlException);
            bool conflict = exception is DbUpdateConcurrencyException
                || (sqlEx != null && sqlEx.Number is 1205 or 2601 or 2627 or 3960);

            bool productListing = context.Request.Path.StartsWithSegments("/api/farmer/products")
                || context.Request.Path.Equals(new PathString("/api/files/images"));
            string? productMessage = productListing
                ? conflict ? ProductListingMessages.MSG122
                    : HttpMethods.IsGet(context.Request.Method)
                        ? ProductListingMessages.MSG126 : ProductListingMessages.MSG123
                : null;
            string? cartMessage = context.Request.Path.StartsWithSegments("/api/cart")
                ? conflict ? "Dữ liệu thay đổi, tải lại và thử lại."
                    : HttpMethods.IsGet(context.Request.Method)
                        ? "Không thể tải dữ liệu. Vui lòng thử lại."
                        : "Không thể lưu dữ liệu. Vui lòng thử lại."
                : null;
            string? orderMessage = context.Request.Path.StartsWithSegments("/api/orders")
                ? conflict ? "Dữ liệu đã thay đổi. Vui lòng tải lại và thử lại."
                    : HttpMethods.IsGet(context.Request.Method)
                        ? "Không thể tải thông tin đơn hàng. Vui lòng thử lại."
                        : "Không thể gửi yêu cầu. Vui lòng thử lại sau."
                : null;
            logger.LogError(exception, "Request failed: {TraceId}", context.TraceIdentifier);
            context.Response.StatusCode = conflict ? 409 : 500;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = productMessage ?? cartMessage ?? orderMessage ?? (context.Request.Path.StartsWithSegments("/api/farmer/orders")
                    || context.Request.Path.StartsWithSegments("/api/files/evidence")
                    ? conflict ? "Yêu cầu xung đột với dữ liệu hiện tại hoặc đã được xử lý. Vui lòng tải lại; không thực hiện thao tác trùng lặp."
                        : HttpMethods.IsGet(context.Request.Method)
                            ? "Không thể tải dữ liệu xử lý đơn hàng. Vui lòng thử lại."
                            : "Không thể lưu dữ liệu hoặc tải bằng chứng. Không có cập nhật nghiệp vụ dở dang được lưu. Vui lòng thử lại."
                    : null) ?? (conflict ? "Dữ liệu thay đổi, tải lại và thử lại (MSG122)."
                    : HttpMethods.IsGet(context.Request.Method)
                        ? "Không thể tải dữ liệu. Vui lòng thử lại (MSG126)."
                        : "Không thể lưu dữ liệu. Vui lòng thử lại (MSG123)."),
                Extensions = { ["code"] = conflict ? "CONFLICT" : "SERVER_ERROR",
                    ["traceId"] = context.TraceIdentifier }
            }, context.RequestAborted);
        }
    }
}

