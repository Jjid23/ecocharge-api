using SmartEVCharging.Application.DTOs.User;

namespace SmartEVCharging.Application.Interfaces;

public interface IUserService
{
    Task<UserProfileResponse> GetProfileAsync(Guid userId);
    Task<UserProfileResponse> DepositBottlesAsync(Guid userId, DepositBottleRequest request);
    Task<UserProfileResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    Task<UserProfileResponse> AdjustPointsAsync(Guid userId, int adjustment, string reason);
}
