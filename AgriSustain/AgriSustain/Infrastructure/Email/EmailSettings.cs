namespace AgriSustain.Infrastructure.Email;

public sealed class EmailSettings
{
    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public string SmtpUser { get; set; } = "";
    public string SmtpPass { get; set; } = "";
    public string FromEmail { get; set; } = "";
    public bool EnableSsl { get; set; } = true;
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
}
