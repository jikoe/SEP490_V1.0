using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AgriSustain.UnitTests.Offtake;

// Runs the real Buyer and Farmer services over the same isolated aggregate.
// Receipt, bank verification and settlement are explicit external-system fixtures,
// not evidence that a live transfer, email or database integration has succeeded.
public sealed class FarmerBuyerContractFlowTests
{
    [Fact]
    public async Task BuyerProposal_FarmerConfirmation_Dispatch_Receipt_Settlement_Completion()
    {
        var store = new FlowStore();
        await store.Submit();
        Assert.Equal("PendingApproval", store.Contract.Status);
        Assert.Single(store.Current.ContractSignatures);
        Assert.Equal(2, store.Notices.Count);

        var confirmation = store.Confirmation();
        var accepted = await store.Farmer.ConfirmTermsAsync(7, 10, confirmation, default);
        Assert.True(accepted.IsSuccess, accepted.Error?.Message);
        Assert.Equal("Active", store.Contract.Status);
        Assert.Equal("Signed", store.Current.VersionStatus);
        Assert.Equal(2, store.Current.ContractSignatures.Count);
        Assert.Equal(4, store.Notices.Count);

        var repeated = await store.Farmer.ConfirmTermsAsync(7, 10, confirmation, default);
        Assert.True(repeated.IsSuccess, repeated.Error?.Message);
        Assert.Equal(2, store.Current.ContractSignatures.Count);
        Assert.Equal(4, store.Notices.Count);

        var dispatch = store.Dispatch();
        var shipped = await store.Farmer.DispatchDeliveryAsync(7, 10, dispatch, default);
        Assert.True(shipped.IsSuccess, shipped.Error?.Message);
        var seller = Assert.Single(store.Contract.SellerOrders);
        Assert.Equal(9, seller.Order.BuyerUserId);
        Assert.Equal("Wholesale", seller.Order.OrderType);
        Assert.Equal(10, store.Milestone.DeliveredQuantity);
        Assert.Equal(90, store.Batch.AvailableQuantity);
        Assert.Equal("InTransit", Assert.Single(seller.Shipments).ShipmentStatus);
        Assert.Equal(2, store.Notices.Count(n => n.UserId == 9 && n.NotificationType == "FarmerOfftakeContract"
            && n.Content.Contains("giao", StringComparison.OrdinalIgnoreCase)));

        var duplicate = await store.Farmer.DispatchDeliveryAsync(7, 10, dispatch, default);
        Assert.True(duplicate.IsSuccess, duplicate.Error?.Message);
        Assert.Single(store.Contract.SellerOrders);
        Assert.Single(store.Batch.InventoryMovements);
        Assert.Equal(90, store.Batch.AvailableQuantity);
        await store.AssertNotCompleted();

        store.RecordAcceptedDeliveryAndInvoice();
        await store.AssertNotCompleted();
        store.RecordVerifiedReceipt();
        await store.AssertNotCompleted();
        store.RecordCompletedSettlement();

        var completed = await store.Farmer.CompleteIfEligibleAsync(7, 10, default);
        Assert.True(completed.IsSuccess, completed.Error?.Message);
        Assert.Equal("Completed", store.Contract.Status);
        Assert.NotNull(store.Contract.CompletedAt);
        Assert.Equal(5, store.Contract.FarmerProfile.ReputationScore);
        Assert.Equal(4, store.Notices.Count(n => n.Content.Contains("MSG159")
            || n.Title.Contains(store.Contract.ContractNumber) && n.PayloadJson!.Contains("Completed")));

        int noticeCount = store.Notices.Count;
        await store.Farmer.CompleteIfEligibleAsync(7, 10, default);
        Assert.Equal(noticeCount, store.Notices.Count);
        Assert.Single(store.Context.History.Where(x => x.Action == "Completed"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BuyerCanEditFarmerRevisionAndSendNewTermsForConfirmation(bool legacyFarmerConfirmation)
    {
        var store = new FlowStore();
        await store.Submit();
        const string requestedChanges = "Giao nhận tại kho của nông trại.";
        const string reason = "Nông trại cần đổi địa điểm giao nhận.";
        var amendment = await store.Farmer.RequestAmendmentAsync(7, 10, new AmendContractRequest
        {
            ExpectedVersion = store.Version,
            RequestKey = Guid.NewGuid().ToString(),
            Reason = reason,
            ProposedChanges = requestedChanges
        }, default);
        Assert.True(amendment.IsSuccess, amendment.Error?.Message);
        var farmerVersion = store.Current;
        if (legacyFarmerConfirmation)
            farmerVersion.ContractSignatures.Add(new ContractSignature
            {
                PartyRole = "Farmer", SignedByUserId = 7, ConfirmationStatus = "Confirmed",
                ConfirmedAt = DateTime.UtcNow
            });
        var staleConfirmation = store.Confirmation();

        var loaded = await store.Buyer.DraftAsync(10, default);
        Assert.True(loaded.Success, loaded.Message);
        var draft = Assert.IsType<OfftakeDraft>(loaded.Data);
        Assert.Equal(2, draft.Version);
        Assert.Equal(reason, draft.RevisionReason);
        Assert.Equal(requestedChanges, draft.RevisionChanges);
        Assert.DoesNotContain(requestedChanges, draft.Proposal.DeliveryTerms!);
        var revised = draft.Proposal with { Quantity = 12, UnitPrice = 11000, PaymentTerms = "Thanh toán trong 7 ngày." };
        Assert.Equal(409, (await store.Buyer.UpdateDraftAsync(10, draft.Version, revised, false, default)).StatusCode);
        Assert.Equal(409, (await store.Buyer.UpdateDraftAsync(10, draft.Version,
            revised with { ProductId = 3 }, true, default)).StatusCode);
        Assert.Same(farmerVersion, store.Current);

        var sent = await store.Buyer.UpdateDraftAsync(10, draft.Version, revised, true, default);
        Assert.True(sent.Success, sent.Message);
        Assert.Equal(3, store.Current.VersionNo);
        Assert.Equal("PendingApproval", store.Contract.Status);
        Assert.Equal(9, store.Current.ProposedByUserId);
        Assert.Equal("WholesaleBuyer", Assert.Single(store.Current.ContractSignatures).PartyRole);
        Assert.Contains(requestedChanges, farmerVersion.TermsAndConditions);
        Assert.Equal(reason, farmerVersion.FeedbackReason);
        Assert.Equal(10, Assert.Single(farmerVersion.ContractLines).CommittedQuantity);
        Assert.Equal(12, Assert.Single(store.Current.ContractLines).CommittedQuantity);
        Assert.Equal(132000, store.Milestone.Amount);
        Assert.Equal(revised.PaymentTerms, store.Contract.PaymentTerms);
        Assert.True((await store.Buyer.UpdateDraftAsync(10, draft.Version, revised, true, default)).Success);
        Assert.Equal(3, store.Contract.ContractVersions.Count);
        Assert.Equal(2, store.Notices.Count(x => x.UserId == 7 && x.PayloadJson!.Contains("offtake-proposal:10:3")));
        Assert.False((await store.Farmer.ConfirmTermsAsync(7, 10, staleConfirmation, default)).IsSuccess);
        Assert.True((await store.Farmer.ConfirmTermsAsync(7, 10, store.Confirmation(), default)).IsSuccess);
        Assert.Equal("Active", store.Contract.Status);
    }

    [Fact]
    public async Task SecondFarmerAmendmentShowsOnlyLatestFeedbackAndWaitsForBuyer()
    {
        var store = new FlowStore();
        await store.Submit();
        var original = store.Current.TermsAndConditions;
        async Task Amend(string reason, string changes) =>
            Assert.True((await store.Farmer.RequestAmendmentAsync(7, 10, new AmendContractRequest
            {
                ExpectedVersion = store.Version, RequestKey = Guid.NewGuid().ToString(),
                Reason = reason, ProposedChanges = changes
            }, default)).IsSuccess);

        await Amend("Cần điều chỉnh do thay đổi lịch thu hoạch.", "Giao buổi sáng.");
        await Amend("Cần điều chỉnh lại do lịch xe thay đổi.", "Giao buổi chiều.");

        Assert.Equal(original + OfftakeContractRules.AmendmentHeading + "Giao buổi chiều.",
            store.Current.TermsAndConditions);
        var detail = await store.Farmer.GetContextAsync(7, 10, default);
        Assert.True(detail.IsSuccess);
        var payload = JsonSerializer.SerializeToElement(detail.Value);
        Assert.Equal("Cần điều chỉnh lại do lịch xe thay đổi.", payload.GetProperty("FeedbackReason").GetString());
        Assert.Equal("Giao buổi chiều.", payload.GetProperty("proposedChanges").GetString());
        Assert.False((await store.Farmer.ConfirmTermsAsync(7, 10, store.Confirmation(), default)).IsSuccess);
        Assert.Empty(store.Current.ContractSignatures);
        var draft = Assert.IsType<OfftakeDraft>((await store.Buyer.DraftAsync(10, default)).Data);
        Assert.Equal("Giao buổi chiều.", draft.RevisionChanges);
        Assert.Equal(original, draft.Proposal.DeliveryTerms);
    }

    [Fact]
    public async Task SubmittedBuyerProposal_CanBeRejectedByFarmer()
    {
        var store = new FlowStore();
        await store.Submit();
        var rejected = await store.Farmer.RejectProposalAsync(7, 10, new ContractActionRequest
        {
            ExpectedVersion = store.Version,
            RequestKey = Guid.NewGuid().ToString(),
            Reason = "Nông trại không đủ năng lực cung ứng theo lịch đề xuất."
        }, default);
        Assert.True(rejected.IsSuccess, rejected.Error?.Message);
        Assert.Equal("Rejected", store.Contract.Status);
        var detail = JsonSerializer.SerializeToElement((await store.Farmer.GetContextAsync(7, 10, default)).Value);
        Assert.Equal("Nông trại không đủ năng lực cung ứng theo lịch đề xuất.",
            detail.GetProperty("FeedbackReason").GetString());
    }

    [Fact]
    public async Task RequiredDeposit_BlocksDispatchUntilVerified()
    {
        var store = new FlowStore();
        await store.Submit(10000);
        var confirmed = await store.Farmer.ConfirmTermsAsync(7, 10, store.Confirmation(), default);
        Assert.True(confirmed.IsSuccess, confirmed.Error?.Message);
        Assert.Equal("AwaitingDeposit", store.Contract.Status);
        var deposit = Assert.Single(store.Current.ContractMilestones, m => m.MilestoneType == "Deposit");
        Assert.Equal(10000, deposit.Amount);
        Assert.Equal(90000, store.Milestone.Amount);
        var result = await store.Farmer.DispatchDeliveryAsync(7, 10, store.Dispatch(), default);
        Assert.False(result.IsSuccess);
        Assert.Empty(store.Contract.SellerOrders);
        Assert.Equal(100, store.Batch.AvailableQuantity);
    }

    [Fact]
    public async Task StaleVersion_UnownedContract_AndInsufficientCapacityAreRejected()
    {
        var store = new FlowStore();
        await store.Submit();
        var stale = store.Confirmation();
        stale.VersionNo = 99;
        Assert.False((await store.Farmer.ConfirmTermsAsync(7, 10, stale, default)).IsSuccess);
        Assert.False((await store.Farmer.ConfirmTermsAsync(8, 10, store.Confirmation(), default)).IsSuccess);
        store.Context.Seasons.Single().EstimatedYield = 5;
        Assert.False((await store.Farmer.ConfirmTermsAsync(7, 10, store.Confirmation(), default)).IsSuccess);
        Assert.Equal("PendingApproval", store.Contract.Status);
        Assert.Single(store.Current.ContractSignatures);
    }

    private sealed class FlowStore : IOfftakeRepository, IFarmerOfftakeContractRepository,
        IUseCaseUnitOfWork, ITransaction, IActorContext, IPersistenceErrorClassifier, IFarmerComplaintFiles
    {
        public long UserId => 9;
        public B2bcontract Contract { get; private set; } = null!;
        public OfftakeContext Context { get; private set; } = null!;
        public ContractVersion Current => OfftakeContractRules.Current(Context);
        public ContractMilestone Milestone => Assert.Single(Current.ContractMilestones, m => m.DeliveryQuantity > 0);
        public HarvestBatch Batch => Assert.Single(Context.Batches);
        public string Version => OfftakeContractRules.Version(Context);
        public List<Notification> Notices { get; } = [];
        public FarmerOfftakeContractService Farmer => new(this, new OfftakeContractRules(), this);
        public OfftakeService Buyer => new(this, this, this, this, TimeProvider.System, new AccountNotificationSettings(null));

        public async Task Submit(decimal deposit = 0)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            var request = new OfftakeProposalRequest(Guid.NewGuid(), 1, 2, 10, 10000,
                today.AddDays(-1), today.AddDays(10), today,
                string.Join("\n\n", new[] { 1, 2, 3, 4, 6, 7, 8 }
                    .Select(n => $"Điều {n}: Điều khoản\nNội dung đã thống nhất.")),
                "Thanh toán sau kiểm nhận.", deposit);
            var result = await Buyer.ProposeAsync(request, default);
            Assert.True(result.Success, result.Message);
        }

        public ContractTermsResponseRequest Confirmation() => new()
        {
            ExpectedVersion = Version, RequestKey = Guid.NewGuid().ToString(),
            VersionNo = Contract.CurrentVersionNo, CropSeasonId = 3
        };

        public DispatchContractDeliveryRequest Dispatch() => new()
        {
            ExpectedVersion = Version, RequestKey = Guid.NewGuid().ToString(),
            MilestoneId = Milestone.ContractMilestoneId, BatchQuantities = [new(4, 10)]
        };

        public async Task AssertNotCompleted()
        {
            var result = await Farmer.CompleteIfEligibleAsync(7, 10, default);
            Assert.True(result.IsSuccess, result.Error?.Message);
            Assert.Equal("Active", Contract.Status);
        }

        public void RecordAcceptedDeliveryAndInvoice()
        {
            var shipment = Assert.Single(Contract.SellerOrders.Single().Shipments);
            Milestone.Shipments.Add(shipment);
            shipment.DeliveredAt = DateTime.UtcNow;
            shipment.ShipmentStatus = "Delivered";
            var item = Assert.Single(shipment.ShipmentItems);
            item.ReceivedQuantity = item.AcceptedQuantity = 10;
            item.ReconciliationStatus = "Accepted";
            Milestone.AcceptedQuantity = 10;
            Milestone.FulfillmentStatus = "Delivered";
            Milestone.Invoices.Add(new Invoice
            {
                InvoiceId = 50, InvoiceNumber = "TEST-INVOICE", InvoiceStatus = "Issued",
                TotalAmount = 100000, IssuedAt = DateTime.UtcNow
            });
        }

        public void RecordVerifiedReceipt()
        {
            Milestone.PaymentStatus = "Paid";
            Milestone.PaidAmount = 100000;
            Contract.PaymentTransactions.Add(new PaymentTransaction
            {
                PaymentPurpose = "ContractInstallment", PaymentStatus = "Succeeded",
                Amount = 100000, PaidAt = DateTime.UtcNow
            });
        }

        public void RecordCompletedSettlement()
        {
            var payout = new FarmerPayout
            {
                PayoutStatus = "Processed", ProcessedAt = DateTime.UtcNow,
                BankTransferRef = "TEST-VERIFIED-PAYOUT"
            };
            payout.PayoutItems.Add(new PayoutItem
            {
                ContractMilestoneId = Milestone.ContractMilestoneId, SourceAmountSettled = 100000
            });
            Context.Payouts.Add(payout);
        }

        public void Add(B2bcontract contract)
        {
            Contract = contract;
            contract.B2bcontractId = 10;
            contract.FarmerProfile = new FarmerProfile
            {
                UserId = 7, FarmerProfileId = 1, VerificationStatus = "Approved",
                User = new User { UserId = 7, Role = "Farmer", Status = "Active" }
            };
            contract.WholesaleBuyerProfile = new WholesaleBuyerProfile
            {
                UserId = 9, BusinessAddress = "Kho test", RepresentativeName = "Buyer",
                User = new User { UserId = 9, Role = "WholesaleBuyer", Status = "Active", PhoneNumber = "0900000000" }
            };
            var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            Context = new OfftakeContext
            {
                Contract = contract, History = [], OtherCommitments = [], Payouts = [],
                Disputes = [], Refunds = [], Reviewers = [],
                Seasons = [new CropSeason
                {
                    CropSeasonId = 3, ProductId = 2, YieldUnit = "kg", EstimatedYield = 100,
                    EstimatedHarvestDate = today.AddDays(-2), Status = "Harvesting"
                }],
                Batches = [new HarvestBatch
                {
                    HarvestBatchId = 4, ProductId = 2, CropSeasonId = 3, BatchCode = "TEST-BATCH",
                    ActualHarvestDate = today.AddDays(-1), ExpirationDate = today.AddDays(7),
                    StockEnteredAt = DateTime.UtcNow, Status = "Available", AvailableQuantity = 100,
                    TraceabilityQrcode = new TraceabilityQrcode { IsRevoked = false }
                }]
            };
            FixRelationships();
        }

        private void FixRelationships()
        {
            foreach (var version in Contract.ContractVersions)
            {
                version.ContractVersionId = version.VersionNo;
                version.B2bcontract = Contract;
                foreach (var line in version.ContractLines.Concat(Contract.ContractLines.Where(l => l.ContractVersion == version)).Distinct())
                {
                    if (!version.ContractLines.Contains(line)) version.ContractLines.Add(line);
                    line.ContractLineId = version.VersionNo * 10;
                    line.ContractVersionId = version.ContractVersionId;
                    line.Product = new Product { ProductId = 2, ProductName = "Gạo" };
                }
                foreach (var milestone in version.ContractMilestones.Concat(Contract.ContractMilestones.Where(m => m.ContractVersion == version)).Distinct())
                {
                    if (!version.ContractMilestones.Contains(milestone)) version.ContractMilestones.Add(milestone);
                    milestone.ContractMilestoneId = version.VersionNo * 100;
                    milestone.ContractVersionId = version.ContractVersionId;
                    milestone.ContractLineId = milestone.ContractLine?.ContractLineId;
                }
            }
        }

        public Task<long?> ApprovedBuyerAsync(long userId, CancellationToken ct) => Task.FromResult<long?>(5);
        public Task<OfftakeProduct?> FindProductAsync(long farmerId, long productId, CancellationToken ct) =>
            Task.FromResult<OfftakeProduct?>(new(productId, farmerId, "Nông trại", "Gạo", "Ngũ cốc", "kg", null));
        public Task<B2bcontract?> FindSubmissionAsync(long buyerId, string code, CancellationToken ct) =>
            Task.FromResult<B2bcontract?>(Contract?.ContractNumber == code ? Contract : null);
        public Task<B2bcontract?> FindManagedAsync(long buyerId, long contractId, CancellationToken ct) => Task.FromResult<B2bcontract?>(Contract);
        public Task<Shipment?> FindShipmentForInspectionAsync(long buyerId, long contractId, long shipmentId,
            CancellationToken ct) => throw new NotSupportedException();
        public Task<DisputeEvidence?> FindContractEvidenceAsync(long buyerUserId, long contractId, long evidenceId,
            CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<OfftakeDiscrepancyReply>> DiscrepancyRepliesAsync(long contractId, long ticketId,
            CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<long>> OfftakeReviewersAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> HasCommitmentsAsync(long contractId, CancellationToken ct, bool editingRevision = false) =>
            Task.FromResult(!editingRevision && Current.ContractSignatures.Any(x => x.PartyRole == "Farmer"));
        public Task<long> FarmerUserIdAsync(long farmerId, CancellationToken ct) => Task.FromResult(7L);
        public void AddNotification(Notification notification) => Notices.Add(notification);
        public Task<ITransaction> BeginAsync(CancellationToken ct) => Task.FromResult<ITransaction>(this);
        public Task SaveChangesAsync(CancellationToken ct) { FixRelationships(); return Task.CompletedTask; }
        public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public bool IsConflict(Exception exception) => false;
        public Task<FarmerProfile?> FarmerAsync(long actor, CancellationToken ct) =>
            Task.FromResult<FarmerProfile?>(actor == 7 ? Contract.FarmerProfile : null);
        public Task<OfftakeContext?> LoadContextAsync(long actor, long id, bool update, CancellationToken ct) =>
            Task.FromResult<OfftakeContext?>(actor == 7 && id == 10 ? Context : null);
        public async Task<FeatureResult<ContractActionResult>> SaveAtomicAsync(long actor, long id,
            Func<OfftakeContext, Task<FeatureResult<ContractActionResult>>> action, CancellationToken ct)
        {
            var result = await action(Context);
            if (result.IsSuccess) FixRelationships();
            return result;
        }
        public void Record(AuditLog entry) { entry.AuditLogId = Context.History.Count + 1; Context.History.Add(entry); }
        public void Queue(IEnumerable<Notification> notifications) => Notices.AddRange(notifications);
        public Task<string> SaveAsync(long actor, IFormFile file, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> IsValidOwnedAsync(long actor, string url, CancellationToken ct) => Task.FromResult(false);
        public string? Resolve(long actor, string url) => null;
        public Task<object> SearchOwnedAsync(long actor, ContractQuery query, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<OfftakeProduct>> CatalogAsync(string? keyword, CancellationToken ct) => throw new NotSupportedException();
        public Task<OfftakeTrackingPage> TrackAsync(long userId, bool orders, OfftakeTrackingQuery query, CancellationToken ct) => throw new NotSupportedException();
        public Task<OfftakeTrackingDetail?> DetailAsync(long userId, bool orders, long id, CancellationToken ct) => throw new NotSupportedException();
        public Task<OfftakeTrackingDetail?> FarmerDetailAsync(long userId, long id, CancellationToken ct) => throw new NotSupportedException();
    }
}
