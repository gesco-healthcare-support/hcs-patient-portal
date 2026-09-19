using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.TenantMigrations
{
    /// <inheritdoc />
    public partial class Added_RescheduledFromAppointmentId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RescheduledFromAppointmentId",
                table: "AppAppointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointments_RescheduledFromAppointmentId",
                table: "AppAppointments",
                column: "RescheduledFromAppointmentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppAppointments_RescheduledFromAppointmentId",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "RescheduledFromAppointmentId",
                table: "AppAppointments");
        }
    }
}
