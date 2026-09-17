namespace SmartEVCharging.Domain.Enums;

public enum UserRole
{
    Admin   = 1,
    StationOwner = 2,
    User    = 3,
    /// <summary>ESP32 hardware device — issues telemetry + relay commands.</summary>
    Device  = 4
}
