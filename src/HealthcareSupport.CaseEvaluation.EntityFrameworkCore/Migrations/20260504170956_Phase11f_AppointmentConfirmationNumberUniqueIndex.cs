using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Phase11f_AppointmentConfirmationNumberUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_AppEntity_Appointments_TenantId_RequestConfirmationNumber",
                table: "AppAppointments",
                columns: new[] { "TenantId", "RequestConfirmationNumber" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppEntity_Appointments_TenantId_RequestConfirmationNumber",
                table: "AppAppointments");
        }
    }
}
