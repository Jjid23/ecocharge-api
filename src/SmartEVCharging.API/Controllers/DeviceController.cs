using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartEVCharging.Application.DTOs.Device;
using SmartEVCharging.Domain.Entities;
using SmartEVCharging.Domain.Enums;
using SmartEVCharging.Infrastructure.Persistence;

namespace SmartEVCharging.API.Controllers;

/// <summary>
/// All ESP32-facing endpoints.
///
/// Authentication flow:
///   1. ESP32-A boots → POST /api/device/token  (serial + shared secret) → gets JWT
///   2. Every ~2 s    → POST /api/device/telemetry (JWT Bearer)           → pushes sensors, gets relay commands
///   3. Admin panel   → PUT  /api/device/relay/{port}                     → queues relay command
///   4. Any client    → GET  /api/device/status                           → latest snapshot
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class DeviceController : ControllerBase
{
    private readonly AppDbContext    _db;
    private readonly IConfiguration _config;

    // Bin is "full" when the top sensor reads less than this distance (cm)
    private const double BinFullThresholdCm  = 5.0;
    // Bin considered empty at this reading
    private const double BinEmptyDistanceCm  = 30.0;
    // Entrance sensor triggers if object < 10 cm away
    private const double EntranceTriggerCm   = 10.0;

    public DeviceController(AppDbContext db, IConfiguration config)
    {
        _db     = db;
        _config = config;
    }

    // ── Task 4: Token provisioning ────────────────────────────────────────────

    /// <summary>
    /// ESP32-A calls this on every boot to get a long-lived JWT (1 year).
    /// Body: { serialNumber, secret }
    /// The secret must match Esp32:Secret in appsettings.json.
    /// </summary>
    [HttpPost("token")]
    [AllowAnonymous]
    public async Task<IActionResult> GetToken([FromBody] DeviceTokenRequest req)
    {
        var expectedSecret = _config["Esp32:Secret"];
        if (req.Secret != expectedSecret)
            return Unauthorized(new { error = "Invalid device secret." });

        var device = await _db.Devices
            .FirstOrDefaultAsync(d => d.SerialNumber == req.SerialNumber);

        if (device is null)
            return NotFound(new { error = $"Device '{req.SerialNumber}' not registered. Contact admin." });

        var token = GenerateDeviceJwt(device);

        return Ok(new
        {
            token,
            deviceId   = device.Id.ToString(),
            deviceName = device.Name,
            expiresIn  = "365 days"
        });
    }

    // ── Task 3: Telemetry push ────────────────────────────────────────────────

    /// <summary>
    /// ESP32-A posts a full sensor snapshot every ~2 s.
    /// Server stores it, updates BinStatus, updates port power readings,
    /// and returns any pending relay commands.
    /// </summary>
    [HttpPost("telemetry")]
    [Authorize(Roles = "Device,Admin")]
    public async Task<IActionResult> PostTelemetry([FromBody] TelemetryRequest req)
    {
        var device = await _db.Devices
            .FirstOrDefaultAsync(d => d.SerialNumber == req.SerialNumber);

        if (device is null)
            return NotFound(new { error = "Device not found." });

        // ── 1. Calculate fill % from bin-top ultrasonic ───────────────────────
        int fillPct = 0;
        if (req.BinTopDistanceCm.HasValue)
        {
            double dist    = req.BinTopDistanceCm.Value;
            double range   = BinEmptyDistanceCm - BinFullThresholdCm;
            double current = dist - BinFullThresholdCm;
            fillPct = (int)(100.0 - Math.Clamp(current / range * 100.0, 0, 100));
        }

        bool bottleAtEntrance = req.EntranceDistanceCm.HasValue
            && req.EntranceDistanceCm.Value < EntranceTriggerCm;

        // ── 2. Persist sensor reading ─────────────────────────────────────────
        var reading = new SensorReading
        {
            DeviceId               = device.Id,
            EntranceDistanceCm     = req.EntranceDistanceCm,
            BinTopDistanceCm       = req.BinTopDistanceCm,
            BinBottomDistanceCm    = req.BinBottomDistanceCm,
            BinFillPercentage      = fillPct,
            BottleDetectedAtEntrance = bottleAtEntrance,
            Sw1VoltageV            = req.Sw1VoltageV,
            Sw1CurrentA            = req.Sw1CurrentA,
            Sw2VoltageV            = req.Sw2VoltageV,
            Sw2CurrentA            = req.Sw2CurrentA,
            Sw3VoltageV            = req.Sw3VoltageV,
            Sw3CurrentA            = req.Sw3CurrentA,
            Sw4VoltageV            = req.Sw4VoltageV,
            Sw4CurrentA            = req.Sw4CurrentA,
            ConveyorRunning        = req.ConveyorRunning,
            ConveyorSpeedPwm       = req.ConveyorSpeedPwm,
            Relay1Active           = req.Relay1Active,
            Relay2Active           = req.Relay2Active,
            Relay3Active           = req.Relay3Active,
            Relay4Active           = req.Relay4Active,
            RecordedAt             = DateTime.UtcNow
        };
        _db.SensorReadings.Add(reading);

        // ── 3. Update BinStatus singleton ─────────────────────────────────────
        var bin = await _db.BinStatuses.FindAsync(1);
        if (bin is null)
        {
            bin = new BinStatus { Id = 1, FillPercentage = fillPct, LastUpdatedAt = DateTime.UtcNow };
            _db.BinStatuses.Add(bin);
        }
        else
        {
            bin.FillPercentage = fillPct;
            bin.LastUpdatedAt  = DateTime.UtcNow;
        }

        // ── 4. Update charging port power readings & relay status ─────────────
        var ports = await _db.ChargingPorts
            .Where(p => p.DeviceId == device.Id)
            .ToListAsync();

        foreach (var port in ports)
        {
            (double? v, double? a, bool relayOn) = port.PortNumber switch
            {
                1 => (req.Sw1VoltageV, req.Sw1CurrentA, req.Relay1Active),
                2 => (req.Sw2VoltageV, req.Sw2CurrentA, req.Relay2Active),
                3 => (req.Sw3VoltageV, req.Sw3CurrentA, req.Relay3Active),
                4 => (req.Sw4VoltageV, req.Sw4CurrentA, req.Relay4Active),
                _ => (null, null, false)
            };

            port.CurrentPowerKw = (v.HasValue && a.HasValue)
                ? (decimal?)Math.Round(v.Value * a.Value / 1000.0, 4)
                : null;

            // Sync port status with relay state
            if (relayOn && port.Status == ChargingPortStatus.Available)
                port.Status = ChargingPortStatus.Occupied;
            else if (!relayOn && port.Status == ChargingPortStatus.Occupied)
                port.Status = ChargingPortStatus.Available;
        }

        // ── 5. Update device heartbeat ────────────────────────────────────────
        device.LastHeartbeat = DateTime.UtcNow;
        device.Status        = DeviceStatus.Online;

        await _db.SaveChangesAsync();

        // ── 6. Collect pending relay commands for this device ─────────────────
        var pending = await _db.RelayCommands
            .Where(c => c.DeviceId == device.Id && !c.Consumed)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var pendingDtos = pending.Select(c => new PendingRelayCommand
        {
            CommandId       = c.Id,
            PortNumber      = c.PortNumber,
            Activate        = c.Activate,
            DurationSeconds = c.DurationSeconds
        }).ToList();

        // Mark all as consumed
        foreach (var cmd in pending)
        {
            cmd.Consumed   = true;
            cmd.ConsumedAt = DateTime.UtcNow;
        }
        if (pending.Any())
            await _db.SaveChangesAsync();

        return Ok(new TelemetryResponse
        {
            Status           = "ok",
            ServerTime       = DateTime.UtcNow.ToString("o"),
            BinFill          = fillPct,
            BottleAtEntrance = bottleAtEntrance,
            PendingCommands  = pendingDtos
        });
    }

    // ── Heartbeat (lightweight ping) ──────────────────────────────────────────

    /// <summary>Minimal heartbeat — just updates LastHeartbeat, no sensor data.</summary>
    [HttpPost("heartbeat")]
    [Authorize(Roles = "Device,Admin")]
    public async Task<IActionResult> Heartbeat([FromBody] HeartbeatRequest req)
    {
        var device = await _db.Devices
            .FirstOrDefaultAsync(d => d.SerialNumber == req.SerialNumber);

        if (device is null)
            return NotFound(new { error = "Device not found." });

        device.LastHeartbeat = DateTime.UtcNow;
        device.Status        = DeviceStatus.Online;
        await _db.SaveChangesAsync();

        return Ok(new { status = "ok", serverTime = DateTime.UtcNow.ToString("o") });
    }

    // ── Latest status snapshot (for admin dashboard + web UI) ────────────────

    /// <summary>Returns the most recent sensor reading for the kiosk ESP32-A.</summary>
    [HttpGet("status")]
    [Authorize]
    public async Task<IActionResult> GetStatus()
    {
        var latest = await _db.SensorReadings
            .AsNoTracking()
            .Include(r => r.Device)
            .OrderByDescending(r => r.RecordedAt)
            .FirstOrDefaultAsync();

        if (latest is null)
            return Ok(new { message = "No telemetry received yet." });

        var device = latest.Device;
        var secondsAgo = (DateTime.UtcNow - device.LastHeartbeat).TotalSeconds;

        return Ok(new
        {
            deviceId         = device.Id.ToString(),
            deviceName       = device.Name,
            serialNumber     = device.SerialNumber,
            onlineStatus     = secondsAgo < 30 ? "Online" : secondsAgo < 120 ? "Stale" : "Offline",
            lastHeartbeat    = device.LastHeartbeat.ToString("o"),
            recordedAt       = latest.RecordedAt.ToString("o"),

            // Ultrasonic
            entranceDistanceCm     = latest.EntranceDistanceCm,
            binTopDistanceCm       = latest.BinTopDistanceCm,
            binBottomDistanceCm    = latest.BinBottomDistanceCm,
            binFillPercentage      = latest.BinFillPercentage,
            bottleDetectedAtEntrance = latest.BottleDetectedAtEntrance,

            // Power per port
            ports = new[]
            {
                new { port = 1, voltageV = latest.Sw1VoltageV, currentA = latest.Sw1CurrentA,
                      powerW = latest.Sw1VoltageV * latest.Sw1CurrentA, relay = latest.Relay1Active },
                new { port = 2, voltageV = latest.Sw2VoltageV, currentA = latest.Sw2CurrentA,
                      powerW = latest.Sw2VoltageV * latest.Sw2CurrentA, relay = latest.Relay2Active },
                new { port = 3, voltageV = latest.Sw3VoltageV, currentA = latest.Sw3CurrentA,
                      powerW = latest.Sw3VoltageV * latest.Sw3CurrentA, relay = latest.Relay3Active },
                new { port = 4, voltageV = latest.Sw4VoltageV, currentA = latest.Sw4CurrentA,
                      powerW = latest.Sw4VoltageV * latest.Sw4CurrentA, relay = latest.Relay4Active },
            },

            // Conveyor
            conveyorRunning  = latest.ConveyorRunning,
            conveyorSpeedPwm = latest.ConveyorSpeedPwm,
        });
    }

    // ── Relay command (from admin panel or session logic) ─────────────────────

    /// <summary>
    /// Queues a relay activate/deactivate command.
    /// The ESP32 picks it up on the next telemetry poll (within ~2 s).
    /// </summary>
    [HttpPut("relay/{portNumber:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SetRelay(int portNumber, [FromBody] RelayCommandRequest req)
    {
        if (portNumber < 1 || portNumber > 4)
            return BadRequest(new { error = "Port number must be 1–4." });

        var device = await _db.Devices
            .FirstOrDefaultAsync(d => d.SerialNumber == "ESP32-A-COM3");

        if (device is null)
            return NotFound(new { error = "ESP32-A device not found." });

        var cmd = new RelayCommand
        {
            DeviceId        = device.Id,
            PortNumber      = portNumber,
            Activate        = req.Activate,
            DurationSeconds = req.DurationSeconds,
            Consumed        = false,
            CreatedAt       = DateTime.UtcNow
        };
        _db.RelayCommands.Add(cmd);
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message   = $"Relay command queued for Port {portNumber}.",
            commandId = cmd.Id.ToString(),
            portNumber,
            activate  = req.Activate
        });
    }

    // ── Sensor history (for charts in admin dashboard) ────────────────────────

    /// <summary>Last N sensor readings for charts (default 60 = 2 minutes at 2s interval).</summary>
    [HttpGet("history")]
    [Authorize]
    public async Task<IActionResult> GetHistory([FromQuery] int count = 60)
    {
        count = Math.Clamp(count, 1, 1000);

        var readings = await _db.SensorReadings
            .AsNoTracking()
            .OrderByDescending(r => r.RecordedAt)
            .Take(count)
            .Select(r => new
            {
                recordedAt           = r.RecordedAt.ToString("o"),
                binFillPercentage    = r.BinFillPercentage,
                bottleAtEntrance     = r.BottleDetectedAtEntrance,
                entranceDistanceCm   = r.EntranceDistanceCm,
                binTopDistanceCm     = r.BinTopDistanceCm,
                sw1W = r.Sw1VoltageV * r.Sw1CurrentA,
                sw2W = r.Sw2VoltageV * r.Sw2CurrentA,
                sw3W = r.Sw3VoltageV * r.Sw3CurrentA,
                sw4W = r.Sw4VoltageV * r.Sw4CurrentA,
                conveyorRunning      = r.ConveyorRunning,
                relay1 = r.Relay1Active,
                relay2 = r.Relay2Active,
                relay3 = r.Relay3Active,
                relay4 = r.Relay4Active,
            })
            .ToListAsync();

        return Ok(new { readings = readings.OrderBy(r => r.recordedAt) });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string GenerateDeviceJwt(Device device)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));

        var expiryDays = int.TryParse(_config["Esp32:TokenExpiryDays"], out var d) ? d : 365;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,        device.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, device.SerialNumber),
            new Claim(ClaimTypes.Role,                    "Device"),
            new Claim(JwtRegisteredClaimNames.Jti,        Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            DateTime.UtcNow.AddDays(expiryDays),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class HeartbeatRequest
{
    public string SerialNumber { get; set; } = string.Empty;
}
