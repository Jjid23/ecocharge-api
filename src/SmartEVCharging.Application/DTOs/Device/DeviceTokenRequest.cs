using System.ComponentModel.DataAnnotations;

namespace SmartEVCharging.Application.DTOs.Device;

/// <summary>
/// ESP32-A uses its serial number + a shared secret to obtain a JWT.
/// Store the secret in appsettings.json under Esp32:Secret.
/// </summary>
public class DeviceTokenRequest
{
    [Required]
    public string SerialNumber { get; set; } = string.Empty;

    [Required]
    public string Secret { get; set; } = string.Empty;
}
