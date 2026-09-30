using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Added_AppointmentDocument_DocumentTypeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AppointmentDocumentTypeId",
                table: "AppAppointmentDocuments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OtherDocumentTypeName",
                table: "AppAppointmentDocuments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceDocumentId",
                table: "AppAppointmentDocuments",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppointmentDocumentTypeId",
                table: "AppAppointmentDocuments");

            migrationBuilder.DropColumn(
                name: "OtherDocumentTypeName",
                table: "AppAppointmentDocuments");

            migrationBuilder.DropColumn(
                name: "SourceDocumentId",
                table: "AppAppointmentDocuments");
        }
    }
}
