using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Complaints;
using AgriSustain.Application.DTOs.Finance;
using AgriSustain.Application.DTOs.Refunds;
using AgriSustain.Application.Services.Finance;
using AgriSustain.Domain.Entities;
using AgriSustain.IntegrationTests.Commerce;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests.UseCaseApi;

/// <summary>UC-ADM-01, UC-ADM-09 and UC-FIN-05 through their controllers on SQL Server (Report 5.2 rows added 08/11/2026).</summary>
[Trait("Layer", "SqlIntegration")]
public sealed class DashboardTierFeeApiTests : IAsyncLifetime
{
    private ApiHost _api = null!;

    public async Task InitializeAsync() => _api = await ApiHost.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    // ---------- UC-ADM-01 ----------

    [CommerceSqlFact(DisplayName = "[IT-ADM01-03] A Farmer cannot open the platform dashboard")]
    public async Task Dashboard_Farmer_Forbidden()
    {
        var response = await _api.As(_api.FarmerUserId, "Farmer").GetAsync("api/admin/dashboard");

        await ApiAssert.Status(response, HttpStatusCode.Forbidden);
    }

    [CommerceSqlFact(DisplayName = "[IT-ADM01-04] A range longer than 12 months is a field error on To (MSG-DB-01)")]
    public async Task Dashboard_RangeAbove12Months_FieldError()
    {
        var response = await _api.Admin().GetAsync("api/admin/dashboard?from=2026-01-01&to=2027-01-01");

        var problem = await ApiAssert.Status(response, HttpStatusCode.BadRequest);
        ApiAssert.HasFieldError(problem, "To");
        var toErrors = problem.GetProperty("errors").EnumerateObject()
            .Single(x => string.Equals(x.Name, "To", StringComparison.OrdinalIgnoreCase)).Value;
        Assert.Contains("không quá 12 tháng", toErrors[0].GetString());
    }

    // ---------- UC-ADM-09 ----------

    private static SubscriptionTierRequest Tier(string name) =>
        new(name, "Gói thử nghiệm", 30, 100_000, false, false, null);

    [CommerceSqlFact(DisplayName = "[IT-ADM09-02] Tier names are unique regardless of case (MSG-ST-02)")]
    public async Task Tier_DuplicateNameAnyCase_FieldError()
    {
        await ApiAssert.Status(await _api.Admin().PostAsJsonAsync("api/admin/subscription-tiers", Tier("Gói Vàng")),
            HttpStatusCode.OK);

        var duplicate = await _api.Admin().PostAsJsonAsync("api/admin/subscription-tiers", Tier("gói vàng"));

        var problem = await ApiAssert.Problem(duplicate, HttpStatusCode.BadRequest, "TIER_DUPLICATE");
        ApiAssert.HasFieldError(problem, "Name");
        await using var db = _api.Open();
        Assert.Single(await db.B2bsubscriptionPackages.ToListAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT-ADM09-03] A tier with only an expired subscription still cannot be deleted (MSG-ST-05)")]
    public async Task Tier_WithPastSubscription_NotDeleted()
    {
        await ApiAssert.Status(await _api.Admin().PostAsJsonAsync("api/admin/subscription-tiers", Tier("Gói Bạc")),
            HttpStatusCode.OK);
        string version;
        long tierId;
        await using (var db = _api.Open())
        {
            var tier = await db.B2bsubscriptionPackages.SingleAsync();
            (tierId, version) = (tier.PackageId, tier.Version());
            var buyer = new User
            {
                Username = "wholesale", Email = "wholesale@example.test", FullName = "Wholesale", PasswordHash = "test-only",
                Role = "WholesaleBuyer", Status = "Active", IsEmailVerified = true, CreatedAt = ApiHost.T0.UtcDateTime
            };
            db.Users.Add(buyer);
            await db.SaveChangesAsync();
            var profile = new WholesaleBuyerProfile
            {
                UserId = buyer.UserId, CompanyName = "Test company", TaxCode = "0101234567",
                RepresentativeName = "Wholesale", BusinessAddress = "Test address", VerificationStatus = "Approved"
            };
            db.WholesaleBuyerProfiles.Add(profile);
            await db.SaveChangesAsync();
            db.B2bsubscriptions.Add(new B2bsubscription
            {
                WholesaleBuyerProfileId = profile.WholesaleBuyerProfileId, PackageId = tierId,
                StartDate = ApiHost.T0.UtcDateTime.AddDays(-60), EndDate = ApiHost.T0.UtcDateTime.AddDays(-30),
                SubscriptionStatus = "Expired", PriceSnapshot = 100_000
            });
            await db.SaveChangesAsync();
        }

        var response = await _api.Admin().PostAsJsonAsync($"api/admin/subscription-tiers/{tierId}/delete",
            new SubscriptionTierChangeRequest(true, version));

        await ApiAssert.Problem(response, HttpStatusCode.Conflict, "TIER_PURCHASED");
        await using var check = _api.Open();
        Assert.Single(await check.B2bsubscriptionPackages.ToListAsync());
    }

    [CommerceSqlFact(DisplayName = "[IT-ADM09-04] A Retail customer cannot open tier management")]
    public async Task Tiers_RetailCustomer_Forbidden()
    {
        await ApiAssert.Status(await _api.Buyer().GetAsync("api/admin/subscription-tiers"), HttpStatusCode.Forbidden);
    }

    // ---------- UC-FIN-05 ----------

    private async Task<long> CommissionTypeAsync()
    {
        await using var db = _api.Open();
        var type = new FeeType
        {
            FeeCode = "PLATFORM_COMMISSION", FeeName = "Hoa hồng nền tảng", FeeGroup = FeeType.GroupPlatformCommission,
            RefundMode = FeeType.RefundProportional, Status = FeeType.StatusActive, CreatedAt = ApiHost.T0.UtcDateTime
        };
        db.FeeTypes.Add(type);
        await db.SaveChangesAsync();
        return type.FeeTypeId;
    }

    private async Task<string> PolicyVersionAsync(long policyId)
    {
        await using var db = _api.Open();
        return PlatformFeeService.VersionOf(await db.FeePolicies.SingleAsync(x => x.FeePolicyId == policyId));
    }

    [CommerceSqlFact(DisplayName = "[IT-FIN05-02] Finance drafts and submits a policy; only the Administrator approves it")]
    public async Task Policy_DraftSubmitApprove_OnlyAdministratorApproves()
    {
        long typeId = await CommissionTypeAsync();
        var today = DateOnly.FromDateTime(ApiHost.T0.UtcDateTime.AddHours(7));
        var input = new FeePolicyInput(FeePolicy.MethodPercentage, FeePolicy.BaseItemSubtotal, 5, null, null, null,
            "Retail", true, null, today.AddDays(1), null, "Phí hoa hồng 5%");

        await ApiAssert.Status(await _api.Finance().PostAsJsonAsync($"api/finance/fees/{typeId}/policies",
            new NewPolicyRequest(input, Submit: false)), HttpStatusCode.OK);
        long policyId;
        await using (var db = _api.Open())
        {
            var draft = await db.FeePolicies.SingleAsync(x => x.FeeTypeId == typeId);
            Assert.Equal(FeePolicy.ApprovalDraft, draft.ApprovalStatus);
            policyId = draft.FeePolicyId;
        }
        await ApiAssert.Status(await _api.Finance().PostAsJsonAsync($"api/finance/fees/policies/{policyId}/submit",
            new PolicyActionRequest(await PolicyVersionAsync(policyId))), HttpStatusCode.OK);

        var byFinance = await _api.Finance().PostAsJsonAsync($"api/finance/fees/policies/{policyId}/approve",
            new PolicyActionRequest(await PolicyVersionAsync(policyId)));
        var byAdmin = await _api.Admin().PostAsJsonAsync($"api/finance/fees/policies/{policyId}/approve",
            new PolicyActionRequest(await PolicyVersionAsync(policyId)));

        await ApiAssert.Status(byFinance, HttpStatusCode.Forbidden);
        await ApiAssert.Status(byAdmin, HttpStatusCode.OK);
        await using var check = _api.Open();
        var policy = await check.FeePolicies.SingleAsync(x => x.FeePolicyId == policyId);
        Assert.Equal(FeePolicy.ApprovalApproved, policy.ApprovalStatus);
        Assert.Equal(FeePolicy.StateScheduled, policy.State(today));
        var actions = await check.AuditLogs.Where(x => x.EntityType == PlatformFeeService.AuditFeePolicy && x.EntityId == policyId)
            .OrderBy(x => x.AuditLogId).Select(x => x.Action).ToListAsync();
        Assert.Equal("CreateFeePolicy", actions[0]);
        Assert.Contains("SubmitFeePolicy", actions);
        Assert.Contains(actions, a => a.StartsWith("Approve", StringComparison.Ordinal));
    }

    [CommerceSqlFact(DisplayName = "[IT-FIN05-03] A completed partial refund reverses the refunded share of the fees (BR-FEE-08)")]
    public async Task PartialRefund_ReversesProportionalFeesOnly()
    {
        long commissionId = await CommissionTypeAsync();
        long tomatoItemId;
        await using (var db = _api.Open())
        {
            var now = ApiHost.T0.UtcDateTime;
            var commissionPolicy = new FeePolicy
            {
                FeeTypeId = commissionId, PolicyCode = "COM-5", Method = FeePolicy.MethodPercentage,
                CalculationBase = FeePolicy.BaseItemSubtotal, Rate = 5, OrderType = "Retail",
                EffectiveFrom = DateOnly.FromDateTime(now.AddDays(-30)), ApprovalStatus = FeePolicy.ApprovalApproved,
                Reason = "test", CreatedByUserId = _api.FinanceId, CreatedAt = now, RequestKey = "fee-com-5"
            };
            var service = new FeeType
            {
                FeeCode = "SERVICE_FIXED", FeeName = "Phí dịch vụ", FeeGroup = FeeType.GroupService,
                RefundMode = FeeType.RefundNone, Status = FeeType.StatusActive, CreatedAt = now
            };
            service.Policies.Add(new FeePolicy
            {
                PolicyCode = "SRV-1", Method = FeePolicy.MethodFixedPerOrder, CalculationBase = FeePolicy.BaseSellerOrderTotal,
                FixedAmount = 3_000, OrderType = "Retail", EffectiveFrom = DateOnly.FromDateTime(now.AddDays(-30)),
                ApprovalStatus = FeePolicy.ApprovalApproved, Reason = "test", CreatedByUserId = _api.FinanceId,
                CreatedAt = now, RequestKey = "fee-srv-1"
            });
            db.FeePolicies.Add(commissionPolicy);
            db.FeeTypes.Add(service);
            await db.SaveChangesAsync();
            var items = await db.OrderItems.Where(x => x.SellerOrderId == _api.SellerOrderId).ToListAsync();
            tomatoItemId = items.Single(x => x.ProductNameSnapshot == "Cà chua").OrderItemId;
            foreach (var item in items)
            {
                db.OrderItemFees.Add(new OrderItemFee
                {
                    OrderItemId = item.OrderItemId, FeeTypeId = commissionId, FeePolicyId = commissionPolicy.FeePolicyId,
                    FeeCode = "PLATFORM_COMMISSION", FeeGroup = FeeType.GroupPlatformCommission,
                    RefundMode = FeeType.RefundProportional, RateSnapshot = 5, BaseAmount = item.SubTotal,
                    Amount = item.SubTotal * 0.05m
                });
            }
            var servicePolicy = service.Policies.Single();
            db.OrderItemFees.Add(new OrderItemFee
            {
                OrderItemId = tomatoItemId, FeeTypeId = service.FeeTypeId, FeePolicyId = servicePolicy.FeePolicyId,
                FeeCode = "SERVICE_FIXED", FeeGroup = FeeType.GroupService, RefundMode = FeeType.RefundNone,
                BaseAmount = 200_000, Amount = 3_000
            });
            await db.SaveChangesAsync();
        }

        // Partial complaint refund: 5 kg of the 10 kg of spinach (UC-CS-01), then Finance completes it (UC-FIN-03).
        await ApiAssert.Status(await _api.Support().PostAsJsonAsync($"api/support/complaints/{_api.TicketId}/resolve",
            new ResolveComplaintRequest(ComplaintResolutions.PartialRefund, ComplaintParties.Seller,
                [new RefundItemRequest(_api.SpinachItemId, 5)], "Hỏng một nửa")), HttpStatusCode.OK);
        long refundId;
        await using (var db = _api.Open())
        {
            refundId = (await db.RefundTransactions.SingleAsync()).RefundTransactionId;
        }
        await ApiAssert.Status(await _api.Finance().PostAsJsonAsync($"api/finance/refunds/{refundId}/start-transfer",
            new RefundTransferRequest(RefundTransaction.StatusPending)), HttpStatusCode.OK);
        await ApiAssert.Status(await _api.Finance().PostAsJsonAsync($"api/finance/refunds/{refundId}/complete",
            new CompleteRefundRequest(RefundMethods.GatewayRefund, "SEPAY-PARTIAL-01", null)), HttpStatusCode.OK);

        await using var check = _api.Open();
        var refund = await check.RefundTransactions.SingleAsync();
        decimal share = refund.Amount / 180_000m;
        Assert.InRange(share, 0.01m, 0.99m);
        var fees = await check.OrderItemFees.ToListAsync();
        foreach (var fee in fees.Where(x => x.RefundMode == FeeType.RefundProportional))
        {
            Assert.Equal(decimal.Round(fee.Amount * share, 0, MidpointRounding.AwayFromZero), fee.ReversedAmount);
        }
        Assert.Equal(0m, fees.Single(x => x.FeeCode == "SERVICE_FIXED").ReversedAmount);
    }

    [CommerceSqlFact(DisplayName = "[IT-FIN05-04] A Retail customer cannot open fee management")]
    public async Task Fees_RetailCustomer_Forbidden()
    {
        await ApiAssert.Status(await _api.Buyer().GetAsync("api/finance/fees"), HttpStatusCode.Forbidden);
    }
}
