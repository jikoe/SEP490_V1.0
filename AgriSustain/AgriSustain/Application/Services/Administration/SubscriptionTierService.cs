using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators.Administration;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.Administration;

/// <summary>
/// UC-ADM-09 Manage Subscription Tiers. Changes apply to new purchases and renewals only; current
/// subscriptions keep their snapshot (BR-ST-03). A purchased tier can only be deactivated (BR-ST-04).
/// </summary>
public sealed class SubscriptionTierService(
    ISubscriptionTierRepository repository,
    IAdministrationRepository administration,
    IUseCaseUnitOfWork unitOfWork,
    IActorContext actorContext,
    TimeProvider clock) : ISubscriptionTierService
{
    public const string AuditEntity = "B2BSubscriptionPackage";

    public async Task<UseCaseResult<Page<SubscriptionTierDto>>> SearchAsync(SubscriptionTierSearch search,
        CancellationToken cancellationToken)
    {
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<Page<SubscriptionTierDto>>();
        }
        if (search.Page <= 0 || search.PageSize is <= 0 or > 100 || search.Keyword?.Length > 100
            || search.Status is not (null or "" or "Active" or "Inactive"))
        {
            return Invalid<Page<SubscriptionTierDto>>();
        }
        return UseCaseResult<Page<SubscriptionTierDto>>.Success(
            await repository.SearchAsync(search, Now(), cancellationToken));
    }

    public async Task<UseCaseResult<SubscriptionTierDto>> GetAsync(long id, CancellationToken cancellationToken)
    {
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<SubscriptionTierDto>();
        }
        var tier = await repository.GetAsync(id, Now(), cancellationToken);
        return tier is null ? Missing<SubscriptionTierDto>() : UseCaseResult<SubscriptionTierDto>.Success(tier);
    }

    public async Task<UseCaseResult<SubscriptionTierResultDto>> SaveAsync(long? id,
        SubscriptionTierRequest request, CancellationToken cancellationToken)
    {
        var validation = await new SubscriptionTierValidator().ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return UseCaseResult<SubscriptionTierResultDto>.Validation(validation.Errors
                .GroupBy(x => x.PropertyName)
                .ToDictionary(x => x.Key, x => x.Select(error => error.ErrorMessage).ToArray()));
        }
        if (id.HasValue && string.IsNullOrWhiteSpace(request.Version))
        {
            return UseCaseResult<SubscriptionTierResultDto>.Validation(new Dictionary<string, string[]>
            {
                ["Version"] = ["Hãy tải lại gói trước khi lưu."]
            });
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<SubscriptionTierResultDto>();
        }
        var tier = id.HasValue ? await repository.FindAsync(id.Value, cancellationToken) : new B2bsubscriptionPackage();
        if (tier is null)
        {
            return Missing<SubscriptionTierResultDto>();
        }
        if (id.HasValue && tier.Version() != request.Version)
        {
            return Changed<SubscriptionTierResultDto>();
        }
        // Changing the status while editing needs the same MSG-ST-03 confirmation as the row action.
        if (id.HasValue && tier.IsActive != request.IsActive && !request.Confirmed)
        {
            return UseCaseResult<SubscriptionTierResultDto>.Failure("CONFIRMATION_REQUIRED",
                "Hãy xác nhận việc kích hoạt hoặc ngừng kích hoạt gói.");
        }
        string name = request.Name.Trim();
        if (await repository.NameExistsAsync(name, id ?? 0, cancellationToken))
        {
            // MSG-ST-02, shown under the name field.
            string message = $"Đã có gói tên {name}.";
            return new UseCaseResult<SubscriptionTierResultDto>(default, new Error("TIER_DUPLICATE", message,
                "Validation", new Dictionary<string, string[]> { ["Name"] = [message] }));
        }
        string? before = id.HasValue ? Snapshot(tier) : null;
        tier.Apply(name, request.Description, request.DurationDays, request.Price,
            request.UnlocksFarmerDirectContact, request.CanDownloadContractTemplate, request.TransactionLimit,
            request.IsActive);
        if (!id.HasValue)
        {
            repository.Add(tier);
        }
        // The new identity and its audit are committed by the same transaction.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        Audit(tier.PackageId, id.HasValue ? "UpdateTier" : "CreateTier", before, Snapshot(tier));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        // MSG-ST-01
        return await ResultAsync(tier.PackageId, $"Đã lưu gói {name}.", cancellationToken);
    }

    public async Task<UseCaseResult<SubscriptionTierResultDto>> SetActiveAsync(long id, bool active,
        SubscriptionTierChangeRequest request, CancellationToken cancellationToken)
    {
        if (CheckChange(request) is { } invalid)
        {
            return invalid;
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<SubscriptionTierResultDto>();
        }
        var tier = await repository.FindAsync(id, cancellationToken);
        if (tier is null)
        {
            return Missing<SubscriptionTierResultDto>();
        }
        if (tier.Version() != request.Version)
        {
            return Changed<SubscriptionTierResultDto>();
        }
        string verb = active ? "kích hoạt" : "ngừng kích hoạt";
        if (tier.IsActive == active)
        {
            return Changed<SubscriptionTierResultDto>();
        }
        string before = Snapshot(tier);
        // BR-ST-02: only Active tiers can be bought or renewed; current subscriptions are not ended.
        tier.IsActive = active;
        Audit(tier.PackageId, active ? "ActivateTier" : "DeactivateTier", before, Snapshot(tier));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        int current = await repository.CountActiveSubscriptionsAsync(id, Now(), cancellationToken);
        return await ResultAsync(id, $"Đã {verb} gói {tier.PackageName}. {current} thuê bao hiện tại "
            + "giữ nguyên điều kiện đã mua cho tới khi hết hạn.", cancellationToken);
    }

    public async Task<UseCaseResult<SubscriptionTierResultDto>> DeleteAsync(long id,
        SubscriptionTierChangeRequest request, CancellationToken cancellationToken)
    {
        if (CheckChange(request) is { } invalid)
        {
            return invalid;
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<SubscriptionTierResultDto>();
        }
        var tier = await repository.FindAsync(id, cancellationToken);
        if (tier is null)
        {
            return Missing<SubscriptionTierResultDto>();
        }
        if (tier.Version() != request.Version)
        {
            return Changed<SubscriptionTierResultDto>();
        }
        if (await repository.HasSubscriptionsAsync(id, cancellationToken))
        {
            // MSG-ST-05 (BR-ST-04)
            return UseCaseResult<SubscriptionTierResultDto>.Failure("TIER_PURCHASED",
                $"Gói {tier.PackageName} đã có người mua nên không thể xóa. Hãy ngừng kích hoạt gói thay vì xóa.",
                "Conflict");
        }
        string name = tier.PackageName;
        Audit(tier.PackageId, "DeleteTier", Snapshot(tier), null);
        repository.Remove(tier);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UseCaseResult<SubscriptionTierResultDto>.Success(
            new SubscriptionTierResultDto($"Đã xóa gói {name}.", null));
    }

    private static UseCaseResult<SubscriptionTierResultDto>? CheckChange(SubscriptionTierChangeRequest request)
    {
        if (!request.Confirmed)
        {
            return Invalid<SubscriptionTierResultDto>();
        }
        return string.IsNullOrWhiteSpace(request.Version)
            ? UseCaseResult<SubscriptionTierResultDto>.Validation(new Dictionary<string, string[]>
            {
                ["Version"] = ["Hãy tải lại gói trước khi xác nhận."]
            })
            : null;
    }

    private async Task<UseCaseResult<SubscriptionTierResultDto>> ResultAsync(long id, string message,
        CancellationToken cancellationToken)
    {
        var dto = await repository.GetAsync(id, Now(), cancellationToken);
        return UseCaseResult<SubscriptionTierResultDto>.Success(new SubscriptionTierResultDto(message, dto));
    }

    private void Audit(long id, string action, string? before, string? after)
    {
        repository.AddAudit(new AuditLog
        {
            EntityType = AuditEntity, EntityId = id, Action = action, OldValue = before, NewValue = after,
            PerformedByUserId = actorContext.UserId, OccurredAt = Now(), TraceId = actorContext.TraceId
        });
    }

    private static string Snapshot(B2bsubscriptionPackage tier) => JsonSerializer.Serialize(new
    {
        tier.PackageName, tier.PackageDescription, tier.DurationDays, tier.Price,
        tier.UnlocksFarmerDirectContact, tier.CanDownloadContractTemplate, tier.TransactionLimit, tier.IsActive
    });

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;

    private async Task<bool> IsAdministratorAsync(CancellationToken cancellationToken)
    {
        var actor = await administration.FindActorAsync(actorContext.UserId, cancellationToken);
        return actor is not null && actor.Status == "Active"
            && !actor.IsTemporarilyLocked(clock.GetUtcNow())
            && actor.Role is "SystemAdmin" or "Administrator" or "Admin";
    }

    // MSG122
    private static UseCaseResult<T> Changed<T>() => UseCaseResult<T>.Failure("TIER_CHANGED",
        "Gói này đã thay đổi hoặc thao tác đã được xử lý. Hãy tải lại để xem thông tin hiện tại; "
            + "chưa có thao tác nào được thực hiện thêm.", "Conflict");

    private static UseCaseResult<T> Denied<T>() => UseCaseResult<T>.Failure(
        "FORBIDDEN", "Bạn không có quyền thực hiện thao tác này.", "Forbidden");

    private static UseCaseResult<T> Invalid<T>() => UseCaseResult<T>.Failure(
        "VALIDATION", "Vui lòng kiểm tra dữ liệu và xác nhận thao tác.");

    private static UseCaseResult<T> Missing<T>() => UseCaseResult<T>.Failure(
        "NOT_FOUND", "Không tìm thấy gói thuê bao.", "NotFound");
}
