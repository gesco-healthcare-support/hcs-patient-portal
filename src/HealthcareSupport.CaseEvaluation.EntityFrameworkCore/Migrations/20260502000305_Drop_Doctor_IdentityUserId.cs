using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Drop_Doctor_IdentityUserId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppDoctors_AbpUsers_IdentityUserId",
                table: "AppDoctors");

            migrationBuilder.DropIndex(
                name: "IX_AppDoctors_IdentityUserId",
                table: "AppDoctors");

            migrationBuilder.DropColumn(
                name: "IdentityUserId",
                table: "AppDoctors");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "IdentityUserId",
                table: "AppDoctors",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppDoctors_IdentityUserId",
                table: "AppDoctors",
                column: "IdentityUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppDoctors_AbpUsers_IdentityUserId",
                table: "AppDoctors",
                column: "IdentityUserId",
                principalTable: "AbpUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
