using System;
using System.ComponentModel.DataAnnotations;

namespace SmartEVCharging.Domain.Entities;

/// <summary>
/// Tracks the plastic-bottle collection bin fill level reported by the
/// ESP32 ultrasonic sensor attached to the kiosk.
/// </summary>
public class BinStatus
{
    [Key]
    public int Id { get; set; } = 1; // singleton row

    /// <summary>0–100 fill percentage.</summary>
    public int FillPercentage { get; set; } = 0;

    /// <summary>All-time total bottles collected into this bin.</summary>
    public int TotalBottlesCollected { get; set; } = 0;

    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
