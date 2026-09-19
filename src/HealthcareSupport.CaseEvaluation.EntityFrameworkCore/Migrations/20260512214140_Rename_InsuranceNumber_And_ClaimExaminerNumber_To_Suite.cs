using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Rename_InsuranceNumber_And_ClaimExaminerNumber_To_Suite : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "InsuranceNumber",
                table: "AppAppointmentPrimaryInsurances",
                newName: "Suite");

            migrationBuilder.RenameColumn(
                name: "ClaimExaminerNumber",
                table: "AppAppointmentClaimExaminers",
                newName: "Suite");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Suite",
                table: "AppAppointmentPrimaryInsurances",
                newName: "InsuranceNumber");

            migrationBuilder.RenameColumn(
                name: "Suite",
                table: "AppAppointmentClaimExaminers",
                newName: "ClaimExaminerNumber");
        }
    }
}
