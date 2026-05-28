using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Phase20_DoctorAvailabilityCapacityAndTypeSet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppDoctorAvailabilities_AppAppointmentTypes_AppointmentTypeId",
                table: "AppDoctorAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_AppDoctorAvailabilities_AppointmentTypeId",
                table: "AppDoctorAvailabilities");

            migrationBuilder.DropColumn(
                name: "AppointmentTypeId",
                table: "AppDoctorAvailabilities");

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "AppDoctorAvailabilities",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.CreateTable(
                name: "AppDoctorAvailabilityAppointmentType",
                columns: table => new
                {
                    DoctorAvailabilityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDoctorAvailabilityAppointmentType", x => new { x.DoctorAvailabilityId, x.AppointmentTypeId });
                    table.ForeignKey(
                        name: "FK_AppDoctorAvailabilityAppointmentType_AppAppointmentTypes_AppointmentTypeId",
                        column: x => x.AppointmentTypeId,
                        principalTable: "AppAppointmentTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppDoctorAvailabilityAppointmentType_AppDoctorAvailabilities_DoctorAvailabilityId",
                        column: x => x.DoctorAvailabilityId,
                        principalTable: "AppDoctorAvailabilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppDoctorAvailabilityAppointmentType_AppointmentTypeId",
                table: "AppDoctorAvailabilityAppointmentType",
                column: "AppointmentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoctorAvailabilityAppointmentType_DoctorAvailabilityId_AppointmentTypeId",
                table: "AppDoctorAvailabilityAppointmentType",
                columns: new[] { "DoctorAvailabilityId", "AppointmentTypeId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppDoctorAvailabilityAppointmentType");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "AppDoctorAvailabilities");

            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentTypeId",
                table: "AppDoctorAvailabilities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppDoctorAvailabilities_AppointmentTypeId",
                table: "AppDoctorAvailabilities",
                column: "AppointmentTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppDoctorAvailabilities_AppAppointmentTypes_AppointmentTypeId",
                table: "AppDoctorAvailabilities",
                column: "AppointmentTypeId",
                principalTable: "AppAppointmentTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
