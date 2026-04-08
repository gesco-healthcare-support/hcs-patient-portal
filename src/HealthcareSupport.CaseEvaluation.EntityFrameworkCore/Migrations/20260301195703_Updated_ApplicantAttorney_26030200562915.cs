using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Updated_ApplicantAttorney_26030200562915 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IdentityUserId",
                table: "AppApplicantAttorneys",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_AppApplicantAttorneys_IdentityUserId",
                table: "AppApplicantAttorneys",
                column: "IdentityUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppApplicantAttorneys_AbpUsers_IdentityUserId",
                table: "AppApplicantAttorneys",
                column: "IdentityUserId",
                principalTable: "AbpUsers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppApplicantAttorneys_AbpUsers_IdentityUserId",
                table: "AppApplicantAttorneys");

            migrationBuilder.DropIndex(
                name: "IX_AppApplicantAttorneys_IdentityUserId",
                table: "AppApplicantAttorneys");

            migrationBuilder.DropColumn(
                name: "IdentityUserId",
                table: "AppApplicantAttorneys");
        }
    }
}
