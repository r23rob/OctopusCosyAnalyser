using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Data.Configurations;

public class TariffRateConfiguration : IEntityTypeConfiguration<TariffRate>
{
    public void Configure(EntityTypeBuilder<TariffRate> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => new { e.DeviceId, e.ValidFrom }).IsUnique();
        entity.Property(e => e.DeviceId).IsRequired().HasMaxLength(100);
        entity.Property(e => e.ValidFrom).IsRequired();
        entity.Property(e => e.UnitRatePence).HasPrecision(18, 6);
    }
}
