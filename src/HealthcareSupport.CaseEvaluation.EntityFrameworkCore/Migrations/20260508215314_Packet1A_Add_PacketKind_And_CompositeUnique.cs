using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Packet1A_Add_PacketKind_And_CompositeUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Kind discriminator. Backfills existing rows to 1 (Patient) so the
            // pre-existing single merged-PDF rows -- written by the legacy
            // GenerateAppointmentPacketJob -- map onto the new per-kind schema
            // as Patient packets. Future rows specify Kind explicitly via the
            // Phase 1C.6 orchestrator. PacketKind enum starts at 1 (mirroring
            // PacketGenerationStatus) so 0 would be an invalid value.
            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "AppAppointmentPackets",
                type: "int",
                nullable: false,
                defaultValue: 1);

            // Composite uniqueness so a single appointment can persist up to
            // 6 distinct rows (Patient, Doctor, 4 attorney/CE variants).
            // Auto-filter on TenantId IS NOT NULL is EF Core's default for
            // nullable composite-key columns and matches the project's
            // existing per-tenant unique-index pattern (e.g. SystemParameter).
            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentPackets_TenantId_AppointmentId_Kind",
                table: "AppAppointmentPackets",
                columns: new[] { "TenantId", "AppointmentId", "Kind" },
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppAppointmentPackets_TenantId_AppointmentId_Kind",
                table: "AppAppointmentPackets");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "AppAppointmentPackets");
        }
    }
}
