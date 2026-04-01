namespace OctopusCosyAnalyser.ApiService.Models;

public class HeatPumpSnapshot
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;

    // Live Performance
    public decimal? CoefficientOfPerformance { get; set; }
    public decimal? OutdoorTemperatureCelsius { get; set; }
    public decimal? HeatOutputKilowatt { get; set; }
    public decimal? PowerInputKilowatt { get; set; }

    // Lifetime Performance
    public decimal? SeasonalCoefficientOfPerformance { get; set; }
    public decimal? LifetimeHeatOutputKwh { get; set; }
    public decimal? LifetimeEnergyInputKwh { get; set; }

    // Controller Status
    public bool? ControllerConnected { get; set; }
    public decimal? PrimaryZoneSetpointCelsius { get; set; }
    public string? PrimaryZoneMode { get; set; }
    public bool? PrimaryZoneHeatDemand { get; set; }
    public decimal? PrimarySensorTemperatureCelsius { get; set; }

    // Heating Zone (first zone with zoneType == "HEAT")
    public decimal? HeatingZoneSetpointCelsius { get; set; }
    public string? HeatingZoneMode { get; set; }
    public bool? HeatingZoneHeatDemand { get; set; }

    // Room Sensor (first Cosy Pod with humidity)
    public decimal? RoomTemperatureCelsius { get; set; }
    public decimal? RoomHumidityPercentage { get; set; }
    public string? RoomSensorCode { get; set; }

    // Weather Compensation & Flow Temperature
    public bool? WeatherCompensationEnabled { get; set; }
    public decimal? WeatherCompensationMinCelsius { get; set; }
    public decimal? WeatherCompensationMaxCelsius { get; set; }
    public decimal? HeatingFlowTemperatureCelsius { get; set; }
    public decimal? HeatingFlowTempAllowableMinCelsius { get; set; }
    public decimal? HeatingFlowTempAllowableMaxCelsius { get; set; }

    // Controller State
    public string? ControllerState { get; set; }

    // Hot Water Zone (first zone with zoneType == "HOT_WATER")
    public decimal? HotWaterZoneSetpointCelsius { get; set; }
    public string? HotWaterZoneMode { get; set; }
    public bool? HotWaterZoneHeatDemand { get; set; }

    // All sensor readings as JSONB
    public string? SensorReadingsJson { get; set; }

    // Metadata
    public DateTime SnapshotTakenAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

