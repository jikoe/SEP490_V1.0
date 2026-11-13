using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AgriSustain.Domain.Entities;

[Table("RevokedTokens")]
public sealed class RevokedToken
{
    [Key]
    [StringLength(64)]
    public string TokenId { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset RevokedAt { get; set; } = DateTimeOffset.UtcNow;
}

