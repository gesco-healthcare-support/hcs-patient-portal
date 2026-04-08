using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Added_Appointment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAppointments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PanelNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AppointmentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsPatientAlreadyExist = table.Column<bool>(type: "bit", nullable: false),
                    RequestConfirmationNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InternalUserComments = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    AppointmentApproveDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AppointmentStatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PatientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdentityUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
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
                    table.PrimaryKey("PK_AppAppointments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAppointments_AbpUsers_IdentityUserId",
                        column: x => x.IdentityUserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppAppointments_AppAppointmentStatuses_AppointmentStatusId",
                        column: x => x.AppointmentStatusId,
                        principalTable: "AppAppointmentStatuses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppAppointments_AppAppointmentTypes_AppointmentTypeId",
                        column: x => x.AppointmentTypeId,
                        principalTable: "AppAppointmentTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppAppointments_AppLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "AppLocations",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppAppointments_AppPatients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "AppPatients",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointments_AppointmentStatusId",
                table: "AppAppointments",
                column: "AppointmentStatusId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointments_AppointmentTypeId",
                table: "AppAppointments",
                column: "AppointmentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointments_IdentityUserId",
                table: "AppAppointments",
                column: "IdentityUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointments_LocationId",
                table: "AppAppointments",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointments_PatientId",
                table: "AppAppointments",
                column: "PatientId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAppointments");
        }
    }
}
