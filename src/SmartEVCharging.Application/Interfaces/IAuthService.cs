using SmartEVCharging.Application.DTOs.Auth;

namespace SmartEVCharging.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
}
