using System.ComponentModel.DataAnnotations;

namespace SmartEVCharging.Application.DTOs.BinStatus;

public class UpdateBinStatusRequest
{
    [Range(0, 100)]
    public int FillPercentage { get; set; }

    public int TotalBottlesCollected { get; set; }
}
