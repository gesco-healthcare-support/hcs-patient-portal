using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Added_ChangeRequestConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentExpiresAt",
                table: "AppAppointmentChangeRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConsentRespondedAt",
                table: "AppAppointmentChangeRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ConsentRespondedByEmail",
                table: "AppAppointmentChangeRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConsentStatus",
                table: "AppAppointmentChangeRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ConsentTokenHash",
                table: "AppAppointmentChangeRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequestingSide",
                table: "AppAppointmentChangeRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByUserId",
                table: "AppAppointmentChangeRequests",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentChangeRequests_ConsentTokenHash",
                table: "AppAppointmentChangeRequests",
                column: "ConsentTokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppAppointmentChangeRequests_ConsentTokenHash",
                table: "AppAppointmentChangeRequests");

            migrationBuilder.DropColumn(
                name: "ConsentExpiresAt",
                table: "AppAppointmentChangeRequests");

            migrationBuilder.DropColumn(
                name: "ConsentRespondedAt",
                table: "AppAppointmentChangeRequests");

            migrationBuilder.DropColumn(
                name: "ConsentRespondedByEmail",
                table: "AppAppointmentChangeRequests");

            migrationBuilder.DropColumn(
                name: "ConsentStatus",
                table: "AppAppointmentChangeRequests");

            migrationBuilder.DropColumn(
                name: "ConsentTokenHash",
                table: "AppAppointmentChangeRequests");

            migrationBuilder.DropColumn(
                name: "RequestingSide",
                table: "AppAppointmentChangeRequests");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "AppAppointmentChangeRequests");
        }
    }
}
