using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OctopusCosyAnalyser.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class RemoveEfficiencyTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HeatPumpEfficiencyRecords");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HeatPumpEfficiencyRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ChangeActive = table.Column<bool>(type: "boolean", nullable: false),
                    ChangeDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ComfortScore = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ElectricityKWh = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false),
                    IndoorAvgC = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OutdoorAvgC = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    OutdoorHighC = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    OutdoorLowC = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeatPumpEfficiencyRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HeatPumpEfficiencyRecords_Date",
                table: "HeatPumpEfficiencyRecords",
                column: "Date",
                unique: true);
        }
    }
}
