using System.ComponentModel.DataAnnotations;

namespace SmartEVCharging.Application.DTOs.User;

public class DepositBottleRequest
{
    [Range(1, 100)]
    public int BottleCount { get; set; } = 1;

    /// <summary>small | medium | large — determines points earned per bottle.</summary>
    public string BottleSize { get; set; } = "medium";
}
