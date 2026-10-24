using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces;
using AgriSustain.Domain.Entities;
using AgriSustain.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public sealed class AdministrationRepository(AgriSustainContext context) : IAdministrationRepository
{
    public Task<User?> FindUserAsync(long id, CancellationToken cancellationToken)
    {
        return context.Users.SingleOrDefaultAsync(x => x.UserId == id, cancellationToken);
    }

    public Task<User?> FindActorAsync(long id, CancellationToken cancellationToken)
    {
        return context.Users.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == id,
            cancellationToken);
    }

    public async Task<Page<UserDto>> SearchUsersAsync(UserSearch search, long actorId,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var query = context.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            var keyword = search.Keyword.Trim();
            query = query.Where(x => x.UserId.ToString() == keyword || x.FullName.Contains(keyword)
                || x.Email.Contains(keyword)
                || x.PhoneNumber != null && x.PhoneNumber.Contains(keyword)
                || x.FarmerProfile != null && x.FarmerProfile.FarmName.Contains(keyword)
                || x.WholesaleBuyerProfile != null
                    && x.WholesaleBuyerProfile.CompanyName.Contains(keyword));
        }
        if (!string.IsNullOrWhiteSpace(search.Role))
        {
            query = query.Where(x => x.Role == search.Role);
        }
        if (!string.IsNullOrWhiteSpace(search.Status))
        {
            query = query.Where(x => x.Status == search.Status);
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await ProjectUsers(query.OrderBy(x => x.UserId)
            .Skip((search.Page - 1) * search.PageSize).Take(search.PageSize), actorId, now)
            .ToListAsync(cancellationToken);
        return new Page<UserDto>(items, total, search.Page, search.PageSize, actorId);
    }

    public Task<UserDto?> GetUserAsync(long id, long actorId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return ProjectUsers(context.Users.AsNoTracking().Where(x => x.UserId == id), actorId, now)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<Category?> FindCategoryAsync(long id, CancellationToken cancellationToken)
    {
        return context.Categories.SingleOrDefaultAsync(x => x.CategoryId == id, cancellationToken);
    }

    public async Task<CategorySettings> GetCategorySettingsAsync(long id,
        CancellationToken cancellationToken)
    {
        var settings = await context.CategorySettings.FindAsync([id], cancellationToken);
        if (settings is null)
        {
            settings = new CategorySettings { CategoryId = id };
            context.CategorySettings.Add(settings);
        }
        return settings;
    }

    public async Task<Page<CategoryDto>> SearchCategoriesAsync(CategorySearch search,
        CancellationToken cancellationToken)
    {
        var query = context.Categories.AsNoTracking().Where(x => !context.CategorySettings
            .Any(settings => settings.CategoryId == x.CategoryId && settings.IsArchived));
        if (!string.IsNullOrWhiteSpace(search.Keyword))
        {
            query = query.Where(x => x.CategoryName.Contains(search.Keyword));
        }
        if (search.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == search.IsActive);
        }
        if (search.RootOnly)
        {
            query = query.Where(x => x.ParentCategoryId == null);
        }
        else if (search.ParentId.HasValue)
        {
            query = query.Where(x => x.ParentCategoryId == search.ParentId);
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await ProjectCategories(query.OrderBy(x => x.Priority).ThenBy(x => x.CategoryId)
            .Skip((search.Page - 1) * search.PageSize).Take(search.PageSize))
            .ToListAsync(cancellationToken);
        return new Page<CategoryDto>(items, total, search.Page, search.PageSize);
    }

    public async Task<CategoryDto?> GetCategoryAsync(long id, CancellationToken cancellationToken)
    {
        var category = await ProjectCategories(context.Categories.AsNoTracking()
            .Where(x => x.CategoryId == id)).SingleOrDefaultAsync(cancellationToken);
        if (category is null)
        {
            return null;
        }
        var media = await context.CategorySettings.AsNoTracking()
            .Where(x => x.CategoryId == id).Select(x => new { x.ImageBytes, x.ImageContentType })
            .SingleOrDefaultAsync(cancellationToken);
        return category with
        {
            ImageBase64 = media?.ImageBytes is null ? null
                : Convert.ToBase64String(media.ImageBytes),
            ImageContentType = media?.ImageContentType
        };
    }

    public Task<bool> CategoryNameExistsAsync(string name, long? parentId, long exceptId,
        CancellationToken cancellationToken)
    {
        return context.Categories.AnyAsync(x => x.CategoryId != exceptId
            && x.ParentCategoryId == parentId && x.CategoryName == name, cancellationToken);
    }

    public Task<bool> HasActiveProductsAsync(long categoryId, CancellationToken cancellationToken)
    {
        return context.Products.AnyAsync(x => x.CategoryId == categoryId && x.IsPublished,
            cancellationToken);
    }

    public async Task<bool> HasCategoryReferencesAsync(long categoryId,
        CancellationToken cancellationToken)
    {
        return await context.Products.AnyAsync(x => x.CategoryId == categoryId, cancellationToken)
            || await context.Categories.AnyAsync(x => x.ParentCategoryId == categoryId,
                cancellationToken)
            || await context.CommissionPolicies.AnyAsync(x => x.CategoryId == categoryId,
                cancellationToken)
            || await context.RegionalPriceBenchmarks.AnyAsync(x => x.CategoryId == categoryId,
                cancellationToken);
    }

    public void AddCategory(Category category, CategorySettings settings)
    {
        settings.Category = category;
        context.CategorySettings.Add(settings);
    }

    public void DeleteCategory(Category category, CategorySettings settings)
    {
        context.CategorySettings.Remove(settings);
        context.Categories.Remove(category);
    }

    public void AddAudit(AuditLog audit) => context.AuditLogs.Add(audit);

    private static IQueryable<UserDto> ProjectUsers(IQueryable<User> query, long actorId,
        DateTimeOffset now)
    {
        return query.Select(x => new UserDto(x.UserId, x.FullName,
            x.FarmerProfile != null ? x.FarmerProfile.FarmName
                : x.WholesaleBuyerProfile != null ? x.WholesaleBuyerProfile.CompanyName : null,
            x.Email, x.PhoneNumber, x.Role, x.Status,
            x.FarmerProfile != null ? x.FarmerProfile.VerificationStatus
                : x.WholesaleBuyerProfile != null ? x.WholesaleBuyerProfile.VerificationStatus
                : x.IsEmailVerified ? "Verified" : "NotVerified",
            new DateTimeOffset(DateTime.SpecifyKind(x.CreatedAt, DateTimeKind.Utc)),
            x.LockedUntil > now.UtcDateTime,
            x.LockedUntil == null ? null : new DateTimeOffset(
                DateTime.SpecifyKind(x.LockedUntil.Value, DateTimeKind.Utc)),
            x.FailedLoginCount, actorId));
    }

    private IQueryable<CategoryDto> ProjectCategories(IQueryable<Category> query)
    {
        return query.Select(x => new CategoryDto(x.CategoryId, x.CategoryName, x.ParentCategoryId,
            x.ParentCategory == null ? null : x.ParentCategory.CategoryName, x.Description,
            x.Priority, x.IsActive, x.Products.Count,
            context.CategorySettings.Any(s => s.CategoryId == x.CategoryId && s.IsArchived),
            null, null));
    }
}
