using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartEVCharging.Domain.Entities;

namespace SmartEVCharging.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(al => al.Id);

        builder.Property(al => al.UserId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(al => al.Action)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(al => al.EntityName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(al => al.EntityId)
            .HasMaxLength(50);

        builder.Property(al => al.Changes)
            .HasMaxLength(500);

        builder.Property(al => al.IpAddress)
            .HasMaxLength(100);

        builder.Property(al => al.CreatedAt)
            .IsRequired();

        builder.HasIndex(al => new { al.EntityName, al.EntityId });
        builder.HasIndex(al => al.CreatedAt);
    }
}
