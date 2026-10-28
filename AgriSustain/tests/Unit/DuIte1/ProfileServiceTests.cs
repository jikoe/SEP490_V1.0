using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Services.Profiles;
using AgriSustain.Application.Validators.Profiles;
using Xunit;

namespace AgriSustain.UnitTests.DuIte1;

/// <summary>UC-AUTH-03 Manage Profile. Report 5.1 – Module 2.</summary>
public sealed class ProfileServiceTests
{
    private const long Consumer = 10, Farmer = 20, Staff = 30;
    private readonly FakeUnitOfWork _uow = new();

    public ProfileServiceTests()
    {
        _uow.ProfileRepo.Accounts[Consumer] = new(Consumer, "RetailCustomer", "Active");
        _uow.ProfileRepo.Accounts[Farmer] = new(Farmer, "Farmer", "Active");
        _uow.ProfileRepo.Accounts[Staff] = new(Staff, "CustomerSupport", "Active");
        _uow.ProfileRepo.Profiles[Consumer] = Profile(Consumer, "RetailCustomer");
        _uow.ProfileRepo.Profiles[Farmer] = Profile(Farmer, "Farmer", Farm("Approved", 4.85m));
        _uow.ProfileRepo.Profiles[Staff] = Profile(Staff, "CustomerSupport");
        _uow.ProfileRepo.Phones.Add(("0912345678", Consumer));
        _uow.ProfileRepo.Phones.Add(("0987654321", Farmer));
    }

    private ProfileService Service(long? userId) => new(
        _uow, new FakeCurrentUser(userId), new FakeAddressCatalog(),
        new UpdateConsumerProfileValidator(), new UpdateFarmerProfileValidator());

    private static UserProfileResponse Profile(long id, string role, FarmerProfileDto? farm = null) =>
        new(id, "Mai Tran", "0900000000", $"u{id}@test.vn", role, "Active", null, null, null, 0,
            role == "RetailCustomer"
                ? new DeliveryAddressDto(1, "Mai Tran", "0900000000", "Hà Nội", "Cầu Giấy", "Dịch Vọng",
                    "123 Cầu Giấy", "100000", "100100", "1001001", true)
                : null,
            farm);

    private static FarmerProfileDto Farm(string status, decimal score = 5m) =>
        new(1, "Nong Trai Xanh", "Tran B", null, null, "Đà Lạt", null, "BL-001", "Thôn 4", "",
            status, score, null, null);

    private static UpdateConsumerProfileRequest ConsumerRequest(
        string name = "Mai Tran Updated", string phone = "0935662018", string? province = "100000",
        string? district = "100100", string? ward = "1001001", string? detail = "123 Cau Giay") =>
        new(name, phone, null, null, null, null, null, detail, province, ward, district);

    private static UpdateFarmerProfileRequest FarmerRequest(
        decimal? area = 2.5m, string phone = "0987654321", string license = "BL-999", string? gps = null) =>
        new("Tran B", phone, "Nong Trai Xanh", "Tran B", "Thôn 4, Đà Lạt", null, "Đà Lạt", "Cá nhân",
            area, gps, license);

    // ---------- GetCurrentProfileAsync ----------

    [Fact(DisplayName = "UTC013 RetailCustomer sees own profile with default address")]
    public async Task UTC013_ConsumerProfile_WithAddress()
    {
        var result = await Service(Consumer).GetCurrentProfileAsync(default);

        Assert.True(result.IsSuccess);
        Assert.Equal(Consumer, result.Value!.UserId);
        Assert.Equal("RetailCustomer", result.Value.Role);
        Assert.NotNull(result.Value.DeliveryAddress);
    }

    [Fact(DisplayName = "UTC014 Farmer sees farm details, verification status and reputation")]
    public async Task UTC014_FarmerProfile_WithFarmDetails()
    {
        var result = await Service(Farmer).GetCurrentProfileAsync(default);

        Assert.Equal("Approved", result.Value!.FarmerProfile!.VerificationStatus);
        Assert.Equal(4.85m, result.Value.FarmerProfile.ReputationScore);
    }

    [Fact(DisplayName = "UTC015 Unknown user returns NotFound")]
    public async Task UTC015_UnknownUser_NotFound()
    {
        var result = await Service(999).GetCurrentProfileAsync(default);

        Assert.Equal("NotFound", result.Error!.Kind);
        Assert.Equal(404, result.StatusCode);
    }

    [Fact(DisplayName = "UTC-AUTH03-B1 Anonymous caller gets 401")]
    public async Task UTC_B1_Anonymous_Unauthorized()
    {
        var get = await Service(null).GetCurrentProfileAsync(default);
        var put = await Service(null).UpdateConsumerProfileAsync(ConsumerRequest(), default);

        Assert.Equal(401, get.StatusCode);
        Assert.Equal(401, put.StatusCode);
    }

    // ---------- UpdateConsumerProfileAsync ----------

    [Fact(DisplayName = "UTC016 Consumer updates name, phone and catalog address")]
    public async Task UTC016_ConsumerUpdate_Succeeds()
    {
        var result = await Service(Consumer).UpdateConsumerProfileAsync(ConsumerRequest(), default);

        Assert.True(result.IsSuccess);
        var saved = _uow.ProfileRepo.LastConsumerUpdate!;
        Assert.Equal("Hà Nội", saved.ProvinceCity);                 // names come from the catalog, not the client
        Assert.Equal("Cầu Giấy", saved.District);
        Assert.Equal("Dịch Vọng", saved.Ward);
        Assert.Equal(1, _uow.Commits);
    }

    [Fact(DisplayName = "UTC018 Invalid phone is rejected and nothing is saved")]
    public async Task UTC018_InvalidPhone_Rejected()
    {
        var result = await Service(Consumer).UpdateConsumerProfileAsync(ConsumerRequest(phone: "abc123"), default);

        Assert.Contains("phoneNumber", result.Error!.Fields!.Keys);
        Assert.Null(_uow.ProfileRepo.LastConsumerUpdate);
    }

    [Theory(DisplayName = "UTC019 Boundary: name 2..50 and address detail up to 150")]
    [InlineData(2, 150, true)]
    [InlineData(1, 10, false)]
    [InlineData(51, 10, false)]
    [InlineData(10, 151, false)]
    public async Task UTC019_Boundaries(int nameLength, int detailLength, bool ok)
    {
        var request = ConsumerRequest(name: new string('a', nameLength), detail: new string('d', detailLength));

        var result = await Service(Consumer).UpdateConsumerProfileAsync(request, default);

        Assert.Equal(ok, result.IsSuccess);
    }

    [Fact(DisplayName = "UTC-AUTH03-B2 Province without district/ward is rejected")]
    public async Task UTC_B2_PartialAddress_Rejected()
    {
        var result = await Service(Consumer).UpdateConsumerProfileAsync(
            ConsumerRequest(district: null, ward: null), default);

        Assert.Contains("districtCode", result.Error!.Fields!.Keys);
        Assert.Contains("wardCode", result.Error.Fields.Keys);
    }

    [Fact(DisplayName = "UTC-AUTH03-B3 Codes unknown to the catalog are rejected")]
    public async Task UTC_B3_UnknownCatalogCodes_Rejected()
    {
        var result = await Service(Consumer).UpdateConsumerProfileAsync(
            ConsumerRequest(province: "01", district: "001", ward: "0001"), default);

        Assert.Equal("ADDRESS_INVALID", result.Error!.Code);
    }

    [Fact(DisplayName = "UTC-AUTH03-B4 Phone already used by another account returns Conflict")]
    public async Task UTC_B4_PhoneTaken_Conflict()
    {
        var result = await Service(Consumer).UpdateConsumerProfileAsync(
            ConsumerRequest(phone: "0987654321"), default);

        Assert.Equal("Conflict", result.Error!.Kind);
        Assert.Null(_uow.ProfileRepo.LastConsumerUpdate);
    }

    [Fact(DisplayName = "UTC-AUTH03-B5 Keeping own phone is not a conflict")]
    public async Task UTC_B5_OwnPhone_Allowed()
    {
        var result = await Service(Consumer).UpdateConsumerProfileAsync(
            ConsumerRequest(phone: "0912345678"), default);

        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "UTC-AUTH03-B6 Staff can update common fields without an address")]
    public async Task UTC_B6_StaffCommonFields()
    {
        var result = await Service(Staff).UpdateConsumerProfileAsync(
            ConsumerRequest(province: null, district: null, ward: null, detail: null), default);

        Assert.True(result.IsSuccess);
    }

    [Fact(DisplayName = "UTC-AUTH03-B7 Farmer cannot use the consumer endpoint")]
    public async Task UTC_B7_FarmerOnConsumerEndpoint_Forbidden()
    {
        var result = await Service(Farmer).UpdateConsumerProfileAsync(ConsumerRequest(), default);

        Assert.Equal(403, result.StatusCode);
    }

    // ---------- UpdateFarmerProfileAsync ----------

    [Fact(DisplayName = "UTC017 Farmer updates farm profile")]
    public async Task UTC017_FarmerUpdate_Succeeds()
    {
        var result = await Service(Farmer).UpdateFarmerProfileAsync(FarmerRequest(), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("BL-999", _uow.ProfileRepo.LastFarmerUpdate!.BusinessLicenseNumber);
    }

    [Theory(DisplayName = "UTC018b Farm size must be > 0")]
    [InlineData(-1.5)]
    [InlineData(0)]
    public async Task UTC018b_NonPositiveArea_Rejected(double area)
    {
        var result = await Service(Farmer).UpdateFarmerProfileAsync(FarmerRequest((decimal)area), default);

        Assert.Contains("cultivatedArea", result.Error!.Fields!.Keys);
    }

    [Theory(DisplayName = "UTC-AUTH03-B8 GPS coordinates format")]
    [InlineData("11.940400, 108.458300", true)]
    [InlineData("abc", false)]
    public async Task UTC_B8_GpsFormat(string gps, bool ok)
    {
        var result = await Service(Farmer).UpdateFarmerProfileAsync(FarmerRequest(gps: gps), default);

        Assert.Equal(ok, result.IsSuccess);
    }

    [Fact(DisplayName = "UTC-AUTH03-B9 RetailCustomer cannot update a farm profile (GBR-01)")]
    public async Task UTC_B9_ConsumerOnFarmerEndpoint_Forbidden()
    {
        var result = await Service(Consumer).UpdateFarmerProfileAsync(FarmerRequest(phone: "0912345678"), default);

        Assert.Equal(403, result.StatusCode);
        Assert.Null(_uow.ProfileRepo.LastFarmerUpdate);
    }

    // ---------- SubmitFarmerKycAsync ----------

    [Fact(DisplayName = "UTC020 Complete NotSubmitted profile moves to PendingApproval")]
    public async Task UTC020_SubmitComplete_PendingApproval()
    {
        var submitted = Profile(Farmer, "Farmer", Farm(VerificationStatuses.PendingApproval));
        _uow.ProfileRepo.KycResult = new(KycSubmissionOutcome.Submitted, [], VerificationStatuses.PendingApproval, submitted);

        var result = await Service(Farmer).SubmitFarmerKycAsync(new SubmitKycRequest(null), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("PendingApproval", result.Value!.FarmerProfile!.VerificationStatus);
    }

    [Fact(DisplayName = "UTC021 Incomplete profile is rejected with the missing fields")]
    public async Task UTC021_SubmitIncomplete_ListsMissingFields()
    {
        _uow.ProfileRepo.KycResult = new(KycSubmissionOutcome.Incomplete,
            ["address", "businessLicenseNumber"], VerificationStatuses.NotSubmitted, null);

        var result = await Service(Farmer).SubmitFarmerKycAsync(new SubmitKycRequest(null), default);

        Assert.Equal("KYC_INCOMPLETE", result.Error!.Code);
        Assert.Equal(new[] { "address", "businessLicenseNumber" }, result.Error.Fields!.Keys.OrderBy(x => x));
        Assert.Equal(1, _uow.Rollbacks);
    }

    [Fact(DisplayName = "UTC-AUTH03-B10 Already approved / pending profile cannot be resubmitted")]
    public async Task UTC_B10_InvalidState_Conflict()
    {
        _uow.ProfileRepo.KycResult = new(KycSubmissionOutcome.InvalidState, [], VerificationStatuses.Approved, null);

        var result = await Service(Farmer).SubmitFarmerKycAsync(new SubmitKycRequest(null), default);

        Assert.Equal(409, result.StatusCode);
    }

    [Fact(DisplayName = "UTC-AUTH03-B11 RetailCustomer cannot submit KYC (TC-SEC-007)")]
    public async Task UTC_B11_ConsumerSubmitKyc_Forbidden()
    {
        var result = await Service(Consumer).SubmitFarmerKycAsync(new SubmitKycRequest(null), default);

        Assert.Equal(403, result.StatusCode);
    }

    [Theory(DisplayName = "UTC-AUTH03-B12 Only NotSubmitted / Rejected may submit; legacy statuses count as pending")]
    [InlineData("NotSubmitted", true, false)]
    [InlineData("Rejected", true, false)]
    [InlineData("PendingApproval", false, true)]
    [InlineData("Pending", false, true)]
    [InlineData("PendingVerification", false, true)]
    [InlineData("Approved", false, false)]
    public void UTC_B12_StatusRules(string status, bool canSubmit, bool isPending)
    {
        Assert.Equal(canSubmit, VerificationStatuses.CanSubmit(status));
        Assert.Equal(isPending, VerificationStatuses.IsPending(status));
    }
}
