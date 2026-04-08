using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Data.Configurations;

public class DailyCostRecordConfiguration : IEntityTypeConfiguration<DailyCostRecord>
{
    public void Configure(EntityTypeBuilder<DailyCostRecord> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.DeviceId, e.Date }).IsUnique();
        entity.Property(e => e.DeviceId).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Date).IsRequired();
        entity.Property(e => e.TotalCostPence).HasPrecision(18, 4);
        entity.Property(e => e.TotalUsageKwh).HasPrecision(18, 6);
        entity.Property(e => e.AvgUnitRatePence).HasPrecision(18, 4);
        entity.Property(e => e.StandingChargePence).HasPrecision(18, 4);
    }
}
