using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Payouts;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Repositories;
using AgriSustain.IntegrationTests.UT1X;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgriSustain.IntegrationTests.UT3X;

public sealed class FarmerRevenuePayoutIntegrationTests
{
    [CommerceSqlFact]
    public async Task IT_FAR_09_001_RepositoryScopesLedgerAndExportReadsOnly()
    {
        await using var f = await CommerceDatabase.Create();
        await using var db = f.Open();
        var repository = new FarmerRevenueRepository(db, Config());
        var service = new FarmerRevenueService(repository, new());
        var seller = await db.SellerOrders.SingleAsync(x => x.SellerOrderId == f.SellerId);
        seller.Status = "Completed";
        seller.ReceivedAt = f.Clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync();
        var date = DateOnly.FromDateTime(seller.ReceivedAt.Value);
        var filter = new AgriSustain.Application.DTOs.Revenue.RevenueFilterRequest
        {
            From = date, To = date, Channel = "Retail"
        };
        var report = await service.GetRevenueAsync(f.FarmerId, filter, default);
        Assert.True(report.IsSuccess, report.Error?.Message);
        Assert.True(report.Value!.Totals.Sales > 0);
        var item = report.Value.Items.First();
        Assert.Equal(item.Id,
            (await service.GetTransactionDetailAsync(f.FarmerId, item.Id, default)).Value?.Id);
        Assert.Equal("FORBIDDEN",
            (await service.GetTransactionDetailAsync(f.OtherBuyerId, item.Id, default)).Error?.Code);
        Assert.True((await service.ExportRevenueAsync(f.FarmerId, filter, "xlsx", default)).IsSuccess);
        Assert.Empty(await db.FarmerPayouts.ToListAsync());
    }

    [CommerceSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IT_FIN_04_001_RequestAndDispositionPersistOnce(bool reject)
    {
        await using var f = await CommerceDatabase.Create();
        var (financeId, sourceId) = await Seed(f);
        await using var db = f.Open();
        var rules = new FarmerPayoutRules(Config());
        var repository = new FinanceFarmerPayoutRepository(db);
        var access = new FarmerRevenueRepository(db, Config());
        var notices = new PayoutNotificationService(repository, access, Config());
        var farmer = new FarmerPaymentRequestService(repository, access, rules, notices);
        var gateway = new SimulatedOutflow();
        var finance = new FinanceFarmerPayoutService(repository, rules, gateway, notices);
        var input = new CreateFarmerPayoutRequest
        {
            SelectedSourceIds = [sourceId], RequestKey = Guid.NewGuid().ToString()
        };
        var preview = await farmer.PreviewAsync(f.FarmerId, input, default);
        Assert.True(preview.IsSuccess && preview.Value!.CanCreate, preview.Error?.Message);
        input.ExpectedBalance = preview.Value!.ExpectedBalance;
        var created = await farmer.SubmitAsync(f.FarmerId, input, default);
        Assert.True(created.IsSuccess, created.Error?.Message);
        long id = created.Value!.Id;
        Assert.Equal(id, (await farmer.SubmitAsync(f.FarmerId, input, default)).Value?.Id);
        long profile = (await repository.FarmerForPayoutAsync(id, default))!.Value;
        var context = (await repository.LoadContextAsync(profile, id, false, default))!;
        Assert.Equal(0, Assert.Single(context.Payout!.PayoutItems).SourceAmountSettled);
        var version = rules.Version(context);
        if (reject)
        {
            var request = new RejectFarmerPayoutRequest
            {
                RequestKey = Guid.NewGuid().ToString(), ExpectedVersion = version,
                Reason = "Test rejection before opening QR"
            };
            Assert.Equal("Rejected", (await finance.RejectRequestAsync(financeId, id, request, default)).Value?.Status);
            Assert.True((await farmer.PreviewAsync(f.FarmerId, input, default)).Value!.CanCreate);
        }
        else
        {
            var request = new ProcessFarmerPayoutRequest
            {
                RequestKey = Guid.NewGuid().ToString(), ExpectedVersion = version,
                ConfirmedNetAmount = preview.Value.Net
            };
            var opened = await finance.ProcessPayoutAsync(financeId, id, request, default);
            Assert.True(opened.IsSuccess, opened.Error?.Message);
            Assert.Equal("Processing", opened.Value!.Status);
            var lookup = new ReconcileFarmerPayoutRequest { ExistingOperationKey = context.Payout.PayoutCode };
            Assert.Equal("Processing", (await finance.ReconcilePayoutAsync(financeId, id, lookup, default)).Value?.Status);
            var debit = new PayoutOutgoingTransfer("TEST-EVENT", "MB", "333333333",
                preview.Value.Net, DateTime.UtcNow, lookup.ExistingOperationKey, "TEST-BANK", "test debit");
            gateway.Transfers = [debit with { EventId = "OTHER-REFERENCE", Content = "FPO000000000000" }];
            var unmatched = await finance.ReconcilePayoutAsync(financeId, id, lookup, default);
            Assert.Equal("Processing", unmatched.Value?.Status);
            Assert.Equal(FinancePayoutMessages.Text("MSG175"), unmatched.Value?.Message);

            PayoutOutgoingTransfer[] invalidDebits =
            [
                debit with { EventId = "WRONG-ACCOUNT", SourceAccountNumber = "444444444" },
                debit with { EventId = "WRONG-BANK", Gateway = "TPB" },
                debit with { EventId = "WRONG-AMOUNT", Amount = debit.Amount + 1m },
                debit with { EventId = "BEFORE-QR", At = debit.At.AddMinutes(-2) },
                debit with { EventId = "FUTURE-DEBIT", At = debit.At.AddMinutes(10) }
            ];
            foreach (var invalid in invalidDebits)
            {
                gateway.Transfers = [invalid];
                var pending = await finance.ReconcilePayoutAsync(financeId, id, lookup, default);
                Assert.Equal("NeedsReconciliation", pending.Value?.Status);
                Assert.Equal(FinancePayoutMessages.Text("MSG175"), pending.Value?.Message);
                await using var pendingDb = f.Open();
                var pendingPayout = await pendingDb.FarmerPayouts.Include(x => x.PayoutItems).SingleAsync();
                Assert.Equal(0, Assert.Single(pendingPayout.PayoutItems).SourceAmountSettled);
                Assert.Null(pendingPayout.BankTransferRef);
                var reserved = await farmer.PreviewAsync(f.FarmerId, input, default);
                Assert.NotEqual(true, reserved.Value?.CanCreate);
                Assert.True(await pendingDb.AuditLogs.AnyAsync(x => x.EntityType == "FinanceFarmerPayout"
                    && x.EntityId == id && x.Action == "MismatchOut"
                    && x.NewValue != null && x.NewValue.Contains(invalid.EventId)));
                Assert.False(await pendingDb.Notifications.AnyAsync(x =>
                    x.RelatedRecordType == "FarmerPayout" && x.RelatedRecordId == id && x.UserId == f.FarmerId));
            }

            gateway.Transfers = [debit];
            var paid = await finance.ReconcilePayoutAsync(financeId, id, lookup, default);
            Assert.Equal("Processed", paid.Value?.Status);
            Assert.Equal(FinancePayoutMessages.Text("MSG171"), paid.Value?.Message);
            Assert.True(await finance.RecordOutgoingAsync(debit, default));
            Assert.False(await finance.RecordOutgoingAsync(debit with { RawPayload = "conflicting replay" }, default));
        }
        await using var verify = f.Open();
        var payout = await verify.FarmerPayouts.Include(x => x.PayoutItems).SingleAsync();
        Assert.Equal(reject ? "Rejected" : "Processed", payout.PayoutStatus);
        Assert.Equal(reject ? null : "TEST-BANK", payout.BankTransferRef);
        var line = Assert.Single(payout.PayoutItems);
        Assert.Equal(reject ? 0 : line.GrossAmount, line.SourceAmountSettled);
        var actions = await verify.AuditLogs.Where(x =>
            x.EntityType == "FinanceFarmerPayout" && x.EntityId == id).ToListAsync();
        Assert.Single(actions, x => x.Action == "Submitted");
        Assert.Single(actions, x => x.Action == (reject ? "Rejected" : "Settled"));
        var recipients = await verify.Notifications.Where(x => x.RelatedRecordType == "FarmerPayout"
            && x.RelatedRecordId == id).Select(x => x.UserId).ToListAsync();
        Assert.Equal(2, recipients.Count(x => x == f.FarmerId));
        Assert.Equal(2, recipients.Count(x => x == financeId));
    }

    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Payouts:WaitingHours"] = "0" }).Build();

    [CommerceSqlFact]
    public async Task IT_FAR_09_007_FailedRequestRollsBackReservationAndCanBeRetried()
    {
        await using var f = await CommerceDatabase.Create();
        var (_, sourceId) = await Seed(f);
        await using var db = f.Open();
        var settings = Config();
        var repository = new FinanceFarmerPayoutRepository(db);
        var access = new FarmerRevenueRepository(db, settings);
        var notices = new PayoutNotificationService(repository, access, settings);
        var service = new FarmerPaymentRequestService(repository, access,
            new FarmerPayoutRules(settings), notices);
        var input = new CreateFarmerPayoutRequest
        {
            SelectedSourceIds = [sourceId], RequestKey = Guid.NewGuid().ToString()
        };
        var preview = await service.PreviewAsync(f.FarmerId, input, default);
        Assert.True(preview.IsSuccess, preview.Error?.Message);
        input.ExpectedBalance = preview.Value!.ExpectedBalance;
        f.Failure.Armed = true;
        await Assert.ThrowsAsync<IOException>(() => service.SubmitAsync(f.FarmerId, input, default));
        await using var verify = f.Open();
        Assert.False(await verify.FarmerPayouts.AnyAsync());
        Assert.False(await verify.PayoutItems.AnyAsync());
        Assert.False(await verify.Notifications.AnyAsync(x => x.RelatedRecordType == "FarmerPayout"));
        Assert.True((await service.PreviewAsync(f.FarmerId, input, default)).Value!.CanCreate);
        Assert.True((await service.SubmitAsync(f.FarmerId, input, default)).IsSuccess);
        Assert.Equal(1, await verify.FarmerPayouts.CountAsync());
    }

    private static async Task<(long Finance, long Source)> Seed(CommerceDatabase f)
    {
        await using var db = f.Open();
        var past = DateTime.UtcNow.AddDays(-3);
        var finance = new User
        {
            Username = "it3x-finance", FullName = "Test Finance", Email = "finance@example.test", PhoneNumber = "0912345604",
            PasswordHash = "test-only", Role = "FinanceStaff", Status = "Active", IsEmailVerified = true
        };
        db.Users.Add(finance);
        var bank = await db.SupportedBanks.FirstOrDefaultAsync(x => x.IsActive);
        bank ??= new SupportedBank { BankCode = "MB", ShortName = "MB", FullName = "Test MB", IsActive = true };
        db.UserBankAccounts.Add(new UserBankAccount
        {
            UserId = f.FarmerId, BankCodeNavigation = bank, AccountNumber = "0123456789",
            AccountHolder = "TEST FARM", IsDefault = true, VerificationStatus = "Verified",
            CreatedAt = past, VerifiedAt = past
        });
        var seller = await db.SellerOrders.Include(x => x.OrderItems)
            .SingleAsync(x => x.SellerOrderId == f.SellerId);
        seller.Status = "Completed";
        seller.ReceivedAt = past;
        var item = Assert.Single(seller.OrderItems);
        var payment = await db.PaymentTransactions.SingleAsync(x => x.OrderId == seller.OrderId);
        var invoice = new Invoice
        {
            SellerOrderId = seller.SellerOrderId, InvoiceNumber = "INV-IT3X",
            IssuedAt = past, TotalAmount = item.SubTotal, InvoiceStatus = "Paid"
        };
        var source = new InvoiceLine
        {
            OrderItemId = item.OrderItemId, Quantity = item.Quantity,
            UnitPriceSnapshot = item.UnitPriceSnapshot, LineAmount = item.SubTotal
        };
        invoice.InvoiceLines.Add(source);
        invoice.PaymentInvoiceAllocations.Add(new PaymentInvoiceAllocation
        {
            PaymentTransactionId = payment.PaymentTransactionId, Amount = item.SubTotal,
            RequestKey = Guid.NewGuid().ToString(), CreatedAt = past
        });
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        return (finance.UserId, source.InvoiceLineId);
    }

    private sealed class SimulatedOutflow : IPayoutGateway
    {
        public IReadOnlyList<PayoutOutgoingTransfer> Transfers { get; set; } = [];
        public PayoutTransferPlan? CreateQr(FarmerPayout payout) => new(
            payout.PayoutCode, "MB", "MB", "333333333", payout.RecipientBankCode!,
            payout.RecipientAccountNumber!, payout.NetAmount, DateTime.UtcNow, "https://example.test/qr");
        public bool AuthenticateWebhook(string authorization, string alternateKey) => false;
        public Task<PayoutGatewayLookup> GetTransferStatusAsync(
            PayoutTransferPlan plan, CancellationToken ct) =>
            Task.FromResult(new PayoutGatewayLookup(Transfers, true, null));
    }
}
