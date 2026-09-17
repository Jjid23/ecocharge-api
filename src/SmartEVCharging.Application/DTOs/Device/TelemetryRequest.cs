namespace SmartEVCharging.Application.DTOs.Device;

/// <summary>
/// Full telemetry payload pushed by ESP32-A every ~2 s.
/// All sensor fields are nullable — the ESP32 omits fields it
/// could not read (e.g. UART timeout from ESP32-B).
/// </summary>
public class TelemetryRequest
{
    public string SerialNumber { get; set; } = string.Empty;

    // ── Ultrasonic sensors ───────────────────────────────────────
    public double? EntranceDistanceCm  { get; set; }   // GPIO 13/36
    public double? BinTopDistanceCm    { get; set; }   // GPIO 14/39
    public double? BinBottomDistanceCm { get; set; }   // GPIO 15/21

    // ── Power monitoring ─────────────────────────────────────────
    public double? Sw1VoltageV { get; set; }   // GPIO 32 (ADC1_CH4)
    public double? Sw1CurrentA { get; set; }   // GPIO 33 (ADC1_CH5)
    public double? Sw2VoltageV { get; set; }   // GPIO 34 (ADC1_CH6)
    public double? Sw2CurrentA { get; set; }   // GPIO 35 (ADC1_CH7)
    public double? Sw3VoltageV { get; set; }   // via UART from ESP32-B
    public double? Sw3CurrentA { get; set; }
    public double? Sw4VoltageV { get; set; }
    public double? Sw4CurrentA { get; set; }

    // ── Conveyor motor ───────────────────────────────────────────
    public bool ConveyorRunning  { get; set; }
    public int  ConveyorSpeedPwm { get; set; }

    // ── Relay states (true = relay energised = port active) ──────
    public bool Relay1Active { get; set; }
    public bool Relay2Active { get; set; }
    public bool Relay3Active { get; set; }
    public bool Relay4Active { get; set; }
}
