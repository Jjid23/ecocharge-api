namespace SmartEVCharging.Application.DTOs.Admin;

public class AdjustPointsRequest
{
    public int    PointsAdjustment { get; set; }
    public string Reason           { get; set; } = string.Empty;
}
