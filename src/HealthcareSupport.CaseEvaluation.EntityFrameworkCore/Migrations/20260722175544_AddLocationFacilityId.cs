using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationFacilityId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FacilityId",
                table: "AppLocations",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_AppLocations_FacilityId",
                table: "AppLocations",
                column: "FacilityId",
                unique: true,
                filter: "[FacilityId] <> ''");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppLocations_FacilityId",
                table: "AppLocations");

            migrationBuilder.DropColumn(
                name: "FacilityId",
                table: "AppLocations");
        }
    }
}
