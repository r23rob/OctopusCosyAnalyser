using Microsoft.EntityFrameworkCore;
using OctopusCosyAnalyser.ApiService.Models;

namespace OctopusCosyAnalyser.ApiService.Data;

public class CosyDbContext : DbContext
{
    public CosyDbContext(DbContextOptions<CosyDbContext> options) : base(options)
    {
    }

    public DbSet<HeatPumpDevice> HeatPumpDevices { get; set; }
    public DbSet<ConsumptionReading> ConsumptionReadings { get; set; }
    public DbSet<OctopusAccountSettings> OctopusAccountSettings { get; set; }
    public DbSet<HeatPumpSnapshot> HeatPumpSnapshots { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<HeatPumpDevice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.DeviceId).IsUnique();
            entity.Property(e => e.AccountNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DeviceId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Euid).HasMaxLength(100);
        });

        modelBuilder.Entity<ConsumptionReading>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.DeviceId, e.ReadAt }).IsUnique();
            entity.Property(e => e.DeviceId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Consumption).HasPrecision(18, 6);
            entity.Property(e => e.ConsumptionDelta).HasPrecision(18, 6);
            entity.Property(e => e.Demand).HasPrecision(18, 2);
        });

        modelBuilder.Entity<OctopusAccountSettings>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AccountNumber).IsUnique();
            entity.Property(e => e.AccountNumber).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ApiKey).IsRequired().HasMaxLength(200);
        });

        modelBuilder.Entity<HeatPumpSnapshot>(entity =>
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
        });
    }
}
