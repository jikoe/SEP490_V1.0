namespace AgriSustain.Infrastructure.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "JWT";

    public string ValidIssuer { get; init; } = string.Empty;

    public string ValidAudience { get; init; } = string.Empty;

    public string Secret { get; init; } = string.Empty;
}
