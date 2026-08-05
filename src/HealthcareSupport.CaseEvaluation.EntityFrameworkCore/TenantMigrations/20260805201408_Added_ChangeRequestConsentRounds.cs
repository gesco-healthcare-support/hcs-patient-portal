using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.TenantMigrations
{
    /// <inheritdoc />
    public partial class Added_ChangeRequestConsentRounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppChangeRequestConsentRounds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentChangeRequestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoundNumber = table.Column<int>(type: "int", nullable: false),
                    ProposedDoctorAvailabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProposedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProposedReason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SendAttempts = table.Column<int>(type: "int", nullable: false),
                    SupersededAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SideAConsentStatus = table.Column<int>(type: "int", nullable: false),
                    SideAConsentTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SideAConsentExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SideAConsentRespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SideAConsentRespondedByEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SideBConsentStatus = table.Column<int>(type: "int", nullable: false),
                    SideBConsentTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SideBConsentExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SideBConsentRespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SideBConsentRespondedByEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
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
                    table.PrimaryKey("PK_AppChangeRequestConsentRounds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppChangeRequestConsentRounds_AppAppointmentChangeRequests_AppointmentChangeRequestId",
                        column: x => x.AppointmentChangeRequestId,
                        principalTable: "AppAppointmentChangeRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppChangeRequestConsentRounds_AppointmentChangeRequestId",
                table: "AppChangeRequestConsentRounds",
                column: "AppointmentChangeRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AppChangeRequestConsentRounds_SideAConsentTokenHash",
                table: "AppChangeRequestConsentRounds",
                column: "SideAConsentTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_AppChangeRequestConsentRounds_SideBConsentTokenHash",
                table: "AppChangeRequestConsentRounds",
                column: "SideBConsentTokenHash");

            migrationBuilder.CreateIndex(
                name: "IX_AppChangeRequestConsentRounds_TenantId_AppointmentChangeRequestId_RoundNumber",
                table: "AppChangeRequestConsentRounds",
                columns: new[] { "TenantId", "AppointmentChangeRequestId", "RoundNumber" },
                unique: true,
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppChangeRequestConsentRounds");
        }
    }
}
