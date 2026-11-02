using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Services.BusinessProfiles;
using AgriSustain.Application.Services.Certificates;
using AgriSustain.Application.Services.Moderation;
using AgriSustain.Application.Services.Refunds;
using AgriSustain.Application.Services.Traceability;
using AgriSustain.UnitTests.DuIte1;
using Xunit;

namespace AgriSustain.UnitTests.DuIte2;

/// <summary>UC-AUTH-02 (part 2) WholesaleBuyer submits the company dossier from the Profile screen.</summary>
public sealed class BusinessProfileServiceTests
{
    private readonly FakeBusinessRepository _repo = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.Zero));

    private BusinessProfileService Service(string role = "WholesaleBuyer") =>
        new(_repo, new FakeCurrentUser(77, role), new SubmitBusinessProfileRequestValidator(), _clock);

    private static SubmitBusinessProfileRequest Valid(string tax = "0101234567") =>
        new("Organic Mart", tax, "Tran Van B", "Supermarket", "12 Ly Thuong Kiet, Ha Noi");

    private static Task<string> Save(long userId) => Task.FromResult($"/api/dossiers/b2b-documents/{userId}/a.pdf");

    [Fact(DisplayName = "UTC-B2B01 New buyer without a dossier is NotSubmitted")]
    public async Task Get_NotSubmitted() =>
        Assert.Equal("NotSubmitted", (await Service().GetMineAsync(default)).Value!.VerificationStatus);

    [Fact(DisplayName = "UTC-B2B02 Valid dossier with license becomes PendingApproval")]
    public async Task Submit_Valid()
    {
        var result = await Service().SubmitAsync(Valid(), Save, default);

        Assert.True(result.IsSuccess);
        Assert.Equal("PendingApproval", result.Value!.VerificationStatus);
        Assert.Equal("/api/dossiers/b2b-documents/77/a.pdf", result.Value.DocumentUrl);
    }

    [Theory(DisplayName = "UTC-B2B03 Invalid tax code is rejected (AF1)")]
    [InlineData("12345")]
    [InlineData("01012345AB")]
    public async Task Submit_InvalidTaxCode(string tax)
    {
        var result = await Service().SubmitAsync(Valid(tax), Save, default);

        Assert.True(result.Error!.Fields!.ContainsKey("taxCode"));
        Assert.Null(_repo.Profile);
    }

    [Fact(DisplayName = "UTC-B2B04 Branch tax code is stored with its dash")]
    public void NormalizeTaxCode_Branch() =>
        Assert.Equal("0101234567-001", BusinessProfileRules.NormalizeTaxCode("0101234567 001"));

    [Fact(DisplayName = "UTC-B2B05 Tax code of another buyer is rejected (AF2)")]
    public async Task Submit_DuplicateTaxCode()
    {
        _repo.OtherTaxCodes.Add("0101234567");

        var result = await Service().SubmitAsync(Valid(), Save, default);

        Assert.Equal("TAX_CODE_EXISTS", result.Error!.Code);
    }

    [Fact(DisplayName = "UTC-B2B06 Business License is optional; tax code alone can be submitted")]
    public async Task Submit_NoDocument()
    {
        var result = await Service().SubmitAsync(Valid(), null, default);

        Assert.Equal("PendingApproval", result.Value!.VerificationStatus);
        Assert.Null(result.Value.DocumentUrl);
    }

    [Fact(DisplayName = "UTC-B2B07 Pending dossier cannot be resubmitted; Rejected keeps the old license")]
    public async Task Submit_StateRules()
    {
        await Service().SubmitAsync(Valid(), Save, default);
        Assert.Equal("INVALID_STATE", (await Service().SubmitAsync(Valid(), Save, default)).Error!.Code);

        _repo.Profile = _repo.Profile! with { VerificationStatus = "Rejected", RejectReason = "Ảnh mờ" };
        var again = await Service().SubmitAsync(Valid(), null, default);

        Assert.Equal("PendingApproval", again.Value!.VerificationStatus);
        Assert.Equal("/api/dossiers/b2b-documents/77/a.pdf", again.Value.DocumentUrl);
    }

    [Fact(DisplayName = "UTC-B2B08 Other roles cannot use the business dossier")]
    public async Task Submit_WrongRole() =>
        Assert.Equal("FORBIDDEN", (await Service("RetailCustomer").SubmitAsync(Valid(), Save, default)).Error!.Code);

    private sealed class FakeBusinessRepository : IBusinessProfileRepository
    {
        public BusinessProfileDto? Profile { get; set; }
        public HashSet<string> OtherTaxCodes { get; } = [];

        public Task<BusinessProfileDto?> GetAsync(long userId, CancellationToken ct) => Task.FromResult(Profile);

        public Task<bool> TaxCodeTakenAsync(string taxCode, long userId, CancellationToken ct) =>
            Task.FromResult(OtherTaxCodes.Contains(taxCode));

        public Task<BusinessProfileDto> SubmitAsync(long userId, SubmitBusinessProfileRequest r, string? documentUrl,
            DateTime now, CancellationToken ct)
        {
            Profile = new BusinessProfileDto(r.CompanyName, r.TaxCode, r.RepresentativeName, r.BusinessType,
                r.BusinessAddress, documentUrl ?? Profile?.DocumentUrl, "PendingApproval", null);
            return Task.FromResult(Profile);
        }
    }
}

/// <summary>UC-FAR-07 Submit Certificate Dossier.</summary>
public sealed class FarmCertificateServiceTests
{
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.Zero));
    private readonly FakeCertificateRepository _repo = new();

    private FarmCertificateService Service(long userId = 20) =>
        new(_repo, new FakeCurrentUser(userId, "Farmer"), new SubmitCertificateRequestValidator(_clock), _clock);

    private static SubmitCertificateRequest Request(string number = "VG-001", DateOnly? expiry = null) =>
        new("VietGAP", number, "Sở NN&PTNT Lâm Đồng", new DateOnly(2026, 1, 10), expiry ?? new DateOnly(2028, 1, 10));

    private static Task<string> Save(long userId) => Task.FromResult($"/api/dossiers/certificates/{userId}/a.pdf");

    [Fact(DisplayName = "UTC-CER01 Approved Farmer submits a dossier as PendingReview")]
    public async Task Submit_Valid()
    {
        var result = await Service().SubmitAsync(Request(), Save, default);

        Assert.Equal(FarmCertificateRules.PendingReview, result.Value!.VerificationStatus);
        Assert.Single(_repo.Items);
    }

    [Fact(DisplayName = "UTC-CER02 Farm that is not Approved cannot submit")]
    public async Task Submit_NotApproved()
    {
        _repo.Status = "PendingApproval";

        var result = await Service().SubmitAsync(Request(), Save, default);

        Assert.Equal("Forbidden", result.Error!.Kind);
    }

    [Fact(DisplayName = "UTC-CER03 Expiry date must be after issue date and in the future (AF1)")]
    public async Task Submit_Expired()
    {
        var result = await Service().SubmitAsync(Request(expiry: new DateOnly(2026, 10, 1)), Save, default);

        Assert.True(result.Error!.Fields!.ContainsKey("expiryDate"));
        Assert.Empty(_repo.Items);
    }

    [Fact(DisplayName = "UTC-CER04 Same type and number is rejected (AF2)")]
    public async Task Submit_Duplicate()
    {
        _repo.Numbers.Add(("VietGAP", "VG-001"));

        var result = await Service().SubmitAsync(Request(), Save, default);

        Assert.Equal("CERTIFICATE_EXISTS", result.Error!.Code);
    }

    private sealed class FakeCertificateRepository : IFarmCertificateRepository
    {
        public string Status { get; set; } = "Approved";
        public HashSet<(string, string)> Numbers { get; } = [];
        public List<FarmCertificateDto> Items { get; } = [];

        public Task<(long FarmerProfileId, string VerificationStatus)?> GetFarmerProfileAsync(long userId, CancellationToken ct) =>
            Task.FromResult<(long, string)?>((5, Status));

        public Task<bool> NumberExistsAsync(string type, string number, CancellationToken ct) =>
            Task.FromResult(Numbers.Contains((type, number)));

        public Task<IReadOnlyList<FarmCertificateDto>> ListAsync(long id, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FarmCertificateDto>>(Items);

        public Task<FarmCertificateDto> AddAsync(NewFarmCertificate c, CancellationToken ct)
        {
            var dto = new FarmCertificateDto(Items.Count + 1, c.CertificateType, c.CertificateNumber, c.IssuedBy,
                c.IssueDate, c.ExpiryDate, FarmCertificateRules.PendingReview, null, null, c.DocumentUrl);
            Items.Add(dto);
            return Task.FromResult(dto);
        }
    }
}

/// <summary>UC-CM-02 Moderate Product Review.</summary>
public sealed class ReviewModerationTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc);
    private readonly FakeReviewRepository _repo = new();

    private ReviewModerationService Service() =>
        new(_repo, new FakeCurrentUser(9, "ContentModerator"), new FakeClock(new DateTimeOffset(Now)));

    [Theory(DisplayName = "UTC-REV01 Flag rules: banned word, phone, link, 1 star with comment")]
    [InlineData(5, "Hàng tốt, gọi 0912 345 678 nhé", "Chứa số điện thoại")]
    [InlineData(5, "Mua rẻ hơn tại www.reshop.vn", "Chứa đường link")]
    [InlineData(4, "shop vcl", "Chứa từ ngữ cấm")]
    [InlineData(1, "Rau héo", "1 sao kèm bình luận")]
    public void Flags(int stars, string comment, string expected) =>
        Assert.Contains(expected, ReviewFlagRules.Evaluate((byte)stars, comment, 1));

    [Fact(DisplayName = "UTC-REV02 Clean review has no flag; more than 5 reviews in 24h is flagged")]
    public void SpamRule()
    {
        Assert.Empty(ReviewFlagRules.Evaluate(5, "Rau tươi, giao nhanh", 1));
        Assert.Contains(ReviewFlagRules.Evaluate(5, "ok", 6), r => r.Contains("24 giờ"));
    }

    [Fact(DisplayName = "UTC-REV03 Queue lists only flagged reviews not yet handled; clean reviews never enter it")]
    public async Task Tabs()
    {
        _repo.Add(1, 5, "Rau tươi", reviewed: false);
        _repo.Add(2, 1, "Rau héo", reviewed: false);
        _repo.Add(3, 5, "Ngon", reviewed: true);

        var result = await Service().ListAsync(new ReviewQuery(ReviewTabs.Flagged), default);

        Assert.Equal(new ReviewTabCounts(1, 0), result.Value!.Counts);
        Assert.Equal(2, Assert.Single(result.Value.Items).ProductReviewId);

        var byDefault = await Service().ListAsync(new ReviewQuery(null), default);
        Assert.Equal(2, Assert.Single(byDefault.Value!.Items).ProductReviewId);
    }

    [Fact(DisplayName = "UTC-REV04 Hiding without a reason is blocked (AF3)")]
    public async Task Hide_NoReason()
    {
        _repo.Add(1, 1, "Rau héo", reviewed: false);

        var result = await Service().ModerateAsync(1, new ModerateReviewRequest(ReviewDecisions.Hide, " "), default);

        Assert.Equal("REASON_REQUIRED", result.Error!.Code);
    }

    [Fact(DisplayName = "UTC-REV05 Hide then Restore (AF2)")]
    public async Task Hide_Restore()
    {
        _repo.Add(1, 1, "shop vcl", reviewed: false);
        var service = Service();

        var hidden = await service.ModerateAsync(1, new ModerateReviewRequest(ReviewDecisions.Hide, "Ngôn từ thô tục"), default);
        var restored = await service.ModerateAsync(1, new ModerateReviewRequest(ReviewDecisions.Restore, null), default);

        Assert.Equal(ReviewStatuses.Hidden, hidden.Value!.ModerationStatus);
        Assert.Equal(ReviewStatuses.Visible, restored.Value!.ModerationStatus);
        Assert.True(restored.Value.IsReviewed);
    }

    private sealed class FakeReviewRepository : IReviewModerationRepository
    {
        private readonly Dictionary<long, ReviewRecord> _rows = [];

        public void Add(long id, byte stars, string comment, bool reviewed) =>
            _rows[id] = new ReviewRecord(id, 100 + id, "Khách " + id, 1, "Rau cải", "ORD-" + id, stars, comment,
                Now.AddHours(-id), ReviewStatuses.Visible, null, reviewed);

        public Task<IReadOnlyList<ReviewRecord>> ListAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ReviewRecord>>(_rows.Values.ToList());

        public Task<ReviewRecord?> FindAsync(long id, CancellationToken ct) => Task.FromResult(_rows.GetValueOrDefault(id));

        public Task<IReadOnlyList<(long UserId, DateTime ReviewDate)>> ReviewDatesAsync(
            IReadOnlyCollection<long> userIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<(long, DateTime)>>(_rows.Values.Select(r => (r.UserId, r.ReviewDate)).ToList());

        public Task ApplyDecisionAsync(long id, string decision, string newStatus, string? reason, long moderator,
            DateTime now, CancellationToken ct)
        {
            _rows[id] = _rows[id] with { ModerationStatus = newStatus, ModerationReason = reason, IsReviewed = true };
            return Task.CompletedTask;
        }
    }
}

/// <summary>UC-TRC-01 View Product Traceability.</summary>
public sealed class TraceabilityServiceTests
{
    private readonly FakeTraceRepository _repo = new();
    private TraceabilityService Service() => new(_repo, new FakeClock(DateTimeOffset.UtcNow));

    [Theory(DisplayName = "UTC-TRC01 Batch code is read from a code or a scanned URL")]
    [InlineData(" BATCH-DL-001 ", "BATCH-DL-001")]
    [InlineData("https://agrisustain.vn/trace/BATCH-DL-001", "BATCH-DL-001")]
    public void Normalize(string input, string expected) => Assert.Equal(expected, TraceabilityService.Normalize(input));

    [Fact(DisplayName = "UTC-TRC02 Unknown code shows the invalid-code message (AF1)")]
    public async Task Unknown()
    {
        var result = await Service().LookupAsync("NOPE", default);

        Assert.Equal(TraceabilityService.InvalidCodeMessage, result.Error!.Message);
    }

    [Fact(DisplayName = "UTC-TRC03 Revoked batch shows only the recall warning (AF2)")]
    public async Task Revoked()
    {
        _repo.Revoked = true;

        var result = await Service().LookupAsync("BATCH-DL-001", default);

        Assert.True(result.Value!.IsRevoked);
        Assert.Empty(result.Value.CultivationLogs);
        Assert.NotNull(result.Value.RecallMessage);
    }

    private sealed class FakeTraceRepository : ITraceabilityRepository
    {
        public bool Revoked { get; set; }

        public Task<TraceabilityDto?> FindAndCountScanAsync(string code, DateOnly today, CancellationToken ct) =>
            Task.FromResult(code != "BATCH-DL-001" ? null : new TraceabilityDto(code, Revoked, null, 1, "Bơ 034", null,
                "kg", "Lâm Đồng", new DateOnly(2026, 10, 1), false, null, "Loại 1", "Nông trại Bơ", "Bảo Lộc",
                "Thôn 4", 4.8m, "Vụ 2026", "Bơ 034", new DateOnly(2026, 3, 1), 3,
                [new TraceCultivationLogDto(new DateTime(2026, 4, 1), "Bón phân", null, null, null, null, null)], []));
    }
}

/// <summary>UC-FIN-08 Provide Refund Account: state of the request.</summary>
public sealed class RefundAccountStateTests
{
    private static RefundRequestRecord Record(string status = "Pending", string? number = null,
        DateTime? provided = null, DateTime? failed = null) =>
        new(1, 10, "RFD-1", "ORD-1", "Nông trại", 85000m, "Rau dập", status, null, number is null ? null : "VCB",
            "Vietcombank", number, number is null ? null : "DO MAI PHUONG", provided, failed is null ? null : "Sai tên", failed);

    [Fact(DisplayName = "UTC-RFA01 Pending without account waits for the customer")]
    public void Awaiting() => Assert.Equal(RefundAccountStates.AwaitingAccount, RefundAccountService.State(Record()));

    [Fact(DisplayName = "UTC-RFA02 Failure after the account was provided asks the customer to correct it")]
    public void Failed() => Assert.Equal(RefundAccountStates.TransferFailed, RefundAccountService.State(
        Record(number: "1023456789", provided: new DateTime(2026, 10, 28), failed: new DateTime(2026, 10, 29))));

    [Fact(DisplayName = "UTC-RFA03 A corrected account after the failure is ready for transfer again")]
    public void Corrected() => Assert.Equal(RefundAccountStates.ReadyForTransfer, RefundAccountService.State(
        Record(number: "1023456789", provided: new DateTime(2026, 10, 30), failed: new DateTime(2026, 10, 29))));

    [Theory(DisplayName = "UTC-RFA15 Transfer in progress is locked; a Failed transfer can be corrected")]
    [InlineData("Processing", "Transferring", false)]
    [InlineData("NeedsReconciliation", "Transferring", false)]
    [InlineData("Failed", "TransferFailed", true)]
    public void FinanceStatuses(string status, string state, bool editable)
    {
        var record = Record(status, "1023456789", provided: new DateTime(2026, 10, 28), failed: new DateTime(2026, 10, 29));
        Assert.Equal(state, RefundAccountService.State(record));
        Assert.Equal(editable, RefundAccountService.IsEditable(status));
    }

    [Fact(DisplayName = "UTC-RFA04 Completed refund is read-only")]
    public void Completed() => Assert.Equal(RefundAccountStates.Completed, RefundAccountService.State(Record("Completed", "1023456789")));
}
