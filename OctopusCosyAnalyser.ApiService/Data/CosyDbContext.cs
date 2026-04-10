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
    public DbSet<HeatPumpTimeSeriesRecord> HeatPumpTimeSeriesRecords { get; set; }
    public DbSet<DailyCostRecord> DailyCostRecords { get; set; }
    public DbSet<TariffRate> TariffRates { get; set; }
    public DbSet<EnergyInterval> EnergyIntervals { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CosyDbContext).Assembly);
    }
}
