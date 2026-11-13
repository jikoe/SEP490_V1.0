namespace AgriSustain.Infrastructure.Email;

// Base address for links in notification emails. SMTP credentials come from GmailOptions.
public sealed class EmailSettings
{
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
}
