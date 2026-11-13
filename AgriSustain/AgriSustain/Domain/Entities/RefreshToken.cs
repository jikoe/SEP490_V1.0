using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriSustain.Domain.Entities;

[Table("RefreshToken")]
public sealed class RefreshToken
{
    [Key]
    public long RefreshTokenId { get; set; }

    public long UserId { get; set; }

    public User User { get; set; } = null!;

    [StringLength(128)]
    public string TokenHash { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [StringLength(128)]
    public string? ReplacedByHash { get; set; }

    [NotMapped]
    public bool IsActive => RevokedAt is null && ExpiresAt > DateTimeOffset.UtcNow;
}

