using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OctopusCosyAnalyser.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyAccountNumberUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OctopusAccountSettings_AccountNumber",
                table: "OctopusAccountSettings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_OctopusAccountSettings_AccountNumber",
                table: "OctopusAccountSettings",
                column: "AccountNumber",
                unique: true);
        }
    }
}
