using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartEVCharging.Domain.Entities;

namespace SmartEVCharging.Infrastructure.Persistence.Configurations;

public class ChargingSessionConfiguration : IEntityTypeConfiguration<ChargingSession>
{
    public void Configure(EntityTypeBuilder<ChargingSession> builder)
    {
        builder.ToTable("ChargingSessions");

        builder.HasKey(cs => cs.Id);

        builder.Property(cs => cs.UserId)
            .IsRequired();

        builder.Property(cs => cs.ChargingPortId)
            .IsRequired();

        builder.Property(cs => cs.Status)
            .IsRequired();

        builder.Property(cs => cs.StartedAt);

        builder.Property(cs => cs.EndedAt);

        builder.Property(cs => cs.EnergyDeliveredKwh)
            .IsRequired()
            .HasPrecision(10, 2);

        builder.Property(cs => cs.Cost)
            .IsRequired()
            .HasPrecision(10, 2);

        builder.Property(cs => cs.Notes)
            .HasMaxLength(500);

        builder.Property(cs => cs.CreatedAt)
            .IsRequired();

        builder.HasOne(cs => cs.User)
            .WithMany(u => u.ChargingSessions)
            .HasForeignKey(cs => cs.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cs => cs.ChargingPort)
            .WithMany(cp => cp.ChargingSessions)
            .HasForeignKey(cs => cs.ChargingPortId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
