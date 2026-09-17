using SmartEVCharging.Application.DTOs.ChargingPort;

namespace SmartEVCharging.Application.Interfaces;

public interface IChargingPortService
{
    Task<IEnumerable<ChargingPortResponse>> GetAllPortsAsync();
}
