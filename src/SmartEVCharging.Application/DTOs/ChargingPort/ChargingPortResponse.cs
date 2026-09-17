namespace SmartEVCharging.Application.DTOs.ChargingPort;

public class ChargingPortResponse
{
    public Guid Id { get; set; }
    public string PortName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal MaxPowerKw { get; set; }
    public decimal? CurrentPowerKw { get; set; }
    public string Type { get; set; } = string.Empty;
}
