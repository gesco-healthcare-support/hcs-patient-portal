using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Backfill_Lookup_IsSystem : Migration
    {
        // Prompt 15 follow-up (F2): the IsSystem columns were added (default 0)
        // AFTER these canonical rows were seeded, and the idempotent seeders skip
        // existing rows -- so pre-existing English / California / AME stayed 0 and
        // showed no "System" chip in the Configuration hub. Backfill them by their
        // stable CaseEvaluationSeedIds GUIDs. Idempotent (safe to re-run); the
        // newly-seeded statuses + the GeneratedPacket doc-type are already flagged.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [AppStates] SET [IsSystem] = 1 WHERE [Id] = 'a0a00001-0000-4000-9000-00000000ca00';");
            migrationBuilder.Sql(
                "UPDATE [AppAppointmentTypes] SET [IsSystem] = 1 WHERE [Id] = 'a0a00002-0000-4000-9000-000000000003';");
            migrationBuilder.Sql(
                "UPDATE [AppAppointmentLanguages] SET [IsSystem] = 1 WHERE [Id] = 'a0a00003-0000-4000-9000-000000000001';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [AppStates] SET [IsSystem] = 0 WHERE [Id] = 'a0a00001-0000-4000-9000-00000000ca00';");
            migrationBuilder.Sql(
                "UPDATE [AppAppointmentTypes] SET [IsSystem] = 0 WHERE [Id] = 'a0a00002-0000-4000-9000-000000000003';");
            migrationBuilder.Sql(
                "UPDATE [AppAppointmentLanguages] SET [IsSystem] = 0 WHERE [Id] = 'a0a00003-0000-4000-9000-000000000001';");
        }
    }
}
