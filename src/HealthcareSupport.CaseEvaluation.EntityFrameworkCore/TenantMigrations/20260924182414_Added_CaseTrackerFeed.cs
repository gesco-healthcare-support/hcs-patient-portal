using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.TenantMigrations
{
    /// <inheritdoc />
    public partial class Added_CaseTrackerFeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "ChangeVersion",
                table: "AppIntegrationOutboxItems",
                type: "rowversion",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AppCaseTrackerFeedStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    FloorPosition = table.Column<long>(type: "bigint", nullable: false),
                    AcknowledgedPosition = table.Column<long>(type: "bigint", nullable: false),
                    HighestIssuedPosition = table.Column<long>(type: "bigint", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StoppedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastRequestAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastAdvancedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SilenceAlertedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    StallAlertedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CursorAheadAlertedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppCaseTrackerFeedStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppIntegrationOutboxItems_TenantId_Status_ChangeVersion",
                table: "AppIntegrationOutboxItems",
                columns: new[] { "TenantId", "Status", "ChangeVersion" });

            migrationBuilder.CreateIndex(
                name: "IX_AppCaseTrackerFeedStates_TenantId",
                table: "AppCaseTrackerFeedStates",
                column: "TenantId",
                unique: true,
                filter: "[TenantId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppCaseTrackerFeedStates");

            migrationBuilder.DropIndex(
                name: "IX_AppIntegrationOutboxItems_TenantId_Status_ChangeVersion",
                table: "AppIntegrationOutboxItems");

            migrationBuilder.DropColumn(
                name: "ChangeVersion",
                table: "AppIntegrationOutboxItems");
        }
    }
}
