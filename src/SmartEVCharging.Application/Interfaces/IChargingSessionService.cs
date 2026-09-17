using SmartEVCharging.Application.DTOs.ChargingSession;

namespace SmartEVCharging.Application.Interfaces;

public interface IChargingSessionService
{
    Task<ChargingSessionResponse> StartSessionAsync(Guid userId, StartSessionRequest request);
    Task<ChargingSessionResponse> EndSessionAsync(Guid userId, Guid sessionId);
    Task<IEnumerable<ChargingSessionResponse>> GetSessionHistoryAsync(Guid userId);
}
