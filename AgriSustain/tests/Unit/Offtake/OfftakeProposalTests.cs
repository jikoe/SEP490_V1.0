using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Application.Validators;
using AgriSustain.Domain.Entities;
using Xunit;

namespace AgriSustain.UnitTests.Offtake;

public sealed class OfftakeProposalTests
{
    [Fact]
    public void AcceptsCompleteProposal()
    {
        Assert.True(OfftakeProposalValidator.IsValid(Proposal()));
    }

    [Fact]
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

    [Fact]
    public async Task SubmitConfirmsExactVersionAndRejectsReusingKeyForChangedTerms()
    {
        var store = new ProposalStore();
        var service = new OfftakeService(store, store, store, store,
            TimeProvider.System, new AccountNotificationSettings(null));
        var proposal = Proposal();

        var created = await service.ProposeAsync(proposal, default);
        var repeated = await service.ProposeAsync(proposal, default);
        var changed = await service.ProposeAsync(proposal with { Quantity = 12 }, default);

        Assert.Equal(201, created.StatusCode);
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

    private static OfftakeProposalRequest Proposal() => new(
        Guid.NewGuid(), 1, 2, 10, 10000, new DateOnly(2026, 11, 9),
        new DateOnly(2026, 12, 30), new DateOnly(2026, 12, 25), "Giao tại kho",
        "Thanh toán khi nhận hàng", 10000);

    private sealed class ProposalStore : IOfftakeRepository, IUseCaseUnitOfWork,
        ITransaction, IPersistenceErrorClassifier, IActorContext
    {
        public B2bcontract? Contract { get; private set; }
        public int Commits { get; private set; }
        public List<Notification> Notices { get; } = [];
        public long UserId => 9;

        public Task<long?> ApprovedBuyerAsync(long userId, CancellationToken ct) =>
            Task.FromResult<long?>(5);

        public Task<OfftakeProduct?> FindProductAsync(long farmerId, long productId,
            CancellationToken ct) => Task.FromResult<OfftakeProduct?>(
                new(productId, farmerId, "Nhà cung cấp", "Gạo", "Ngũ cốc", "kg", null));

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

        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;

        public Task CommitAsync(CancellationToken ct)
        {
            Commits++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public bool IsConflict(Exception exception) => false;

        public Task<IReadOnlyList<OfftakeProduct>> CatalogAsync(string? keyword,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<B2bcontract?> FindRevisionAsync(long buyerId, long contractId,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<B2bcontract?> FindManagedAsync(long buyerId, long contractId,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<bool> HasCommitmentsAsync(long contractId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<long> FarmerUserIdAsync(long farmerId, CancellationToken ct) =>
            Task.FromResult(7L);

        public void AddNotification(Notification notification) => Notices.Add(notification);

        public Task<OfftakeTrackingPage> TrackAsync(long userId, bool orders,
            OfftakeTrackingQuery query, CancellationToken ct) => throw new NotSupportedException();

        public Task<OfftakeTrackingDetail?> DetailAsync(long userId, bool orders, long id,
            CancellationToken ct) => throw new NotSupportedException();

        public Task<OfftakeTrackingDetail?> FarmerDetailAsync(long userId, long id,
            CancellationToken ct) => throw new NotSupportedException();
    }
}
