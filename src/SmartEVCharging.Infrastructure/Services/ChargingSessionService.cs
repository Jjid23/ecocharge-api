using Microsoft.EntityFrameworkCore;
using SmartEVCharging.Application.DTOs.ChargingSession;
using SmartEVCharging.Application.Interfaces;
using SmartEVCharging.Domain.Entities;
using SmartEVCharging.Domain.Enums;
using SmartEVCharging.Infrastructure.Persistence;

namespace SmartEVCharging.Infrastructure.Services;

public class ChargingSessionService : IChargingSessionService
{
    private readonly AppDbContext _db;

    public ChargingSessionService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ChargingSessionResponse> StartSessionAsync(Guid userId, StartSessionRequest request)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        var port = await _db.ChargingPorts.FindAsync(request.ChargingPortId)
            ?? throw new KeyNotFoundException("Charging port not found.");

        // Credit calculation: earn from bottles deposited first, then spend
        int earnedSeconds = request.BottlesDeposited * 60;
        user.CreditsSeconds += earnedSeconds;
        user.BottlesDepositedTotal += request.BottlesDeposited;

        if (user.CreditsSeconds < request.RequestedSeconds)
            throw new InvalidOperationException("Insufficient credits for requested session duration.");

        user.CreditsSeconds -= request.RequestedSeconds;
        user.UpdatedAt = DateTime.UtcNow;

        // Mark port occupied while session is active
        port.Status = ChargingPortStatus.Occupied;

        var session = new ChargingSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ChargingPortId = request.ChargingPortId,
            Status = ChargingSessionStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            // Store allocated seconds in the Notes field until a dedicated column is added
            Notes = $"AllocatedSeconds:{request.RequestedSeconds}",
            CreatedAt = DateTime.UtcNow
        };

        _db.ChargingSessions.Add(session);
        await _db.SaveChangesAsync();

        return Map(session, port.PortName, user.CreditsSeconds, request.RequestedSeconds);
    }

    public async Task<ChargingSessionResponse> EndSessionAsync(Guid userId, Guid sessionId)
    {
        var session = await _db.ChargingSessions
            .Include(s => s.ChargingPort)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId)
            ?? throw new KeyNotFoundException("Session not found.");

        session.Status = ChargingSessionStatus.Completed;
        session.EndedAt = DateTime.UtcNow;
        session.ChargingPort.Status = ChargingPortStatus.Available;

        var user = await _db.Users.FindAsync(userId);
        await _db.SaveChangesAsync();

        int allocatedSeconds = ParseAllocatedSeconds(session.Notes);
        return Map(session, session.ChargingPort.PortName, user?.CreditsSeconds ?? 0, allocatedSeconds);
    }

    public async Task<IEnumerable<ChargingSessionResponse>> GetSessionHistoryAsync(Guid userId)
    {
        var sessions = await _db.ChargingSessions
            .AsNoTracking()
            .Include(s => s.ChargingPort)
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var user = await _db.Users.FindAsync(userId);
        int currentCredits = user?.CreditsSeconds ?? 0;

        return sessions.Select(s =>
            Map(s, s.ChargingPort.PortName, currentCredits, ParseAllocatedSeconds(s.Notes)));
    }

    // ---------------------------------------------------------------
    private static ChargingSessionResponse Map(
        ChargingSession s, string portName, int remainingCredits, int allocatedSeconds) => new()
    {
        Id = s.Id,
        PortName = portName,
        Status = s.Status.ToString(),
        StartedAt = s.StartedAt,
        EndedAt = s.EndedAt,
        AllocatedSeconds = allocatedSeconds,
        RemainingCreditsSeconds = remainingCredits
    };

    private static int ParseAllocatedSeconds(string? notes)
    {
        if (notes is null) return 0;
        var prefix = "AllocatedSeconds:";
        int idx = notes.IndexOf(prefix, StringComparison.Ordinal);
        if (idx < 0) return 0;
        return int.TryParse(notes[(idx + prefix.Length)..].Split(';')[0].Trim(), out int v) ? v : 0;
    }
}
