using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace OctopusCosyAnalyser.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyCostRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyCostRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeviceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    TotalCostPence = table.Column<double>(type: "double precision", nullable: false),
                    TotalUsageKwh = table.Column<double>(type: "double precision", nullable: false),
                    AvgUnitRatePence = table.Column<double>(type: "double precision", nullable: false),
                    StandingChargePence = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCostRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCostRecords_DeviceId_Date",
                table: "DailyCostRecords",
                columns: new[] { "DeviceId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyCostRecords");
        }
    }
}
