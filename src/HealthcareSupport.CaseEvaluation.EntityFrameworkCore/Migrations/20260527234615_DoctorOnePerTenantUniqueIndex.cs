using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class DoctorOnePerTenantUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppDoctors_TenantId",
                table: "AppDoctors");

            migrationBuilder.CreateIndex(
                name: "IX_AppEntity_Doctors_TenantId_Unique",
                table: "AppDoctors",
                column: "TenantId",
                unique: true,
                filter: "[TenantId] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppEntity_Doctors_TenantId_Unique",
                table: "AppDoctors");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoctors_TenantId",
                table: "AppDoctors",
                column: "TenantId");
        }
    }
}
