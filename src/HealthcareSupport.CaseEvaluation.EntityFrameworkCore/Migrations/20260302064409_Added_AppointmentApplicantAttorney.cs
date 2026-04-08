using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Added_AppointmentApplicantAttorney : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAppointmentApplicantAttorneys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicantAttorneyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdentityUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_AppAppointmentApplicantAttorneys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAppointmentApplicantAttorneys_AbpUsers_IdentityUserId",
                        column: x => x.IdentityUserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppAppointmentApplicantAttorneys_AppApplicantAttorneys_ApplicantAttorneyId",
                        column: x => x.ApplicantAttorneyId,
                        principalTable: "AppApplicantAttorneys",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppAppointmentApplicantAttorneys_AppAppointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "AppAppointments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentApplicantAttorneys_ApplicantAttorneyId",
                table: "AppAppointmentApplicantAttorneys",
                column: "ApplicantAttorneyId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentApplicantAttorneys_AppointmentId",
                table: "AppAppointmentApplicantAttorneys",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentApplicantAttorneys_IdentityUserId",
                table: "AppAppointmentApplicantAttorneys",
                column: "IdentityUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAppointmentApplicantAttorneys");
        }
    }
}
