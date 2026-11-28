using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AgriSustain.UnitTests.UT3X;

public sealed class FarmerOfftakeContractTests
{
    [Fact]
    public async Task UT_FAR_08_001_FilterAndAccessAreCheckedBeforeReading()
    {
        var f = new Fixture();
        Assert.Equal("FORBIDDEN", (await f.Service.GetContractsAsync(99, new(), default)).Error?.Code);
        var filter = new ContractQuery { From = new(2025, 11, 2), To = new(2025, 11, 1) };
        Assert.Equal("MSG111", (await f.Service.GetContractsAsync(7, filter, default)).Error?.Code);
        Assert.Null(f.ReadFilter);
        filter.To = filter.From;
        Assert.True((await f.Service.GetContractsAsync(7, filter, default)).IsSuccess);
        Assert.Same(filter, f.ReadFilter);
        Assert.Equal("FORBIDDEN", (await f.Service.GetContextAsync(7, 99, default)).Error?.Code);
    }

    [Fact]
    public async Task UT_FAR_08_002_DelayIsRecordedOnceWithoutChangingAgreedSchedule()
    {
        var f = new Fixture();
        var input = new ContractDelayRequest
        {
            MilestoneId = 31, Quantity = 3, Reason = "Crop shortage",
            ProposedDate = f.Milestone.DueDate.AddDays(3),
            ExpectedVersion = OfftakeContractRules.Version(f.Context),
            RequestKey = Guid.NewGuid().ToString()
        };
        var due = f.Milestone.DueDate;
        var result = await f.Service.ReportDelayAsync(7, 10, input, default);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True((await f.Service.ReportDelayAsync(7, 10, input, default)).IsSuccess);
        Assert.Equal(due, f.Milestone.DueDate);
        Assert.Equal("Delay", Assert.Single(f.Context.History).Action);
        Assert.Equal(2, f.Notifications.Count);
        input.Quantity = 4;
        Assert.Equal("CONFLICT", (await f.Service.ReportDelayAsync(7, 10, input, default)).Error?.Code);
    }

    [Fact]
    public async Task UT_FAR_08_003_TerminationRequiresOwnedEvidenceAndRemainsPending()
    {
        var f = new Fixture();
        var input = new TerminateContractRequest
        {
            Reason = "Weather damaged all remaining crops", ExpectedVersion = OfftakeContractRules.Version(f.Context),
            RequestKey = Guid.NewGuid().ToString()
        };
        Assert.Equal("MSG02", (await f.Service.RequestTerminationAsync(7, 10, input, default)).Error?.Code);
        input.Evidence = ["/other/proof.jpg"];
        Assert.Equal("MSG120", (await f.Service.RequestTerminationAsync(7, 10, input, default)).Error?.Code);
        input.Evidence = ["/test/proof.jpg"];
        var result = await f.Service.RequestTerminationAsync(7, 10, input, default);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Active", f.Context.Contract.Status);
        Assert.Equal("Pending", f.Context.Contract.ContractTerminationRequest?.RequestStatus);
        Assert.True((await f.Service.RequestTerminationAsync(7, 10, input, default)).IsSuccess);
        Assert.Single(f.Context.History);
    }

    [Fact]
    public async Task UT_FAR_08_004_DiscrepancyDoesNotChangeAcceptedQuantity()
    {
        var f = new Fixture();
        var shipment = new Shipment { SellerOrderId = 22 };
        var item = new ShipmentItem
        {
            ShipmentItemId = 44, Shipment = shipment, ShippedQuantity = 10, ReceivedQuantity = 9,
            AcceptedQuantity = 8, ReconciliationStatus = "Disputed"
        };
        shipment.ShipmentItems.Add(item);
        var seller = new SellerOrder { SellerOrderId = 22 };
        seller.Shipments.Add(shipment);
        f.Context.Contract.SellerOrders.Add(seller);
        var ticket = new DisputeTicket
        {
            DisputeTicketId = 51, SellerOrderId = 22, TicketStatus = "Open",
            DisputeReason = "ContractDeliveryDiscrepancy"
        };
        f.Context.Disputes.Add(ticket);
        var reply = new ContractDiscrepancyResponseRequest
        {
            TicketId = 51, ShipmentItemId = 44, ProposedQuantity = 10,
            ProposedResolution = "Recount the delivery", Reason = "Compare signed delivery slip",
            Evidence = ["/test/proof.jpg"], ExpectedVersion = OfftakeContractRules.Version(f.Context),
            RequestKey = Guid.NewGuid().ToString()
        };
        Assert.Equal("MSG120",
            (await f.Service.SubmitDiscrepancyResponseAsync(7, 10, reply, default)).Error?.Code);
        reply.ProposedQuantity = 9;
        var valid = await f.Service.SubmitDiscrepancyResponseAsync(7, 10, reply, default);
        Assert.True(valid.IsSuccess, valid.Error?.Message);
        Assert.Equal(8, item.AcceptedQuantity);
        Assert.Single(ticket.DisputeEvidences);
        reply.ExpectedVersion = OfftakeContractRules.Version(f.Context);
        reply.RequestKey = Guid.NewGuid().ToString();
        Assert.True((await f.Service.RequestReviewAsync(7, 10, reply, default)).IsSuccess);
        Assert.Equal("UnderReview", ticket.TicketStatus);
    }

    [Theory]
    [InlineData("expired", "MSG139")]
    [InlineData("blocked", "MSG139")]
    [InlineData("recalled", "MSG139")]
    [InlineData("missing-qr", "MSG139")]
    [InlineData("no-harvest", "MSG139")]
    [InlineData("excess-stock", "MSG139")]
    [InlineData("excess-commitment", "MSG120")]
    [InlineData("early", "MSG120")]
    public void UT_FAR_08_005_DispatchEligibility(string condition, string expected)
    {
        var f = new Fixture();
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var version = OfftakeContractRules.Current(f.Context);
        foreach (var role in new[] { "Farmer", "WholesaleBuyer" })
        {
            version.ContractSignatures.Add(new ContractSignature
            {
                PartyRole = role, ConfirmationStatus = "Confirmed", ConfirmedAt = DateTime.UtcNow
            });
        }
        version.ContractLines.Add(new ContractLine
        {
            ContractLineId = 51, ProductId = 1, CropSeasonId = 2,
            CommittedQuantity = 10, UnitSnapshot = "kg", Product = new Product { Unit = "kg" }
        });
        f.Milestone.ContractLineId = 51;
        f.Milestone.DueDate = today;
        var batch = new HarvestBatch
        {
            HarvestBatchId = 3, ProductId = 1, CropSeasonId = 2, Status = "Available",
            ActualHarvestDate = today.AddDays(-1), StockEnteredAt = DateTime.UtcNow.AddDays(-1),
            ExpirationDate = today.AddDays(2), TraceabilityQrcode = new() { IsRevoked = false },
            AvailableQuantity = 10
        };
        f.Context.Batches.Add(batch);
        var request = new DispatchContractDeliveryRequest
        {
            MilestoneId = 31, BatchQuantities = [new(3, 5)]
        };
        var rules = new OfftakeContractRules();
        Assert.Null(rules.ValidateDispatch(f.Context, request));
        switch (condition)
        {
            case "expired": batch.ExpirationDate = today; break;
            case "blocked": batch.Status = "Blocked"; break;
            case "recalled": batch.Status = "Recalled"; break;
            case "missing-qr": batch.TraceabilityQrcode = null; break;
            case "no-harvest": batch.ActualHarvestDate = null; break;
            case "excess-stock": batch.AvailableQuantity = 4; break;
            case "excess-commitment": request.BatchQuantities = [new(3, 11)]; break;
            case "early": f.Milestone.DueDate = today.AddDays(1); break;
        }
        Assert.Equal(expected, rules.ValidateDispatch(f.Context, request));
        Assert.Equal(0, f.Milestone.DeliveredQuantity);
        Assert.Empty(f.Context.Contract.SellerOrders);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(19)]
    public async Task RejectProposalRequiresMinimumTrimmedFeedback(int length)
    {
        var f = new Fixture();
        f.Context.Contract.Status = "PendingApproval";
        var input = new ContractActionRequest
        {
            RequestKey = Guid.NewGuid().ToString(),
            ExpectedVersion = OfftakeContractRules.Version(f.Context),
            Reason = "  " + new string('x', length) + "  "
        };

        var result = await f.Service.RejectProposalAsync(7, 10, input, default);

        Assert.Equal("MSG109", result.Error?.Code);
        Assert.Equal("PendingApproval", f.Context.Contract.Status);
        Assert.Empty(f.Context.History);
        Assert.Empty(f.Notifications);
    }

    [Fact]
    public async Task RejectProposalAcceptsExactlyTwentyTrimmedCharacters()
    {
        var f = new Fixture();
        f.Context.Contract.Status = "PendingApproval";
        string reason = new string('x', 20);
        var input = new ContractActionRequest
        {
            RequestKey = Guid.NewGuid().ToString(),
            ExpectedVersion = OfftakeContractRules.Version(f.Context),
            Reason = " " + reason + " "
        };

        var result = await f.Service.RejectProposalAsync(7, 10, input, default);
        var duplicate = await f.Service.RejectProposalAsync(7, 10, input, default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.True(duplicate.IsSuccess, duplicate.Error?.Message);
        Assert.Equal("Rejected", f.Context.Contract.Status);
        Assert.Equal("Reject", Assert.Single(f.Context.History).Action);
        Assert.Equal(new[] { "Email", "InApp" }, f.Notifications.Select(x => x.DeliveryChannel).Order());
    }

    [Theory]
    [InlineData("missing-changes", "MSG02")]
    [InlineData("short-reason", "MSG109")]
    [InlineData("awaiting-deposit", "MSG131")]
    public async Task InvalidAmendmentDoesNotCreateVersion(string scenario, string code)
    {
        var f = new Fixture();
        if (scenario == "awaiting-deposit")
            f.Context.Contract.Status = "AwaitingDeposit";
        var input = new AmendContractRequest
        {
            RequestKey = Guid.NewGuid().ToString(),
            ExpectedVersion = OfftakeContractRules.Version(f.Context),
            Reason = scenario == "short-reason" ? "short" : "A detailed reason for this amendment.",
            ProposedChanges = scenario == "missing-changes" ? " " : "New packaging requirements"
        };

        var result = await f.Service.RequestAmendmentAsync(7, 10, input, default);

        Assert.Equal(code, result.Error?.Code);
        Assert.Single(f.Context.Contract.ContractVersions);
        Assert.Empty(f.Context.History);
        Assert.Empty(f.Notifications);
    }

    [Theory]
    [InlineData("missing-reason", "MSG02")]
    [InlineData("oversized-proposal", "MSG02")]
    [InlineData("closed", "MSG131")]
    [InlineData("duplicate", "CONFLICT")]
    public async Task InvalidTerminationDoesNotChangeContract(string scenario, string code)
    {
        var f = new Fixture();
        if (scenario == "closed")
            f.Context.Contract.Status = "Completed";
        if (scenario == "duplicate")
            f.Context.Contract.ContractTerminationRequest = new() { RequestStatus = "PendingReview" };
        var status = f.Context.Contract.Status;
        var existing = f.Context.Contract.ContractTerminationRequest;
        var input = new TerminateContractRequest
        {
            RequestKey = Guid.NewGuid().ToString(),
            ExpectedVersion = OfftakeContractRules.Version(f.Context),
            Reason = scenario == "missing-reason" ? " " : "Crop failure with evidence",
            SettlementProposal = scenario == "oversized-proposal" ? new string('x', 2001) : "Settle accepted goods",
            Evidence = ["/test/proof.jpg"]
        };

        var result = await f.Service.RequestTerminationAsync(7, 10, input, default);

        Assert.Equal(code, result.Error?.Code);
        Assert.Equal(status, f.Context.Contract.Status);
        Assert.Same(existing, f.Context.Contract.ContractTerminationRequest);
        Assert.Empty(f.Context.History);
        Assert.Empty(f.Notifications);
    }

    [Theory]
    [InlineData("undelivered")]
    [InlineData("unpaid")]
    [InlineData("inspection-pending")]
    [InlineData("refund-pending")]
    [InlineData("payout-pending")]
    [InlineData("termination-pending")]
    [InlineData("missing-invoice")]
    [InlineData("funds-unsettled")]
    public async Task OpenObligationsPreventAutomaticCompletion(string obligation)
    {
        var f = new Fixture();
        f.Milestone.DeliveredQuantity = 10;
        f.Milestone.AcceptedQuantity = 10;
        f.Milestone.Amount = 1000;
        f.Milestone.PaymentStatus = "Paid";
        var item = new ShipmentItem
        {
            ShippedQuantity = 10, ReceivedQuantity = 10, AcceptedQuantity = 10,
            ReconciliationStatus = "Accepted"
        };
        var shipment = new Shipment();
        shipment.ShipmentItems.Add(item);
        f.Milestone.Shipments.Add(shipment);
        var invoice = new Invoice { InvoiceId = 10, InvoiceStatus = "Paid", TotalAmount = 1000 };
        f.Milestone.Invoices.Add(invoice);
        f.Context.Contract.PaymentTransactions.Add(new()
        {
            PaymentPurpose = "ContractDelivery", PaymentStatus = "Success", Amount = 1000, PaidAt = DateTime.UtcNow
        });
        var payout = new FarmerPayout { PayoutStatus = "Processed", ProcessedAt = DateTime.UtcNow, BankTransferRef = "TEST-REF" };
        payout.PayoutItems.Add(new() { ContractMilestoneId = f.Milestone.ContractMilestoneId, SourceAmountSettled = 1000 });
        var context = f.Context;
        context.Payouts.Add(payout);
        Assert.True(new OfftakeContractRules().CanComplete(context));

        switch (obligation)
        {
            case "undelivered": f.Milestone.DeliveredQuantity = 9; break;
            case "unpaid": f.Milestone.PaymentStatus = "Unpaid"; break;
            case "inspection-pending": item.ReconciliationStatus = "Pending"; break;
            case "refund-pending": context.Refunds.Add(new() { RefundStatus = "Pending" }); break;
            case "payout-pending": payout.PayoutStatus = "Processing"; break;
            case "termination-pending":
                context.Contract.ContractTerminationRequest = new() { RequestStatus = "PendingReview" };
                break;
            case "missing-invoice": f.Milestone.Invoices.Clear(); break;
            case "funds-unsettled": Assert.Single(payout.PayoutItems).SourceAmountSettled = 999; break;
        }

        var result = await f.Service.GetFulfillmentProgressAsync(7, 10, default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Active", context.Contract.Status);
        Assert.Empty(context.History);
        Assert.Empty(f.Notifications);
    }

    [Theory]
    [InlineData("PendingReview")]
    [InlineData("Rejected")]
    [InlineData("Approved")]
    public async Task ReadTerminationDisplaysOnlyRecordedOutcome(string requestStatus)
    {
        var f = new Fixture();
        f.Context.Contract.ContractTerminationRequest = new()
        {
            RequestStatus = requestStatus, Reason = "Recorded reason", SettlementAmount = 15000
        };

        var result = await f.Service.GetContextAsync(7, 10, default);

        Assert.True(result.IsSuccess, result.Error?.Message);
        var detail = System.Text.Json.JsonSerializer.SerializeToElement(result.Value);
        Assert.Equal(requestStatus, detail.GetProperty("termination").GetProperty("RequestStatus").GetString());
        Assert.Equal("Active", f.Context.Contract.Status);
        Assert.Empty(f.Context.History);
        Assert.Empty(f.Notifications);
    }

    [Fact]
    public async Task UT_FAR_08_022_StaleVersionBlocksDelayAndTermination()
    {
        var f = new Fixture();
        string stale = new('0', 64);
        var delay = new ContractDelayRequest
        {
            MilestoneId = 31, Quantity = 2, Reason = "Weather delay",
            ProposedDate = f.Milestone.DueDate.AddDays(2),
            ExpectedVersion = stale, RequestKey = Guid.NewGuid().ToString()
        };
        Assert.Equal("CONFLICT", (await f.Service.ReportDelayAsync(7, 10, delay, default)).Error?.Code);

        var term = new TerminateContractRequest
        {
            Reason = "Force majeure flood", Evidence = ["/test/proof.jpg"],
            ExpectedVersion = stale, RequestKey = Guid.NewGuid().ToString()
        };
        Assert.Equal("CONFLICT", (await f.Service.RequestTerminationAsync(7, 10, term, default)).Error?.Code);
        Assert.Null(f.Context.Contract.ContractTerminationRequest);
        Assert.Empty(f.Context.History);
        Assert.Empty(f.Notifications);
    }

    [Fact]
    public async Task UT_FAR_08_023_RejectProposalBlockedWhenAlreadyActive()
    {
        var f = new Fixture();
        var input = new ContractActionRequest
        {
            RequestKey = Guid.NewGuid().ToString(),
            ExpectedVersion = OfftakeContractRules.Version(f.Context),
            Reason = new string('x', 25)
        };
        var result = await f.Service.RejectProposalAsync(7, 10, input, default);
        Assert.Equal("MSG131", result.Error?.Code);
        Assert.Equal("Active", f.Context.Contract.Status);
        Assert.Empty(f.Context.History);
        Assert.Empty(f.Notifications);
    }

    [Fact]
    public async Task UT_FAR_08_024_AmendmentAcceptedAtExact20CharBoundary()
    {
        var f = new Fixture();
        var input = new AmendContractRequest
        {
            RequestKey = Guid.NewGuid().ToString(),
            ExpectedVersion = OfftakeContractRules.Version(f.Context),
            Reason = "  " + new string('r', 20) + "  ",
            ProposedChanges = "Adjust delivery window by 2 days"
        };
        var result = await f.Service.RequestAmendmentAsync(7, 10, input, default);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal(2, f.Context.Contract.ContractVersions.Count);
        Assert.Equal(new[] { "Email", "InApp" }, f.Notifications.Select(x => x.DeliveryChannel).Order());
    }

    [Fact]
    public async Task UT_FAR_08_025_TerminationAcceptsExact2000CharSettlementProposal()
    {
        var f = new Fixture();
        var input = new TerminateContractRequest
        {
            RequestKey = Guid.NewGuid().ToString(),
            ExpectedVersion = OfftakeContractRules.Version(f.Context),
            Reason = "Severe storm damage",
            SettlementProposal = new string('s', 2000),
            Evidence = ["/test/proof.jpg"]
        };
        var result = await f.Service.RequestTerminationAsync(7, 10, input, default);
        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.Equal("Active", f.Context.Contract.Status);
        Assert.Equal("Pending", f.Context.Contract.ContractTerminationRequest?.RequestStatus);
    }

    private sealed class Fixture : IFarmerOfftakeContractRepository, IFarmerComplaintFiles
    {
        public OfftakeContext Context { get; }
        public OfftakeContext? CompletionContext { get; set; }
        public ContractMilestone Milestone { get; }
        public FarmerOfftakeContractService Service { get; }
        public ContractQuery? ReadFilter { get; private set; }
        public List<Notification> Notifications { get; } = [];

        public Fixture()
        {
            Milestone = new ContractMilestone
            {
                ContractMilestoneId = 31, MilestoneType = "Delivery", DeliveryQuantity = 10,
                DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10))
            };
            var version = new ContractVersion { VersionNo = 1, VersionStatus = "Signed", TermsAndConditions = "Fixture terms" };
            version.ContractMilestones.Add(Milestone);
            var contract = new B2bcontract
            {
                B2bcontractId = 10, ContractNumber = "TEST-CONTRACT", Status = "Active",
                CurrentVersionNo = 1,
                FarmerProfile = new FarmerProfile
                {
                    UserId = 7, VerificationStatus = "Approved",
                    User = new User { Status = "Active", Role = "Farmer" }
                },
                WholesaleBuyerProfile = new WholesaleBuyerProfile { UserId = 8 }
            };
            contract.ContractVersions.Add(version);
            Context = new OfftakeContext
            {
                Contract = contract, Seasons = [], Batches = [], OtherCommitments = [], History = [],
                Payouts = [], Disputes = [], Refunds = [], Reviewers = [new User { UserId = 9 }]
            };
            Service = new FarmerOfftakeContractService(this, new(), this);
        }

        public Task<FarmerProfile?> FarmerAsync(long actor, CancellationToken ct) =>
            Task.FromResult<FarmerProfile?>(actor == 7 ? Context.Contract.FarmerProfile : null);
        public Task<object> SearchOwnedAsync(long actor, ContractQuery query, CancellationToken ct)
        {
            ReadFilter = query;
            return Task.FromResult<object>(Array.Empty<object>());
        }
        public Task<OfftakeContext?> LoadContextAsync(long actor, long id, bool update, CancellationToken ct) =>
            Task.FromResult<OfftakeContext?>(actor == 7 && id == 10 ? Context : null);
        public Task<FeatureResult<ContractActionResult>> SaveAtomicAsync(
            long actor, long id, Func<OfftakeContext, Task<FeatureResult<ContractActionResult>>> action,
            CancellationToken ct) => action(Context);
        public void Record(AuditLog entry)
        {
            entry.AuditLogId = Context.History.Count + 1;
            Context.History.Add(entry);
        }
        public void Queue(IEnumerable<Notification> notifications) => Notifications.AddRange(notifications);
        public Task<string> SaveAsync(long actor, IFormFile file, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<bool> IsValidOwnedAsync(long actor, string url, CancellationToken ct) =>
            Task.FromResult(actor == 7 && url == "/test/proof.jpg");
        public string? Resolve(long actor, string url) => null;
    }
}





