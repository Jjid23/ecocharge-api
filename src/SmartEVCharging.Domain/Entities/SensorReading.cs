using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartEVCharging.Domain.Entities;

/// <summary>
/// One telemetry snapshot pushed by ESP32-A every ~2 seconds.
/// Covers all three ultrasonic sensors, four power-switch channels (SW1-SW4),
/// conveyor state, and relay states for ports 1-4.
/// </summary>
public class SensorReading
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid DeviceId { get; set; }

    // ── Ultrasonic sensors (distances in cm) ─────────────────────────────────
    /// <summary>HC-SR04 at entrance — detects bottle insertion (GPIO 13/36).</summary>
    public double? EntranceDistanceCm { get; set; }

    /// <summary>HC-SR04 at bin top — measures fill from above (GPIO 14/39).</summary>
    public double? BinTopDistanceCm { get; set; }

    /// <summary>HC-SR04 at bin bottom — secondary fill sensor (GPIO 15/21).</summary>
    public double? BinBottomDistanceCm { get; set; }

    /// <summary>Derived fill percentage (0-100) from bin sensor readings.</summary>
    public int BinFillPercentage { get; set; }

    /// <summary>True when entrance sensor detects object closer than 10 cm.</summary>
    public bool BottleDetectedAtEntrance { get; set; }

    // ── Power monitoring (ESP32-A ADC + ESP32-B ADC via UART) ───────────────
    // SW1 = Port 1, SW2 = Port 2 (ESP32-A ADC1_CH4/CH5/CH6/CH7)
    // SW3 = Port 3, SW4 = Port 4 (ESP32-B ADC1_CH4/CH5/CH6/CH7 via UART)

    public double? Sw1VoltageV  { get; set; }   // GPIO 32
    public double? Sw1CurrentA  { get; set; }   // GPIO 33
    public double? Sw2VoltageV  { get; set; }   // GPIO 34
    public double? Sw2CurrentA  { get; set; }   // GPIO 35
    public double? Sw3VoltageV  { get; set; }   // via UART from ESP32-B
    public double? Sw3CurrentA  { get; set; }
    public double? Sw4VoltageV  { get; set; }
    public double? Sw4CurrentA  { get; set; }

    // ── Conveyor motor state ─────────────────────────────────────────────────
    /// <summary>True when conveyor is running (L298N IN1/IN2/ENA).</summary>
    public bool ConveyorRunning { get; set; }

    /// <summary>0-255 PWM speed value (LEDC ch0 on GPIO 18).</summary>
    public int ConveyorSpeedPwm { get; set; }

    // ── Relay states (active LOW — false = relay energised = port ON) ────────
    public bool Relay1Active { get; set; }   // GPIO 25 → Port 1
    public bool Relay2Active { get; set; }   // GPIO 26 → Port 2
    public bool Relay3Active { get; set; }   // GPIO 16 → Port 3
    public bool Relay4Active { get; set; }   // GPIO 5  → Port 4

    // ── Metadata ─────────────────────────────────────────────────────────────
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(DeviceId))]
    public virtual Device Device { get; set; } = null!;
}
