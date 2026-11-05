using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.TenantMigrations
{
    /// <inheritdoc />
    public partial class Added_EmailStops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAppointmentEmailStops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    StoppedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StoppedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    StoppedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ExtraProperties = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAppointmentEmailStops", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAppointmentEmailStops_AppAppointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "AppAppointments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAppointmentEmailStops_AppointmentId_NormalizedEmail",
                table: "AppAppointmentEmailStops",
                columns: new[] { "AppointmentId", "NormalizedEmail" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAppointmentEmailStops");
        }
    }
}
