using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartEVCharging.Domain.Entities;

/// <summary>
/// A relay command queued by the server (from a charging session start/stop or admin panel)
/// and consumed by the next ESP32-A poll.  Port number 1-4 maps to GPIO 25/26/16/5.
/// </summary>
public class RelayCommand
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid DeviceId { get; set; }

    /// <summary>1–4 matching charging port numbers.</summary>
    [Range(1, 4)]
    public int PortNumber { get; set; }

    /// <summary>True = activate relay (energise port), False = deactivate.</summary>
    public bool Activate { get; set; }

    /// <summary>Optional duration in seconds (0 = indefinite).</summary>
    public int DurationSeconds { get; set; }

    public bool Consumed { get; set; } = false;

    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
    public DateTime? ConsumedAt { get; set; }

    [ForeignKey(nameof(DeviceId))]
    public virtual Device Device { get; set; } = null!;
}
