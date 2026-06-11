using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Added_ParalegalDelegate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicantParalegalEmail",
                table: "AppAppointments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenseParalegalEmail",
                table: "AppAppointments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParalegalEmail",
                table: "AppAppointmentDefenseAttorneys",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParalegalFirstName",
                table: "AppAppointmentDefenseAttorneys",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParalegalIdentityUserId",
                table: "AppAppointmentDefenseAttorneys",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParalegalLastName",
                table: "AppAppointmentDefenseAttorneys",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParalegalEmail",
                table: "AppAppointmentApplicantAttorneys",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParalegalFirstName",
                table: "AppAppointmentApplicantAttorneys",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParalegalIdentityUserId",
                table: "AppAppointmentApplicantAttorneys",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParalegalLastName",
                table: "AppAppointmentApplicantAttorneys",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentDefenseAttorneys_ParalegalIdentityUserId",
                table: "AppAppointmentDefenseAttorneys",
                column: "ParalegalIdentityUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentApplicantAttorneys_ParalegalIdentityUserId",
                table: "AppAppointmentApplicantAttorneys",
                column: "ParalegalIdentityUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppAppointmentApplicantAttorneys_AbpUsers_ParalegalIdentityUserId",
                table: "AppAppointmentApplicantAttorneys",
                column: "ParalegalIdentityUserId",
                principalTable: "AbpUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AppAppointmentDefenseAttorneys_AbpUsers_ParalegalIdentityUserId",
                table: "AppAppointmentDefenseAttorneys",
                column: "ParalegalIdentityUserId",
                principalTable: "AbpUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppAppointmentApplicantAttorneys_AbpUsers_ParalegalIdentityUserId",
                table: "AppAppointmentApplicantAttorneys");

            migrationBuilder.DropForeignKey(
                name: "FK_AppAppointmentDefenseAttorneys_AbpUsers_ParalegalIdentityUserId",
                table: "AppAppointmentDefenseAttorneys");

            migrationBuilder.DropIndex(
                name: "IX_AppAppointmentDefenseAttorneys_ParalegalIdentityUserId",
                table: "AppAppointmentDefenseAttorneys");

            migrationBuilder.DropIndex(
                name: "IX_AppAppointmentApplicantAttorneys_ParalegalIdentityUserId",
                table: "AppAppointmentApplicantAttorneys");

            migrationBuilder.DropColumn(
                name: "ApplicantParalegalEmail",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "DefenseParalegalEmail",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "ParalegalEmail",
                table: "AppAppointmentDefenseAttorneys");

            migrationBuilder.DropColumn(
                name: "ParalegalFirstName",
                table: "AppAppointmentDefenseAttorneys");

            migrationBuilder.DropColumn(
                name: "ParalegalIdentityUserId",
                table: "AppAppointmentDefenseAttorneys");

            migrationBuilder.DropColumn(
                name: "ParalegalLastName",
                table: "AppAppointmentDefenseAttorneys");

            migrationBuilder.DropColumn(
                name: "ParalegalEmail",
                table: "AppAppointmentApplicantAttorneys");

            migrationBuilder.DropColumn(
                name: "ParalegalFirstName",
                table: "AppAppointmentApplicantAttorneys");

            migrationBuilder.DropColumn(
                name: "ParalegalIdentityUserId",
                table: "AppAppointmentApplicantAttorneys");

            migrationBuilder.DropColumn(
                name: "ParalegalLastName",
                table: "AppAppointmentApplicantAttorneys");
        }
    }
}
