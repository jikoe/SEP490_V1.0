using System.Net;
using System.Net.Http.Json;
using AgriSustain.Domain.Entities;
using AgriSustain.IntegrationTests.Commerce;
using AgriSustain.IntegrationTests.UseCaseApi;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.DuIT;

/// <summary>UC-FIN-08 Provide Refund Account (RetailCustomer / WholesaleBuyer) against SQL Server.</summary>
[Trait("Layer", "SqlIntegration"), Trait("UseCase", "UC-FIN-08")]
public sealed class RefundAccountApiTests : IAsyncLifetime
{
    private DuApiHost _api = null!;

    public async Task InitializeAsync() => _api = await DuApiHost.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private static object NewAccount(string number = "1023456789", bool saveAsDefault = false, string? otp = null) => new
    {
        useDefaultAccount = false, bankCode = "VCB", accountNumber = number, accountHolder = (string?)null,
        confirmRecipient = true, saveAsDefault, otpCode = otp
    };

    private Task<HttpResponseMessage> Provide(long refundId, object body) =>
        _api.Customer().PutAsJsonAsync($"api/profile/refunds/{refundId}/account", body);

    private async Task<RefundTransaction> RefundAsync(long id)
    {
        await using var db = _api.Open();
        return await db.RefundTransactions.AsNoTracking().SingleAsync(r => r.RefundTransactionId == id);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-08 #01] The customer's refund list shows only their own refunds and the account state")]
    public async Task List_OwnRefunds()
    {
        var json = await ApiAssert.Status(await _api.Customer().GetAsync("api/profile/refunds"), HttpStatusCode.OK);

        var rows = json.EnumerateArray().ToDictionary(r => r.GetProperty("refundCode").GetString()!, r => r.GetProperty("accountState").GetString());
        Assert.Equal(2, rows.Count);
        Assert.Equal(("AwaitingAccount", "Completed"), (rows["RFD-IT-001"], rows["RFD-IT-002"]));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-08 #02] A looked-up account is stored on the refund with an audit entry; the default account is untouched")]
    public async Task Provide_NewAccount_SavedOnRefund()
    {
        var response = await Provide(_api.PendingRefundId, NewAccount());

        await ApiAssert.Status(response, HttpStatusCode.OK);
        var refund = await RefundAsync(_api.PendingRefundId);
        Assert.Equal(("VCB", "1023456789", "NGUYEN VAN AGRI"), (refund.ReceiverBankCode, refund.ReceiverAccountNumber, refund.ReceiverAccountHolder));
        Assert.Equal(DuApiHost.T0.UtcDateTime, refund.ReceiverAccountProvidedAt);
        await using var db = _api.Open();
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityType == "RefundTransaction" && a.EntityId == _api.PendingRefundId
            && a.Action == "RefundAccountProvided" && a.PerformedByUserId == _api.CustomerId));
        Assert.False(await db.UserBankAccounts.AnyAsync(a => a.UserId == _api.CustomerId));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-08 #03] Using the saved default account copies it onto the refund")]
    public async Task Provide_DefaultAccount_Copied()
    {
        await using (var seed = _api.Open())
        {
            seed.UserBankAccounts.Add(new UserBankAccount
            {
                UserId = _api.CustomerId, BankCode = "VCB", AccountNumber = "5555666677", AccountHolder = "CUSTOMER",
                IsDefault = true, VerificationStatus = "Verified", CreatedAt = DuApiHost.T0.UtcDateTime
            });
            await seed.SaveChangesAsync();
        }

        var response = await Provide(_api.PendingRefundId, new
        {
            useDefaultAccount = true, bankCode = (string?)null, accountNumber = (string?)null, accountHolder = (string?)null,
            confirmRecipient = true, saveAsDefault = false, otpCode = (string?)null
        });

        await ApiAssert.Status(response, HttpStatusCode.OK);
        Assert.Equal("5555666677", (await RefundAsync(_api.PendingRefundId)).ReceiverAccountNumber);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-08 #04] An invalid account number is a field error and nothing is stored")]
    public async Task Provide_InvalidNumber_FieldError()
    {
        var response = await Provide(_api.PendingRefundId, NewAccount(number: "12345"));

        var problem = await ApiAssert.Status(response, HttpStatusCode.BadRequest);
        ApiAssert.HasFieldError(problem, "accountNumber");
        Assert.Null((await RefundAsync(_api.PendingRefundId)).ReceiverAccountNumber);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-08 #05] An account the bank cannot find is refused")]
    public async Task Provide_NotFoundAtBank_FieldError()
    {
        var response = await Provide(_api.PendingRefundId, NewAccount(number: "1023450000"));

        var problem = await ApiAssert.Status(response, HttpStatusCode.BadRequest);
        ApiAssert.HasFieldError(problem, "accountNumber");
        Assert.Null((await RefundAsync(_api.PendingRefundId)).ReceiverAccountNumber);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-08 #06] Saving as default needs the e-mail OTP and then stores the default account")]
    public async Task Provide_SaveAsDefault_WithOtp()
    {
        var withoutOtp = await Provide(_api.PendingRefundId, NewAccount(saveAsDefault: true));
        ApiAssert.HasFieldError(await ApiAssert.Status(withoutOtp, HttpStatusCode.BadRequest), "otpCode");
        await ApiAssert.Status(await _api.Customer().PostAsync("api/profile/refunds/otp", null), HttpStatusCode.OK);

        var response = await Provide(_api.PendingRefundId, NewAccount(saveAsDefault: true, otp: DuApiHost.Otp));

        await ApiAssert.Status(response, HttpStatusCode.OK);
        await using var db = _api.Open();
        var saved = await db.UserBankAccounts.SingleAsync(a => a.UserId == _api.CustomerId);
        Assert.Equal(("1023456789", true), (saved.AccountNumber, saved.IsDefault));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-08 #07] A completed refund is locked")]
    public async Task Provide_CompletedRefund_Locked()
    {
        var response = await Provide(_api.CompletedRefundId, NewAccount());

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "REFUND_LOCKED");
        Assert.Null((await RefundAsync(_api.CompletedRefundId)).ReceiverAccountNumber);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-08 #08] Another customer's refund is reported as not found")]
    public async Task Provide_OtherCustomer_NotFound()
    {
        var response = await Provide(_api.OtherRefundId, NewAccount());

        await ApiAssert.Problem(response, HttpStatusCode.NotFound, "REFUND_NOT_FOUND");
        Assert.Null((await RefundAsync(_api.OtherRefundId)).ReceiverAccountNumber);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-FIN-08 #09] A Farmer cannot use the refund account screen")]
    public async Task List_Farmer_Forbidden()
    {
        var response = await _api.FarmerA().GetAsync("api/profile/refunds");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
