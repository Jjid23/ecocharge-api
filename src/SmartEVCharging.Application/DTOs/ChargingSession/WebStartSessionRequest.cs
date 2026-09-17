using System.ComponentModel.DataAnnotations;

namespace SmartEVCharging.Application.DTOs.ChargingSession;

/// <summary>
/// Session start request from the web frontend (uses portId string + durationMinutes).
/// </summary>
public class WebStartSessionRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string PortId { get; set; } = string.Empty;

    [Range(1, 120)]
    public int DurationMinutes { get; set; }
}
