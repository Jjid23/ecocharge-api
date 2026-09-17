using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEVCharging.Application.DTOs.Admin;
using SmartEVCharging.Application.Interfaces;
using SmartEVCharging.Domain.Enums;
using SmartEVCharging.Infrastructure.Persistence;

namespace SmartEVCharging.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IUserService _users;
    private const int SecondsPerPoint = 6;

    public AdminController(AppDbContext db, IUserService users)
    {
        _db    = db;
        _users = users;
    }

    /// <summary>Overall system stats for the admin dashboard.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> Stats()
    {
        var totalUsers    = await _db.Users.CountAsync();
        var totalSessions = await _db.ChargingSessions.CountAsync();
        var totalBottles  = await _db.Users.SumAsync(u => u.BottlesDepositedTotal);
        var totalPoints   = await _db.Users.SumAsync(u => u.CurrentPoints);
        var availCount    = await _db.ChargingPorts.CountAsync(p => p.Status == ChargingPortStatus.Available);
        var inUseCount    = await _db.ChargingPorts.CountAsync(p => p.Status == ChargingPortStatus.Occupied);

        var recentBottleSessions = await _db.ChargingSessions
            .Include(s => s.User)
            .Include(s => s.ChargingPort)
            .OrderByDescending(s => s.CreatedAt)
            .Take(5)
            .AsNoTracking()
            .ToListAsync();

        var stats = new AdminStatsResponse
        {
            TotalBottlesRecycled  = totalBottles,
            TotalPointsAwarded    = totalPoints,
            TotalChargingSessions = totalSessions,
            TotalRegisteredUsers  = totalUsers,
            TotalCo2SavedKg       = (totalBottles * 0.082).ToString("F2"),
            ActivePortsCount      = availCount,
            AvailablePortsCount   = availCount,
            InUsePortsCount       = inUseCount
        };

        return Ok(new { stats, recentBottles = new List<object>(), recentSessions = new List<object>() });
    }

    /// <summary>List all users.</summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users.AsNoTracking().ToListAsync();
        var result = users.Select(u => new
        {
            userId        = u.Id.ToString(),
            fullName      = u.FullName,
            username      = u.Username,
            email         = u.Email,
            currentPoints = u.CurrentPoints,
            role          = u.Role.ToString().ToLower(),
            createdAt     = u.CreatedAt.ToString("o"),
            updatedAt     = (u.UpdatedAt ?? u.CreatedAt).ToString("o")
        });
        return Ok(new { users = result });
    }

    /// <summary>Adjust a user's points balance.</summary>
    [HttpPut("users/{userId}/points")]
    public async Task<IActionResult> AdjustPoints(string userId, [FromBody] AdjustPointsRequest request)
    {
        if (!Guid.TryParse(userId, out var guid))
            return BadRequest(new { error = "Invalid user ID." });
        try
        {
            var profile = await _users.AdjustPointsAsync(guid, request.PointsAdjustment, request.Reason);
            return Ok(new { message = "User points updated", user = profile });
        }
        catch (KeyNotFoundException ex) { return NotFound(new { error = ex.Message }); }
    }

    /// <summary>Update a port's status or connector type.</summary>
    [HttpPut("ports/{portId}/status")]
    public async Task<IActionResult> UpdatePort(string portId, [FromBody] UpdatePortRequest request)
    {
        if (!Guid.TryParse(portId, out var guid))
            return BadRequest(new { error = "Invalid port ID." });

        var port = await _db.ChargingPorts.FindAsync(guid);
        if (port is null) return NotFound(new { error = "Port not found." });

        if (!string.IsNullOrWhiteSpace(request.PortStatus))
        {
            port.Status = request.PortStatus switch
            {
                "Available"   => ChargingPortStatus.Available,
                "In Use"      => ChargingPortStatus.Occupied,
                "Maintenance" => ChargingPortStatus.Maintenance,
                _             => ChargingPortStatus.Offline
            };
        }
        if (!string.IsNullOrWhiteSpace(request.ConnectorType))
            port.ConnectorType = request.ConnectorType;

        port.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new
        {
            message = "Port updated",
            port    = new
            {
                portId        = port.Id.ToString(),
                portNumber    = port.PortNumber,
                portStatus    = port.Status switch
                {
                    ChargingPortStatus.Available   => "Available",
                    ChargingPortStatus.Occupied    => "In Use",
                    ChargingPortStatus.Maintenance => "Maintenance",
                    _                              => "Disabled"
                },
                connectorType = port.ConnectorType,
                updatedAt     = port.UpdatedAt?.ToString("o")
            }
        });
    }

    /// <summary>Get system settings (points per bottle size, duration options).</summary>
    [HttpGet("settings")]
    public IActionResult GetSettings()
    {
        return Ok(new
        {
            settings = new
            {
                smallBottlePoints  = 5,
                mediumBottlePoints = 10,
                largeBottlePoints  = 15,
                durationOptions    = new[]
                {
                    new { minutes = 5,  points = 50  },
                    new { minutes = 10, points = 100 },
                    new { minutes = 15, points = 150 },
                    new { minutes = 20, points = 200 },
                    new { minutes = 30, points = 300 }
                },
                systemMaintenanceMode = false
            }
        });
    }

    /// <summary>Update system settings (stub — future DB-backed).</summary>
    [HttpPut("settings")]
    public IActionResult UpdateSettings([FromBody] object settings)
    {
        return Ok(new { message = "Settings updated successfully", settings });
    }

    /// <summary>All recycling (bottle deposit) transaction logs.</summary>
    [HttpGet("transactions/recycling")]
    public async Task<IActionResult> RecyclingLogs()
    {
        var users = await _db.Users.AsNoTracking().ToListAsync();
        var txs = users.Select(u => new
        {
            transactionId   = u.Id.ToString(),
            userId          = u.Id.ToString(),
            userFullName    = u.FullName,
            bottleSize      = "medium",
            bottleType      = "PET Clear Plastic",
            quantity        = u.BottlesDepositedTotal,
            pointsEarned    = u.CurrentPoints,
            detectionStatus = "Verified",
            detectedAt      = u.CreatedAt.ToString("o")
        });
        return Ok(new { transactions = txs });
    }

    /// <summary>All charging session logs.</summary>
    [HttpGet("transactions/charging")]
    public async Task<IActionResult> ChargingLogs()
    {
        var sessions = await _db.ChargingSessions
            .Include(s => s.User)
            .Include(s => s.ChargingPort)
            .OrderByDescending(s => s.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var result = sessions.Select(s => new
        {
            sessionId       = s.Id.ToString(),
            userId          = s.UserId.ToString(),
            userFullName    = s.User?.FullName ?? "",
            portId          = s.ChargingPortId.ToString(),
            portNumber      = s.ChargingPort?.PortNumber ?? 0,
            connectorType   = s.ChargingPort?.ConnectorType ?? "",
            durationMinutes = 0,
            pointsUsed      = 0,
            startTime       = s.StartedAt?.ToString("o") ?? s.CreatedAt.ToString("o"),
            endTime         = s.EndedAt?.ToString("o") ?? "",
            sessionStatus   = s.Status.ToString()
        });

        return Ok(new { sessions = result });
    }

    // ── Also expose /api/history/recycling/:userId here ────────────

    [HttpGet("~/api/history/recycling/{userId}")]
    public async Task<IActionResult> UserRecyclingHistory(string userId)
    {
        if (!Guid.TryParse(userId, out var guid))
            return BadRequest(new { error = "Invalid user ID." });

        var user = await _db.Users.FindAsync(guid);
        if (user is null) return NotFound(new { error = "User not found." });

        // Create one entry per bottle-deposit event (approximated from total)
        var tx = new[]
        {
            new
            {
                transactionId   = user.Id.ToString(),
                userId,
                userFullName    = user.FullName,
                bottleSize      = "medium",
                bottleType      = "PET Clear Plastic",
                quantity        = user.BottlesDepositedTotal,
                pointsEarned    = user.BottlesDepositedTotal * 10,
                detectionStatus = "Verified",
                detectedAt      = user.CreatedAt.ToString("o"),
                confidenceScore = 0.97
            }
        };

        return Ok(new { transactions = tx });
    }
}

public class UpdatePortRequest
{
    public string? PortStatus     { get; set; }
    public string? ConnectorType  { get; set; }
}
