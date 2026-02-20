using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OctopusCosyAnalyser.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConsumptionReadings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ReadAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Consumption = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    ConsumptionDelta = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    Demand = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsumptionReadings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HeatPumpDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MeterSerialNumber = table.Column<string>(type: "text", nullable: true),
                    Mpan = table.Column<string>(type: "text", nullable: true),
                    Euid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PropertyId = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSyncAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeatPumpDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HeatPumpEfficiencyRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ElectricityKWh = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    OutdoorAvgC = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    OutdoorHighC = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    OutdoorLowC = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    IndoorAvgC = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    ComfortScore = table.Column<int>(type: "integer", nullable: true),
                    ChangeActive = table.Column<bool>(type: "boolean", nullable: false),
                    ChangeDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeatPumpEfficiencyRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HeatPumpSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AccountNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CoefficientOfPerformance = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    OutdoorTemperatureCelsius = table.Column<decimal>(type: "numeric", nullable: true),
                    HeatOutputKilowatt = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    PowerInputKilowatt = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    SeasonalCoefficientOfPerformance = table.Column<decimal>(type: "numeric", nullable: true),
                    LifetimeHeatOutputKwh = table.Column<decimal>(type: "numeric", nullable: true),
                    LifetimeEnergyInputKwh = table.Column<decimal>(type: "numeric", nullable: true),
                    ControllerConnected = table.Column<bool>(type: "boolean", nullable: true),
                    PrimaryZoneSetpointCelsius = table.Column<decimal>(type: "numeric", nullable: true),
                    PrimaryZoneMode = table.Column<string>(type: "text", nullable: true),
                    PrimaryZoneHeatDemand = table.Column<bool>(type: "boolean", nullable: true),
                    PrimarySensorTemperatureCelsius = table.Column<decimal>(type: "numeric", nullable: true),
                    HeatingZoneSetpointCelsius = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    HeatingZoneMode = table.Column<string>(type: "text", nullable: true),
                    HeatingZoneHeatDemand = table.Column<bool>(type: "boolean", nullable: true),
                    RoomTemperatureCelsius = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    RoomHumidityPercentage = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    RoomSensorCode = table.Column<string>(type: "text", nullable: true),
                    WeatherCompensationEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    WeatherCompensationMinCelsius = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    WeatherCompensationMaxCelsius = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    HeatingFlowTemperatureCelsius = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    SnapshotTakenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeatPumpSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OctopusAccountSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccountNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ApiKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OctopusAccountSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TadoSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Password = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    HomeId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TadoSettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConsumptionReadings_DeviceId_ReadAt",
                table: "ConsumptionReadings",
                columns: new[] { "DeviceId", "ReadAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HeatPumpDevices_DeviceId",
                table: "HeatPumpDevices",
                column: "DeviceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HeatPumpEfficiencyRecords_Date",
                table: "HeatPumpEfficiencyRecords",
                column: "Date",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HeatPumpSnapshots_DeviceId_SnapshotTakenAt",
                table: "HeatPumpSnapshots",
                columns: new[] { "DeviceId", "SnapshotTakenAt" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OctopusAccountSettings_AccountNumber",
                table: "OctopusAccountSettings",
                column: "AccountNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConsumptionReadings");

            migrationBuilder.DropTable(
                name: "HeatPumpDevices");

            migrationBuilder.DropTable(
                name: "HeatPumpEfficiencyRecords");

            migrationBuilder.DropTable(
                name: "HeatPumpSnapshots");

            migrationBuilder.DropTable(
                name: "OctopusAccountSettings");

            migrationBuilder.DropTable(
                name: "TadoSettings");
        }
    }
}
