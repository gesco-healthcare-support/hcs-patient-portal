using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.TenantMigrations
{
    /// <inheritdoc />
    public partial class TwoSidedChangeRequestConsent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ConsentTokenHash",
                table: "AppAppointmentChangeRequests",
                newName: "SideBConsentTokenHash");

            migrationBuilder.RenameColumn(
                name: "ConsentStatus",
                table: "AppAppointmentChangeRequests",
                newName: "SideBConsentStatus");

            migrationBuilder.RenameColumn(
                name: "ConsentRespondedByEmail",
                table: "AppAppointmentChangeRequests",
                newName: "SideBConsentRespondedByEmail");

            migrationBuilder.RenameColumn(
                name: "ConsentRespondedAt",
                table: "AppAppointmentChangeRequests",
                newName: "SideBConsentRespondedAt");

            migrationBuilder.RenameColumn(
                name: "ConsentExpiresAt",
                table: "AppAppointmentChangeRequests",
                newName: "SideBConsentExpiresAt");

            migrationBuilder.RenameIndex(
                name: "IX_AppAppointmentChangeRequests_ConsentTokenHash",
                table: "AppAppointmentChangeRequests",
                newName: "IX_AppAppointmentChangeRequests_SideBConsentTokenHash");

            migrationBuilder.AddColumn<DateTime>(
                name: "SideAConsentExpiresAt",
                table: "AppAppointmentChangeRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SideAConsentRespondedAt",
                table: "AppAppointmentChangeRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SideAConsentRespondedByEmail",
                table: "AppAppointmentChangeRequests",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SideAConsentStatus",
                table: "AppAppointmentChangeRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SideAConsentTokenHash",
                table: "AppAppointmentChangeRequests",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentChangeRequests_SideAConsentTokenHash",
                table: "AppAppointmentChangeRequests",
                column: "SideAConsentTokenHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AppAppointmentChangeRequests_SideAConsentTokenHash",
                table: "AppAppointmentChangeRequests");

            migrationBuilder.DropColumn(
                name: "SideAConsentExpiresAt",
                table: "AppAppointmentChangeRequests");

            migrationBuilder.DropColumn(
                name: "SideAConsentRespondedAt",
                table: "AppAppointmentChangeRequests");

            migrationBuilder.DropColumn(
                name: "SideAConsentRespondedByEmail",
                table: "AppAppointmentChangeRequests");

            migrationBuilder.DropColumn(
                name: "SideAConsentStatus",
                table: "AppAppointmentChangeRequests");

            migrationBuilder.DropColumn(
                name: "SideAConsentTokenHash",
                table: "AppAppointmentChangeRequests");

            migrationBuilder.RenameColumn(
                name: "SideBConsentTokenHash",
                table: "AppAppointmentChangeRequests",
                newName: "ConsentTokenHash");

            migrationBuilder.RenameColumn(
                name: "SideBConsentStatus",
                table: "AppAppointmentChangeRequests",
                newName: "ConsentStatus");

            migrationBuilder.RenameColumn(
                name: "SideBConsentRespondedByEmail",
                table: "AppAppointmentChangeRequests",
                newName: "ConsentRespondedByEmail");

            migrationBuilder.RenameColumn(
                name: "SideBConsentRespondedAt",
                table: "AppAppointmentChangeRequests",
                newName: "ConsentRespondedAt");

            migrationBuilder.RenameColumn(
                name: "SideBConsentExpiresAt",
                table: "AppAppointmentChangeRequests",
                newName: "ConsentExpiresAt");

            migrationBuilder.RenameIndex(
                name: "IX_AppAppointmentChangeRequests_SideBConsentTokenHash",
                table: "AppAppointmentChangeRequests",
                newName: "IX_AppAppointmentChangeRequests_ConsentTokenHash");
        }
    }
}
