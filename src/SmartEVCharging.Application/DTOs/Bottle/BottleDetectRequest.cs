using System.ComponentModel.DataAnnotations;

namespace SmartEVCharging.Application.DTOs.Bottle;

public class BottleDetectRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>small | medium | large — fallback when no image is provided.</summary>
    public string BottleSize { get; set; } = "medium";

    [Range(1, 50)]
    public int Quantity { get; set; } = 1;

    public string? BottleType { get; set; }

    /// <summary>
    /// Optional base64-encoded image (with or without data:image/... prefix).
    /// When provided the YOLO Python server is called for real detection.
    /// </summary>
    public string? ImageData { get; set; }
}
