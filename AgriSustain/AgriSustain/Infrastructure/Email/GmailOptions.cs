namespace AgriSustain.Infrastructure.Email;

/// <summary>
/// Gmail Notification Service API (API-02) over Gmail SMTP. The sender is a Gmail account with
/// 2-Step Verification and an App Password; both come from .env, never from source control.
/// </summary>
public sealed class GmailOptions
{
    public const string SectionName = "Gmail";

    /// <summary>"Gmail" sends real e-mail; "Log" writes the OTP to the server log (local work only).</summary>
    public string Provider { get; set; } = "Gmail";

    public string Host { get; set; } = "smtp.gmail.com";

    public int Port { get; set; } = 587;

    /// <summary>The Gmail address that sends the e-mail and signs in to SMTP.</summary>
    public string? SenderAddress { get; set; }

    public string SenderName { get; set; } = "AgriSustain";

    /// <summary>16-character Google App Password (spaces are ignored).</summary>
    public string? AppPassword { get; set; }

    public int TimeoutSeconds { get; set; } = 15;

    public bool UsesGmail => string.Equals(Provider, "Gmail", StringComparison.OrdinalIgnoreCase);
}
