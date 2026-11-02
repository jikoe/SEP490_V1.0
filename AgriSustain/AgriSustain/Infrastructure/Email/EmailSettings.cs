namespace AgriSustain.Infrastructure.Email;

// Product review link configuration only. SMTP credentials come from the shared GmailOptions.
public sealed class EmailSettings
{
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
}
