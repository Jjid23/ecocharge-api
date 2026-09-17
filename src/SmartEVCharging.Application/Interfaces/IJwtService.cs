using SmartEVCharging.Domain.Entities;

namespace SmartEVCharging.Application.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
}
