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

            bool farmerPayout = context.Request.Path.StartsWithSegments("/api/finance/farmer-payouts");
            string? farmerPayoutMessage = farmerPayout ? FinancePayoutMessages.Text(conflict ? "MSG174"
                : HttpMethods.IsGet(context.Request.Method) ? "MSG177" : "MSG178") : null;
            bool farmerBusiness = context.Request.Path.StartsWithSegments("/api/farmer/offtake-contracts")
                || context.Request.Path.StartsWithSegments("/api/farmer/revenue")
                || context.Request.Path.StartsWithSegments("/api/business-notifications");
            string? farmerBusinessMessage = farmerBusiness ? FarmerBusinessMessages.Text(conflict ? "MSG122"
                : context.Request.Path.Value?.EndsWith("/export") == true ? "MSG125"
                : HttpMethods.IsGet(context.Request.Method) ? "MSG126" : "MSG123") : null;
            bool farmManagement = context.Request.Path.StartsWithSegments("/api/farmer/crop-seasons")
                || context.Request.Path.StartsWithSegments("/api/farmer/farming-logs")
                || context.Request.Path.StartsWithSegments("/api/farmer/harvest-batches")
                || context.Request.Path.StartsWithSegments("/api/farmer/evidence");
            bool farmerComplaint = context.Request.Path.StartsWithSegments("/api/farmer/complaints");
            string? farmerComplaintMessage = farmerComplaint
                ? conflict ? FarmerComplaintMessages.Conflict : HttpMethods.IsGet(context.Request.Method)
                    ? FarmerComplaintMessages.LoadFailed : FarmerComplaintMessages.SaveFailed : null;
            string? farmMessage = farmManagement
                ? conflict ? FarmManagementMessages.MSG122 : HttpMethods.IsGet(context.Request.Method)
                    ? FarmManagementMessages.MSG126 : FarmManagementMessages.MSG123 : null;
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
            string? financeMessage = context.Request.Path.StartsWithSegments("/api/finance")
                ? conflict ? "Dữ liệu tài chính đã thay đổi. Vui lòng tải lại trước khi thử lại."
                    : HttpMethods.IsGet(context.Request.Method)
                        ? "Không thể tải dữ liệu tài chính. Vui lòng thử lại."
                        : "Không thể lưu kết quả đối soát. Vui lòng tải lại trước khi thử lại."
                : null;
            logger.LogError(exception, "Request failed: {TraceId}", context.TraceIdentifier);
            context.Response.StatusCode = conflict ? 409 : 500;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = farmerPayoutMessage ?? farmerBusinessMessage ?? farmerComplaintMessage ?? farmMessage ?? productMessage ?? cartMessage ?? orderMessage ?? financeMessage ?? (context.Request.Path.StartsWithSegments("/api/farmer/orders")
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
