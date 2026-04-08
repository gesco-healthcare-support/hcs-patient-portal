using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Added_Doctor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppDoctors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(49)", maxLength: 49, nullable: false),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    IdentityUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
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
                    table.PrimaryKey("PK_AppDoctors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppDoctors_AbpUsers_IdentityUserId",
                        column: x => x.IdentityUserId,
                        principalTable: "AbpUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AppDoctors_SaasTenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "SaasTenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AppDoctorAppointmentType",
                columns: table => new
                {
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDoctorAppointmentType", x => new { x.DoctorId, x.AppointmentTypeId });
                    table.ForeignKey(
                        name: "FK_AppDoctorAppointmentType_AppAppointmentTypes_AppointmentTypeId",
                        column: x => x.AppointmentTypeId,
                        principalTable: "AppAppointmentTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppDoctorAppointmentType_AppDoctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "AppDoctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppDoctorLocation",
                columns: table => new
                {
                    DoctorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppDoctorLocation", x => new { x.DoctorId, x.LocationId });
                    table.ForeignKey(
                        name: "FK_AppDoctorLocation_AppDoctors_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "AppDoctors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppDoctorLocation_AppLocations_LocationId",
                        column: x => x.LocationId,
                        principalTable: "AppLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppDoctorAppointmentType_AppointmentTypeId",
                table: "AppDoctorAppointmentType",
                column: "AppointmentTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoctorAppointmentType_DoctorId_AppointmentTypeId",
                table: "AppDoctorAppointmentType",
                columns: new[] { "DoctorId", "AppointmentTypeId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppDoctorLocation_DoctorId_LocationId",
                table: "AppDoctorLocation",
                columns: new[] { "DoctorId", "LocationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AppDoctorLocation_LocationId",
                table: "AppDoctorLocation",
                column: "LocationId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoctors_IdentityUserId",
                table: "AppDoctors",
                column: "IdentityUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppDoctors_TenantId",
                table: "AppDoctors",
                column: "TenantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppDoctorAppointmentType");

            migrationBuilder.DropTable(
                name: "AppDoctorLocation");

            migrationBuilder.DropTable(
                name: "AppDoctors");
        }
    }
}
