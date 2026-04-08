using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Data.Configurations;

public class ConsumptionReadingConfiguration : IEntityTypeConfiguration<ConsumptionReading>
{
    public void Configure(EntityTypeBuilder<ConsumptionReading> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.DeviceId, e.ReadAt }).IsUnique();
        entity.Property(e => e.DeviceId).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Consumption).HasPrecision(18, 6);
        entity.Property(e => e.ConsumptionDelta).HasPrecision(18, 6);
        entity.Property(e => e.Demand).HasPrecision(18, 2);
    }
}
