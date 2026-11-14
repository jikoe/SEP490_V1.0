using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Application.Validators;
using AgriSustain.Domain.Entities;
using AgriSustain.UnitTests.DuIte1;
using Xunit;

namespace AgriSustain.UnitTests.Offtake;

[Trait("UseCase", "UC-WB-05")]
public sealed class OfftakeProposalTests
{
    [Fact(DisplayName = "[UC-WB-05 #001] Accept complete proposal terms")]
    public void AcceptsCompleteProposal()
    {
        Assert.True(OfftakeProposalValidator.IsValid(Proposal()));
        Assert.True(OfftakeProposalValidator.HasCompleteArticles(Proposal()));
    }

    [Fact(DisplayName = "[UC-WB-05 #002] Reject incomplete terms without creating a proposal")]
    public async Task RejectsNewProposalWithMissingArticle()
    {
        var store = new OfftakeTestStore();
        var service = store.Service();
        var proposal = Proposal() with { DeliveryTerms = "Điều 1: Cam kết\n  " };

        var result = await service.ProposeAsync(proposal, default);

        Assert.False(result.Success);
        Assert.Null(store.Contract);
        Assert.False(OfftakeProposalValidator.HasCompleteArticles(Proposal() with { DeliveryTerms = "Giao tại kho" }));
    }

    [Theory]
    [InlineData("Điều 1: Nội dung hợp đồng\n  Cung ứng theo thỏa thuận.")]
    [InlineData("Điều 1: Phạm vi\n  Các bên thống nhất.\n\nĐiều 2: Điều kiện riêng\n  Kiểm nhận tại kho.")]
    public async Task SavesCustomArticlesWithoutSeparatePaymentText(string terms)
    {
        var store = new OfftakeTestStore();
        var service = store.Service();
        var proposal = Proposal() with { DeliveryTerms = terms, PaymentTerms = null };
        var saved = await service.ProposeAsync(proposal, default, draft: true);
        Assert.True(saved.Success, saved.Message);
        var draft = await service.DraftAsync(saved.Data!.Id, default);
        Assert.Equal(terms, draft.Data?.Proposal.DeliveryTerms);
    }

    [Theory]
    [InlineData("Điều 1: \n  Có nội dung")]
    [InlineData("Điều 1: Phạm vi\n  Có nội dung\n\nĐiều 3: Thiếu điều 2\n  Có nội dung")]
    [InlineData("Điều 1: Phạm vi\n  Có nội dung\n\nĐiều 2: Để trống\n  ")]
    public void RejectsMissingCustomTitlesOrContents(string terms) =>
        Assert.False(OfftakeProposalValidator.HasCompleteArticles(Proposal() with { DeliveryTerms = terms, PaymentTerms = null }));

    [Fact(DisplayName = "[UC-WB-05 #003] Accept 10000 characters at the validation boundary")]
    public void AcceptsMaximumArticleLength() =>
        Assert.True(OfftakeProposalValidator.IsValid(Proposal() with { DeliveryTerms = new string('x', 10000) }));

    [Fact(DisplayName = "[UC-WB-05 #004] Reject 10001 characters at the validation boundary")]
    public void RejectsExcessArticleLength() =>
        Assert.False(OfftakeProposalValidator.IsValid(Proposal() with { DeliveryTerms = new string('x', 10001) }));

    [Fact(DisplayName = "[UC-WB-05 #005] Preserve the selected draft unit and base quantity")]
    public async Task SavesTonneSelectionInDraftWithoutChangingTheProductUnit()
    {
        var store = new OfftakeTestStore();
        var service = store.Service();
        var proposal = Proposal() with { Quantity = 1500, QuantityUnit = "tấn" };

        var created = await service.ProposeAsync(proposal, default, draft: true);
        var draft = await service.DraftAsync(10, default);

        Assert.Equal(201, created.StatusCode);
        Assert.Equal("tấn", draft.Data!.Proposal.QuantityUnit);
        Assert.Contains(draft.Data.Product!.QuantityUnits, unit => unit.Unit == "tấn" && unit.Factor == 1000);
        var line = Assert.Single(store.Contract!.ContractLines);
        Assert.Equal("kg", line.UnitSnapshot);
        Assert.Equal(1500m, line.CommittedQuantity);
        Assert.Equal(15000000m, Assert.Single(store.Contract.ContractMilestones).Amount);
    }

    [Fact(DisplayName = "[UC-WB-05 #006] Reject a unit from another measurement family")]
    public async Task RejectsQuantityUnitFromAnotherMeasurementFamily()
    {
        var store = new OfftakeTestStore();
        var service = store.Service();

        var result = await service.ProposeAsync(Proposal() with { QuantityUnit = "thùng" }, default);

        Assert.Null(result.Data);
        Assert.Null(store.Contract);
    }

    [Fact(DisplayName = "[UC-WB-05 #007] Reject invalid quantity precision, dates, terms and deposit")]
    public void RejectsInvalidPrecisionAndDeliveryOutsideValidity()
    {
        var proposal = Proposal();

        Assert.False(OfftakeProposalValidator.IsValid(proposal with { Quantity = 0.0001m }));
        Assert.False(OfftakeProposalValidator.IsValid(proposal with
        {
            DeliveryDate = proposal.ValidUntil.AddDays(1)
        }));
        Assert.False(OfftakeProposalValidator.IsValid(proposal with { DeliveryTerms = " " }));
        Assert.False(OfftakeProposalValidator.IsValid(proposal with { DepositAmount = 100001 }));
    }

    [Fact(DisplayName = "[UC-WB-05 #008] Submit version one once and reject changed terms with the same key")]
    public async Task SubmitConfirmsExactVersionAndRejectsReusingKeyForChangedTerms()
    {
        var store = new OfftakeTestStore();
        var service = store.Service();
        var proposal = Proposal();

        var created = await service.ProposeAsync(proposal, default);
        var repeated = await service.ProposeAsync(proposal, default);
        var changed = await service.ProposeAsync(proposal with { Quantity = 12 }, default);

        Assert.Equal(201, created.StatusCode);
        Assert.StartsWith("MSG34", created.Message);
        Assert.Equal(created.Data, repeated.Data);
        Assert.Equal(409, changed.StatusCode);
        var contract = Assert.IsType<B2bcontract>(store.Contract);
        Assert.Equal("PendingApproval", contract.Status);
        var version = Assert.Single(contract.ContractVersions);
        Assert.Equal(1, version.VersionNo);
        var signature = Assert.Single(version.ContractSignatures);
        Assert.Equal("Confirmed", signature.ConfirmationStatus);
        Assert.Equal("WholesaleBuyer", signature.PartyRole);
        Assert.Equal(store.UserId, signature.SignedByUserId);
        Assert.Same(version, Assert.Single(contract.ContractLines).ContractVersion);
        Assert.Equal(2, store.Notices.Count);
        Assert.Single(store.Notices, notice => notice.DeliveryChannel == "Email"
            && notice.DeliveryStatus == "Pending" && notice.UserId == 7);
        Assert.Equal(1, store.Commits);
    }

    [Fact(DisplayName = "[UC-WB-05 #009] Reject a buyer without an approved business profile")]
    public async Task RejectsUnapprovedBuyerBeforeAddingProposal()
    {
        var store = new OfftakeTestStore { ApprovedBuyerId = null };

        var result = await store.Service().ProposeAsync(Proposal(), default);

        Assert.Equal(403, result.StatusCode);
        Assert.Null(store.Contract);
        Assert.Empty(store.Notices);
        Assert.Equal(0, store.Commits);
    }

    [Fact(DisplayName = "[UC-WB-05 #010] Reject an unavailable farmer-product combination")]
    public async Task RejectsMissingProductBeforeAddingProposal()
    {
        var store = new OfftakeTestStore { ProductAvailable = false };

        var result = await store.Service().ProposeAsync(Proposal(), default);

        Assert.Equal(400, result.StatusCode);
        Assert.StartsWith("MSG48", result.Message);
        Assert.Null(store.Contract);
        Assert.Equal(0, store.Commits);
    }

    [Fact(DisplayName = "[UC-WB-05 #011] Report a save conflict without committing or notifying")]
    public async Task SaveConflictReturnsMsg49WithoutCommit()
    {
        var store = new OfftakeTestStore { SaveConflict = new InvalidOperationException("Duplicate proposal") };

        var result = await store.Service().ProposeAsync(Proposal(), default);

        Assert.Equal(409, result.StatusCode);
        Assert.StartsWith("MSG49", result.Message);
        Assert.Null(result.Data);
        Assert.Empty(store.Notices);
        Assert.Equal(0, store.Commits);
    }

    private static OfftakeProposalRequest Proposal() => new(
        Guid.NewGuid(), 1, 2, 10, 10000, new DateOnly(2026, 11, 9),
        new DateOnly(2026, 12, 30), new DateOnly(2026, 12, 25),
        string.Join("\n\n", new[] { 1, 2, 3, 4, 6, 7, 8 }.Select(number => $"Điều {number}: Điều khoản\n  Nội dung {number}")),
        "Thanh toán khi nhận hàng", 10000);
}

internal sealed class OfftakeTestStore : IOfftakeRepository, IUseCaseUnitOfWork,
    ITransaction, IPersistenceErrorClassifier, IActorContext
{
    public B2bcontract? Contract { get; private set; }
    public int Commits { get; private set; }
    public List<Notification> Notices { get; } = [];
    public long UserId => 9;
    public long? ApprovedBuyerId { get; init; } = 5;
    public bool ProductAvailable { get; init; } = true;
    public Exception? SaveConflict { get; init; }
    public OfftakeTrackingPage TrackingPage { get; init; } = new([], 0, 1, 10, []);
    public OfftakeTrackingDetail? TrackingDetail { get; init; }
    public (long UserId, bool Orders, OfftakeTrackingQuery Query)? TrackCall { get; private set; }
    public (long UserId, bool Orders, long Id)? DetailCall { get; private set; }

    public OfftakeService Service() => new(this, this, this, this,
        new FakeClock(new DateTimeOffset(2026, 11, 9, 0, 0, 0, TimeSpan.Zero)),
        new AccountNotificationSettings(null));

    public Task<long?> ApprovedBuyerAsync(long userId, CancellationToken ct) =>
        Task.FromResult(ApprovedBuyerId);

    public Task<OfftakeProduct?> FindProductAsync(long farmerId, long productId,
        CancellationToken ct) => Task.FromResult<OfftakeProduct?>(
            ProductAvailable ? new(productId, farmerId, "Nhà cung cấp", "Gạo", "Ngũ cốc", "kg", null) : null);

    public Task<B2bcontract?> FindSubmissionAsync(long buyerId, string code,
        CancellationToken ct) => Task.FromResult(Contract?.ContractNumber == code ? Contract : null);

    public void Add(B2bcontract contract)
    {
        Assert.Null(Contract);
        contract.B2bcontractId = 10;
        Contract = contract;
    }

    public Task<ITransaction> BeginAsync(CancellationToken ct) =>
        Task.FromResult<ITransaction>(this);

    public Task SaveChangesAsync(CancellationToken ct) =>
        SaveConflict is null ? Task.CompletedTask : Task.FromException(SaveConflict);

    public Task CommitAsync(CancellationToken ct)
    {
        Commits++;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public bool IsConflict(Exception exception) => ReferenceEquals(exception, SaveConflict);

    public Task<IReadOnlyList<OfftakeProduct>> CatalogAsync(string? keyword,
        CancellationToken ct) => throw new NotSupportedException();

    public Task<B2bcontract?> FindManagedAsync(long buyerId, long contractId,
        CancellationToken ct) => Task.FromResult(Contract?.B2bcontractId == contractId ? Contract : null);

    public Task<bool> HasCommitmentsAsync(long contractId, CancellationToken ct, bool editingRevision = false) =>
        throw new NotSupportedException();

    public Task<Shipment?> FindShipmentForInspectionAsync(long buyerId, long contractId, long shipmentId,
        CancellationToken ct) => throw new NotSupportedException();
    public Task<DisputeEvidence?> FindContractEvidenceAsync(long buyerUserId, long contractId, long evidenceId,
        CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<OfftakeDiscrepancyReply>> DiscrepancyRepliesAsync(long contractId, long ticketId,
        CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<long>> OfftakeReviewersAsync(CancellationToken ct) => throw new NotSupportedException();

    public Task<long> FarmerUserIdAsync(long farmerId, CancellationToken ct) =>
        Task.FromResult(7L);

    public void AddNotification(Notification notification) => Notices.Add(notification);

    public Task<OfftakeTrackingPage> TrackAsync(long userId, bool orders,
        OfftakeTrackingQuery query, CancellationToken ct)
    {
        TrackCall = (userId, orders, query);
        return Task.FromResult(TrackingPage);
    }

    public Task<OfftakeTrackingDetail?> DetailAsync(long userId, bool orders, long id,
        CancellationToken ct)
    {
        DetailCall = (userId, orders, id);
        return Task.FromResult(TrackingDetail);
    }

    public Task<OfftakeTrackingDetail?> FarmerDetailAsync(long userId, long id,
        CancellationToken ct) => throw new NotSupportedException();
}
