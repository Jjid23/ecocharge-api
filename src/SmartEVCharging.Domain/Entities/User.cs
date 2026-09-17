using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartEVCharging.Domain.Enums;

namespace SmartEVCharging.Domain.Entities;

public class User
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    /// <summary>Display name shown in the web frontend.</summary>
    [MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [Required]
    public Enums.UserRole Role { get; set; }

    /// <summary>
    /// Points balance used by the web frontend.
    /// 1 point = 6 seconds of charging (10 pts = 1 minute).
    /// Kept in sync with CreditsSeconds: CurrentPoints = CreditsSeconds / 6.
    /// </summary>
    public int CurrentPoints { get; set; } = 50; // 20 welcome pts + small bonus

    public bool IsActive { get; set; } = true;

    /// <summary>Charging credits in seconds earned by depositing plastic bottles.</summary>
    public int CreditsSeconds { get; set; } = 300;

    /// <summary>Cumulative number of plastic bottles deposited by this user.</summary>
    public int BottlesDepositedTotal { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    public virtual ICollection<ChargingSession> ChargingSessions { get; set; } = new List<ChargingSession>();
    public virtual ICollection<Device> Devices { get; set; } = new List<Device>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
