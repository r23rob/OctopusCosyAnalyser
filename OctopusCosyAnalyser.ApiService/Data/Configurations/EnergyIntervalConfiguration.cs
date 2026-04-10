using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Data.Configurations;

public class EnergyIntervalConfiguration : IEntityTypeConfiguration<EnergyInterval>
{
    public void Configure(EntityTypeBuilder<EnergyInterval> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.DeviceId, e.IntervalStart }).IsUnique();
        entity.Property(e => e.DeviceId).IsRequired().HasMaxLength(100);
        entity.Property(e => e.IntervalStart).IsRequired();
        entity.Property(e => e.IntervalEnd).IsRequired();

        // Consumption
        entity.Property(e => e.ConsumptionKwh).HasPrecision(18, 6);
        entity.Property(e => e.DemandW).HasPrecision(18, 2);

        // Heat pump
        entity.Property(e => e.HeatOutputKwh).HasPrecision(18, 6);
        entity.Property(e => e.AvgCop).HasPrecision(10, 2);
        entity.Property(e => e.AvgPowerInputKw).HasPrecision(10, 3);
        entity.Property(e => e.AvgOutdoorTempC).HasPrecision(10, 2);
        entity.Property(e => e.AvgRoomTempC).HasPrecision(10, 2);
        entity.Property(e => e.AvgFlowTempC).HasPrecision(10, 2);

        // Tariff
        entity.Property(e => e.UnitRatePencePerKwh).HasPrecision(18, 6);
        entity.Property(e => e.StandingChargePence).HasPrecision(18, 4);

        // Derived
        entity.Property(e => e.CostPence).HasPrecision(18, 4);
    }
}
