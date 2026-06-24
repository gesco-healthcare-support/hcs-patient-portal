using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Phase6_Add_CustomFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppCustomFields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FieldLabel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    FieldType = table.Column<int>(type: "int", nullable: false),
                    FieldLength = table.Column<int>(type: "int", nullable: true),
                    MultipleValues = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DefaultValue = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsMandatory = table.Column<bool>(type: "bit", nullable: false),
                    AppointmentTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_AppCustomFields", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppCustomFieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CustomFieldId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppointmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
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
                    table.PrimaryKey("PK_AppCustomFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppCustomFieldValues_AppAppointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "AppAppointments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_AppCustomFieldValues_AppCustomFields_CustomFieldId",
                        column: x => x.CustomFieldId,
                        principalTable: "AppCustomFields",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppCustomFields_TenantId_AppointmentTypeId_IsActive",
                table: "AppCustomFields",
                columns: new[] { "TenantId", "AppointmentTypeId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_AppCustomFieldValues_AppointmentId",
                table: "AppCustomFieldValues",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_AppCustomFieldValues_CustomFieldId",
                table: "AppCustomFieldValues",
                column: "CustomFieldId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppCustomFieldValues");

            migrationBuilder.DropTable(
                name: "AppCustomFields");
        }
    }
}
