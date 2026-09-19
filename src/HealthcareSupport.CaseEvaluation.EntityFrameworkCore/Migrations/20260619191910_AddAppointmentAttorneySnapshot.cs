using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentAttorneySnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicantAttorneyCity",
                table: "AppAppointments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantAttorneyFaxNumber",
                table: "AppAppointments",
                type: "nvarchar(19)",
                maxLength: 19,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantAttorneyFirmName",
                table: "AppAppointments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantAttorneyFirstName",
                table: "AppAppointments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantAttorneyLastName",
                table: "AppAppointments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantAttorneyPhoneNumber",
                table: "AppAppointments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApplicantAttorneyStateId",
                table: "AppAppointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantAttorneyStreet",
                table: "AppAppointments",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantAttorneyWebAddress",
                table: "AppAppointments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApplicantAttorneyZipCode",
                table: "AppAppointments",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenseAttorneyCity",
                table: "AppAppointments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenseAttorneyFaxNumber",
                table: "AppAppointments",
                type: "nvarchar(19)",
                maxLength: 19,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenseAttorneyFirmName",
                table: "AppAppointments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenseAttorneyFirstName",
                table: "AppAppointments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenseAttorneyLastName",
                table: "AppAppointments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenseAttorneyPhoneNumber",
                table: "AppAppointments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DefenseAttorneyStateId",
                table: "AppAppointments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenseAttorneyStreet",
                table: "AppAppointments",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenseAttorneyWebAddress",
                table: "AppAppointments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenseAttorneyZipCode",
                table: "AppAppointments",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicantAttorneyCity",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "ApplicantAttorneyFaxNumber",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "ApplicantAttorneyFirmName",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "ApplicantAttorneyFirstName",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "ApplicantAttorneyLastName",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "ApplicantAttorneyPhoneNumber",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "ApplicantAttorneyStateId",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "ApplicantAttorneyStreet",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "ApplicantAttorneyWebAddress",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "ApplicantAttorneyZipCode",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "DefenseAttorneyCity",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "DefenseAttorneyFaxNumber",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "DefenseAttorneyFirmName",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "DefenseAttorneyFirstName",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "DefenseAttorneyLastName",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "DefenseAttorneyPhoneNumber",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "DefenseAttorneyStateId",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "DefenseAttorneyStreet",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "DefenseAttorneyWebAddress",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "DefenseAttorneyZipCode",
                table: "AppAppointments");
        }
    }
}
