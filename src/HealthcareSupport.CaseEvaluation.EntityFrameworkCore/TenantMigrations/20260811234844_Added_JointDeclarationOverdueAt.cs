using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.TenantMigrations
{
    /// <inheritdoc />
    public partial class Added_JointDeclarationOverdueAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "JointDeclarationOverdueAt",
                table: "AppAppointments",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JointDeclarationOverdueAt",
                table: "AppAppointments");
        }
    }
}
