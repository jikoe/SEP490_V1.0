using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Offtake;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Application.Services.Finance;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.BackgroundWorkers;
using AgriSustain.Infrastructure.Data;
using AgriSustain.Infrastructure.Repositories;
using AgriSustain.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgriSustain.IntegrationTests.Commerce;

public sealed class OfftakeSqlTests
{
    [CommerceSqlFact]
    public async Task ProposalAndTrackingUseTheSameOwnedContractAndCurrentVersion()
    {
        await using var fixture = await CommerceDatabase.Create();
        await using var db = fixture.Open();
        var profile = new WholesaleBuyerProfile
        {
            UserId = fixture.BuyerId, CompanyName = "Buyer Co", TaxCode = "0123456789",
            RepresentativeName = "Buyer", BusinessAddress = "Test address",
            VerificationStatus = "Approved"
        };
        db.WholesaleBuyerProfiles.Add(profile);
        await db.SaveChangesAsync();
        var farmerId = await db.Products.Where(x => x.ProductId == fixture.ProductId)
            .Select(x => x.FarmerProfileId).SingleAsync();
        var repository = new OfftakeRepository(db);
        var service = new OfftakeService(repository, new SqlUnitOfWork(db),
            new BuyerActor(fixture.BuyerId), new PersistenceErrorClassifier(), fixture.Clock,
            new AccountNotificationSettings(null));
        var proposal = new OfftakeProposalRequest(Guid.NewGuid(), farmerId, fixture.ProductId,
            20, 25000, new(2026, 11, 1), new(2026, 12, 31), new(2026, 11, 20),
            string.Join("\n\n", new[] { 1, 2, 3, 4, 6, 7, 8 }.Select(number => $"Điều {number}: Điều khoản\n  Nội dung {number}")),
            "Thanh toán khi nhận", 100000);

        var submitted = await service.ProposeAsync(proposal, default);
        Assert.True(submitted.Success, submitted.Message);
        var id = Assert.IsType<OfftakeProposalReceipt>(submitted.Data).Id;
        var query = new OfftakeTrackingQuery("Test farm", "PendingApproval", "Tomato",
            new(2026, 10, 27), new(2026, 10, 27));
        var list = await repository.TrackAsync(fixture.BuyerId, false, query, default);
        Assert.Equal(id, Assert.Single(list.Items).Id);
        Assert.Empty((await repository.TrackAsync(fixture.OtherBuyerId, false, query, default)).Items);

        var detail = Assert.IsType<OfftakeTrackingDetail>(
            await repository.DetailAsync(fixture.BuyerId, false, id, default));
        Assert.Equal("Confirmed", Assert.Single(detail.Confirmations).Status);
        var notices = await db.Notifications.Where(x => x.RelatedRecordType == "B2BContract"
            && x.RelatedRecordId == id).OrderBy(x => x.DeliveryChannel).ToListAsync();
        Assert.Collection(notices,
            email =>
            {
                Assert.Equal(fixture.FarmerId, email.UserId);
                Assert.Equal("Email", email.DeliveryChannel);
                Assert.Equal("Pending", email.DeliveryStatus);
                Assert.Contains(detail.Code, email.Content);
            },
            inApp =>
            {
                Assert.Equal(fixture.FarmerId, inApp.UserId);
                Assert.Equal("InApp", inApp.DeliveryChannel);
                Assert.Equal("Sent", inApp.DeliveryStatus);
            });
        Assert.Equal(proposal.DeliveryDate, Assert.Single(detail.Milestones).DueDate);
        Assert.Null(await repository.DetailAsync(fixture.OtherBuyerId, false, id, default));
        var farmerUrl = $"api/farmer/offtake-proposals/{id}";
        var farmerDetail = await fixture.Farmer().GetFromJsonAsync<Result<OfftakeTrackingDetail>>(
            farmerUrl);
        Assert.Equal("Buyer Co", farmerDetail?.Data?.Partner);
        Assert.Null(await repository.FarmerDetailAsync(fixture.OtherBuyerId, id, default));
        await VerifyEmailRetryAsync(fixture, notices[0].NotificationId, id);
        Assert.Empty((await repository.TrackAsync(fixture.BuyerId, true,
            new OfftakeTrackingQuery(), default)).Items);

        var order = await db.Orders.SingleAsync(x => x.BuyerUserId == fixture.BuyerId);
        order.OrderType = "PreOrder";
        await db.SaveChangesAsync();
        var orders = await repository.TrackAsync(fixture.BuyerId, true,
            new OfftakeTrackingQuery("TEST-ORDER", Product: "Tomato"), default);
        Assert.Equal(order.OrderId, Assert.Single(orders.Items).Id);
        Assert.Null(await repository.DetailAsync(fixture.OtherBuyerId, true, order.OrderId, default));

        var contract = await db.B2bcontracts.SingleAsync(x => x.B2bcontractId == id);
        await db.Entry(contract).Collection(x => x.ContractVersions).LoadAsync();
        const string rejection = "Nông trại không đủ sản lượng theo lịch giao đề xuất.";
        contract.RejectProposal(rejection);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var buyer = fixture.As(fixture.BuyerId, "WholesaleBuyer");
        var rejected = (await buyer.GetFromJsonAsync<Result<OfftakeTrackingDetail>>(
            $"api/b2b/contracts/{id}"))?.Data;
        Assert.Equal(rejection, rejected?.FeedbackReason);
        Assert.Null(rejected?.ProposedChanges);
        using var oldConfirmation = await buyer.PostAsync($"api/b2b/contracts/{id}/versions/2/confirmation", null);
        Assert.Equal(HttpStatusCode.NotFound, oldConfirmation.StatusCode);
        using var blocked = await buyer.PostAsync($"api/b2b/contracts/{id}/cancellation", null);
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        var draftProposal = proposal with { RequestId = Guid.NewGuid() };
        using var saveResponse = await buyer.PostAsJsonAsync("api/b2b/contracts/drafts", draftProposal);
        Assert.Equal(HttpStatusCode.Created, saveResponse.StatusCode);
        var saved = await saveResponse.Content.ReadFromJsonAsync<Result<OfftakeProposalReceipt>>();
        var draftId = Assert.IsType<OfftakeProposalReceipt>(saved?.Data).Id;
        var draftUrl = $"api/b2b/contracts/{draftId}/draft";
        var draft = Assert.IsType<OfftakeDraft>(
            (await buyer.GetFromJsonAsync<Result<OfftakeDraft>>(draftUrl))?.Data);
        var draftDetail = Assert.IsType<OfftakeTrackingDetail>(
            await repository.DetailAsync(fixture.BuyerId, false, draftId, default));
        Assert.Equal("Draft", draftDetail.Status);
        Assert.Equal(profile.CompanyName, draftDetail.Document?.Buyer.Name);
        Assert.Equal(profile.TaxCode, draftDetail.Document?.Buyer.TaxCode);
        Assert.Equal(draft.CreatedAt, draftDetail.Document?.VersionCreatedAt);
        Assert.Empty(draftDetail.Confirmations);
        Assert.False(await db.Notifications.AnyAsync(x => x.RelatedRecordType == "B2BContract"
            && x.RelatedRecordId == draftId));
        using var unpublished = await fixture.Farmer().GetAsync(
            $"api/farmer/offtake-proposals/{draftId}");
        Assert.Equal(HttpStatusCode.NotFound, unpublished.StatusCode);
        fixture.As(fixture.BuyerId, "WholesaleBuyer");
        var updated = draftProposal with { Quantity = 25 };
        using var updateResponse = await buyer.PutAsJsonAsync($"{draftUrl}/{draft.Version}", updated);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(409, (await service.UpdateDraftAsync(
            draftId, draft.Version, draftProposal, true, default)).StatusCode);
        var latest = Assert.IsType<OfftakeDraft>((await service.DraftAsync(draftId, default)).Data);
        Assert.Equal(25, latest.Proposal.Quantity);
        using var submitResponse = await buyer.PutAsJsonAsync(
            $"{draftUrl}/{latest.Version}?submit=true", updated);
        Assert.Equal(HttpStatusCode.OK, submitResponse.StatusCode);
        using var submitRetry = await buyer.PutAsJsonAsync(
            $"{draftUrl}/{latest.Version}?submit=true", updated);
        Assert.Equal(HttpStatusCode.OK, submitRetry.StatusCode);
        Assert.Equal(2, await db.Notifications.CountAsync(x => x.RelatedRecordType == "B2BContract"
            && x.RelatedRecordId == draftId && x.UserId == fixture.FarmerId));
        var sent = Assert.IsType<OfftakeTrackingDetail>(
            await repository.DetailAsync(fixture.BuyerId, false, draftId, default));
        Assert.Equal("PendingApproval", sent.Status);
        Assert.Equal("Confirmed", Assert.Single(sent.Confirmations).Status);

        var negotiating = await db.B2bcontracts.SingleAsync(x => x.B2bcontractId == draftId);
        var farmerVersion = new ContractVersion
        {
            B2bcontractId = draftId, VersionNo = latest.Version + 1, VersionStatus = "Pending",
            ProposedByUserId = fixture.FarmerId, FeedbackReason = "Đề nghị thay đổi số lượng giao.",
            TermsAndConditions = updated.DeliveryTerms + OfftakeContractRules.AmendmentHeading + "Giao 25 kg."
                + OfftakeContractRules.AmendmentHeading + "Giao 26 kg."
        };
        farmerVersion.ContractSignatures.Add(new ContractSignature
        {
            PartyRole = "Farmer", SignedByUserId = fixture.FarmerId,
            ConfirmationMethod = "InApp", ConfirmationStatus = "Confirmed",
            ConfirmedAt = fixture.Clock.GetUtcNow().UtcDateTime
        });
        var farmerLine = new ContractLine
        {
            B2bcontractId = draftId, ProductId = updated.ProductId, UnitSnapshot = "kg",
            CommittedQuantity = updated.Quantity, PriceType = "Fixed", AgreedPrice = updated.UnitPrice
        };
        farmerVersion.ContractLines.Add(farmerLine);
        farmerVersion.ContractMilestones.Add(new ContractMilestone
        {
            B2bcontractId = draftId, ContractLine = farmerLine, MilestoneType = "DeliveryAndPayment",
            MilestoneTitle = "Giao hàng theo đề xuất", DueDate = updated.DeliveryDate,
            DeliveryQuantity = updated.Quantity, Amount = updated.Quantity * updated.UnitPrice,
            FulfillmentStatus = "Pending", PaymentStatus = "Unpaid"
        });
        db.ContractVersions.Add(farmerVersion);
        negotiating.CurrentVersionNo = farmerVersion.VersionNo;
        negotiating.Status = "Negotiating";
        await db.SaveChangesAsync();
        var feedback = await repository.DetailAsync(fixture.BuyerId, false, draftId, default);
        Assert.True(feedback?.CanEditRevision);
        Assert.Equal(updated.DeliveryTerms, feedback?.Terms);
        Assert.Equal("Giao 26 kg.", feedback?.ProposedChanges);
        Assert.Equal(farmerVersion.FeedbackReason, feedback?.FeedbackReason);
        var negotiationList = await repository.TrackAsync(fixture.BuyerId, false,
            new OfftakeTrackingQuery(Status: "Negotiating"), default);
        Assert.True(Assert.Single(negotiationList.Items).CanEditRevision);

        var edit = Assert.IsType<OfftakeDraft>((await buyer.GetFromJsonAsync<Result<OfftakeDraft>>(draftUrl))?.Data);
        Assert.Equal("Giao 26 kg.", edit.RevisionChanges);
        var counterproposal = edit.Proposal with { Quantity = 26 };
        using var resend = await buyer.PutAsJsonAsync($"{draftUrl}/{edit.Version}?submit=true", counterproposal);
        Assert.Equal(HttpStatusCode.OK, resend.StatusCode);
        var returned = (await fixture.Farmer().GetFromJsonAsync<Result<OfftakeTrackingDetail>>(
            $"api/farmer/offtake-proposals/{draftId}"))?.Data;
        Assert.NotNull(returned);
        Assert.Equal(edit.Version + 1, returned.Version);
        Assert.Equal("PendingApproval", returned.Status);
        Assert.Equal(26, Assert.Single(returned.Lines).Quantity);
        Assert.Equal(26, Assert.Single(returned.Milestones).Quantity);
        Assert.Equal("WholesaleBuyer", Assert.Single(returned.Confirmations).Role);

        fixture.As(fixture.BuyerId, "WholesaleBuyer");
        using var cancelled = await buyer.PostAsync($"api/b2b/contracts/{draftId}/cancellation", null);
        Assert.Equal(HttpStatusCode.Conflict, cancelled.StatusCode);
    }

    [CommerceSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingSignedContractCanGenerateDepositAndActivateOnlyAfterFullReceipt(bool useWebhook)
    {
        await using var fixture = await CommerceDatabase.Create(new Dictionary<string, string?>
        {
            ["Payment:SePay:AccountNumber"] = "1234567890",
            ["Payment:SePay:BankShortName"] = "MB",
            ["Payment:SePay:AccountName"] = "Test Platform",
            ["Payment:SePay:WebhookApiKey"] = "test-only-key"
        });
        await using var db = fixture.Open();
        var buyer = new WholesaleBuyerProfile
        {
            UserId = fixture.BuyerId, CompanyName = "Buyer Co", TaxCode = "0123456789",
            RepresentativeName = "Buyer", BusinessAddress = "Test address", VerificationStatus = "Approved"
        };
        db.WholesaleBuyerProfiles.Add(buyer);
        var financeUser = new User
        {
            Username = "finance", Email = "finance@example.test", FullName = "Finance",
            PasswordHash = "test-only", Role = "FinanceStaff", Status = "Active", IsEmailVerified = true
        };
        db.Users.Add(financeUser);
        await db.SaveChangesAsync();
        var farmerId = await db.Products.Where(x => x.ProductId == fixture.ProductId).Select(x => x.FarmerProfileId).SingleAsync();
        var service = new OfftakeService(new OfftakeRepository(db), new SqlUnitOfWork(db),
            new BuyerActor(fixture.BuyerId), new PersistenceErrorClassifier(), fixture.Clock, new AccountNotificationSettings(null));
        var request = new OfftakeProposalRequest(Guid.NewGuid(), farmerId, fixture.ProductId,
            20, 25000, new(2026, 11, 1), new(2026, 12, 31), new(2026, 11, 20),
            string.Join("\n\n", new[] { 1, 2, 3, 4, 6, 7, 8 }.Select(n => $"Điều {n}: Điều khoản\n  Nội dung {n}")),
            "Thanh toán khi nhận", 100000);
        var proposal = await service.ProposeAsync(request, default);
        Assert.True(proposal.Success, proposal.Message);
        var id = proposal.Data!.Id;
        var url = $"api/b2b/contracts/{id}/deposit";
        var client = fixture.As(fixture.BuyerId, "WholesaleBuyer");
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(url, null)).StatusCode);

        var contract = await db.B2bcontracts.Include(c => c.ContractVersions).SingleAsync(c => c.B2bcontractId == id);
        contract.Status = "AwaitingDeposit";
        contract.DigitalSignatureStatus = "FullySigned";
        contract.SignedDate = fixture.Clock.GetUtcNow().UtcDateTime;
        var version = Assert.Single(contract.ContractVersions);
        version.VersionStatus = "Signed";
        version.ContractSignatures.Add(new ContractSignature
        {
            PartyRole = "Farmer", SignedByUserId = fixture.FarmerId, ConfirmationMethod = "InSystem",
            ConfirmationStatus = "Confirmed", ConfirmedAt = contract.SignedDate
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Farmer().PostAsync(url, null)).StatusCode);
        fixture.As(fixture.BuyerId, "WholesaleBuyer");
        async Task<PaymentQr> Instructions()
        {
            using var response = await client.PostAsync(url, null);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<Result<PaymentQr>>())!.Data!;
        }
        var qr = await Instructions();
        Assert.Equal(100000, qr.Amount);
        Assert.Equal("1234567890", qr.AccountNumber);
        Assert.Equal(qr.Reference, (await Instructions()).Reference);
        var milestones = await db.ContractMilestones.Where(m => m.B2bcontractId == id).AsNoTracking().ToListAsync();
        var deposit = Assert.Single(milestones, m => m.MilestoneType == "Deposit");
        var delivery = Assert.Single(milestones, m => m.MilestoneType == "DeliveryAndPayment");
        Assert.Equal(400000, delivery.Amount);
        Assert.Equal(500000, milestones.Sum(m => m.Amount));
        Assert.Equal(ContractPaymentRules.Reference(deposit.ContractMilestoneId), qr.Reference);

        var finance = fixture.As(financeUser.UserId, "FinanceStaff");
        var confirmationUrl = $"api/finance/contract-payments/lines/{deposit.ContractMilestoneId}/confirm";
        Assert.Equal(HttpStatusCode.OK, (await finance.PostAsJsonAsync(confirmationUrl,
            new ConfirmReceivedRequest(40000, "DEPOSIT-1", null))).StatusCode);
        Assert.Equal("AwaitingDeposit", (await db.B2bcontracts.AsNoTracking().SingleAsync(c => c.B2bcontractId == id)).Status);
        fixture.As(fixture.BuyerId, "WholesaleBuyer");
        Assert.Equal(60000, (await Instructions()).Amount);
        fixture.As(financeUser.UserId, "FinanceStaff");
        Assert.Equal(HttpStatusCode.Conflict, (await finance.PostAsJsonAsync(confirmationUrl,
            new ConfirmReceivedRequest(40000, "DEPOSIT-1", null))).StatusCode);
        if (useWebhook)
        {
            async Task<HttpStatusCode> SendCredit(string accountNumber)
            {
                using var message = new HttpRequestMessage(HttpMethod.Post, "/api/payment/sepay-webhook")
                {
                    Content = JsonContent.Create(new
                    {
                        id = 613, transferType = "in", transferAmount = 60000, accountNumber,
                        content = qr.Reference, referenceCode = "DEPOSIT-2"
                    })
                };
                message.Headers.TryAddWithoutValidation("x-api-key", "test-only-key");
                using var response = await client.SendAsync(message);
                return response.StatusCode;
            }
            Assert.Equal(HttpStatusCode.BadRequest, await SendCredit("0000000000"));
            Assert.Equal(HttpStatusCode.OK, await SendCredit(qr.AccountNumber));
            Assert.Equal(HttpStatusCode.OK, await SendCredit(qr.AccountNumber));
        }
        else
        {
            using var response = await finance.PostAsJsonAsync(confirmationUrl, new ConfirmReceivedRequest(60000, "DEPOSIT-2", null));
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        }
        var paid = await db.B2bcontracts.AsNoTracking().Include(c => c.PaymentTransactions)
            .ThenInclude(p => p.ContractDepositAllocations).SingleAsync(c => c.B2bcontractId == id);
        Assert.Equal("Active", paid.Status);
        Assert.Equal(100000, OfftakeContractRules.DepositPaid(paid));
        Assert.Equal(100000, paid.PaymentTransactions.SelectMany(p => p.ContractDepositAllocations).Sum(a => a.Amount));
    }

    private sealed record BuyerActor(long UserId) : IActorContext;

    [CommerceSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BuyerInspectionRecordsAcceptanceOrSharesDiscrepancyWithFarmer(bool discrepancy)
    {
        await using var fixture = await CommerceDatabase.Create(new Dictionary<string, string?>
        {
            ["Payment:SePay:AccountNumber"] = "1234567890", ["Payment:SePay:BankShortName"] = "MB"
        });
        await using var db = fixture.Open();
        var now = fixture.Clock.GetUtcNow().UtcDateTime;
        var batch = await db.HarvestBatches.Include(b => b.CropSeason).SingleAsync(b => b.HarvestBatchId == fixture.BatchId);
        var farmId = batch.CropSeason.FarmerProfileId;
        var buyer = new WholesaleBuyerProfile
        {
            UserId = fixture.BuyerId, CompanyName = "Buyer Co", TaxCode = "0123456789",
            RepresentativeName = "Buyer", BusinessAddress = "Test address", VerificationStatus = "Approved"
        };
        db.WholesaleBuyerProfiles.Add(buyer);
        db.WholesaleBuyerProfiles.Add(new WholesaleBuyerProfile
        {
            UserId = fixture.OtherBuyerId, CompanyName = "Other Co", TaxCode = "0987654321",
            RepresentativeName = "Other", BusinessAddress = "Other address", VerificationStatus = "Approved"
        });
        var finance = new User
        {
            Username = "finance", Email = "finance@example.test", FullName = "Finance",
            PasswordHash = "test-only", Role = "FinanceStaff", Status = "Active", IsEmailVerified = true
        };
        db.Users.Add(finance);
        var contract = new B2bcontract
        {
            WholesaleBuyerProfile = buyer, FarmerProfileId = farmId, ContractNumber = "OFTC-INSPECTION",
            Status = "Active", DigitalSignatureStatus = "FullySigned", SignedDate = now, CurrentVersionNo = 1,
            ValidFrom = new(2026, 10, 1), ValidUntil = new(2026, 11, 30), CreatedAt = now
        };
        var version = new ContractVersion
        {
            VersionNo = 1, ProposedByUserId = fixture.BuyerId, TermsAndConditions = "Điều khoản",
            VersionStatus = "Signed"
        };
        var line = new ContractLine
        {
            B2bcontract = contract, ProductId = fixture.ProductId, CropSeasonId = batch.CropSeasonId,
            UnitSnapshot = "kg", PriceType = "Fixed", CommittedQuantity = 10, AgreedPrice = 25000
        };
        version.ContractLines.Add(line);
        var milestone = new ContractMilestone
        {
            B2bcontract = contract, ContractLine = line, MilestoneTitle = "Giao theo hợp đồng", MilestoneType = "DeliveryAndPayment",
            DueDate = new(2026, 10, 27), DeliveryQuantity = 10, DeliveredQuantity = 10, Amount = 250000,
            FulfillmentStatus = "Shipped", PaymentStatus = "Unpaid"
        };
        version.ContractMilestones.Add(milestone);
        contract.ContractVersions.Add(version);
        var order = new Order
        {
            BuyerUserId = fixture.BuyerId, OrderCode = "OFT-INSPECTION", OrderType = "Wholesale", OrderDate = now,
            ShippingAddress = "Test address", RecipientNameSnapshot = "Buyer", RecipientPhoneSnapshot = "0900000000",
            OrderStatus = "Shipping", CheckoutRequestKey = "offtake-inspection", TotalAmount = 250000, FinalAmount = 250000
        };
        var seller = new SellerOrder
        {
            Order = order, FarmerProfileId = farmId, SellerOrderCode = "OFT-INSPECTION-1", Status = "Shipping",
            Subtotal = 250000, FinalAmount = 250000
        };
        var orderItem = new OrderItem
        {
            ProductId = fixture.ProductId, ContractLine = line, ProductNameSnapshot = "Tomato", UnitSnapshot = "kg",
            Quantity = 10, UnitPriceSnapshot = 25000, SubTotal = 250000, ItemStatus = "Shipping"
        };
        seller.OrderItems.Add(orderItem);
        var shipment = new Shipment { ContractMilestone = milestone, ShipmentStatus = "InTransit", ShippedAt = now };
        var item = new ShipmentItem
        {
            OrderItemBatchAllocation = new OrderItemBatchAllocation
            {
                OrderItem = orderItem, HarvestBatchId = fixture.BatchId, AllocationSequence = 1,
                AllocatedQuantity = 10, AllocationStatus = "Shipped", ReservedAt = now
            },
            ShippedQuantity = 10, ReconciliationStatus = "Pending"
        };
        shipment.ShipmentItems.Add(item);
        seller.Shipments.Add(shipment);
        contract.SellerOrders.Add(seller);
        db.B2bcontracts.Add(contract);
        await db.SaveChangesAsync();
        var url = $"api/b2b/contracts/{contract.B2bcontractId}";
        var inspection = $"{url}/shipments/{shipment.ShipmentId}/inspection";
        var input = new OfftakeInspectionRequest([new(item.ShipmentItemId, 10, discrepancy ? 8 : 10, "Loại A")],
            discrepancy ? "Hai kg không đạt phẩm cấp." : null, null);
        var client = fixture.As(fixture.OtherBuyerId, "WholesaleBuyer");
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(inspection, input)).StatusCode);
        fixture.As(fixture.BuyerId, "WholesaleBuyer");
        async Task<OfftakeTrackingDetail> BuyerDetail()
        {
            using var response = await client.GetAsync(url);
            Assert.True(response.IsSuccessStatusCode, fixture.Errors.ToString());
            return (await response.Content.ReadFromJsonAsync<Result<OfftakeTrackingDetail>>())!.Data!;
        }
        var detail = await BuyerDetail();
        Assert.True(Assert.Single(detail.Shipments).CanInspect);
        Assert.Equal("TEST-BATCH", Assert.Single(Assert.Single(detail.Shipments).Items).Batch);
        Assert.Equal(25, Assert.Single(detail.Production).Harvested);
        if (discrepancy)
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(inspection, input)).StatusCode);
            input = input with { Evidence = [await Upload($"{url}/evidence")] };
        }
        using var inspected = await client.PostAsJsonAsync(inspection, input);
        Assert.True(inspected.IsSuccessStatusCode, await inspected.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(inspection, input)).StatusCode);
        if (discrepancy)
        {
            var ticket = Assert.Single((await BuyerDetail()).Discrepancies);
            var evidence = Assert.Single(ticket.Evidence);
            fixture.As(fixture.OtherBuyerId, "WholesaleBuyer");
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{url}/evidence/{evidence.Id}")).StatusCode);
            fixture.Farmer();
            var farmerUrl = $"api/farmer/offtake-contracts/{contract.B2bcontractId}";
            using var farmerEvidence = await client.GetAsync($"{farmerUrl}/evidence/{evidence.Id}");
            Assert.True(farmerEvidence.IsSuccessStatusCode, fixture.Errors.ToString());
            using var farmerDetail = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync(farmerUrl));
            var expectedVersion = farmerDetail.RootElement.GetProperty("version").GetString();
            using var replied = await client.PostAsJsonAsync($"{farmerUrl}/discrepancy", new ContractDiscrepancyResponseRequest
            {
                TicketId = ticket.Id, ShipmentItemId = item.ShipmentItemId, ProposedQuantity = 8,
                ProposedResolution = "Chấp nhận nghiệm thu 8 kg.", Reason = "Đồng ý số lượng kiểm nhận.",
                ExpectedVersion = expectedVersion!, RequestKey = Guid.NewGuid().ToString(),
                Evidence = [await Upload("api/farmer/offtake-contracts/evidence")]
            });
            Assert.True(replied.IsSuccessStatusCode, await replied.Content.ReadAsStringAsync());
            fixture.As(fixture.BuyerId, "WholesaleBuyer");
            ticket = Assert.Single((await BuyerDetail()).Discrepancies);
            var reply = Assert.Single(ticket.Replies);
            Assert.Equal(8, reply.Quantity);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{url}/evidence/{ticket.Evidence.Single(e => !e.FromBuyer).Id}")).StatusCode);
            using var decided = await client.PostAsJsonAsync(
                $"{url}/shipments/{shipment.ShipmentId}/discrepancies/{ticket.Id}/decision",
                new OfftakeDiscrepancyDecision(true, null, reply.At));
            Assert.True(decided.IsSuccessStatusCode, await decided.Content.ReadAsStringAsync());
            Assert.Equal("Resolved", Assert.Single((await BuyerDetail()).Discrepancies).Status);
        }
        var invoice = Assert.Single((await BuyerDetail()).Invoices);
        Assert.True(invoice.CanPay);
        Assert.Equal(discrepancy ? 200000 : 250000, invoice.Amount);
        var paymentUrl = $"{url}/milestones/{milestone.ContractMilestoneId}/payment";
        var payment = (await client.GetFromJsonAsync<Result<PaymentQr>>(paymentUrl))!.Data!;
        Assert.Equal(invoice.Amount, payment.Amount);
        fixture.As(finance.UserId, "FinanceStaff");
        using var paid = await client.PostAsJsonAsync($"api/finance/contract-payments/lines/{milestone.ContractMilestoneId}/confirm",
            new ConfirmReceivedRequest(payment.Amount, "OFT-INVOICE-RECEIPT", null));
        Assert.True(paid.IsSuccessStatusCode, await paid.Content.ReadAsStringAsync());
        Assert.Equal(invoice.Amount, await db.PaymentInvoiceAllocations.AsNoTracking()
            .Where(a => a.Invoice.ContractMilestoneId == milestone.ContractMilestoneId).SumAsync(a => a.Amount));

        async Task<string> Upload(string endpoint)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent("%PDF-1.4"u8.ToArray()), "file", "sample.pdf");
            using var response = await client.PostAsync(endpoint, form);
            var content = await response.Content.ReadAsStringAsync();
            Assert.True(response.IsSuccessStatusCode, content);
            using var json = System.Text.Json.JsonDocument.Parse(content);
            var root = json.RootElement;
            return (root.TryGetProperty("data", out var data) ? data : root).GetProperty("url").GetString()!;
        }
    }

    private static async Task VerifyEmailRetryAsync(CommerceDatabase fixture, long noticeId,
        long contractId)
    {
        await RunWorkerUntilAsync("Failed");
        var first = Assert.Single(fixture.NotificationGateway.Attempts);
        Assert.Equal("farmer@example.test", first.Recipient);
        Assert.Contains($"https://agrisustain.example.test/Farmer/offtake-proposal.html?id={contractId}",
            first.Body);

        await using var db = fixture.Open();
        var email = await db.Notifications.SingleAsync(x => x.NotificationId == noticeId);
        Assert.Equal(1, email.RetryCount);
        email.SentAt = fixture.Clock.GetUtcNow().UtcDateTime.AddMinutes(-1);
        await db.SaveChangesAsync();

        await RunWorkerUntilAsync("Sent");
        Assert.Equal(new[] { first, first }, fixture.NotificationGateway.Attempts.ToArray());
        Assert.Equal(2, await db.Notifications.CountAsync(x =>
            x.RelatedRecordType == "B2BContract" && x.RelatedRecordId == contractId));

        async Task RunWorkerUntilAsync(string status)
        {
            var scopes = fixture.Host.Services.GetRequiredService<IServiceScopeFactory>();
            using var worker = new OrderNotificationWorker(scopes,
                NullLogger<OrderNotificationWorker>.Instance);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            await worker.StartAsync(timeout.Token);
            try
            {
                await using var probe = fixture.Open();
                while (!await probe.Notifications.AnyAsync(x =>
                    x.NotificationId == noticeId && x.DeliveryStatus == status, timeout.Token))
                {
                    await Task.Delay(30, timeout.Token);
                }
            }
            finally
            {
                await worker.StopAsync(CancellationToken.None);
            }
        }
    }
}
