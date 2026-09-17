using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartEVCharging.Domain.Enums;

namespace SmartEVCharging.Domain.Entities;

public class ChargingSession
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    public Guid ChargingPortId { get; set; }

    [Required]
    public Enums.ChargingSessionStatus Status { get; set; } = Enums.ChargingSessionStatus.InProgress;

    public DateTime? StartedAt { get; set; }

    public DateTime? EndedAt { get; set; }

    public decimal EnergyDeliveredKwh { get; set; }

    public decimal Cost { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public virtual User User { get; set; } = null!;

    [ForeignKey(nameof(ChargingPortId))]
    public virtual ChargingPort ChargingPort { get; set; } = null!;
}
