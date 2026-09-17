namespace SmartEVCharging.Application.DTOs.Device;

/// <summary>
/// Server reply to each telemetry POST.
/// Contains any pending relay commands for the ESP32 to execute
/// plus the current server time (so the ESP32 can sync its clock).
/// </summary>
public class TelemetryResponse
{
    public string  Status      { get; set; } = "ok";
    public string  ServerTime  { get; set; } = DateTime.UtcNow.ToString("o");
    public int     BinFill     { get; set; }
    public bool    BottleAtEntrance { get; set; }
    public List<PendingRelayCommand> PendingCommands { get; set; } = new();
}

public class PendingRelayCommand
{
    public Guid   CommandId      { get; set; }
    public int    PortNumber     { get; set; }
    public bool   Activate       { get; set; }
    public int    DurationSeconds { get; set; }
}
