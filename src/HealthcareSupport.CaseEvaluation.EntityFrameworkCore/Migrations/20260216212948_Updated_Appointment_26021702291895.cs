using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Updated_Appointment_26021702291895 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppAppointments_AppAppointmentStatuses_AppointmentStatusId",
                table: "AppAppointments");

            migrationBuilder.DropIndex(
                name: "IX_AppAppointments_AppointmentStatusId",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "AppointmentStatusId",
                table: "AppAppointments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentStatusId",
                table: "AppAppointments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointments_AppointmentStatusId",
                table: "AppAppointments",
                column: "AppointmentStatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppAppointments_AppAppointmentStatuses_AppointmentStatusId",
                table: "AppAppointments",
                column: "AppointmentStatusId",
                principalTable: "AppAppointmentStatuses",
                principalColumn: "Id");
        }
    }
}
