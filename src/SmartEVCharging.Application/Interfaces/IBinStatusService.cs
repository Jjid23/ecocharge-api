using SmartEVCharging.Application.DTOs.BinStatus;

namespace SmartEVCharging.Application.Interfaces;

public interface IBinStatusService
{
    Task<BinStatusResponse> GetBinStatusAsync();
    Task<BinStatusResponse> UpdateBinStatusAsync(UpdateBinStatusRequest request);
}
