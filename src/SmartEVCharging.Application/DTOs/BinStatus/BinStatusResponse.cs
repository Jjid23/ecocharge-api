namespace SmartEVCharging.Application.DTOs.BinStatus;

public class BinStatusResponse
{
    public int FillPercentage { get; set; }
    public int TotalBottlesCollected { get; set; }
    public DateTime LastUpdatedAt { get; set; }
}
