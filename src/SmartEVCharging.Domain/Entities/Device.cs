using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartEVCharging.Domain.Enums;

namespace SmartEVCharging.Domain.Entities;

public class Device
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string SerialNumber { get; set; } = string.Empty;

    [Required]
    public Enums.DeviceType Type { get; set; }

    [Required]
    public Enums.DeviceStatus Status { get; set; } = Enums.DeviceStatus.Online;

    [MaxLength(500)]
    public string? Location { get; set; }

    [Required]
    public Guid OwnerId { get; set; }

    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(OwnerId))]
    public virtual User Owner { get; set; } = null!;

    public virtual ICollection<ChargingPort> ChargingPorts { get; set; } = new List<ChargingPort>();
}
