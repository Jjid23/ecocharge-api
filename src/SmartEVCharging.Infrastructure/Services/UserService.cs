using Microsoft.EntityFrameworkCore;
using SmartEVCharging.Application.DTOs.User;
using SmartEVCharging.Application.Interfaces;
using SmartEVCharging.Infrastructure.Persistence;

namespace SmartEVCharging.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly AppDbContext _db;
    private const int SecondsPerPoint = 6; // 1 pt = 6 s, 10 pts = 1 min

    public UserService(AppDbContext db) { _db = db; }

    public async Task<UserProfileResponse> GetProfileAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");
        return Map(user);
    }

    public async Task<UserProfileResponse> DepositBottlesAsync(Guid userId, DepositBottleRequest request)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        // Points earned per bottle size (matches web frontend settings)
        int pointsPerBottle = request.BottleSize?.ToLower() switch
        {
            "small" => 5,
            "large" => 15,
            _       => 10  // medium default
        };

        int totalPoints  = pointsPerBottle * request.BottleCount;
        int earnedSeconds = totalPoints * SecondsPerPoint;

        user.CurrentPoints         += totalPoints;
        user.CreditsSeconds        += earnedSeconds;
        user.BottlesDepositedTotal += request.BottleCount;
        user.UpdatedAt              = DateTime.UtcNow;

        // Keep bin status in sync
        var bin = await _db.BinStatuses.FindAsync(1);
        if (bin is null)
        {
            bin = new Domain.Entities.BinStatus
            {
                Id                    = 1,
                TotalBottlesCollected = request.BottleCount,
                FillPercentage        = Math.Min(request.BottleCount * 5, 100),
                LastUpdatedAt         = DateTime.UtcNow
            };
            _db.BinStatuses.Add(bin);
        }
        else
        {
            bin.TotalBottlesCollected += request.BottleCount;
            bin.FillPercentage         = Math.Min(bin.TotalBottlesCollected * 5, 100);
            bin.LastUpdatedAt          = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Map(user);
    }

    public async Task<UserProfileResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        // Check username / email uniqueness
        if (!string.IsNullOrWhiteSpace(request.Username) &&
            request.Username != user.Username)
        {
            bool taken = await _db.Users.AnyAsync(u =>
                u.Username == request.Username && u.Id != userId);
            if (taken) throw new InvalidOperationException("Username already in use.");
            user.Username = request.Username.Trim().ToLower();
        }

        if (!string.IsNullOrWhiteSpace(request.Email) &&
            request.Email != user.Email)
        {
            bool taken = await _db.Users.AnyAsync(u =>
                u.Email == request.Email && u.Id != userId);
            if (taken) throw new InvalidOperationException("Email already in use.");
            user.Email = request.Email.Trim().ToLower();
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
            user.FullName = request.FullName.Trim();

        if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            user.PhoneNumber = request.PhoneNumber;

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword ?? "", user.PasswordHash))
                throw new UnauthorizedAccessException("Incorrect current password.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return Map(user);
    }

    public async Task<UserProfileResponse> AdjustPointsAsync(
        Guid userId, int adjustment, string reason)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        user.CurrentPoints  = Math.Max(0, user.CurrentPoints + adjustment);
        user.CreditsSeconds = user.CurrentPoints * SecondsPerPoint;
        user.UpdatedAt      = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Map(user);
    }

    // ---------------------------------------------------------------
    private static UserProfileResponse Map(Domain.Entities.User u) => new()
    {
        UserId                = u.Id.ToString(),
        FullName              = u.FullName,
        Username              = u.Username,
        Email                 = u.Email,
        PhoneNumber           = u.PhoneNumber,
        Role                  = u.Role.ToString().ToLower(),
        CurrentPoints         = u.CurrentPoints,
        CreatedAt             = u.CreatedAt.ToString("o"),
        UpdatedAt             = (u.UpdatedAt ?? u.CreatedAt).ToString("o"),
        CreditsSeconds        = u.CreditsSeconds,
        BottlesDepositedTotal = u.BottlesDepositedTotal
    };
}
