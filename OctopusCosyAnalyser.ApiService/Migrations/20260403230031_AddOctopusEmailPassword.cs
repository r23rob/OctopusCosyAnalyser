using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OctopusCosyAnalyser.ApiService.Migrations
{
    /// <inheritdoc />
    public partial class AddOctopusEmailPassword : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "OctopusAccountSettings",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OctopusPassword",
                table: "OctopusAccountSettings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "OctopusAccountSettings");

            migrationBuilder.DropColumn(
                name: "OctopusPassword",
                table: "OctopusAccountSettings");
        }
    }
}
