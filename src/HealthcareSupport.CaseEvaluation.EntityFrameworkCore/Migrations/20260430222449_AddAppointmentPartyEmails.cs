using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentPartyEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApplicantAttorneyEmail",
                table: "AppAppointments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClaimExaminerEmail",
                table: "AppAppointments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefenseAttorneyEmail",
                table: "AppAppointments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientEmail",
                table: "AppAppointments",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApplicantAttorneyEmail",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "ClaimExaminerEmail",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "DefenseAttorneyEmail",
                table: "AppAppointments");

            migrationBuilder.DropColumn(
                name: "PatientEmail",
                table: "AppAppointments");
        }
    }
}
