using Microsoft.EntityFrameworkCore;
using SmartEVCharging.Application.DTOs.Auth;
using SmartEVCharging.Application.Interfaces;
using SmartEVCharging.Domain.Entities;
using SmartEVCharging.Domain.Enums;
using SmartEVCharging.Infrastructure.Persistence;

namespace SmartEVCharging.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IJwtService  _jwt;

    // Points-to-seconds ratio: 1 point = 6 seconds (10 pts = 1 minute)
    private const int SecondsPerPoint = 6;

    public AuthService(AppDbContext db, IJwtService jwt)
    {
        _db  = db;
        _jwt = jwt;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        bool exists = await _db.Users.AnyAsync(u =>
            u.Username == request.Username || u.Email == request.Email);

        if (exists)
            throw new InvalidOperationException("Username or email already in use.");

        // Welcome bonus: 20 points = 120 seconds
        const int welcomePoints  = 20;
        const int welcomeSeconds = welcomePoints * SecondsPerPoint;

        var user = new User
        {
            Id                    = Guid.NewGuid(),
            FullName              = request.FullName?.Trim() ?? request.Username,
            Username              = request.Username,
            Email                 = request.Email,
            PhoneNumber           = request.PhoneNumber,
            PasswordHash          = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role                  = UserRole.User,
            CurrentPoints         = welcomePoints,
            CreditsSeconds        = welcomeSeconds,
            BottlesDepositedTotal = 0,
            IsActive              = true,
            CreatedAt             = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return BuildResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        // Support login by username or email (web frontend sends identifier field)
        var user = await _db.Users.FirstOrDefaultAsync(u =>
            (u.Username == request.Username || u.Email == request.Username) && u.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid username/email or password.");

        return BuildResponse(user);
    }

    // ---------------------------------------------------------------
    private AuthResponse BuildResponse(User user) => new()
    {
        Token                 = _jwt.GenerateToken(user),
        UserId                = user.Id.ToString(),
        FullName              = user.FullName,
        Username              = user.Username,
        Email                 = user.Email,
        PhoneNumber           = user.PhoneNumber,
        Role                  = user.Role.ToString().ToLower(),
        CurrentPoints         = user.CurrentPoints,
        CreatedAt             = user.CreatedAt.ToString("o"),
        UpdatedAt             = (user.UpdatedAt ?? user.CreatedAt).ToString("o"),
        CreditsSeconds        = user.CreditsSeconds,
        BottlesDepositedTotal = user.BottlesDepositedTotal
    };
}
