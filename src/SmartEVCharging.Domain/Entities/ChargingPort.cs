using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SmartEVCharging.Domain.Enums;

namespace SmartEVCharging.Domain.Entities;

public class ChargingPort
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string PortName { get; set; } = string.Empty;

    /// <summary>Display number shown in the web and Android UI (e.g. 1, 2, 3…).</summary>
    public int PortNumber { get; set; } = 0;

    /// <summary>Connector type description shown in the web UI.</summary>
    [MaxLength(100)]
    public string ConnectorType { get; set; } = string.Empty;

    public DateTime? UpdatedAt { get; set; }

    [Required]
    public Enums.ChargingPortStatus Status { get; set; } = Enums.ChargingPortStatus.Available;

    [Required]
    public decimal MaxPowerKw { get; set; }

    public decimal? CurrentPowerKw { get; set; }

    [Required]
    public Enums.ChargingPortType Type { get; set; }

    /// <summary>Optional link to the IoT device managing this port. Null for manually configured ports.</summary>
    public Guid? DeviceId { get; set; }

    // Navigation properties
    [ForeignKey(nameof(DeviceId))]
    public virtual Device? Device { get; set; }

    public virtual ICollection<ChargingSession> ChargingSessions { get; set; } = new List<ChargingSession>();
}
