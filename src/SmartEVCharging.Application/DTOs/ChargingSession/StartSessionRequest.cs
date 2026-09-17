using System.ComponentModel.DataAnnotations;

namespace SmartEVCharging.Application.DTOs.ChargingSession;

public class StartSessionRequest
{
    [Required]
    public Guid ChargingPortId { get; set; }

    /// <summary>Credits (seconds) the user wants to spend on this session.</summary>
    [Range(1, int.MaxValue)]
    public int RequestedSeconds { get; set; }

    /// <summary>Bottles deposited in this visit (0 if using existing credit).</summary>
    [Range(0, 100)]
    public int BottlesDeposited { get; set; } = 0;
}
