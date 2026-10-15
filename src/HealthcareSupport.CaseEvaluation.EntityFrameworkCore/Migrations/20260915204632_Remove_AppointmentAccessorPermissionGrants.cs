using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <inheritdoc />
    public partial class Remove_AppointmentAccessorPermissionGrants : Migration
    {
        /// <summary>
        /// Deletes the role grants for the AppointmentAccessors permission surface, which this
        /// change removes. These are NOT pre-existing orphans: they are live grants to real roles
        /// (ProviderName 'R'), and they become orphaned only because the definition goes away.
        /// Measured before writing this: 8 rows in the host database (admin, IT Admin) and 10 in
        /// the office database (admin, Intake Staff, Staff Supervisor).
        ///
        /// <para>WHY THIS IS NOT COSMETIC. ABP's PermissionManager.SetAsync resolves a name with
        /// GetOrNullAsync and silently RETURNS when it is undefined, so these rows would simply sit
        /// there granting nothing and reporting nothing. The hazard is reuse: if a permission of the
        /// SAME NAME is ever defined again, every one of these stale rows reactivates as a real
        /// grant, to roles nobody chose.</para>
        ///
        /// <para>WHY THIS MIGRATION SHIPS WITH THE CODE REMOVAL AND NOT SEPARATELY. The seed
        /// contributors CREATE these rows. A migration landing without the code change would be
        /// undone by the next db-migrator run, report success, and change nothing durably.</para>
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EXACT names, never LIKE 'CaseEvaluation.AppointmentAccessors%'. A prefix reads as
            // precise and is not: a future permission such as AppointmentAccessorsArchive would be
            // swept up silently, and nothing would report it. Same shape as the over-matching
            // suffix bug #872 fixed. The four names are also listed individually because they do
            // NOT travel as a set -- Intake Staff holds Default and Create but not Edit or Delete.
            migrationBuilder.Sql(@"
                DELETE FROM AbpPermissionGrants
                WHERE Name IN (
                    'CaseEvaluation.AppointmentAccessors',
                    'CaseEvaluation.AppointmentAccessors.Create',
                    'CaseEvaluation.AppointmentAccessors.Edit',
                    'CaseEvaluation.AppointmentAccessors.Delete');");
        }

        /// <summary>
        /// Deliberately empty. This IS reversible -- but by reverting the CODE and re-running the
        /// seeders, not by this method.
        ///
        /// <para>The seed contributors own these grants and reproduce them with the correct
        /// per-role asymmetry, derived from the grant generators. Re-inserting rows here would mean
        /// hardcoding eighteen (role, permission) pairs and a ProviderKey per database -- inventing
        /// state rather than restoring it, and wrong the moment the role set changes. If you came
        /// here to add a Down body, revert the code removal instead and let db-migrator do it.</para>
        /// </summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
