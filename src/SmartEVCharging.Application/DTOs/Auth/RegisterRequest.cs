using System.ComponentModel.DataAnnotations;

namespace SmartEVCharging.Application.DTOs.Auth;

public class RegisterRequest
{
    /// <summary>Display name (web frontend uses fullName).</summary>
    [MaxLength(200)]
    public string? FullName { get; set; }

    [Required, MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }
}
