using System.Net;
using System.Net.Http.Json;
using AgriSustain.IntegrationTests.UT1X;
using AgriSustain.IntegrationTests.UseCaseApi;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.DuIT;

/// <summary>UC-AUTH-02 Register WholesaleBuyer account and submit the company dossier (KYC) against SQL Server.</summary>
[Trait("Layer", "SqlIntegration"), Trait("UseCase", "UC-AUTH-02")]
public sealed class BusinessProfileApiTests : IAsyncLifetime
{
    private DuApiHost _api = null!;

    public async Task InitializeAsync() => _api = await DuApiHost.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private static MultipartFormDataContent Dossier(string taxCode = "0109876543", HttpContent? file = null,
        string fileName = "license.pdf")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent("Fresh Foods JSC"), "companyName" },
            { new StringContent(taxCode), "taxCode" },
            { new StringContent("Phạm Văn D"), "representativeName" },
            { new StringContent("Restaurant"), "businessType" },
            { new StringContent("Số 1 Láng Hạ, Đống Đa, Hà Nội"), "businessAddress" },
        };
        form.Add(file ?? DuApiHost.Pdf(), "file", fileName);
        return form;
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-02 #01] WholesaleBuyer registration with OTP creates the account without a dossier")]
    public async Task Register_WholesaleBuyer_CreatesAccount()
    {
        _api.OtpCodes.Next("482913");
        await ApiAssert.Status(await _api.Anonymous().PostAsJsonAsync("api/auth/register", new
        {
            fullName = "Phạm Văn D", phoneNumber = "0977111222", email = "buyer.d@gmail.com", password = "Buyer1234",
            confirmPassword = "Buyer1234", role = "WholesaleBuyer", agreeTerms = true
        }), HttpStatusCode.OK);

        await ApiAssert.Status(await _api.Anonymous().PostAsJsonAsync("api/auth/verify-otp",
            new { email = "buyer.d@gmail.com", otpCode = "482913" }), HttpStatusCode.OK);

        await using var db = _api.Open();
        var user = await db.Users.SingleAsync(u => u.Email == "buyer.d@gmail.com");
        Assert.Equal(("WholesaleBuyer", "Active"), (user.Role, user.Status));
        Assert.False(await db.WholesaleBuyerProfiles.AnyAsync(p => p.UserId == user.UserId));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-02 #02] A buyer without a dossier sees NotSubmitted")]
    public async Task Get_NoDossier_NotSubmitted()
    {
        var json = await ApiAssert.Status(await _api.WholesaleNew().GetAsync("api/profile/business"), HttpStatusCode.OK);

        Assert.Equal("NotSubmitted", json.GetProperty("verificationStatus").GetString());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-02 #03] A valid dossier is saved as PendingApproval with the licence file, audit and officer notifications")]
    public async Task Submit_Valid_PendingApprovalAndNotifiesOfficer()
    {
        var response = await _api.WholesaleNew().PostAsync("api/profile/business", Dossier());

        var json = await ApiAssert.Status(response, HttpStatusCode.OK);
        Assert.Equal("PendingApproval", json.GetProperty("profile").GetProperty("verificationStatus").GetString());
        await using var db = _api.Open();
        var profile = await db.WholesaleBuyerProfiles.SingleAsync(p => p.UserId == _api.WholesaleNewId);
        Assert.Equal(("Fresh Foods JSC", "0109876543", "PendingApproval"), (profile.CompanyName, profile.TaxCode, profile.VerificationStatus));
        Assert.StartsWith($"/api/dossiers/b2b-documents/{_api.WholesaleNewId}/", profile.DocumentUrl);
        string stored = Path.Combine(_api.ContentRoot, "private-files", "dossiers", "b2b-documents",
            _api.WholesaleNewId.ToString(), Path.GetFileName(profile.DocumentUrl!));
        Assert.True(File.Exists(stored), "The licence file must be stored in private dossier storage.");
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityType == "WholesaleBuyerProfile"
            && a.EntityId == profile.WholesaleBuyerProfileId && a.PerformedByUserId == _api.WholesaleNewId));
        var channels = await db.Notifications.Where(n => n.UserId == _api.OfficerId)
            .Select(n => n.DeliveryChannel).OrderBy(c => c).ToListAsync();
        Assert.Equal(new[] { "Email", "InApp" }, channels);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-02 #04] A tax code already used by another company returns 409 and saves nothing")]
    public async Task Submit_DuplicateTaxCode_Conflict()
    {
        var response = await _api.WholesaleNew().PostAsync("api/profile/business", Dossier(taxCode: "0101234567"));

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "TAX_CODE_EXISTS");
        await using var db = _api.Open();
        Assert.False(await db.WholesaleBuyerProfiles.AnyAsync(p => p.UserId == _api.WholesaleNewId));
        Assert.False(await db.Notifications.AnyAsync(n => n.UserId == _api.OfficerId));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-02 #05] An invalid tax code is a field error")]
    public async Task Submit_InvalidTaxCode_FieldError()
    {
        var response = await _api.WholesaleNew().PostAsync("api/profile/business", Dossier(taxCode: "12345"));

        var problem = await ApiAssert.Status(response, HttpStatusCode.BadRequest);
        ApiAssert.HasFieldError(problem, "taxCode");
        await using var db = _api.Open();
        Assert.False(await db.WholesaleBuyerProfiles.AnyAsync(p => p.UserId == _api.WholesaleNewId));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-02 #06] A file that is not really a PDF is rejected before anything is stored")]
    public async Task Submit_FakePdf_FileInvalid()
    {
        var fake = new ByteArrayContent("this is not a pdf"u8.ToArray());

        var response = await _api.WholesaleNew().PostAsync("api/profile/business", Dossier(file: fake));

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "FILE_INVALID");
        await using var db = _api.Open();
        Assert.False(await db.WholesaleBuyerProfiles.AnyAsync(p => p.UserId == _api.WholesaleNewId));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-02 #07] A dossier waiting for review cannot be submitted again")]
    public async Task Submit_WhilePending_Conflict()
    {
        await ApiAssert.Status(await _api.WholesaleNew().PostAsync("api/profile/business", Dossier()), HttpStatusCode.OK);

        var response = await _api.WholesaleNew().PostAsync("api/profile/business", Dossier());

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "INVALID_STATE");
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-02 #08] A RetailCustomer cannot submit a business dossier")]
    public async Task Submit_RetailCustomer_Forbidden()
    {
        var response = await _api.Customer().PostAsync("api/profile/business", Dossier());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var db = _api.Open();
        Assert.False(await db.WholesaleBuyerProfiles.AnyAsync(p => p.UserId == _api.CustomerId));
    }
}
