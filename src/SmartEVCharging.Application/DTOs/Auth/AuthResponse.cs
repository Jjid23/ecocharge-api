namespace SmartEVCharging.Application.DTOs.Auth;

/// <summary>
/// Returned by login and register.
/// Shape matches what the ecocharge-web frontend expects.
/// </summary>
public class AuthResponse
{
    // --- Web frontend fields ---
    public string Token        { get; set; } = string.Empty;
    public string UserId       { get; set; } = string.Empty;
    public string FullName     { get; set; } = string.Empty;
    public string Username     { get; set; } = string.Empty;
    public string Email        { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role         { get; set; } = "user";
    public int    CurrentPoints { get; set; }
    public string CreatedAt    { get; set; } = string.Empty;
    public string UpdatedAt    { get; set; } = string.Empty;

    // --- Android app fields (kept for backwards compat) ---
    public int CreditsSeconds       { get; set; }
    public int BottlesDepositedTotal { get; set; }
}
