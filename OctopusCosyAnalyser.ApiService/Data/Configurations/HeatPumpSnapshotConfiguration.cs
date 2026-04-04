using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Data.Configurations;

public class HeatPumpSnapshotConfiguration : IEntityTypeConfiguration<HeatPumpSnapshot>
{
    public void Configure(EntityTypeBuilder<HeatPumpSnapshot> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.DeviceId, e.SnapshotTakenAt }).IsUnique();
        entity.Property(e => e.DeviceId).IsRequired().HasMaxLength(100);
        entity.Property(e => e.AccountNumber).IsRequired().HasMaxLength(100);
        entity.Property(e => e.SnapshotTakenAt).IsRequired();
        entity.Property(e => e.CoefficientOfPerformance).HasPrecision(10, 2);
        entity.Property(e => e.HeatOutputKilowatt).HasPrecision(10, 3);
        entity.Property(e => e.PowerInputKilowatt).HasPrecision(10, 3);
        entity.Property(e => e.WeatherCompensationMinCelsius).HasPrecision(10, 2);
        entity.Property(e => e.WeatherCompensationMaxCelsius).HasPrecision(10, 2);
        entity.Property(e => e.HeatingFlowTemperatureCelsius).HasPrecision(10, 2);
        entity.Property(e => e.HeatingFlowTempAllowableMinCelsius).HasPrecision(10, 2);
        entity.Property(e => e.HeatingFlowTempAllowableMaxCelsius).HasPrecision(10, 2);
        entity.Property(e => e.HotWaterZoneSetpointCelsius).HasPrecision(10, 2);
        entity.Property(e => e.SensorReadingsJson).HasColumnType("jsonb");
        entity.Property(e => e.HeatingZoneSetpointCelsius).HasPrecision(10, 2);
        entity.Property(e => e.RoomTemperatureCelsius).HasPrecision(10, 2);
        entity.Property(e => e.RoomHumidityPercentage).HasPrecision(10, 2);
    }
}
