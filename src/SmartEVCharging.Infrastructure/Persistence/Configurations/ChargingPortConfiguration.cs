using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartEVCharging.Domain.Entities;

namespace SmartEVCharging.Infrastructure.Persistence.Configurations;

public class ChargingPortConfiguration : IEntityTypeConfiguration<ChargingPort>
{
    public void Configure(EntityTypeBuilder<ChargingPort> builder)
    {
        builder.ToTable("ChargingPorts");

        builder.HasKey(cp => cp.Id);

        builder.Property(cp => cp.PortName)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(cp => cp.Status)
            .IsRequired();

        builder.Property(cp => cp.MaxPowerKw)
            .IsRequired()
            .HasPrecision(10, 2);

        builder.Property(cp => cp.CurrentPowerKw)
            .HasPrecision(10, 2);

        builder.Property(cp => cp.Type)
            .IsRequired();

        builder.Property(cp => cp.DeviceId)
            .IsRequired(false);  // nullable — ports don't always have a device

        builder.HasOne(cp => cp.Device)
            .WithMany(d => d.ChargingPorts)
            .HasForeignKey(cp => cp.DeviceId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(cp => cp.ChargingSessions)
            .WithOne(s => s.ChargingPort)
            .HasForeignKey(s => s.ChargingPortId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
