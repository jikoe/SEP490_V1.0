using System.Net;
using System.Net.Http.Json;
using AgriSustain.Domain.Entities;
using AgriSustain.IntegrationTests.UT1X;
using AgriSustain.IntegrationTests.UseCaseApi;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.DuIT;

/// <summary>UC-AUTH-03 Manage Profile (personal, farm, KYC submission and the Bank Account tab) against SQL Server.</summary>
[Trait("Layer", "SqlIntegration"), Trait("UseCase", "UC-AUTH-03")]
public sealed class ProfileBankAccountApiTests : IAsyncLifetime
{
    private DuApiHost _api = null!;

    public async Task InitializeAsync() => _api = await DuApiHost.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    private static object Consumer(string phone = "0935662018", string province = "100000", string district = "100300",
        string ward = "19") => new
    {
        fullName = "Mai Tran Updated", phoneNumber = phone, gender = "Female", birthDate = "2004-02-03",
        provinceCity = "x", district = "x", ward = "x", addressDetail = "123 Cầu Giấy",
        provinceCode = province, wardCode = ward, districtCode = district,
        recipientName = "Mai Tran", recipientPhone = "0935662018"
    };

    private static object Farm(string scale = "Vừa") => new
    {
        fullName = "Lê Văn C", phoneNumber = "0901234569", farmName = "Vườn Mới Xanh", representativeName = "Lê Văn C",
        address = "Xã Tân Lập, Đan Phượng", provinceCode = "100000", cultivationZone = "Hà Nội", productionScale = scale,
        cultivatedArea = 2.5m, gpsCoordinates = "21.086400, 105.669800", businessLicenseNumber = (string?)null,
        productionModel = "Individual", shortDescription = "Rau hữu cơ",
        pickupProvinceCode = "100000", pickupDistrictCode = "100300", pickupWardCode = "19", pickupAddressText = "Cổng trang trại"
    };

    private static object Account(string number = "1023456789", string otp = DuApiHost.Otp) => new
    {
        bankCode = "VCB", accountNumber = number, accountHolder = (string?)null, confirmRecipient = true, otpCode = otp
    };

    // ---------- Personal and farm profile ----------

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-03 #01] The signed-in customer reads their own profile")]
    public async Task Get_Customer_OwnProfile()
    {
        var json = await ApiAssert.Status(await _api.Customer().GetAsync("api/profile"), HttpStatusCode.OK);

        Assert.Equal(_api.CustomerId, json.GetProperty("userId").GetInt64());
        Assert.Equal("RetailCustomer", json.GetProperty("role").GetString());
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-03 #02] Updating the personal profile saves the address names from the Goship catalogue")]
    public async Task UpdateConsumer_Valid_SavedWithCatalogNames()
    {
        var response = await _api.Customer().PutAsJsonAsync("api/profile/consumer", Consumer());

        await ApiAssert.Status(response, HttpStatusCode.OK);
        await using var db = _api.Open();
        var user = await db.Users.SingleAsync(u => u.UserId == _api.CustomerId);
        Assert.Equal(("Mai Tran Updated", "0935662018", "Female"), (user.FullName, user.PhoneNumber, user.Gender));
        var address = await db.DeliveryAddresses.SingleAsync(a => a.UserId == _api.CustomerId);
        Assert.Equal(("Hà Nội", "Quận Hai Bà Trưng", "Phường Quỳnh Lôi", "123 Cầu Giấy"),
            (address.ProvinceCity, address.District, address.Ward, address.AddressDetail));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-03 #03] Address codes that are not in the catalogue are refused and nothing changes")]
    public async Task UpdateConsumer_UnknownAddress_Rejected()
    {
        var response = await _api.Customer().PutAsJsonAsync("api/profile/consumer", Consumer(province: "01", district: "001", ward: "0001"));

        await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "ADDRESS_INVALID");
        await using var db = _api.Open();
        Assert.Equal("customer", (await db.Users.SingleAsync(u => u.UserId == _api.CustomerId)).FullName);
        Assert.False(await db.DeliveryAddresses.AnyAsync(a => a.UserId == _api.CustomerId));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-03 #04] A phone number used by another account returns 409")]
    public async Task UpdateConsumer_PhoneTaken_Conflict()
    {
        var response = await _api.Customer().PutAsJsonAsync("api/profile/consumer", Consumer(phone: "0901234567"));

        await ApiAssert.Status(response, HttpStatusCode.Conflict);
        await using var db = _api.Open();
        Assert.Equal("0912000001", (await db.Users.SingleAsync(u => u.UserId == _api.CustomerId)).PhoneNumber);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-03 #05] The farmer updates the farm profile: scale, area, GPS and pickup address")]
    public async Task UpdateFarmer_Valid_Saved()
    {
        var response = await _api.FarmerNew().PutAsJsonAsync("api/profile/farmer", Farm());

        await ApiAssert.Status(response, HttpStatusCode.OK);
        await using var db = _api.Open();
        var farm = await db.FarmerProfiles.SingleAsync(f => f.UserId == _api.FarmerNewId);
        Assert.Equal(("Vườn Mới Xanh", "Vừa", 2.5m), (farm.FarmName, farm.ProductionScale, farm.CultivatedArea));
        Assert.StartsWith("21.0864", farm.Gpscoordinates);
        Assert.Equal(("100000", "100300", "19"), (farm.PickupProvinceCode, farm.PickupDistrictCode, farm.PickupWardCode));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-03 #06] A production scale outside Nhỏ / Vừa / Lớn is a field error")]
    public async Task UpdateFarmer_FreeTextScale_FieldError()
    {
        var response = await _api.FarmerNew().PutAsJsonAsync("api/profile/farmer", Farm(scale: "3.5 ha nhà kính"));

        var problem = await ApiAssert.Status(response, HttpStatusCode.BadRequest);
        ApiAssert.HasFieldError(problem, "productionScale");
        await using var db = _api.Open();
        Assert.Null((await db.FarmerProfiles.SingleAsync(f => f.UserId == _api.FarmerNewId)).ProductionScale);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-03 #07] KYC submission without a payout bank account lists the missing item")]
    public async Task SubmitKyc_NoBankAccount_Incomplete()
    {
        var response = await _api.FarmerNew().PostAsJsonAsync("api/profile/farmer/submit-kyc", new { notes = (string?)null });

        var problem = await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "KYC_INCOMPLETE");
        ApiAssert.HasFieldError(problem, "bankAccount");
        await using var db = _api.Open();
        Assert.Equal("NotSubmitted", (await db.FarmerProfiles.SingleAsync(f => f.UserId == _api.FarmerNewId)).VerificationStatus);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-03 #08] A complete farm profile is submitted for verification (PendingApproval + audit)")]
    public async Task SubmitKyc_Complete_PendingApproval()
    {
        await using (var seed = _api.Open())
        {
            seed.UserBankAccounts.Add(new UserBankAccount
            {
                UserId = _api.FarmerNewId, BankCode = "VCB", AccountNumber = "0071000333333", AccountHolder = "LE VAN C",
                IsDefault = true, VerificationStatus = "Verified", CreatedAt = DuApiHost.T0.UtcDateTime
            });
            await seed.SaveChangesAsync();
        }

        var response = await _api.FarmerNew().PostAsJsonAsync("api/profile/farmer/submit-kyc", new { notes = "Xin duyệt" });

        await ApiAssert.Status(response, HttpStatusCode.OK);
        await using var db = _api.Open();
        Assert.Equal("PendingApproval", (await db.FarmerProfiles.SingleAsync(f => f.UserId == _api.FarmerNewId)).VerificationStatus);
        Assert.True(await db.AuditLogs.AnyAsync(a => a.EntityType == "FarmerProfile" && a.EntityId == _api.FarmNewId
            && a.PerformedByUserId == _api.FarmerNewId));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-03 #09] A RetailCustomer cannot use the farm profile endpoint")]
    public async Task UpdateFarmer_RetailCustomer_Forbidden()
    {
        var response = await _api.Customer().PutAsJsonAsync("api/profile/farmer", Farm());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- Bank Account tab ----------

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-03 #10] OTP-confirmed bank account is verified with the bank holder name and the change is e-mailed")]
    public async Task SaveBankAccount_WithOtp_VerifiedAndNotified()
    {
        await ApiAssert.Status(await _api.Customer().PostAsync("api/profile/bank-account/otp", null), HttpStatusCode.OK);
        var otpMail = Assert.Single(_api.Email.Sent);
        Assert.Equal(("customer@example.test", DuApiHost.Otp), (otpMail.Email, otpMail.Otp));

        var response = await _api.Customer().PutAsJsonAsync("api/profile/bank-account", Account());

        await ApiAssert.Status(response, HttpStatusCode.OK);
        await using var db = _api.Open();
        var account = await db.UserBankAccounts.SingleAsync(a => a.UserId == _api.CustomerId);
        Assert.Equal(("VCB", "1023456789", "NGUYEN VAN AGRI", "Verified", true),
            (account.BankCode, account.AccountNumber, account.AccountHolder, account.VerificationStatus, account.IsDefault));
        var changed = Assert.Single(_api.BankMail.Sent);
        Assert.Equal(("customer@example.test", "6789"), (changed.Email, changed.Last4));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-03 #11] A wrong OTP saves no bank account")]
    public async Task SaveBankAccount_WrongOtp_NothingSaved()
    {
        await ApiAssert.Status(await _api.Customer().PostAsync("api/profile/bank-account/otp", null), HttpStatusCode.OK);

        var response = await _api.Customer().PutAsJsonAsync("api/profile/bank-account", Account(otp: "999999"));

        var problem = await ApiAssert.Status(response, HttpStatusCode.BadRequest);
        ApiAssert.HasFieldError(problem, "otpCode");
        await using var db = _api.Open();
        Assert.False(await db.UserBankAccounts.AnyAsync(a => a.UserId == _api.CustomerId));
        Assert.Empty(_api.BankMail.Sent);
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-03 #12] An account the bank cannot find is a field error and is not saved")]
    public async Task SaveBankAccount_NotFoundAtBank_NotSaved()
    {
        await ApiAssert.Status(await _api.Customer().PostAsync("api/profile/bank-account/otp", null), HttpStatusCode.OK);

        var response = await _api.Customer().PutAsJsonAsync("api/profile/bank-account", Account(number: "1023450000"));

        var problem = await ApiAssert.Problem(response, HttpStatusCode.BadRequest, "BANK_ACCOUNT_NOT_FOUND");
        ApiAssert.HasFieldError(problem, "accountNumber");
        await using var db = _api.Open();
        Assert.False(await db.UserBankAccounts.AnyAsync(a => a.UserId == _api.CustomerId));
    }

    [CommerceSqlFact(DisplayName = "[IT UC-AUTH-03 #13] A farmer cannot remove the payout account")]
    public async Task RemoveBankAccount_Farmer_Rejected()
    {
        await using (var seed = _api.Open())
        {
            seed.UserBankAccounts.Add(new UserBankAccount
            {
                UserId = _api.FarmerAId, BankCode = "VCB", AccountNumber = "0071000444444", AccountHolder = "NGUYEN VAN A",
                IsDefault = true, VerificationStatus = "Verified", CreatedAt = DuApiHost.T0.UtcDateTime
            });
            await seed.SaveChangesAsync();
        }
        await ApiAssert.Status(await _api.FarmerA().PostAsync("api/profile/bank-account/otp", null), HttpStatusCode.OK);

        var response = await _api.FarmerA().PostAsJsonAsync("api/profile/bank-account/remove", new { otpCode = DuApiHost.Otp });

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "FARMER_ACCOUNT_REQUIRED");
        await using var db = _api.Open();
        Assert.True(await db.UserBankAccounts.AnyAsync(a => a.UserId == _api.FarmerAId));
    }
}
