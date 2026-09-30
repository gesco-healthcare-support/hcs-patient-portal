using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.TenantMigrations
{
    /// <inheritdoc />
    public partial class Added_DoctorAvailabilitySlotIdentityIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AppDoctorAvailabilities_Slot_Identity",
                table: "AppDoctorAvailabilities",
                columns: new[] { "TenantId", "LocationId", "AvailableDate", "FromTime", "ToTime" },
                unique: true,
                filter: "[TenantId] IS NOT NULL AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppDoctorAvailabilities_Slot_Identity",
                table: "AppDoctorAvailabilities");
        }
    }
}
