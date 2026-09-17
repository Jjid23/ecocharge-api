using Microsoft.EntityFrameworkCore;
using SmartEVCharging.Application.DTOs.ChargingPort;
using SmartEVCharging.Application.Interfaces;
using SmartEVCharging.Infrastructure.Persistence;

namespace SmartEVCharging.Infrastructure.Services;

public class ChargingPortService : IChargingPortService
{
    private readonly AppDbContext _db;

    public ChargingPortService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<ChargingPortResponse>> GetAllPortsAsync()
    {
        var ports = await _db.ChargingPorts.AsNoTracking().ToListAsync();

        return ports.Select(p => new ChargingPortResponse
        {
            Id = p.Id,
            PortName = p.PortName,
            Status = p.Status.ToString(),
            MaxPowerKw = p.MaxPowerKw,
            CurrentPowerKw = p.CurrentPowerKw,
            Type = p.Type.ToString()
        });
    }
}
