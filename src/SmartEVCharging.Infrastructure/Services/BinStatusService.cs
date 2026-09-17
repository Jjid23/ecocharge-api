using SmartEVCharging.Application.DTOs.BinStatus;
using SmartEVCharging.Application.Interfaces;
using SmartEVCharging.Domain.Entities;
using SmartEVCharging.Infrastructure.Persistence;

namespace SmartEVCharging.Infrastructure.Services;

public class BinStatusService : IBinStatusService
{
    private readonly AppDbContext _db;

    public BinStatusService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<BinStatusResponse> GetBinStatusAsync()
    {
        var bin = await _db.BinStatuses.FindAsync(1);
        if (bin is null)
            return new BinStatusResponse { FillPercentage = 0, TotalBottlesCollected = 0, LastUpdatedAt = DateTime.UtcNow };

        return Map(bin);
    }

    public async Task<BinStatusResponse> UpdateBinStatusAsync(UpdateBinStatusRequest request)
    {
        var bin = await _db.BinStatuses.FindAsync(1);
        if (bin is null)
        {
            bin = new BinStatus
            {
                Id = 1,
                FillPercentage = request.FillPercentage,
                TotalBottlesCollected = request.TotalBottlesCollected,
                LastUpdatedAt = DateTime.UtcNow
            };
            _db.BinStatuses.Add(bin);
        }
        else
        {
            bin.FillPercentage = request.FillPercentage;
            bin.TotalBottlesCollected = request.TotalBottlesCollected;
            bin.LastUpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return Map(bin);
    }

    // ---------------------------------------------------------------
    private static BinStatusResponse Map(BinStatus bin) => new()
    {
        FillPercentage = bin.FillPercentage,
        TotalBottlesCollected = bin.TotalBottlesCollected,
        LastUpdatedAt = bin.LastUpdatedAt
    };
}
