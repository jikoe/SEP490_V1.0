using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces;
using AgriSustain.Application.Validators.Administration;
using AgriSustain.Domain.Entities;

namespace AgriSustain.Application.Services.Administration;

public sealed class AdministrationService(
    IAdministrationRepository repository,
    IUseCaseUnitOfWork unitOfWork,
    IActorContext actorContext,
    TimeProvider clock) : IAdministrationService
{
    public async Task<bool> IsAdministratorAsync(CancellationToken cancellationToken)
    {
        var actor = await repository.FindActorAsync(actorContext.UserId, cancellationToken);
        return actor is not null && actor.Status == "Active"
            && !actor.IsTemporarilyLocked(clock.GetUtcNow())
            && actor.Role is "SystemAdmin" or "Administrator" or "Admin";
    }

    public async Task<UseCaseResult<Page<UserDto>>> SearchUsersAsync(UserSearch search,
        CancellationToken cancellationToken)
    {
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<Page<UserDto>>();
        }
        if (!ValidPage(search.Page, search.PageSize) || search.Keyword?.Length > 100)
        {
            return Invalid<Page<UserDto>>();
        }
        return UseCaseResult<Page<UserDto>>.Success(await repository.SearchUsersAsync(
            search, actorContext.UserId, clock.GetUtcNow(), cancellationToken));
    }

    public async Task<UseCaseResult<UserDto>> GetUserAsync(long id, CancellationToken cancellationToken)
    {
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<UserDto>();
        }
        var user = await repository.GetUserAsync(
            id, actorContext.UserId, clock.GetUtcNow(), cancellationToken);
        return user is null ? Missing<UserDto>() : UseCaseResult<UserDto>.Success(user);
    }

    public async Task<UseCaseResult<UserDto>> ChangeAccountAsync(long id, AccountActionRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await new AccountActionValidator()
            .ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return UseCaseResult<UserDto>.Validation(validation.Errors.GroupBy(x => x.PropertyName)
                .ToDictionary(x => x.Key, x => x.Select(error => error.ErrorMessage).ToArray()));
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<UserDto>();
        }
        var actor = await repository.FindActorAsync(actorContext.UserId, cancellationToken);
        var user = await repository.FindUserAsync(id, cancellationToken);
        if (user is null)
        {
            return Missing<UserDto>();
        }
        if (!user.CanBeManagedBy(actor!))
        {
            return UseCaseResult<UserDto>.Failure("SELF_ACTION", "Không thể thao tác tài khoản của mình.",
                "Forbidden");
        }
        var eligible = request.Action switch
        {
            "suspend" => user.Status == "Active",
            "activate" => user.Status == "Suspended",
            "unlock" => user.IsTemporarilyLocked(clock.GetUtcNow()),
            _ => false
        };
        if (!eligible)
        {
            return UseCaseResult<UserDto>.Failure("STATE_CONFLICT", "Trạng thái tài khoản đã thay đổi.",
                "Conflict");
        }
        var before = AccountSnapshot(user);
        switch (request.Action)
        {
            case "suspend": user.Suspend(request.Reason!); break;
            case "activate": user.Activate(request.Reason!); break;
            case "unlock": user.Unlock(); break;
        }
        Audit("User", id, request.Action, before, AccountSnapshot(user), request.Reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetUserAsync(id, cancellationToken);
    }

    public async Task<UseCaseResult<Page<CategoryDto>>> SearchCategoriesAsync(CategorySearch search,
        CancellationToken cancellationToken)
    {
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<Page<CategoryDto>>();
        }
        if (!ValidPage(search.Page, search.PageSize) || search.Keyword?.Length > 100)
        {
            return Invalid<Page<CategoryDto>>();
        }
        return UseCaseResult<Page<CategoryDto>>.Success(
            await repository.SearchCategoriesAsync(search, cancellationToken));
    }

    public async Task<UseCaseResult<CategoryDto>> GetCategoryAsync(long id,
        CancellationToken cancellationToken)
    {
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<CategoryDto>();
        }
        var category = await repository.GetCategoryAsync(id, cancellationToken);
        return category is null ? Missing<CategoryDto>() : UseCaseResult<CategoryDto>.Success(category);
    }

    public async Task<UseCaseResult<CategoryDto>> SaveCategoryAsync(long? id, CategoryRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await new CategoryValidator().ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return UseCaseResult<CategoryDto>.Validation(validation.Errors.GroupBy(x => x.PropertyName)
                .ToDictionary(x => x.Key, x => x.Select(error => error.ErrorMessage).ToArray()));
        }
        var image = DecodeImage(request);
        if (!image.IsSuccess)
        {
            return UseCaseResult<CategoryDto>.Failure("INVALID_IMAGE", image.Error!.Message);
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<CategoryDto>();
        }
        var category = id.HasValue
            ? await repository.FindCategoryAsync(id.Value, cancellationToken) : new Category();
        if (category is null)
        {
            return Missing<CategoryDto>();
        }
        var settings = id.HasValue
            ? await repository.GetCategorySettingsAsync(id.Value, cancellationToken)
            : new CategorySettings();
        if (settings.IsArchived)
        {
            return UseCaseResult<CategoryDto>.Failure("ARCHIVED", "Danh mục đã lưu trữ.", "Conflict");
        }
        if (id.HasValue && category.IsActive != request.IsActive && !request.Confirmed)
        {
            return Invalid<CategoryDto>();
        }
        if (await repository.CategoryNameExistsAsync(request.Name.Trim(), request.ParentId,
            id ?? 0, cancellationToken))
        {
            return UseCaseResult<CategoryDto>.Failure("CATEGORY_DUPLICATE",
                "Tên danh mục đã tồn tại trong cùng danh mục cha.", "Conflict");
        }
        var parentId = request.ParentId;
        var visited = new HashSet<long> { id ?? 0 };
        while (parentId.HasValue)
        {
            if (!visited.Add(parentId.Value))
            {
                return UseCaseResult<CategoryDto>.Failure("CATEGORY_CYCLE", "Danh mục cha tạo vòng lặp.");
            }
            var parent = await repository.FindCategoryAsync(parentId.Value, cancellationToken);
            if (parent is null || !parent.IsActive
                || (await repository.GetCategorySettingsAsync(parentId.Value,
                    cancellationToken)).IsArchived)
            {
                return UseCaseResult<CategoryDto>.Failure("CATEGORY_PARENT", "Danh mục cha không hợp lệ.");
            }
            parentId = parent.ParentCategoryId;
        }
        var before = CategorySnapshot(category, settings);
        category.UpdateDetails(
            request.Name, request.ParentId, request.Description, request.Priority);
        category.SetAvailability(request.IsActive);
        if (request.ImageBase64 is not null || request.RemoveImage)
        {
            settings.SetImage(image.Value, image.Value is null ? null : request.ImageContentType);
        }
        if (!id.HasValue)
        {
            repository.AddCategory(category, settings);
        }
        // Identity allocation and its audit are committed by the same explicit transaction.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        Audit("Category", category.CategoryId, id.HasValue ? "Update" : "Create", before,
            CategorySnapshot(category, settings), null);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetCategoryAsync(category.CategoryId, cancellationToken);
    }

    public async Task<UseCaseResult<bool>> ChangeCategoryAsync(long id, string action, bool confirmed,
        CancellationToken cancellationToken)
    {
        if (!confirmed || action is not ("activate" or "deactivate" or "delete" or "archive"))
        {
            return Invalid<bool>();
        }
        await using var transaction = await unitOfWork.BeginAsync(cancellationToken);
        if (!await IsAdministratorAsync(cancellationToken))
        {
            return Denied<bool>();
        }
        var category = await repository.FindCategoryAsync(id, cancellationToken);
        if (category is null)
        {
            return Missing<bool>();
        }
        var settings = await repository.GetCategorySettingsAsync(id, cancellationToken);
        if (settings.IsArchived)
        {
            return UseCaseResult<bool>.Failure("ARCHIVED", "Danh mục đã lưu trữ.", "Conflict");
        }
        var before = CategorySnapshot(category, settings);
        if (action is "delete" or "archive")
        {
            if (await repository.HasActiveProductsAsync(id, cancellationToken))
            {
                return UseCaseResult<bool>.Failure("CATEGORY_IN_USE",
                    "Danh mục còn sản phẩm đang hoạt động; hãy chuyển sản phẩm trước.", "Conflict");
            }
            if (action == "archive"
                || await repository.HasCategoryReferencesAsync(id, cancellationToken))
            {
                category.SetAvailability(false);
                settings.Archive();
                action = "archive";
            }
            else
            {
                repository.DeleteCategory(category, settings);
            }
        }
        else
        {
            category.SetAvailability(action == "activate");
        }
        Audit("Category", id, action, before, CategorySnapshot(category, settings), null);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return UseCaseResult<bool>.Success(true);
    }

    private void Audit(string type, long id, string action, string before, string after,
        string? reason)
    {
        repository.AddAudit(new AuditLog
        {
            EntityType = type, EntityId = id, Action = action,
            OldValue = before, NewValue = after, Reason = reason,
            PerformedByUserId = actorContext.UserId, OccurredAt = clock.GetUtcNow().UtcDateTime,
            TraceId = actorContext.TraceId
        });
    }

    private static string AccountSnapshot(User user) => JsonSerializer.Serialize(new
    {
        user.Status, user.LockedUntil, user.FailedLoginCount
    });

    private static string CategorySnapshot(Category category, CategorySettings settings)
    {
        return JsonSerializer.Serialize(new
        {
            category.CategoryName, category.ParentCategoryId, category.Description,
            category.Priority, category.IsActive, settings.IsArchived,
            HasImage = settings.ImageBytes is not null
        });
    }

    private static UseCaseResult<byte[]?> DecodeImage(CategoryRequest request)
    {
        if (request.ImageBase64 is null)
        {
            return UseCaseResult<byte[]?>.Success(null);
        }
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(request.ImageBase64);
        }
        catch (FormatException)
        {
            return UseCaseResult<byte[]?>.Failure("INVALID_IMAGE", "Ảnh không đúng định dạng base64.");
        }
        var png = bytes.Length >= 24
            && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = bytes.Length >= 4 && bytes[0] == 255 && bytes[1] == 216
            && bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217;
        if (bytes.Length > 5 * 1024 * 1024
            || !(png && request.ImageContentType == "image/png"
                || jpeg && request.ImageContentType == "image/jpeg"))
        {
            return UseCaseResult<byte[]?>.Failure("INVALID_IMAGE", "Chỉ nhận PNG/JPEG tối đa 5 MB.");
        }
        return UseCaseResult<byte[]?>.Success(bytes);
    }

    private static bool ValidPage(int page, int size) => page > 0 && size is > 0 and <= 100;

    private static UseCaseResult<T> Denied<T>() => UseCaseResult<T>.Failure(
        "FORBIDDEN", "Bạn không có quyền thực hiện thao tác này.", "Forbidden");

    private static UseCaseResult<T> Invalid<T>() => UseCaseResult<T>.Failure(
        "VALIDATION", "Vui lòng kiểm tra dữ liệu và xác nhận thao tác.");

    private static UseCaseResult<T> Missing<T>() => UseCaseResult<T>.Failure(
        "NOT_FOUND", "Không tìm thấy bản ghi.", "NotFound");
}
