using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Packet_FilteredUniqueIndex_SoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppAppointmentPackets_TenantId_AppointmentId_Kind",
                table: "AppAppointmentPackets");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentPackets_TenantId_AppointmentId_Kind",
                table: "AppAppointmentPackets",
                columns: new[] { "TenantId", "AppointmentId", "Kind" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [TenantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppAppointmentPackets_TenantId_AppointmentId_Kind",
                table: "AppAppointmentPackets");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentPackets_TenantId_AppointmentId_Kind",
                table: "AppAppointmentPackets",
                columns: new[] { "TenantId", "AppointmentId", "Kind" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }
    }
}
