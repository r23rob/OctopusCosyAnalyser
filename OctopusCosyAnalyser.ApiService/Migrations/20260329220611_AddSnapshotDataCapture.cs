using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OctopusCosyAnalyser.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddSnapshotDataCapture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ControllerState",
                table: "HeatPumpSnapshots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HeatingFlowTempAllowableMaxCelsius",
                table: "HeatPumpSnapshots",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HeatingFlowTempAllowableMinCelsius",
                table: "HeatPumpSnapshots",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "HotWaterZoneHeatDemand",
                table: "HeatPumpSnapshots",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HotWaterZoneMode",
                table: "HeatPumpSnapshots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "HotWaterZoneSetpointCelsius",
                table: "HeatPumpSnapshots",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SensorReadingsJson",
                table: "HeatPumpSnapshots",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ControllerState",
                table: "HeatPumpSnapshots");

            migrationBuilder.DropColumn(
                name: "HeatingFlowTempAllowableMaxCelsius",
                table: "HeatPumpSnapshots");

            migrationBuilder.DropColumn(
                name: "HeatingFlowTempAllowableMinCelsius",
                table: "HeatPumpSnapshots");

            migrationBuilder.DropColumn(
                name: "HotWaterZoneHeatDemand",
                table: "HeatPumpSnapshots");

            migrationBuilder.DropColumn(
                name: "HotWaterZoneMode",
                table: "HeatPumpSnapshots");

            migrationBuilder.DropColumn(
                name: "HotWaterZoneSetpointCelsius",
                table: "HeatPumpSnapshots");

            migrationBuilder.DropColumn(
                name: "SensorReadingsJson",
                table: "HeatPumpSnapshots");
        }
    }
}
