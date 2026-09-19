using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartEVCharging.Application.DTOs.ChargingSession;
using SmartEVCharging.Application.Interfaces;
using SmartEVCharging.Domain.Enums;
using SmartEVCharging.Infrastructure.Persistence;

namespace SmartEVCharging.API.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public class ChargingSessionController : ControllerBase
{
    private readonly IChargingSessionService _sessions;
    private readonly AppDbContext            _db;
    private const int SecondsPerPoint = 6;

    public ChargingSessionController(IChargingSessionService sessions, AppDbContext db)
    {
        _sessions = sessions;
        _db       = db;
    }

    // ── Android routes ─────────────────────────────────────────────

    [HttpPost("chargingsession/start")]
    public async Task<IActionResult> Start([FromBody] StartSessionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var userId = GetUserId();
        try
        {
            var session = await _sessions.StartSessionAsync(userId, request);
            return StatusCode(201, session);
        }
        catch (KeyNotFoundException ex)      { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("chargingsession/{sessionId:guid}/end")]
    public async Task<IActionResult> End(Guid sessionId)
    {
        var userId = GetUserId();
        try   { return Ok(await _sessions.EndSessionAsync(userId, sessionId)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    [HttpGet("chargingsession/history")]
    public async Task<IActionResult> History()
    {
        var history = await _sessions.GetSessionHistoryAsync(GetUserId());
        return Ok(history);
    }

    // ── Web routes ────────────────────────────────────────────────

    /// <summary>Start charging session (web frontend format: portId string + durationMinutes).</summary>
    [HttpPost("charging/start")]
    public async Task<IActionResult> WebStart([FromBody] WebStartSessionRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        if (!Guid.TryParse(request.UserId, out var userId))
            return BadRequest(new { error = "Invalid user ID." });

        if (!Guid.TryParse(request.PortId, out var portId))
            return BadRequest(new { error = "Invalid port ID." });

        var user = await _db.Users.FindAsync(userId);
        if (user is null) return NotFound(new { error = "User not found." });

        var port = await _db.ChargingPorts.FindAsync(portId);
        if (port is null) return NotFound(new { error = "Port not found." });

        if (port.Status != ChargingPortStatus.Available)
            return BadRequest(new { error = $"Port {port.PortName} is currently {port.Status}. Please select an available port." });

        // Points-based: 10 pts per minute (2 pts/s × 6s/pt)
        int requiredPoints  = request.DurationMinutes * 10;
        int requiredSeconds = requiredPoints * SecondsPerPoint;

        if (user.CurrentPoints < requiredPoints)
            return BadRequest(new
            {
                error          = $"Insufficient points. You need {requiredPoints} pts for {request.DurationMinutes} min but only have {user.CurrentPoints} pts.",
                requiredPoints,
                currentPoints  = user.CurrentPoints
            });

        user.CurrentPoints  -= requiredPoints;
        user.CreditsSeconds -= requiredSeconds;
        user.UpdatedAt       = DateTime.UtcNow;

        var startTime = DateTime.UtcNow;
        var endTime   = startTime.AddMinutes(request.DurationMinutes);

        var session = new Domain.Entities.ChargingSession
        {
            Id            = Guid.NewGuid(),
            UserId        = userId,
            ChargingPortId = portId,
            Status        = ChargingSessionStatus.InProgress,
            StartedAt     = startTime,
            EndedAt       = endTime,
            Notes         = $"AllocatedSeconds:{requiredSeconds};Bottles:0",
            CreatedAt     = startTime
        };

        port.Status = ChargingPortStatus.Occupied;
        port.UpdatedAt = DateTime.UtcNow;
        _db.ChargingSessions.Add(session);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Database error: " + ex.Message + " | Inner: " + ex.InnerException?.Message });
        }

        // Detach all tracked entities to prevent circular reference serialization
        _db.ChangeTracker.Clear();

        // Build response manually — avoid navigation property serialization issues
        return Ok(new
        {
            message = "Charging Session Started Successfully!",
            session = new
            {
                sessionId        = session.Id.ToString(),
                userId           = session.UserId.ToString(),
                userFullName     = user.FullName,
                portId           = portId.ToString(),
                portNumber       = port.PortNumber,
                connectorType    = port.ConnectorType,
                durationMinutes  = request.DurationMinutes,
                pointsUsed       = requiredPoints,
                startTime        = startTime.ToString("o"),
                endTime          = endTime.ToString("o"),
                sessionStatus    = "Active",
                remainingSeconds = requiredSeconds
            },
            port = new
            {
                portId        = portId.ToString(),
                portNumber    = port.PortNumber,
                portStatus    = "In Use",
                connectorType = port.ConnectorType,
                updatedAt     = DateTime.UtcNow.ToString("o")
            },
            remainingPoints = user.CurrentPoints
        });
    }

    /// <summary>Stop a charging session early (web frontend).
    /// Calculates remaining seconds, stores them in Notes, refunds unused points,
    /// and marks the session as Stopped (not Completed) so it can be resumed.
    /// </summary>
    [HttpPost("charging/stop/{sessionId}")]
    public async Task<IActionResult> WebStop(string sessionId)
    {
        var session = await _db.ChargingSessions
            .Include(s => s.ChargingPort)
            .FirstOrDefaultAsync(s => s.Id.ToString() == sessionId);

        if (session is null) return NotFound(new { error = "Session not found." });

        int remainingSeconds = 0;
        int refundedPoints   = 0;

        if (session.Status == ChargingSessionStatus.InProgress)
        {
            var now = DateTime.UtcNow;

            // Calculate how many seconds were actually used
            int allocatedSeconds = ParseNote(session.Notes, "AllocatedSeconds");
            int usedSeconds      = session.StartedAt.HasValue
                ? (int)Math.Max(0, (now - session.StartedAt.Value).TotalSeconds)
                : allocatedSeconds;

            remainingSeconds = Math.Max(0, allocatedSeconds - usedSeconds);
            refundedPoints   = remainingSeconds / SecondsPerPoint; // 1 pt per 6 s

            // Store remaining seconds in Notes so resume can use them
            var existingNotes = session.Notes ?? $"AllocatedSeconds:{allocatedSeconds};Bottles:0";
            // Replace or append RemainingSeconds key
            existingNotes = SetNote(existingNotes, "RemainingSeconds", remainingSeconds.ToString());
            existingNotes = SetNote(existingNotes, "StoppedAt", now.ToString("o"));
            session.Notes  = existingNotes;

            // Mark as Stopped (not Completed) so it can be resumed
            session.Status  = ChargingSessionStatus.Completed; // reuse Completed — we distinguish via Notes
            session.Notes   = SetNote(session.Notes, "Resumable", remainingSeconds > 0 ? "1" : "0");
            session.EndedAt = now;

            // Free the port
            if (session.ChargingPort is not null)
                session.ChargingPort.Status = ChargingPortStatus.Available;

            // Refund unused points to user
            if (refundedPoints > 0)
            {
                var user2 = await _db.Users.FindAsync(session.UserId);
                if (user2 is not null)
                {
                    user2.CurrentPoints  += refundedPoints;
                    user2.CreditsSeconds += remainingSeconds;
                    user2.UpdatedAt       = now;
                }
            }

            await _db.SaveChangesAsync();
        }

        var user = await _db.Users.FindAsync(session.UserId);
        var resp = MapWebSession(session, user?.FullName ?? "", session.ChargingPort!);
        resp.RemainingSeconds = remainingSeconds;

        return Ok(new
        {
            message          = remainingSeconds > 0
                ? $"Session paused. {remainingSeconds / 60} min {remainingSeconds % 60} sec remaining — {refundedPoints} pts refunded."
                : "Charging session stopped.",
            session          = resp,
            remainingSeconds,
            refundedPoints,
            resumable        = remainingSeconds > 0
        });
    }

    /// <summary>
    /// Resume a previously stopped session that has remaining time.
    /// Picks up where it left off — deducts no extra points for the remaining time
    /// (they were already refunded on stop, so we deduct them again here).
    /// </summary>
    [HttpPost("charging/resume/{sessionId}")]
    public async Task<IActionResult> WebResume(string sessionId)
    {
        if (!Guid.TryParse(sessionId, out var sid))
            return BadRequest(new { error = "Invalid session ID." });

        var oldSession = await _db.ChargingSessions
            .Include(s => s.ChargingPort)
            .FirstOrDefaultAsync(s => s.Id == sid);

        if (oldSession is null) return NotFound(new { error = "Session not found." });

        bool resumable       = ParseNote(oldSession.Notes, "Resumable") == 1;
        int  remainingSeconds = ParseNote(oldSession.Notes, "RemainingSeconds");

        if (!resumable || remainingSeconds <= 0)
            return BadRequest(new { error = "This session has no remaining time to resume." });

        // Find a port — prefer the same port if available, else any available port
        var preferredPort = oldSession.ChargingPort;
        Domain.Entities.ChargingPort? port = null;

        if (preferredPort is not null)
        {
            // Re-fetch to get latest status
            port = await _db.ChargingPorts.FindAsync(preferredPort.Id);
        }

        if (port is null || port.Status != ChargingPortStatus.Available)
        {
            // Find any available port
            port = await _db.ChargingPorts
                .FirstOrDefaultAsync(p => p.Status == ChargingPortStatus.Available);
        }

        if (port is null)
            return BadRequest(new { error = "No available ports to resume session." });

        var user = await _db.Users.FindAsync(oldSession.UserId);
        if (user is null) return NotFound(new { error = "User not found." });

        // Deduct the refunded points again (they were given back on stop)
        int pointsNeeded = remainingSeconds / SecondsPerPoint;
        if (user.CurrentPoints < pointsNeeded)
            return BadRequest(new
            {
                error         = $"Insufficient points to resume. Need {pointsNeeded} pts but have {user.CurrentPoints} pts.",
                pointsNeeded,
                currentPoints = user.CurrentPoints
            });

        user.CurrentPoints  -= pointsNeeded;
        user.CreditsSeconds -= remainingSeconds;
        user.UpdatedAt       = DateTime.UtcNow;

        // Mark old session as non-resumable
        oldSession.Notes = SetNote(oldSession.Notes!, "Resumable", "0");

        // Create a new session for the remaining time
        var now            = DateTime.UtcNow;
        var remainingMins  = (int)Math.Ceiling(remainingSeconds / 60.0);
        var newSession     = new Domain.Entities.ChargingSession
        {
            Id            = Guid.NewGuid(),
            UserId        = oldSession.UserId,
            ChargingPortId = port.Id,
            Status        = ChargingSessionStatus.InProgress,
            StartedAt     = now,
            EndedAt       = now.AddSeconds(remainingSeconds),
            Notes         = $"AllocatedSeconds:{remainingSeconds};Bottles:0;ResumedFrom:{oldSession.Id}",
            CreatedAt     = now
        };

        port.Status = ChargingPortStatus.Occupied;
        port.UpdatedAt = now;
        _db.ChargingSessions.Add(newSession);
        await _db.SaveChangesAsync();

        var webSession = MapWebSession(newSession, user.FullName, port);
        webSession.RemainingSeconds = remainingSeconds;

        return Ok(new
        {
            message         = $"Session resumed! {remainingMins} min remaining on Port {port.PortNumber}.",
            session         = webSession,
            port            = MapPort(port),
            remainingPoints = user.CurrentPoints
        });
    }

    /// <summary>Get all resumable (stopped with remaining time) sessions for a user.</summary>
    [HttpGet("charging/resumable/{userId}")]
    public async Task<IActionResult> ResumableSessions(string userId)
    {
        if (!Guid.TryParse(userId, out var guid))
            return BadRequest(new { error = "Invalid user ID." });

        var user = await _db.Users.FindAsync(guid);
        if (user is null) return NotFound(new { error = "User not found." });

        var sessions = await _db.ChargingSessions
            .Include(s => s.ChargingPort)
            .Where(s => s.UserId == guid
                && s.Status == ChargingSessionStatus.Completed
                && s.Notes != null && s.Notes.Contains("Resumable:1"))
            .OrderByDescending(s => s.EndedAt)
            .AsNoTracking()
            .ToListAsync();

        var result = sessions.Select(s =>
        {
            int remSecs = ParseNote(s.Notes, "RemainingSeconds");
            var ws      = MapWebSession(s, user.FullName, s.ChargingPort!);
            ws.RemainingSeconds = remSecs;
            return new
            {
                sessionId        = ws.SessionId,
                portNumber       = ws.PortNumber,
                remainingSeconds = remSecs,
                remainingMinutes = (int)Math.Ceiling(remSecs / 60.0),
                pointsRefunded   = remSecs / SecondsPerPoint,
                stoppedAt        = s.EndedAt?.ToString("o") ?? ""
            };
        }).ToList();

        return Ok(new { resumableSessions = result });
    }

    /// <summary>Get active session for a userId (web frontend polling).</summary>
    [HttpGet("charging/active/{userId}")]
    public async Task<IActionResult> ActiveSession(string userId)
    {
        if (!Guid.TryParse(userId, out var guid))
            return BadRequest(new { error = "Invalid user ID." });

        var session = await _db.ChargingSessions
            .Include(s => s.ChargingPort)
            .FirstOrDefaultAsync(s =>
                s.UserId == guid &&
                s.Status == ChargingSessionStatus.InProgress);

        if (session is null) return Ok(new { activeSession = (object?)null });

        // Auto-complete expired sessions
        if (session.EndedAt.HasValue && DateTime.UtcNow >= session.EndedAt.Value)
        {
            session.Status = ChargingSessionStatus.Completed;
            session.ChargingPort.Status = ChargingPortStatus.Available;
            await _db.SaveChangesAsync();
            return Ok(new { activeSession = (object?)null });
        }

        var user             = await _db.Users.FindAsync(guid);
        var remainingSeconds = session.EndedAt.HasValue
            ? (int)Math.Max(0, (session.EndedAt.Value - DateTime.UtcNow).TotalSeconds)
            : 0;

        var webSession       = MapWebSession(session, user?.FullName ?? "", session.ChargingPort);
        webSession.RemainingSeconds = remainingSeconds;

        return Ok(new { activeSession = webSession });
    }

    /// <summary>Get charging session history for a userId (web frontend).</summary>
    [HttpGet("history/charging/{userId}")]
    public async Task<IActionResult> ChargingHistory(string userId)
    {
        if (!Guid.TryParse(userId, out var guid))
            return BadRequest(new { error = "Invalid user ID." });

        var user     = await _db.Users.FindAsync(guid);
        var sessions = await _db.ChargingSessions
            .Include(s => s.ChargingPort)
            .Where(s => s.UserId == guid)
            .OrderByDescending(s => s.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var result = sessions.Select(s =>
            MapWebSession(s, user?.FullName ?? "", s.ChargingPort)).ToList();

        return Ok(new { sessions = result });
    }

    /// <summary>Get points transaction history for a userId (web frontend).</summary>
    [HttpGet("history/points/{userId}")]
    public async Task<IActionResult> PointsHistory(string userId)
    {
        if (!Guid.TryParse(userId, out var guid))
            return BadRequest(new { error = "Invalid user ID." });

        var user = await _db.Users.FindAsync(guid);
        if (user is null) return NotFound(new { error = "User not found." });

        var sessions = await _db.ChargingSessions
            .Include(s => s.ChargingPort)
            .Where(s => s.UserId == guid)
            .OrderByDescending(s => s.CreatedAt)
            .AsNoTracking()
            .ToListAsync();

        var chargingTxs = sessions.Select(s =>
        {
            int allocSecs  = ParseNote(s.Notes, "AllocatedSeconds");
            int pointsUsed = allocSecs / SecondsPerPoint;
            return new
            {
                pointTransactionId = s.Id.ToString(),
                userId,
                transactionType    = "Deducted",
                pointsAdded        = 0,
                pointsDeducted     = pointsUsed,
                balanceAfter       = 0,
                description        = $"Charging Session on {s.ChargingPort?.PortName ?? "Port"} ({allocSecs / 60} mins)",
                createdAt          = s.CreatedAt.ToString("o")
            };
        }).ToList<object>();

        // Prepend welcome bonus entry so new users see their 20 free points
        var welcomeEntry = new
        {
            pointTransactionId = $"welcome-{guid}",
            userId,
            transactionType    = "Admin Bonus",
            pointsAdded        = 20,
            pointsDeducted     = 0,
            balanceAfter       = 20,
            description        = "Welcome Bonus — 20 Free Points on Sign Up! 🎉",
            createdAt          = user.CreatedAt.ToString("o")
        };

        // Also add bottle deposit entries
        if (user.BottlesDepositedTotal > 0)
        {
            chargingTxs.Insert(0, (object)new
            {
                pointTransactionId = $"bottles-{guid}",
                userId,
                transactionType    = "Earned",
                pointsAdded        = user.BottlesDepositedTotal * 10,
                pointsDeducted     = 0,
                balanceAfter       = user.CurrentPoints,
                description        = $"Bottle Recycling Rewards ({user.BottlesDepositedTotal} bottles)",
                createdAt          = user.CreatedAt.ToString("o")
            });
        }

        chargingTxs.Add((object)welcomeEntry);

        return Ok(new { transactions = chargingTxs });
    }

    // ── One-time seed endpoint (only works when ports table is empty) ──────────

    [HttpGet("~/api/seed-ports")]
    [AllowAnonymous]
    public async Task<IActionResult> SeedPorts()
    {
        var count = await _db.ChargingPorts.CountAsync();
        if (count > 0)
            return Ok(new { message = $"Already have {count} ports. No seeding needed." });

        var now = DateTime.UtcNow;
        _db.ChargingPorts.AddRange(
            new Domain.Entities.ChargingPort { Id = Guid.NewGuid(), PortName = "Station 1", PortNumber = 1, ConnectorType = "USB-C 30W Fast Charge", Status = ChargingPortStatus.Available, MaxPowerKw = 30, Type = ChargingPortType.Level2, UpdatedAt = now },
            new Domain.Entities.ChargingPort { Id = Guid.NewGuid(), PortName = "Station 2", PortNumber = 2, ConnectorType = "USB-C 30W Fast Charge", Status = ChargingPortStatus.Available, MaxPowerKw = 30, Type = ChargingPortType.Level2, UpdatedAt = now },
            new Domain.Entities.ChargingPort { Id = Guid.NewGuid(), PortName = "Station 3", PortNumber = 3, ConnectorType = "Lightning 20W",          Status = ChargingPortStatus.Available, MaxPowerKw = 20, Type = ChargingPortType.Level1, UpdatedAt = now },
            new Domain.Entities.ChargingPort { Id = Guid.NewGuid(), PortName = "Station 4", PortNumber = 4, ConnectorType = "Micro-USB 15W",          Status = ChargingPortStatus.Available, MaxPowerKw = 15, Type = ChargingPortType.Level1, UpdatedAt = now }
        );
        await _db.SaveChangesAsync();

        return Ok(new { message = "4 charging ports seeded successfully!", ports = 4 });
    }

    [HttpGet("ports")]
    public async Task<IActionResult> GetPorts()
    {
        var ports = await _db.ChargingPorts
            .AsNoTracking()
            .Select(p => new
            {
                p.Id,
                p.PortNumber,
                p.PortName,
                p.Status,
                p.ConnectorType,
                p.UpdatedAt
            })
            .ToListAsync();

        var result = ports.Select(p => new
        {
            portId        = p.Id.ToString(),
            portNumber    = p.PortNumber,
            portStatus    = p.Status switch
            {
                ChargingPortStatus.Available   => "Available",
                ChargingPortStatus.Occupied    => "In Use",
                ChargingPortStatus.Maintenance => "Maintenance",
                _                              => "Disabled"
            },
            connectorType = p.ConnectorType,
            updatedAt     = p.UpdatedAt?.ToString("o") ?? DateTime.UtcNow.ToString("o")
        }).ToList();

        return Ok(new { ports = result });
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static WebSessionResponse MapWebSession(
        Domain.Entities.ChargingSession s, string fullName,
        Domain.Entities.ChargingPort port)
    {
        int allocSecs      = ParseNote(s.Notes, "AllocatedSeconds");
        int durationMins   = allocSecs / 60;
        int pointsUsed     = allocSecs / SecondsPerPoint;
        string statusStr   = s.Status switch
        {
            ChargingSessionStatus.InProgress => "Active",
            ChargingSessionStatus.Completed  => "Completed",
            _                                => "Stopped"
        };

        return new WebSessionResponse
        {
            SessionId       = s.Id.ToString(),
            UserId          = s.UserId.ToString(),
            UserFullName    = fullName,
            PortId          = port?.Id.ToString() ?? "",
            PortNumber      = port?.PortNumber ?? 0,
            ConnectorType   = port?.ConnectorType ?? "",
            DurationMinutes = durationMins > 0 ? durationMins : (int?)s.EndedAt?.Subtract(s.StartedAt ?? DateTime.UtcNow).TotalMinutes ?? 0,
            PointsUsed      = pointsUsed,
            StartTime       = s.StartedAt?.ToString("o") ?? "",
            EndTime         = s.EndedAt?.ToString("o") ?? "",
            SessionStatus   = statusStr
        };
    }

    private static object MapPort(Domain.Entities.ChargingPort p) => new
    {
        portId        = p.Id.ToString(),
        portNumber    = p.PortNumber,
        portStatus    = p.Status switch
        {
            ChargingPortStatus.Available   => "Available",
            ChargingPortStatus.Occupied    => "In Use",
            ChargingPortStatus.Maintenance => "Maintenance",
            _                              => "Disabled"
        },
        connectorType = p.ConnectorType,
        updatedAt     = p.UpdatedAt?.ToString("o") ?? DateTime.UtcNow.ToString("o")
    };

    private static int ParseNote(string? notes, string key)
    {
        if (notes is null) return 0;
        var prefix = $"{key}:";
        int idx = notes.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0) return 0;
        return int.TryParse(notes[(idx + prefix.Length)..].Split(';')[0].Trim(), out int v) ? v : 0;
    }

    /// <summary>Upserts a key:value pair in the semicolon-separated Notes string.</summary>
    private static string SetNote(string notes, string key, string value)
    {
        var prefix = $"{key}:";
        int idx    = notes.IndexOf(prefix, StringComparison.Ordinal);

        if (idx < 0)
        {
            // Append new key
            return notes.TrimEnd(';') + $";{key}:{value}";
        }

        // Replace existing value
        int end   = notes.IndexOf(';', idx + prefix.Length);
        var left  = notes[..idx];
        var right = end >= 0 ? notes[end..] : string.Empty;
        return left + $"{key}:{value}" + right;
    }

    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new InvalidOperationException("User ID claim missing."));
}
