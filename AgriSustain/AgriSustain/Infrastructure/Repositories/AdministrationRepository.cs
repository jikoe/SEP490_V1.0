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
        var rows = WithAccountStatus(query);
        if (!string.IsNullOrWhiteSpace(search.Status))
        {
            rows = rows.Where(x => x.AccountStatus == search.Status);
        }
        var total = await rows.CountAsync(cancellationToken);
        var items = await ProjectUsers(rows.OrderBy(x => x.User.UserId)
            .Skip((search.Page - 1) * search.PageSize).Take(search.PageSize), actorId, now)
            .ToListAsync(cancellationToken);
        return new Page<UserDto>(items, total, search.Page, search.PageSize, actorId);
    }

    public Task<UserDto?> GetUserAsync(long id, long actorId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        return ProjectUsers(WithAccountStatus(context.Users.AsNoTracking()
            .Where(x => x.UserId == id)), actorId, now).SingleOrDefaultAsync(cancellationToken);
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
            var keyword = search.Keyword.Trim();
            query = query.Where(x => x.CategoryName.Contains(keyword));
        }
        if (search.IsActive.HasValue)
        {
            query = query.Where(x => x.IsActive == search.IsActive);
        }
        var total = await query.CountAsync(cancellationToken);
        var items = await ProjectCategories(query.OrderBy(x => x.CategoryName).ThenBy(x => x.CategoryId)
            .Skip((search.Page - 1) * search.PageSize).Take(search.PageSize))
            .ToListAsync(cancellationToken);
        return new Page<CategoryDto>(items, total, search.Page, search.PageSize);
    }

    public Task<CategoryDto?> GetCategoryAsync(long id, CancellationToken cancellationToken)
    {
        return ProjectCategories(context.Categories.AsNoTracking().Where(x => x.CategoryId == id))
            .SingleOrDefaultAsync(cancellationToken);
    }

    /// <summary>Names are unique across all categories; case-insensitivity follows the CI collation.</summary>
    public Task<bool> CategoryNameExistsAsync(string name, long exceptId,
        CancellationToken cancellationToken)
    {
        return context.Categories.AnyAsync(x => x.CategoryId != exceptId && x.CategoryName == name,
            cancellationToken);
    }

    public Task<bool> HasActiveProductsAsync(long categoryId, CancellationToken cancellationToken)
    {
        return context.Products.AnyAsync(x => x.CategoryId == categoryId && x.IsPublished,
            cancellationToken);
    }

    public async Task<bool> HasCategoryReferencesAsync(long categoryId,
        CancellationToken cancellationToken)
    {
        // Legacy child rows may remain until Update_DB.sql flattens them; their FK still blocks deletion.
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

    /// <summary>
    /// Derives the SRS account status: Farmer/WholesaleBuyer stay Pending until KYC is Approved
    /// (Rejected when declined); staff and retail accounts are Active unless suspended.
    /// </summary>
    private static IQueryable<AccountRow> WithAccountStatus(IQueryable<User> query)
    {
        return query.Select(x => new AccountRow
        {
            User = x,
            AccountStatus = x.Status == "Suspended" ? "Suspended"
                : x.Role == "Farmer" ? (x.FarmerProfile == null ? "Pending"
                    : x.FarmerProfile.VerificationStatus == "Approved" ? "Active"
                    : x.FarmerProfile.VerificationStatus == "Rejected" ? "Rejected" : "Pending")
                : x.Role == "WholesaleBuyer" ? (x.WholesaleBuyerProfile == null ? "Pending"
                    : x.WholesaleBuyerProfile.VerificationStatus == "Approved" ? "Active"
                    : x.WholesaleBuyerProfile.VerificationStatus == "Rejected" ? "Rejected"
                    : "Pending")
                : "Active"
        });
    }

    private static IQueryable<UserDto> ProjectUsers(IQueryable<AccountRow> query, long actorId,
        DateTimeOffset now)
    {
        return query.Select(row => new UserDto(row.User.UserId, row.User.FullName,
            row.User.FarmerProfile != null ? row.User.FarmerProfile.FarmName
                : row.User.WholesaleBuyerProfile != null
                    ? row.User.WholesaleBuyerProfile.CompanyName : null,
            row.User.Email, row.User.PhoneNumber, row.User.Role, row.AccountStatus,
            row.User.Role == "Farmer" ? (row.User.FarmerProfile != null
                    ? row.User.FarmerProfile.VerificationStatus : "NotSubmitted")
                : row.User.Role == "WholesaleBuyer" ? (row.User.WholesaleBuyerProfile != null
                    ? row.User.WholesaleBuyerProfile.VerificationStatus : "NotSubmitted")
                : null,
            new DateTimeOffset(DateTime.SpecifyKind(row.User.CreatedAt, DateTimeKind.Utc)),
            row.User.LockedUntil > now.UtcDateTime,
            row.User.LockedUntil == null ? null : new DateTimeOffset(
                DateTime.SpecifyKind(row.User.LockedUntil.Value, DateTimeKind.Utc)),
            row.User.FailedLoginCount,
            // Mirrors User.CanBeSuspended so the rule stays translatable to SQL.
            row.User.Role == "Farmer" || row.User.Role == "WholesaleBuyer"
                || row.User.Role == "RetailCustomer",
            actorId));
    }

    private sealed class AccountRow
    {
        public User User { get; init; } = null!;

        public string AccountStatus { get; init; } = "";
    }

    private IQueryable<CategoryDto> ProjectCategories(IQueryable<Category> query)
    {
        return query.Select(x => new CategoryDto(x.CategoryId, x.CategoryName, x.Description,
            x.IsActive, x.Products.Count,
            context.CategorySettings.Any(s => s.CategoryId == x.CategoryId && s.IsArchived)));
    }
}
