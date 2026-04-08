using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Data.Configurations;

public class HeatPumpDeviceConfiguration : IEntityTypeConfiguration<HeatPumpDevice>
{
    public void Configure(EntityTypeBuilder<HeatPumpDevice> entity)
    {
        entity.HasKey(e => e.Id);
        entity.HasIndex(e => e.DeviceId).IsUnique();
        entity.Property(e => e.AccountNumber).IsRequired().HasMaxLength(100);
        entity.Property(e => e.DeviceId).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Euid).HasMaxLength(100);
    }
}
