using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.DTOs.Checkout;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Domain.Entities;
using TeamUnitOfWork = AgriSustain.Application.Interfaces.UnitOfWork.IUnitOfWork;

namespace AgriSustain.UnitTests.DuIte1;

/// <summary>Hand-written test doubles for UC-AUTH-01, UC-AUTH-03 and UC-RC-04 unit tests.</summary>
internal sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}

internal sealed class FakeCurrentUser(long? userId, string role = "") : ICurrentActor
{
    // ICurrentActor uses 0 for an anonymous caller.
    public long UserId { get; } = userId ?? 0;

    public string Role { get; } = role;
}

internal sealed class FakeOtpGenerator(params string[] codes) : IOtpGenerator
{
    private readonly Queue<string> _codes = new(codes);
    public string Generate() => _codes.Count > 0 ? _codes.Dequeue() : "111111";
}

internal sealed class FakeEmailService : IEmailService
{
    public bool Succeeds { get; set; } = true;
    public List<(string Email, string Otp)> Sent { get; } = [];
    public List<string> Purposes { get; } = [];

    public Task<bool> SendOtpEmailAsync(string email, string otpCode, string purpose, CancellationToken cancellationToken)
    {
        if (Succeeds) { Sent.Add((email, otpCode)); Purposes.Add(purpose); }
        return Task.FromResult(Succeeds);
    }
}

internal sealed class FakeAuthRepository : IAuthRepository
{
    public HashSet<string> ExistingEmails { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ExistingPhones { get; } = [];
    public Dictionary<string, PendingRegistration> Pending { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<User> CreatedUsers { get; } = [];

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct) => Task.FromResult(ExistingEmails.Contains(email));
    public Task<bool> PhoneExistsAsync(string phone, CancellationToken ct) => Task.FromResult(ExistingPhones.Contains(phone));
    public Task SavePendingRegistrationAsync(PendingRegistration r, CancellationToken ct) { Pending[r.Email] = r; return Task.CompletedTask; }
    public Task<PendingRegistration?> FindPendingRegistrationAsync(string email, CancellationToken ct) =>
        Task.FromResult(Pending.TryGetValue(email, out var r) ? r : null);
    public Task UpdatePendingRegistrationAsync(PendingRegistration r, CancellationToken ct) { Pending[r.Email] = r; return Task.CompletedTask; }
    public Task RemovePendingRegistrationAsync(string email, CancellationToken ct) { Pending.Remove(email); return Task.CompletedTask; }

    public Task<User> CreateUserFromPendingAsync(PendingRegistration r, CancellationToken ct)
    {
        var user = new User
        {
            UserId = 100 + CreatedUsers.Count, Email = r.Email, FullName = r.FullName,
            PhoneNumber = r.PhoneNumber, Role = r.Role, Status = "Active", IsEmailVerified = true,
            PasswordHash = r.PasswordHash, Username = r.Email
        };
        CreatedUsers.Add(user);
        ExistingEmails.Add(r.Email);
        ExistingPhones.Add(r.PhoneNumber);
        return Task.FromResult(user);
    }
}

internal sealed class FakeProfileRepository : IProfileRepository
{
    public Dictionary<long, ProfileAccount> Accounts { get; } = [];
    public Dictionary<long, UserProfileResponse> Profiles { get; } = [];
    public HashSet<(string Phone, long OwnerId)> Phones { get; } = [];
    public KycSubmissionResult KycResult { get; set; } =
        new(KycSubmissionOutcome.Submitted, [], VerificationStatuses.PendingApproval, null);
    public UpdateConsumerProfileRequest? LastConsumerUpdate { get; private set; }
    public UpdateFarmerProfileRequest? LastFarmerUpdate { get; private set; }

    public Task<UserProfileResponse?> GetProfileAsync(long userId, CancellationToken ct) =>
        Task.FromResult(Profiles.TryGetValue(userId, out var p) ? p : null);
    public Task<ProfileAccount?> GetAccountAsync(long userId, CancellationToken ct) =>
        Task.FromResult(Accounts.TryGetValue(userId, out var a) ? a : null);
    public Task<bool> PhoneUsedByAnotherUserAsync(string phone, long userId, CancellationToken ct) =>
        Task.FromResult(Phones.Any(x => x.Phone == phone && x.OwnerId != userId));

    public Task<UserProfileResponse?> UpdateConsumerProfileAsync(long userId, UpdateConsumerProfileRequest request, CancellationToken ct)
    {
        LastConsumerUpdate = request;
        return GetProfileAsync(userId, ct);
    }

    public Task<UserProfileResponse?> UpdateFarmerProfileAsync(long userId, UpdateFarmerProfileRequest request, CancellationToken ct)
    {
        LastFarmerUpdate = request;
        return GetProfileAsync(userId, ct);
    }

    public string? LastAvatarUrl { get; private set; }

    public Task<UserProfileResponse?> UpdateAvatarAsync(long userId, string avatarUrl, CancellationToken ct)
    {
        LastAvatarUrl = avatarUrl;
        return GetProfileAsync(userId, ct);
    }

    public Task<KycSubmissionResult> SubmitFarmerKycAsync(long userId, SubmitKycRequest request, CancellationToken ct) =>
        Task.FromResult(KycResult);
}

internal sealed class FakeOrderRepository : IOrderTrackingRepository
{
    public Dictionary<long, OrderDetailDto> Details { get; } = [];
    public Dictionary<long, long> Owners { get; } = [];
    public List<OrderSummaryDto> Summaries { get; } = [];
    public List<(long Buyer, long Order)> MarkedRead { get; } = [];
    public OrderQuery? LastQuery { get; private set; }

    public Task<PagedResponse<OrderSummaryDto>> GetOrdersByBuyerAsync(long buyerId, OrderQuery query, CancellationToken ct)
    {
        LastQuery = query;
        var items = Summaries.OrderByDescending(x => x.OrderDate).ToList();
        return Task.FromResult(new PagedResponse<OrderSummaryDto>(items, items.Count, query.Page, query.PageSize));
    }

    public Task<OrderDetailDto?> GetOrderDetailAsync(long buyerId, long orderId, CancellationToken ct) =>
        Task.FromResult(Details.TryGetValue(orderId, out var d) && Owners[orderId] == buyerId ? d : null);

    public Task<OrderDetailDto?> GetOrderDetailByCodeAsync(long buyerId, string orderCode, CancellationToken ct) =>
        Task.FromResult(Details.Values.FirstOrDefault(d => d.OrderCode == orderCode && Owners[d.OrderId] == buyerId));

    public Task MarkOrderNotificationsReadAsync(long buyerId, long orderId, CancellationToken ct)
    {
        MarkedRead.Add((buyerId, orderId));
        return Task.CompletedTask;
    }
}

/// <summary>Executes the operation directly; a failed result counts as a rollback.</summary>
internal sealed class FakeUnitOfWork : TeamUnitOfWork
{
    public FakeAuthRepository AuthRepo { get; } = new();
    public FakeProfileRepository ProfileRepo { get; } = new();
    public FakeOrderRepository OrderRepo { get; } = new();
    public int Commits { get; private set; }
    public int Rollbacks { get; private set; }

    public IAuthRepository Auth => AuthRepo;
    public IProfileRepository Profiles => ProfileRepo;
    public IOrderTrackingRepository Orders => OrderRepo;

    public Task<Result<T>> ReadAsync<T>(Func<TeamUnitOfWork, Task<Result<T>>> operation, CancellationToken ct = default) =>
        operation(this);

    public async Task<Result<T>> ExecuteAsync<T>(Func<TeamUnitOfWork, Task<Result<T>>> operation, CancellationToken ct = default)
    {
        var result = await operation(this);
        if (result.IsSuccess) Commits++; else Rollbacks++;
        return result;
    }
}

internal sealed class FakeAddressCatalog : IAddressCatalog
{
    public Task<IReadOnlyList<ProvinceOption>> GetProvincesAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProvinceOption>>([new("100000", "Hà Nội")]);
    public Task<IReadOnlyList<DistrictOption>> GetDistrictsAsync(string provinceCode, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DistrictOption>>([new("100100", "100000", "Cầu Giấy")]);
    public Task<IReadOnlyList<WardOption>> GetWardsAsync(string districtCode, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WardOption>>([new("1001001", "100100", "Dịch Vọng")]);

    public Task<CheckoutAddress?> NormalizeAsync(CheckoutAddress a, CancellationToken ct) =>
        Task.FromResult(a.ProvinceCode == "100000" && a.DistrictCode == "100100" && a.WardCode == "1001001"
            ? a with { Province = "Hà Nội", District = "Cầu Giấy", Ward = "Dịch Vọng", CatalogVersion = "test" }
            : null);
}
