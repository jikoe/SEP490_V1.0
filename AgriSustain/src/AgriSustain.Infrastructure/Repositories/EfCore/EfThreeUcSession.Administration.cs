using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Domain.Entities.Accounts;
using AgriSustain.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using Db = AgriSustain.Infrastructure.Data.Entities;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

internal sealed partial class EfThreeUcSession
{
    private sealed record AccountData(long Id, string Name, string Email, string? Phone,
        string Role, string Status, DateTime CreatedAt, DateTime? LockedUntil, int Failures,
        string? OrganizationName, string? VerificationStatus);

    /// <summary>Projects only approved account/profile fields; PasswordHash is never selected.</summary>
    private static IQueryable<AccountData> ProjectAccounts(IQueryable<Db.User> query) =>
        query.Select(user => new AccountData(user.UserId, user.FullName, user.Email,
            user.PhoneNumber, user.Role, user.Status, user.CreatedAt, user.LockedUntil,
            user.FailedLoginCount, user.FarmerProfile != null ? user.FarmerProfile.FarmName
                : user.WholesaleBuyerProfile != null ? user.WholesaleBuyerProfile.CompanyName : null,
            user.FarmerProfile != null ? user.FarmerProfile.VerificationStatus
                : user.WholesaleBuyerProfile != null
                    ? user.WholesaleBuyerProfile.VerificationStatus : null));

    /// <summary>Restores persisted status and the separate process-local session-revocation mock.</summary>
    private UserAccount Account(AccountData row) => UserAccount.RestorePersisted(new UserAccount
    {
        Id = row.Id, Name = row.Name, Email = row.Email, Phone = row.Phone, Role = row.Role,
        CreatedAt = Utc(row.CreatedAt), OrganizationName = row.OrganizationName,
        VerificationStatus = row.VerificationStatus
    }, row.Status, row.LockedUntil is { } until ? Utc(until) : null,
        row.Failures, SessionVersions.GetValueOrDefault(row.Id));

    /// <summary>Reads the current account within the shared transaction without loading credentials.</summary>
    async Task<UserAccount?> IAccountRepository.FindAsync(long id, CancellationToken cancellationToken)
    {
        var row = await ProjectAccounts(_db.User.AsNoTracking().Where(user => user.UserId == id))
            .SingleOrDefaultAsync(cancellationToken);
        return row == null ? null : Account(row);
    }

    /// <summary>Filters, counts and pages users in SQL using EF LINQ; KYC/profile data is read-only.</summary>
    public async Task<PagedResponse<UserResponse>> SearchAsync(UserQuery query,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var users = _db.User.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            users = users.Where(user => user.Role == query.Role);
        }
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            users = users.Where(user => user.Status == query.Status);
        }
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            string keyword = query.Keyword.Trim();
            users = users.Where(user => user.UserId.ToString().Contains(keyword)
                || user.FullName.Contains(keyword) || user.Email.Contains(keyword)
                || user.PhoneNumber != null && user.PhoneNumber.Contains(keyword)
                || user.FarmerProfile != null && user.FarmerProfile.FarmName.Contains(keyword)
                || user.WholesaleBuyerProfile != null
                    && user.WholesaleBuyerProfile.CompanyName.Contains(keyword));
        }
        int total = await users.CountAsync(cancellationToken);
        var rows = await ProjectAccounts(users.OrderBy(user => user.UserId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize))
            .ToListAsync(cancellationToken);
        return new PagedResponse<UserResponse>(rows.Select(row =>
            Application.Mappings.UserMappings.ToResponse(Account(row), now)).ToArray(),
            total, query.Page, query.PageSize);
    }

    /// <summary>Changes only status/lock fields owned by ADM-03; the UoW saves them with audit.</summary>
    public async Task SaveAsync(UserAccount account, CancellationToken cancellationToken)
    {
        var row = await _db.User.FindAsync([account.Id], cancellationToken)
            ?? throw new InvalidOperationException("Account disappeared inside transaction.");
        row.Status = account.Status;
        row.LockedUntil = account.LockedUntil?.UtcDateTime;
        row.FailedLoginCount = account.FailedLoginCount;
        SessionVersions[account.Id] = account.SessionVersion;
    }

    /// <summary>Maps the existing category fields without inventing image/archive/Version columns.</summary>
    private static Category Category(Db.Category row)
    {
        var category = new Category { Id = row.CategoryId };
        category.Update(row.CategoryName, row.ParentCategoryId, row.Description,
            row.Priority, row.IsActive);
        return category;
    }

    /// <summary>Reads one category for the existing parent/reference business validation.</summary>
    async Task<Category?> ICategoryRepository.FindAsync(long id, CancellationToken cancellationToken)
    {
        var row = await _db.Category.AsNoTracking()
            .SingleOrDefaultAsync(category => category.CategoryId == id, cancellationToken);
        return row == null ? null : Category(row);
    }

    /// <summary>Pages category DTOs and counts every product reference, including unpublished products.</summary>
    public async Task<PagedResponse<CategoryResponse>> SearchAsync(
        CategoryQuery query, CancellationToken cancellationToken)
    {
        var categories = _db.Category.AsNoTracking();
        if (query.Id != null)
        {
            categories = categories.Where(row => row.CategoryId == query.Id);
        }
        if (query.IsActive != null)
        {
            categories = categories.Where(row => row.IsActive == query.IsActive);
        }
        if (query.ParentId != null)
        {
            categories = categories.Where(row => row.ParentCategoryId == query.ParentId);
        }
        if (query.RootOnly)
        {
            categories = categories.Where(row => row.ParentCategoryId == null);
        }
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            string keyword = query.Keyword.Trim();
            categories = categories.Where(row => row.CategoryName.Contains(keyword));
        }
        int total = await categories.CountAsync(cancellationToken);
        var rows = await categories.OrderBy(row => row.Priority).ThenBy(row => row.CategoryId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(row => new CategoryResponse(row.CategoryId, row.CategoryName,
                row.ParentCategoryId, row.ParentCategory != null ? row.ParentCategory.CategoryName : null,
                row.Description, row.Priority, row.IsActive, row.Product.Count))
            .ToListAsync(cancellationToken);
        return new PagedResponse<CategoryResponse>(rows, total, query.Page, query.PageSize);
    }

    /// <summary>Checks all products; unpublished/unapproved references still prevent deletion.</summary>
    public Task<bool> HasProductsAsync(long id, CancellationToken cancellationToken) =>
        _db.Product.AnyAsync(row => row.CategoryId == id, cancellationToken);

    /// <summary>Checks child, regional-price and commission references while SQL FKs remain enabled.</summary>
    public async Task<bool> HasOtherReferencesAsync(long id, CancellationToken cancellationToken) =>
        await _db.Category.AnyAsync(row => row.ParentCategoryId == id, cancellationToken)
        || await _db.RegionalPriceBenchmark.AnyAsync(row => row.CategoryId == id, cancellationToken)
        || await _db.CommissionPolicy.AnyAsync(row => row.CategoryId == id, cancellationToken);

    /// <summary>Stages physical deletion; failed audit/save rolls it back with the rest of the UoW.</summary>
    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        var row = await _db.Category.FindAsync([id], cancellationToken)
            ?? throw new InvalidOperationException("Category disappeared inside transaction.");
        _db.Category.Remove(row);
    }

    /// <summary>
    /// Obtains the real IDENTITY for the existing NextId contract using an uncommitted EF insert.
    /// SaveChanges here does not commit; the final category/audit must succeed in the same UoW.
    /// </summary>
    public async Task<long> NextIdAsync(CancellationToken cancellationToken)
    {
        var row = new Db.Category { CategoryName = "__UNCOMMITTED_CATEGORY__", IsActive = true };
        _db.Category.Add(row);
        await _db.SaveChangesAsync(cancellationToken);
        return row.CategoryId;
    }

    /// <summary>Stages approved category fields for an insert/update; the UoW performs final SaveChanges.</summary>
    public async Task SaveAsync(Category category, CancellationToken cancellationToken)
    {
        var row = await _db.Category.FindAsync([category.Id], cancellationToken)
            ?? throw new InvalidOperationException("Category disappeared inside transaction.");
        row.CategoryName = category.Name;
        row.ParentCategoryId = category.ParentId;
        row.Description = category.Description;
        row.Priority = category.Priority;
        row.IsActive = category.IsActive;
    }
}
