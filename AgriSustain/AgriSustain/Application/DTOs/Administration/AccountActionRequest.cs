namespace AgriSustain.Application.DTOs.Administration;

public sealed record AccountActionRequest(string Action, string? Reason, bool Confirmed);
