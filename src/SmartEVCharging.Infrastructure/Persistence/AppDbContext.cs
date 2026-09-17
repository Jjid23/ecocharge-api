using Microsoft.EntityFrameworkCore;
using SmartEVCharging.Domain.Entities;
using SmartEVCharging.Infrastructure.Persistence.Configurations;

namespace SmartEVCharging.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<ChargingPort> ChargingPorts { get; set; }
    public DbSet<ChargingSession> ChargingSessions { get; set; }
    public DbSet<Device> Devices { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }
    public DbSet<BinStatus> BinStatuses { get; set; }
    public DbSet<SensorReading> SensorReadings { get; set; }
    public DbSet<RelayCommand> RelayCommands { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply configurations
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new ChargingPortConfiguration());
        modelBuilder.ApplyConfiguration(new ChargingSessionConfiguration());
        modelBuilder.ApplyConfiguration(new DeviceConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationConfiguration());
        modelBuilder.ApplyConfiguration(new AuditLogConfiguration());
    }
}
