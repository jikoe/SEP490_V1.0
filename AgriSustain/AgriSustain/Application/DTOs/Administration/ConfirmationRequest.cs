namespace AgriSustain.Application.DTOs.Administration;

/// <summary>Version is the CategoryDto.Version shown when the action was confirmed.</summary>
public sealed record ConfirmationRequest(bool Confirmed, string? Version = null);
