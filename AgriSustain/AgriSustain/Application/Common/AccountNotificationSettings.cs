namespace AgriSustain.Application.Common;

/// <summary>Support contact quoted in UC-ADM-03 account emails (BR-UM-11) and MSG-UM-11.</summary>
public sealed record AccountNotificationSettings(string? SupportEmail);
