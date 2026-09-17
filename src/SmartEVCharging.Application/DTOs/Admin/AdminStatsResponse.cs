namespace SmartEVCharging.Application.DTOs.Admin;

public class AdminStatsResponse
{
    public int    TotalBottlesRecycled    { get; set; }
    public int    TotalPointsAwarded      { get; set; }
    public int    TotalChargingSessions   { get; set; }
    public int    TotalRegisteredUsers    { get; set; }
    public string TotalCo2SavedKg        { get; set; } = "0.00";
    public int    ActivePortsCount        { get; set; }
    public int    AvailablePortsCount     { get; set; }
    public int    InUsePortsCount         { get; set; }
}
