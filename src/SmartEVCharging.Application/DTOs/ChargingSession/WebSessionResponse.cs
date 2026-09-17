namespace SmartEVCharging.Application.DTOs.ChargingSession;

/// <summary>
/// Charging session shape expected by the web frontend.
/// </summary>
public class WebSessionResponse
{
    public string  SessionId        { get; set; } = string.Empty;
    public string  UserId           { get; set; } = string.Empty;
    public string  UserFullName     { get; set; } = string.Empty;
    public string  PortId           { get; set; } = string.Empty;
    public int     PortNumber       { get; set; }
    public string  ConnectorType    { get; set; } = string.Empty;
    public int     DurationMinutes  { get; set; }
    public int     PointsUsed       { get; set; }
    public string  StartTime        { get; set; } = string.Empty;
    public string  EndTime          { get; set; } = string.Empty;
    public string  SessionStatus    { get; set; } = string.Empty;
    public int?    RemainingSeconds { get; set; }
}
