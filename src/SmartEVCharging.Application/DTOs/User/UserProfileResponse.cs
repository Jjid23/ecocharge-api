namespace SmartEVCharging.Application.DTOs.User;

/// <summary>
/// Full user profile — matches both web and Android shapes.
/// </summary>
public class UserProfileResponse
{
    public string  UserId       { get; set; } = string.Empty;
    public string  FullName     { get; set; } = string.Empty;
    public string  Username     { get; set; } = string.Empty;
    public string  Email        { get; set; } = string.Empty;
    public string? PhoneNumber  { get; set; }
    public string  Role         { get; set; } = "user";
    public int     CurrentPoints { get; set; }
    public string  CreatedAt    { get; set; } = string.Empty;
    public string  UpdatedAt    { get; set; } = string.Empty;

    // Android compat
    public int CreditsSeconds        { get; set; }
    public int BottlesDepositedTotal { get; set; }
}
