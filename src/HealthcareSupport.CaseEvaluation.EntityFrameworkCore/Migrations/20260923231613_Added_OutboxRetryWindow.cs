using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Added_OutboxRetryWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EarlyWarnedAt",
                table: "AppIntegrationOutboxItems",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FirstFailedAt",
                table: "AppIntegrationOutboxItems",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EarlyWarnedAt",
                table: "AppIntegrationOutboxItems");

            migrationBuilder.DropColumn(
                name: "FirstFailedAt",
                table: "AppIntegrationOutboxItems");
        }
    }
}
