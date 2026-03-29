using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OctopusCosyAnalyser.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddFlowTempAllowableRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HeatingFlowTempAllowableMaxCelsius",
                table: "HeatPumpSnapshots");

            migrationBuilder.DropColumn(
                name: "HeatingFlowTempAllowableMinCelsius",
                table: "HeatPumpSnapshots");
        }
    }
}
