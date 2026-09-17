using System.ComponentModel.DataAnnotations;

namespace SmartEVCharging.Application.DTOs.Device;

/// <summary>Queues a relay command from the admin panel or charging session logic.</summary>
public class RelayCommandRequest
{
    /// <summary>1–4 matching port numbers.</summary>
    [Range(1, 4)]
    public int PortNumber { get; set; }

    public bool Activate { get; set; }

    /// <summary>0 = indefinite (until explicit deactivate).</summary>
    public int DurationSeconds { get; set; } = 0;
}
