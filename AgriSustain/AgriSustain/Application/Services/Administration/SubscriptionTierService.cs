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
    /// <summary>
    /// EntityType of the audit rows of this use case.
    /// </summary>
    public const string AuditEntity = "B2BSubscriptionPackage";

    /// <summary>
    /// SD1. Tier list: keyword on the name, status Active / Inactive, sorted by name, paged. Each row shows current
    /// subscribers, whether it was ever purchased and its version token.
    /// </summary>
    /// <param name="search">Keyword (≤ 100), Status, Page and PageSize (1..100).</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The page; 403 FORBIDDEN; 400 VALIDATION for invalid filters.</returns>
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

    /// <summary>
    /// One tier with subscribers, purchased flag and version.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>The tier; 403 / 404.</returns>
    public async Task<UseCaseResult<SubscriptionTierDto>> GetAsync(long id, CancellationToken cancellationToken)
    {
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<SubscriptionTierDto>();
        }
        var tier = await repository.GetAsync(id, Now(), cancellationToken);
        return tier is null ? Missing<SubscriptionTierDto>() : UseCaseResult<SubscriptionTierDto>.Success(tier);
    }

    /// <summary>
    /// SD2. Create (id null) or edit a tier. Steps: 1) SubscriptionTierValidator (BR-ST-01); 2) editing needs a version;
    /// 3) transaction + Administrator check; 4) the tier exists and its version still matches (MSG122); 5) changing the
    /// status while editing needs the MSG-ST-03 confirmation; 6) the trimmed name is unique (MSG-ST-02); 7) apply the
    /// values (BR-ST-03: they only affect new purchases and renewals), add when new, save once for the id, audit
    /// CreateTier / UpdateTier with before and after snapshots, save and commit.
    /// </summary>
    /// <param name="id">Tier to edit, or null to create.</param>
    /// <param name="request">Name, description, duration, price, benefits, transaction limit, IsActive, Confirmed and Version.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>MSG-ST-01 "Đã lưu gói …" with the tier; or 400 / 403 / 404 / 409 TIER_CHANGED.</returns>
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

    /// <summary>
    /// SD3. Activate or deactivate after confirmation (MSG-ST-03). The version must match and the tier must not already
    /// have that status (MSG122). BR-ST-02: only Active tiers can be bought or renewed; current subscriptions are not
    /// ended. The message tells how many current subscribers keep their purchased terms.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="active">True to activate, false to deactivate.</param>
    /// <param name="request">Confirmed and the Version the screen showed.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Message with the subscriber count and the tier; or 400 / 403 / 404 / 409.</returns>
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

    /// <summary>
    /// SD4. Delete after confirmation (MSG-ST-04). The version must match; a tier ever purchased cannot be deleted
    /// (BR-ST-04, MSG-ST-05 — deactivate it instead). The audit row is written before the removal, in the same
    /// transaction.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="request">Confirmed and the Version the screen showed.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>"Đã xóa gói …" without a tier; or 400 / 403 / 404 / 409 TIER_CHANGED / TIER_PURCHASED.</returns>
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

    /// <summary>
    /// Checks a status change or delete before the transaction: Confirmed must be true and a version must be sent.
    /// </summary>
    /// <param name="request">Confirmed and the Version the screen showed.</param>
    /// <returns>Null when valid; otherwise VALIDATION or the Version field error.</returns>
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

    /// <summary>
    /// Reloads the tier so the screen gets fresh counts and the new version.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="message">Message to show.</param>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>Success with the message and tier.</returns>
    private async Task<UseCaseResult<SubscriptionTierResultDto>> ResultAsync(long id, string message,
        CancellationToken cancellationToken)
    {
        var dto = await repository.GetAsync(id, Now(), cancellationToken);
        return UseCaseResult<SubscriptionTierResultDto>.Success(new SubscriptionTierResultDto(message, dto));
    }

    /// <summary>
    /// Queues an audit row with the current Administrator, time and trace id.
    /// </summary>
    /// <param name="id">Tier id.</param>
    /// <param name="action">CreateTier, UpdateTier, ActivateTier, DeactivateTier or DeleteTier.</param>
    /// <param name="before">JSON before, or null.</param>
    /// <param name="after">JSON after, or null.</param>
    private void Audit(long id, string action, string? before, string? after)
    {
        repository.AddAudit(new AuditLog
        {
            EntityType = AuditEntity, EntityId = id, Action = action, OldValue = before, NewValue = after,
            PerformedByUserId = actorContext.UserId, OccurredAt = Now(), TraceId = actorContext.TraceId
        });
    }

    /// <summary>
    /// JSON of the audited tier fields.
    /// </summary>
    /// <param name="tier">Tier.</param>
    /// <returns>JSON text.</returns>
    private static string Snapshot(B2bsubscriptionPackage tier) => JsonSerializer.Serialize(new
    {
        tier.PackageName, tier.PackageDescription, tier.DurationDays, tier.Price,
        tier.UnlocksFarmerDirectContact, tier.CanDownloadContractTemplate, tier.TransactionLimit, tier.IsActive
    });

    /// <summary>
    /// Current UTC time from the injected clock.
    /// </summary>
    private DateTime Now() => clock.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Checks the caller in the database: an Active, unlocked SystemAdmin / Administrator / Admin.
    /// </summary>
    /// <param name="cancellationToken">Cancels the work when the HTTP request is aborted.</param>
    /// <returns>True when the caller may act.</returns>
    private async Task<bool> IsAdministratorAsync(CancellationToken cancellationToken)
    {
        var actor = await administration.FindActorAsync(actorContext.UserId, cancellationToken);
        return actor is not null && actor.Status == "Active"
            && !actor.IsTemporarilyLocked(clock.GetUtcNow())
            && actor.Role is "SystemAdmin" or "Administrator" or "Admin";
    }

    // MSG122
    /// <summary>
    /// 409 TIER_CHANGED (MSG122): the tier changed or the action was already processed.
    /// </summary>
    /// <typeparam name="T">Result payload type.</typeparam>
    /// <returns>Failure result.</returns>
    private static UseCaseResult<T> Changed<T>() => UseCaseResult<T>.Failure("TIER_CHANGED",
        "Gói này đã thay đổi hoặc thao tác đã được xử lý. Hãy tải lại để xem thông tin hiện tại; "
            + "chưa có thao tác nào được thực hiện thêm.", "Conflict");

    /// <summary>
    /// 403 FORBIDDEN.
    /// </summary>
    /// <typeparam name="T">Result payload type.</typeparam>
    /// <returns>Failure result.</returns>
    private static UseCaseResult<T> Denied<T>() => UseCaseResult<T>.Failure(
        "FORBIDDEN", "Bạn không có quyền thực hiện thao tác này.", "Forbidden");

    /// <summary>
    /// 400 VALIDATION: check the data and confirm the action.
    /// </summary>
    /// <typeparam name="T">Result payload type.</typeparam>
    /// <returns>Failure result.</returns>
    private static UseCaseResult<T> Invalid<T>() => UseCaseResult<T>.Failure(
        "VALIDATION", "Vui lòng kiểm tra dữ liệu và xác nhận thao tác.");

    /// <summary>
    /// 404 NOT_FOUND.
    /// </summary>
    /// <typeparam name="T">Result payload type.</typeparam>
    /// <returns>Failure result.</returns>
    private static UseCaseResult<T> Missing<T>() => UseCaseResult<T>.Failure(
        "NOT_FOUND", "Không tìm thấy gói thuê bao.", "NotFound");
}
