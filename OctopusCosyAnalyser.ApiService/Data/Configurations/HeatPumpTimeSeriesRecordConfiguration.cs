using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Data.Configurations;

public class HeatPumpTimeSeriesRecordConfiguration : IEntityTypeConfiguration<HeatPumpTimeSeriesRecord>
{
    public void Configure(EntityTypeBuilder<HeatPumpTimeSeriesRecord> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.DeviceId, e.StartAt }).IsUnique();
        entity.Property(e => e.DeviceId).IsRequired().HasMaxLength(100);
        entity.Property(e => e.StartAt).IsRequired();
        entity.Property(e => e.EndAt).IsRequired();
        entity.Property(e => e.EnergyInputKwh).HasPrecision(18, 6);
        entity.Property(e => e.EnergyOutputKwh).HasPrecision(18, 6);
        entity.Property(e => e.OutdoorTemperatureCelsius).HasPrecision(10, 2);
    }
}
