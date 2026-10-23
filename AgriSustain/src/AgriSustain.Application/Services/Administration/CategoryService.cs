using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Domain.Entities.Accounts;
using AgriSustain.Domain.Entities.Catalog;
using FluentValidation;

namespace AgriSustain.Application.Services.Administration;

/// <summary>
/// Coordinates UC-ADM-05 with the current actor, approved field validators and one
/// mutation-plus-audit transaction.
/// </summary>
public sealed class CategoryService(
    IThreeUcUnitOfWork unitOfWork, ICurrentUser currentUser, TimeProvider timeProvider,
    IValidator<CategoryQuery> queryValidator, IValidator<SaveCategoryRequest> saveValidator)
    : ICategoryService
{
    /// <summary>
    /// Validates paging/filter input and reads permitted category rows. Hidden categories remain
    /// queryable by the administrator; there is no archive or image field.
    /// </summary>
    public async Task<Result<PagedResponse<CategoryResponse>>> SearchAsync(
        CategoryQuery query, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(queryValidator, query, cancellationToken);
        if (error != null)
        {
            return Result<PagedResponse<CategoryResponse>>.Failure(error);
        }

        return await unitOfWork.ReadAsync(async session =>
        {
            var access = await AuthorizeAsync(session, cancellationToken);
            return access.Error != null
                ? Result<PagedResponse<CategoryResponse>>.Failure(access.Error)
                : Result<PagedResponse<CategoryResponse>>.Success(
                    await session.Categories.SearchAsync(query, cancellationToken));
        }, cancellationToken);
    }

    /// <summary>
    /// Validates approved category fields and the parent reference, then saves the category and
    /// audit together. Duplicate-name, cycle-write and Version rules were declined and are not
    /// silently restored.
    /// </summary>
    public async Task<Result<long>> SaveAsync(
        long? id, SaveCategoryRequest request, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(
            saveValidator, request, cancellationToken);
        if (error != null)
        {
            return Result<long>.Failure(error);
        }

        return await unitOfWork.ExecuteAsync(async session =>
        {
            var access = await AuthorizeAsync(session, cancellationToken);
            if (access.Error != null || access.Value == null)
            {
                return Result<long>.Failure(access.Error
                    ?? new Error("FORBIDDEN", "Không đủ quyền."));
            }

            Category? category = id == null ? null
                : await session.Categories.FindAsync(id.Value, cancellationToken);
            if (id != null && category == null)
            {
                return Missing();
            }

            if (category != null && category.IsActive != request.IsActive && !request.Confirmed)
            {
                return Result<long>.Failure(new Error(
                    "CONFIRMATION_REQUIRED", "Cần xác nhận thay đổi trạng thái danh mục."));
            }

            Error? parentError = await ValidateParentAsync(
                session, request.ParentId, cancellationToken);
            if (parentError != null)
            {
                return Result<long>.Failure(parentError);
            }

            // DB-02/03/04 were declined: no sibling uniqueness, tree-write guard or Version.
            string before = category == null ? "null" : Snapshot(category);
            category ??= new Category
            {
                Id = await session.Categories.NextIdAsync(cancellationToken)
            };
            category.Update(request.Name, request.ParentId, request.Description,
                request.Priority, request.IsActive);
            await session.Categories.SaveAsync(category, cancellationToken);
            await session.AddAuditAsync(new AuditRecord(access.Value.Id, "Category", category.Id,
                id == null ? "create" : "update", before, Snapshot(category), null,
                timeProvider.GetUtcNow()), cancellationToken);
            return Result<long>.Success(category.Id);
        }, cancellationToken);
    }

    /// <summary>
    /// Requires explicit confirmation. A delete removes only an unreferenced category; any
    /// product, child, benchmark or commission reference blocks it. Activate/deactivate use
    /// IsActive without archive metadata.
    /// </summary>
    public Task<Result<long>> ChangeAsync(
        long id, string action, CategoryActionRequest request,
        CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async session =>
        {
            var access = await AuthorizeAsync(session, cancellationToken);
            if (access.Error != null || access.Value == null)
            {
                return Result<long>.Failure(access.Error
                    ?? new Error("FORBIDDEN", "Không đủ quyền."));
            }

            Category? category = await session.Categories.FindAsync(id, cancellationToken);
            if (category == null)
            {
                return Missing();
            }

            if (!request.Confirmed || action is not ("activate" or "deactivate" or "delete"))
            {
                return Result<long>.Failure(new Error("INVALID_ACTION", "Cần xác nhận thao tác."));
            }

            if (action == "delete" && (await session.Categories.HasProductsAsync(id,
                cancellationToken) || await session.Categories.HasOtherReferencesAsync(id,
                cancellationToken)))
            {
                return Result<long>.Failure(new Error("CATEGORY_IN_USE",
                    "Danh mục còn sản phẩm hoặc liên kết khác; chỉ có thể ẩn/khóa.", "Conflict"));
            }

            if (action == "activate")
            {
                Error? parentError = await ValidateParentAsync(
                    session, category.ParentId, cancellationToken);
                if (parentError != null)
                {
                    return Result<long>.Failure(parentError);
                }
            }

            string before = Snapshot(category);
            if (action == "delete")
            {
                await session.Categories.DeleteAsync(id, cancellationToken);
            }
            else
            {
                category.SetActive(action == "activate");
                await session.Categories.SaveAsync(category, cancellationToken);
            }

            // The SQL adapter must keep FK checks, mutation and audit in one transaction.
            await session.AddAuditAsync(new AuditRecord(access.Value.Id, "Category", id, action,
                before, action == "delete" ? "null" : Snapshot(category), null,
                timeProvider.GetUtcNow()), cancellationToken);
            return Result<long>.Success(id);
        }, cancellationToken);

    /// <summary>
    /// Requires the active SystemAdmin fixture for this UC; the team must replace the role
    /// shortcut with the approved staff permission matrix before real authentication integration.
    /// </summary>
    private Task<Result<UserAccount>> AuthorizeAsync(
        IThreeUcSession session, CancellationToken cancellationToken) =>
        CurrentUserAccess.RequireAsync(session, currentUser, "SystemAdmin",
            timeProvider.GetUtcNow(), cancellationToken);

    /// <summary>
    /// Checks the selected parent exists and is active as required by the original form contract.
    /// This is a direct reference check, not the declined recursive cycle-write rule.
    /// </summary>
    private static async Task<Error?> ValidateParentAsync(
        IThreeUcSession session, long? parentId, CancellationToken cancellationToken)
    {
        if (parentId == null)
        {
            return null;
        }

        Category? parent = await session.Categories.FindAsync(parentId.Value, cancellationToken);
        return parent is { IsActive: true } ? null
            : new Error("INVALID_PARENT", "Danh mục cha không tồn tại hoặc đang ẩn/khóa.");
    }

    /// <summary>
    /// Builds the stable category-not-found application error, shared by save and status/delete
    /// actions.
    /// </summary>
    private static Result<long> Missing() => Result<long>.Failure(new Error(
        "CATEGORY_NOT_FOUND", "Không tìm thấy danh mục.", "NotFound"));

    /// <summary>
    /// Captures the persisted category fields before and after a mutation for audit. A physical
    /// delete records null as its new value, retaining the old target identity.
    /// </summary>
    private static string Snapshot(Category category) => JsonSerializer.Serialize(category);
}
