using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Verification;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Services;
using AgriSustain.Domain.Entities;
using Xunit;

namespace AgriSustain.UnitTests;

public sealed class KycCertificateVerificationTests
{
    private static readonly DateOnly Today = new(2026, 11, 8);
    private static readonly DateTime NowUtc = new(2026, 11, 8, 2, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("Verified", true)]
    [InlineData("Approved", true)]
    [InlineData("PendingReview", false)]
    [InlineData("Pending", false)]
    [InlineData("Rejected", false)]
    [InlineData("Revoked", false)]
    [InlineData(null, false)]
    public void ValidCertificate_StatusCheck_AcceptsVerifiedAndApproved(string? status, bool expected)
    {
        var cert = new FarmCertificate
        {
            FarmCertificateId = 1,
            VerificationStatus = status!,
            IssueDate = Today.AddDays(-10),
            ExpiryDate = Today.AddDays(30)
        };

        var predicate = MarketplaceRules.ValidCertificate(Today).Compile();
        Assert.Equal(expected, predicate(cert));
    }

    [Fact]
    public void ValidCertificate_ExpiredOrFuture_ReturnsFalse()
    {
        var expired = new FarmCertificate
        {
            FarmCertificateId = 1,
            VerificationStatus = "Verified",
            IssueDate = Today.AddDays(-30),
            ExpiryDate = Today.AddDays(-1)
        };

        var future = new FarmCertificate
        {
            FarmCertificateId = 2,
            VerificationStatus = "Verified",
            IssueDate = Today.AddDays(5),
            ExpiryDate = Today.AddDays(60)
        };

        var predicate = MarketplaceRules.ValidCertificate(Today).Compile();
        Assert.False(predicate(expired));
        Assert.False(predicate(future));
    }

    [Fact]
    public async Task ReviewAsync_ApproveFarmer_OnlyUpdatesPendingToVerified_PreservesOthers()
    {
        var repo = new FakeKycRepo();
        repo.Users[1] = CreateOfficerUser(1);
        var farmerUser = CreateFarmerUser(10);

        var pendingCert = new FarmCertificate
        {
            FarmCertificateId = 1,
            VerificationStatus = "PendingReview",
            CertificateType = "VietGAP"
        };
        var existingVerified = new FarmCertificate
        {
            FarmCertificateId = 2,
            VerificationStatus = "Verified",
            CertificateType = "GlobalGAP"
        };
        var revokedCert = new FarmCertificate
        {
            FarmCertificateId = 3,
            VerificationStatus = "Revoked",
            CertificateType = "Organic",
            RevocationReason = "Vi phạm tồn dư"
        };

        farmerUser.FarmerProfile!.FarmCertificates = [pendingCert, existingVerified, revokedCert];
        repo.Users[10] = farmerUser;

        var service = new KycVerificationService(repo, new FakeActorContext(1), new FakeClock(NowUtc));
        var result = await service.ReviewAsync(10, new KycReviewDecisionRequest("approve", "Hồ sơ hợp lệ"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Verified", pendingCert.VerificationStatus);
        Assert.Equal("Verified", existingVerified.VerificationStatus);
        Assert.Equal("Revoked", revokedCert.VerificationStatus);
        Assert.Equal("Vi phạm tồn dư", revokedCert.RevocationReason);
    }

    [Fact]
    public async Task ReviewCertificateAsync_Approve_SetsToVerified()
    {
        var repo = new FakeKycRepo();
        repo.Users[1] = CreateOfficerUser(1);
        var cert = new FarmCertificate
        {
            FarmCertificateId = 42,
            FarmerProfileId = 5,
            FarmerProfile = new FarmerProfile { User = new User { UserId = 100 } },
            VerificationStatus = "PendingReview",
            CertificateType = "VietGAP"
        };
        repo.Certificates[42] = cert;

        var service = new KycVerificationService(repo, new FakeActorContext(1), new FakeClock(NowUtc));
        var result = await service.ReviewCertificateAsync(42, new CertificateReviewDecisionRequest("approve"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Verified", cert.VerificationStatus);
        Assert.Equal(NowUtc, cert.ReviewedAt);
        Assert.Equal(1, cert.ReviewedByUserId);
        Assert.Single(repo.Notifications);
        Assert.Single(repo.Audits);
    }

    [Fact]
    public async Task ReviewCertificateAsync_RejectWithoutReason_Fails()
    {
        var repo = new FakeKycRepo();
        repo.Users[1] = CreateOfficerUser(1);
        var cert = new FarmCertificate
        {
            FarmCertificateId = 42,
            VerificationStatus = "PendingReview"
        };
        repo.Certificates[42] = cert;

        var service = new KycVerificationService(repo, new FakeActorContext(1), new FakeClock(NowUtc));
        var result = await service.ReviewCertificateAsync(42, new CertificateReviewDecisionRequest("reject", ""), default);

        Assert.False(result.IsSuccess);
        Assert.Equal("REASON_REQUIRED", result.Error!.Code);
    }

    [Fact]
    public async Task ReviewCertificateAsync_RejectWithReason_SetsRejectedAndReason()
    {
        var repo = new FakeKycRepo();
        repo.Users[1] = CreateOfficerUser(1);
        var cert = new FarmCertificate
        {
            FarmCertificateId = 42,
            FarmerProfileId = 5,
            FarmerProfile = new FarmerProfile { User = new User { UserId = 100 } },
            VerificationStatus = "PendingReview",
            CertificateType = "VietGAP"
        };
        repo.Certificates[42] = cert;

        var service = new KycVerificationService(repo, new FakeActorContext(1), new FakeClock(NowUtc));
        var result = await service.ReviewCertificateAsync(42, new CertificateReviewDecisionRequest("reject", "Chứng nhận đã mờ không đọc được mã số"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Rejected", cert.VerificationStatus);
        Assert.Equal("Chứng nhận đã mờ không đọc được mã số", cert.RevocationReason);
    }

    [Fact]
    public async Task ReviewCertificateAsync_Revoke_SetsRevokedAndReason()
    {
        var repo = new FakeKycRepo();
        repo.Users[1] = CreateOfficerUser(1);
        var cert = new FarmCertificate
        {
            FarmCertificateId = 42,
            FarmerProfileId = 5,
            FarmerProfile = new FarmerProfile { User = new User { UserId = 100 } },
            VerificationStatus = "Verified",
            CertificateType = "VietGAP"
        };
        repo.Certificates[42] = cert;

        var service = new KycVerificationService(repo, new FakeActorContext(1), new FakeClock(NowUtc));
        var result = await service.ReviewCertificateAsync(42, new CertificateReviewDecisionRequest("revoke", "Chứng nhận bị đình chỉ hiệu lực"), default);

        Assert.True(result.IsSuccess);
        Assert.Equal("Revoked", cert.VerificationStatus);
        Assert.Equal("Chứng nhận bị đình chỉ hiệu lực", cert.RevocationReason);
    }

    private static User CreateOfficerUser(long userId)
    {
        return new User
        {
            UserId = userId,
            Username = "officer",
            FullName = "Chuyên viên thẩm định",
            Email = "officer@agrisustain.com",
            Role = "VerificationOfficer",
            Status = "Active"
        };
    }

    private static User CreateFarmerUser(long userId)
    {
        return new User
        {
            UserId = userId,
            Username = $"farmer{userId}",
            FullName = $"Nông dân {userId}",
            Email = $"farmer{userId}@test.com",
            Role = "Farmer",
            Status = "Pending",
            FarmerProfile = new FarmerProfile
            {
                FarmerProfileId = userId,
                UserId = userId,
                FarmName = "Trang trại mẫu",
                VerificationStatus = "Pending"
            }
        };
    }

    private sealed class FakeKycRepo : IKycVerificationRepository
    {
        public Dictionary<long, User> Users { get; } = [];
        public Dictionary<long, FarmCertificate> Certificates { get; } = [];
        public List<AuditLog> Audits { get; } = [];
        public List<Notification> Notifications { get; } = [];

        public Task<User?> FindActorAsync(long userId, CancellationToken cancellationToken)
        {
            Users.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }

        public Task<Page<KycApplicationSummaryDto>> SearchAsync(KycApplicationSearch search, CancellationToken cancellationToken) =>
            Task.FromResult(new Page<KycApplicationSummaryDto>([], 0, 1, 10));

        public Task<KycApplicationDetailDto?> GetDetailAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult<KycApplicationDetailDto?>(null);

        public Task<User?> GetUserForKycReviewAsync(long userId, CancellationToken cancellationToken)
        {
            Users.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }

        public Task<FarmCertificate?> FindCertificateAsync(long certificateId, CancellationToken cancellationToken)
        {
            Certificates.TryGetValue(certificateId, out var cert);
            return Task.FromResult(cert);
        }

        public void AddAudit(AuditLog audit) => Audits.Add(audit);
        public void AddNotification(Notification notification) => Notifications.Add(notification);
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeActorContext(long userId) : IActorContext
    {
        public long UserId => userId;
    }

    private sealed class FakeClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
