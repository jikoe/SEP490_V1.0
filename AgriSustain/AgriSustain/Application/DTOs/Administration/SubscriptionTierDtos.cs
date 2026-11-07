namespace AgriSustain.Application.DTOs.Administration;

/// <summary>UC-ADM-09 list filters. Status: empty (all), Active or Inactive.</summary>
public sealed record SubscriptionTierSearch(string? Keyword = null, string? Status = null, int Page = 1,
    int PageSize = 10);

/// <summary>
/// ActiveSubscriptions counts current subscribers; Purchased is true when any subscription, current
/// or past, used the tier (BR-ST-04).
/// </summary>
public sealed record SubscriptionTierDto(
    long Id,
    string Name,
    string? Description,
    int DurationDays,
    decimal Price,
    bool UnlocksFarmerDirectContact,
    bool CanDownloadContractTemplate,
    int? TransactionLimit,
    bool IsActive,
    int ActiveSubscriptions,
    bool Purchased,
    string Version);

/// <summary>
/// Version is the SubscriptionTierDto.Version the form was opened with; required when editing.
/// Confirmed is required when an edit also changes the status (MSG-ST-03).
/// </summary>
public sealed record SubscriptionTierRequest(
    string Name,
    string? Description,
    int DurationDays,
    decimal Price,
    bool UnlocksFarmerDirectContact,
    bool CanDownloadContractTemplate,
    int? TransactionLimit,
    bool IsActive = true,
    bool Confirmed = false,
    string? Version = null);

public sealed record SubscriptionTierChangeRequest(bool Confirmed, string? Version);

public sealed record SubscriptionTierResultDto(string Message, SubscriptionTierDto? Tier);
