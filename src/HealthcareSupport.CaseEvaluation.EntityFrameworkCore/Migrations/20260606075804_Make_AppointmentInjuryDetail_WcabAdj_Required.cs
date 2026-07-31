using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Make_AppointmentInjuryDetail_WcabAdj_Required : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CI3 cleanup: backfill legacy null ADJ# rows to "" BEFORE the NOT NULL
            // alter. SQL Server's ALTER COLUMN ... NOT NULL fails if any existing
            // rows are NULL (the defaultValue below only applies to future inserts),
            // so existing rows booked before ADJ# became required are normalized to
            // empty string here. New bookings always carry a real ADJ# (DTO [Required]
            // + AppointmentInjuryDetailWcabAdjUnitTests domain guard).
            migrationBuilder.Sql(
                "UPDATE [AppAppointmentInjuryDetails] SET [WcabAdj] = N'' WHERE [WcabAdj] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "WcabAdj",
                table: "AppAppointmentInjuryDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "WcabAdj",
                table: "AppAppointmentInjuryDetails",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);
        }
    }
}
