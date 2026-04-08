using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Added_DocAvailabilityId_Appointment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "DoctorAvailabilityId",
                table: "AppAppointments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointments_DoctorAvailabilityId",
                table: "AppAppointments",
                column: "DoctorAvailabilityId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppAppointments_AppDoctorAvailabilities_DoctorAvailabilityId",
                table: "AppAppointments",
                column: "DoctorAvailabilityId",
                principalTable: "AppDoctorAvailabilities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppAppointments_AppDoctorAvailabilities_DoctorAvailabilityId",
                table: "AppAppointments");

            migrationBuilder.DropIndex(
                name: "IX_AppAppointments_DoctorAvailabilityId",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "DoctorAvailabilityId",
                table: "AppAppointments");
        }
    }
}
