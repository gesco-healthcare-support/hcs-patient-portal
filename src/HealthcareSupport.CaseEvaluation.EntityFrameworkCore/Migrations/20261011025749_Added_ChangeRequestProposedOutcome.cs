using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Added_ChangeRequestProposedOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProposedOutcome",
                table: "AppChangeRequestConsentRounds",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProposedOutcome",
                table: "AppAppointmentChangeRequests",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProposedOutcome",
                table: "AppChangeRequestConsentRounds");

            migrationBuilder.DropColumn(
                name: "ProposedOutcome",
                table: "AppAppointmentChangeRequests");
        }
    }
}
