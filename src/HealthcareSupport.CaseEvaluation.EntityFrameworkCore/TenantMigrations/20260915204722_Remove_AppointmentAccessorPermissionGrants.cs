using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.TenantMigrations
{
    /// <inheritdoc />
    public partial class Remove_AppointmentAccessorPermissionGrants : Migration
    {
        /// <summary>
        /// The per-office half of the AppointmentAccessors grant cleanup. Required separately
        /// because every office database carries its own AbpPermissionGrants table -- the host
        /// migration cannot reach them, and a cleanup landing in only one set would leave the
        /// hazard intact for exactly the databases that hold more of it (10 rows measured in the
        /// office database against 8 in the host).
        ///
        /// <para>See the host migration of the same name for the full rationale: these are live
        /// role grants rather than pre-existing orphans, ABP silently ignores an undefined
        /// permission name so nothing else would report them, and the migration ships with the
        /// code removal because the seeders would otherwise re-create the rows.</para>
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EXACT names, never a LIKE prefix -- see the host migration. The four are listed
            // individually because they do not travel as a set: Intake Staff holds Default and
            // Create but not Edit or Delete.
            migrationBuilder.Sql(@"
                DELETE FROM AbpPermissionGrants
                WHERE Name IN (
                    'CaseEvaluation.AppointmentAccessors',
                    'CaseEvaluation.AppointmentAccessors.Create',
                    'CaseEvaluation.AppointmentAccessors.Edit',
                    'CaseEvaluation.AppointmentAccessors.Delete');");
        }

        /// <summary>
        /// Deliberately empty, for the same reason as the host migration: reversible by reverting
        /// the CODE and re-running the seeders, which reproduce these grants with the correct
        /// per-role asymmetry, not by hardcoding rows here.
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
