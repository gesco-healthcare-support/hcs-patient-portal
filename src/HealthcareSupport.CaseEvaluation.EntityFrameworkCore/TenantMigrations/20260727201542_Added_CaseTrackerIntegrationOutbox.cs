using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.TenantMigrations
{
    /// <inheritdoc />
    public partial class Added_CaseTrackerIntegrationOutbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue 1 == EvaluationKind.Evaluation, hand-corrected from EF's generated 0.
            // Zero is NOT a valid EvaluationKind (the enum starts at 1 to avoid the default(int)
            // trap), so the generated default would have backfilled every existing appointment with
            // an unmappable value. 1 is exact here: production has no re-evaluations.
            migrationBuilder.AddColumn<int>(
                name: "EvaluationKind",
                table: "AppAppointments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "AppIntegrationOutboxItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageType = table.Column<int>(type: "int", nullable: false),
                    TargetPath = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    MaxAttempts = table.Column<int>(type: "int", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedUntil = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppIntegrationOutboxItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppIntegrationOutboxItems_TenantId_AppointmentId",
                table: "AppIntegrationOutboxItems",
                columns: new[] { "TenantId", "AppointmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppIntegrationOutboxItems_TenantId_IdempotencyKey",
                table: "AppIntegrationOutboxItems",
                columns: new[] { "TenantId", "IdempotencyKey" },
                unique: true,
                filter: "[IsDeleted] = 0 AND [TenantId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppIntegrationOutboxItems_TenantId_Status_NextAttemptAt",
                table: "AppIntegrationOutboxItems",
                columns: new[] { "TenantId", "Status", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppIntegrationOutboxItems");

            migrationBuilder.DropColumn(
                name: "EvaluationKind",
                table: "AppAppointments");
        }
    }
}
