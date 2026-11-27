using System.Net;
using System.Net.Http.Json;
using AgriSustain.IntegrationTests.UT1X;
using AgriSustain.IntegrationTests.UseCaseApi;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.DuIT;

/// <summary>UC-AUTH-01 Register account (RetailCustomer / Farmer) through AuthController against SQL Server.</summary>
[Trait("Layer", "SqlIntegration"), Trait("UseCase", "UC-AUTH-01")]
public sealed class AuthRegistrationApiTests : IAsyncLifetime
{
    private DuApiHost _api = null!;

    public async Task InitializeAsync() => _api = await DuApiHost.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private static object Registration(string email = "vana@gmail.com", string phone = "0987654321",
        string role = "RetailCustomer", string password = "Agri1234", string? confirm = null, bool terms = true) => new
    {
        fullName = "Nguyen Van A", phoneNumber = phone, email, password,
        confirmPassword = confirm ?? password, role, agreeTerms = terms
    };

    private Task<HttpResponseMessage> Post(string url, object body) => _api.Anonymous().PostAsJsonAsync(url, body);

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-01 #01] Valid registration e-mails one OTP and creates no account yet")]
    public async Task Register_Valid_SendsOtpOnly()
    {
        _api.OtpCodes.Next("482913");

        var response = await Post("api/auth/register", Registration());

        string body = (await ApiAssert.Status(response, HttpStatusCode.OK)).ToString();
        Assert.DoesNotContain("482913", body);
        var sent = Assert.Single(_api.Email.Sent);
        Assert.Equal(("vana@gmail.com", "482913"), (sent.Email, sent.Otp));
        await using var db = _api.Open();
        Assert.False(await db.Users.AnyAsync(u => u.Email == "vana@gmail.com"));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-01 #02] Correct OTP creates an active, verified RetailCustomer with a hashed password")]
    public async Task VerifyOtp_Correct_CreatesRetailCustomer()
    {
        _api.OtpCodes.Next("482913");
        await ApiAssert.Status(await Post("api/auth/register", Registration()), HttpStatusCode.OK);

        var response = await Post("api/auth/verify-otp", new { email = "vana@gmail.com", otpCode = "482913" });

        var json = await ApiAssert.Status(response, HttpStatusCode.OK);
        Assert.Equal("RetailCustomer", json.GetProperty("role").GetString());
        await using var db = _api.Open();
        var user = await db.Users.SingleAsync(u => u.Email == "vana@gmail.com");
        Assert.Equal(("RetailCustomer", "Active", true, "0987654321"), (user.Role, user.Status, user.IsEmailVerified, user.PhoneNumber));
        Assert.NotEqual("Agri1234", user.PasswordHash);
        Assert.StartsWith("$2", user.PasswordHash);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-01 #03] Farmer registration also creates a NotSubmitted farm profile")]
    public async Task VerifyOtp_Farmer_CreatesFarmProfile()
    {
        _api.OtpCodes.Next("654321");
        await ApiAssert.Status(await Post("api/auth/register",
            Registration("htx@gmail.com", "0987650000", "Farmer", "Farmer123")), HttpStatusCode.OK);

        await ApiAssert.Status(await Post("api/auth/verify-otp", new { email = "htx@gmail.com", otpCode = "654321" }),
            HttpStatusCode.OK);

        await using var db = _api.Open();
        var user = await db.Users.Include(u => u.FarmerProfile).SingleAsync(u => u.Email == "htx@gmail.com");
        Assert.Equal("Farmer", user.Role);
        Assert.NotNull(user.FarmerProfile);
        Assert.Equal("NotSubmitted", user.FarmerProfile!.VerificationStatus);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-01 #04] An e-mail that already has an account returns 409 and sends nothing")]
    public async Task Register_DuplicateEmail_Conflict()
    {
        var response = await Post("api/auth/register", Registration("customer@example.test"));

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "EMAIL_EXISTS");
        Assert.Empty(_api.Email.Sent);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-01 #05] A phone number that already has an account returns 409")]
    public async Task Register_DuplicatePhone_Conflict()
    {
        var response = await Post("api/auth/register", Registration(phone: "0912000001"));

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "PHONE_EXISTS");
        Assert.Empty(_api.Email.Sent);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-01 #06] Mismatched confirmation and unticked terms are field errors")]
    public async Task Register_InvalidInput_FieldErrors()
    {
        var response = await Post("api/auth/register", Registration(confirm: "Agri9999", terms: false));

        var problem = await ApiAssert.Status(response, HttpStatusCode.BadRequest);
        ApiAssert.HasFieldError(problem, "confirmPassword");
        ApiAssert.HasFieldError(problem, "agreeTerms");
        Assert.Empty(_api.Email.Sent);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-01 #07] A wrong OTP creates no account")]
    public async Task VerifyOtp_Wrong_NoAccount()
    {
        _api.OtpCodes.Next("482913");
        await ApiAssert.Status(await Post("api/auth/register", Registration()), HttpStatusCode.OK);

        var response = await Post("api/auth/verify-otp", new { email = "vana@gmail.com", otpCode = "000000" });

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "INVALID_OTP");
        await using var db = _api.Open();
        Assert.False(await db.Users.AnyAsync(u => u.Email == "vana@gmail.com"));
    }
}
