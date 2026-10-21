using System.ComponentModel.DataAnnotations;

namespace AgriSustain.Application.DTOs.Auth;

public sealed class LoginRequestDto
{
    [Required]
    [StringLength(255)]
    public string Identifier { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Password { get; set; } = string.Empty;
}
