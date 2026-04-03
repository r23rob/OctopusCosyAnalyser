using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OctopusCosyAnalyser.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddFlowTempMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FlowTempMode",
                table: "HeatPumpSnapshots",
                type: "text",
                nullable: true);

            // Backfill FlowTempMode from the old WeatherCompensationEnabled bool
            migrationBuilder.Sql("""
                UPDATE "HeatPumpSnapshots"
                SET "FlowTempMode" = CASE
                    WHEN "WeatherCompensationEnabled" = true  THEN 'WeatherCompensation'
                    WHEN "WeatherCompensationEnabled" = false THEN 'FixedFlow'
                    ELSE NULL
                END;
                """);

            // Null out WC curve range for FixedFlow rows — not meaningful in that mode
            migrationBuilder.Sql("""
                UPDATE "HeatPumpSnapshots"
                SET "WeatherCompensationMinCelsius" = NULL,
                    "WeatherCompensationMaxCelsius" = NULL
                WHERE "FlowTempMode" = 'FixedFlow';
                """);

            // Null out fixed flow setpoint for WeatherCompensation rows — not meaningful in that mode
            migrationBuilder.Sql("""
                UPDATE "HeatPumpSnapshots"
                SET "HeatingFlowTemperatureCelsius" = NULL
                WHERE "FlowTempMode" = 'WeatherCompensation';
                """);

            migrationBuilder.DropColumn(
                name: "WeatherCompensationEnabled",
                table: "HeatPumpSnapshots");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "WeatherCompensationEnabled",
                table: "HeatPumpSnapshots",
                type: "boolean",
                nullable: true);

            // Backfill WeatherCompensationEnabled from FlowTempMode
            migrationBuilder.Sql("""
                UPDATE "HeatPumpSnapshots"
                SET "WeatherCompensationEnabled" = CASE
                    WHEN "FlowTempMode" = 'WeatherCompensation' THEN true
                    WHEN "FlowTempMode" = 'FixedFlow'           THEN false
                    ELSE NULL
                END;
                """);

            migrationBuilder.DropColumn(
                name: "FlowTempMode",
                table: "HeatPumpSnapshots");
        }
    }
}
