using System.Text.Json;
using AgriSustain.Api.Middlewares;
using AgriSustain.Application.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgriSustain.UnitTests.TestIT3X;

public sealed class FarmerBusinessErrorResponseTests
{
    [Theory]
    [InlineData("GET", "/api/farmer/offtake-contracts", "MSG126", false, false)]
    [InlineData("POST", "/api/farmer/offtake-contracts/10/dispatch", "MSG123", false, false)]
    [InlineData("POST", "/api/farmer/offtake-contracts/10/dispatch", "MSG122", false, true)]
    [InlineData("POST", "/api/farmer/revenue/payment-requests", "MSG178", true, false)]
    [InlineData("GET", "/api/finance/farmer-payouts", "MSG177", true, false)]
    [InlineData("POST", "/api/finance/farmer-payouts/1/reconcile", "MSG178", true, false)]
    [InlineData("POST", "/api/finance/farmer-payouts/1/reconcile", "MSG174", true, true)]
    public async Task BackendFailureUsesTheSpecifiedErrorMessage(
        string method, string path, string code, bool payout, bool conflict)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        await using var response = new MemoryStream();
        context.Response.Body = response;
        Exception failure = conflict ? new DbUpdateConcurrencyException() : new IOException();
        var middleware = new FeatureExceptionMiddleware(
            _ => Task.FromException(failure), NullLogger<FeatureExceptionMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(conflict ? 409 : 500, context.Response.StatusCode);
        response.Position = 0;
        using var error = await JsonDocument.ParseAsync(response);
        var message = payout ? FinancePayoutMessages.Text(code) : FarmerBusinessMessages.Text(code);
        Assert.Equal(message, error.RootElement.GetProperty("title").GetString());
        Assert.Equal(conflict ? "CONFLICT" : "SERVER_ERROR", error.RootElement.GetProperty("code").GetString());
    }
}
