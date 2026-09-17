using System.ComponentModel.DataAnnotations;

namespace SmartEVCharging.Application.DTOs.User;

public class UpdateProfileRequest
{
    [MaxLength(200)]
    public string? FullName { get; set; }

    [MaxLength(100)]
    public string? Username { get; set; }

    [MaxLength(100)]
    public string? Email { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    /// <summary>Required when changing password.</summary>
    public string? CurrentPassword { get; set; }

    [MinLength(6)]
    public string? NewPassword { get; set; }
}
