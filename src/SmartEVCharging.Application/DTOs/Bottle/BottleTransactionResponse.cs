namespace SmartEVCharging.Application.DTOs.Bottle;

public class BottleTransactionResponse
{
    public string TransactionId     { get; set; } = string.Empty;
    public string UserId            { get; set; } = string.Empty;
    public string UserFullName      { get; set; } = string.Empty;
    public string BottleSize        { get; set; } = string.Empty;
    public string BottleType        { get; set; } = string.Empty;
    public int    Quantity          { get; set; }
    public int    PointsEarned      { get; set; }
    public string DetectionStatus   { get; set; } = "Verified";
    public string DetectedAt        { get; set; } = string.Empty;
    public double ConfidenceScore   { get; set; } = 0.97;
}
