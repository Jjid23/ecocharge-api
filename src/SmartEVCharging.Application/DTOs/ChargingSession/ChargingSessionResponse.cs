namespace SmartEVCharging.Application.DTOs.ChargingSession;

public class ChargingSessionResponse
{
    public Guid Id { get; set; }
    public string PortName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    /// <summary>Seconds allocated for this session.</summary>
    public int AllocatedSeconds { get; set; }

    /// <summary>Remaining credit seconds on the user account after this session started.</summary>
    public int RemainingCreditsSeconds { get; set; }
}
