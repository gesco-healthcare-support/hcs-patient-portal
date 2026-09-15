using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Added_PatientSnapshotFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PatientApptNumber",
                table: "AppAppointments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientCellPhoneNumber",
                table: "AppAppointments",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientCity",
                table: "AppAppointments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PatientDateOfBirth",
                table: "AppAppointments",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientFirstName",
                table: "AppAppointments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PatientGenderId",
                table: "AppAppointments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientInterpreterVendorName",
                table: "AppAppointments",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientLastName",
                table: "AppAppointments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientMiddleName",
                table: "AppAppointments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientPhoneNumber",
                table: "AppAppointments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PatientPhoneNumberTypeId",
                table: "AppAppointments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientSocialSecurityNumber",
                table: "AppAppointments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PatientStateId",
                table: "AppAppointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientStreet",
                table: "AppAppointments",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientZipCode",
                table: "AppAppointments",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PatientApptNumber",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "PatientCellPhoneNumber",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "PatientCity",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "PatientDateOfBirth",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "PatientFirstName",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "PatientGenderId",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "PatientInterpreterVendorName",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "PatientLastName",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "PatientMiddleName",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "PatientPhoneNumber",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "PatientPhoneNumberTypeId",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "PatientSocialSecurityNumber",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "PatientStateId",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "PatientStreet",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "PatientZipCode",
                table: "AppAppointments");
        }
    }
}
