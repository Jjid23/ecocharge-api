using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartEVCharging.Domain.Entities;

namespace SmartEVCharging.Infrastructure.Persistence.Configurations;

public class DeviceConfiguration : IEntityTypeConfiguration<Device>
{
    public void Configure(EntityTypeBuilder<Device> builder)
    {
        builder.ToTable("Devices");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.SerialNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.Type)
            .IsRequired();

        builder.Property(d => d.Status)
            .IsRequired();

        builder.Property(d => d.Location)
            .HasMaxLength(500);

        builder.Property(d => d.OwnerId)
            .IsRequired();

        builder.Property(d => d.LastHeartbeat)
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.Property(d => d.UpdatedAt);

        builder.HasIndex(d => d.SerialNumber).IsUnique();

        builder.HasOne(d => d.Owner)
            .WithMany(u => u.Devices)
            .HasForeignKey(d => d.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(d => d.ChargingPorts)
            .WithOne(cp => cp.Device)
            .HasForeignKey(cp => cp.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
