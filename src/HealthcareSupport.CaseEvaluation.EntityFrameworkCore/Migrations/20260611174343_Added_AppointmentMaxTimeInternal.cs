using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Added_AppointmentMaxTimeInternal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // defaultValue 90 (not 0): existing SystemParameter rows predate this
            // column, so the DB default must equal the seed default
            // (DefaultAppointmentMaxTimeInternal = 90). A 0 default would cap
            // internal staff at 0 days and block all internal bookings.
            migrationBuilder.AddColumn<int>(
                name: "AppointmentMaxTimeInternal",
                table: "AppSystemParameters",
                type: "int",
                nullable: false,
                defaultValue: 90);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppointmentMaxTimeInternal",
                table: "AppSystemParameters");
        }
    }
}
