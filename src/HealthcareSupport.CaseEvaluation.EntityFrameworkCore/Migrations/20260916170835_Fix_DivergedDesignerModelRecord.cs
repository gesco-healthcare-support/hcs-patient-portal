using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <summary>
    /// Schema-neutral by design: <see cref="Up"/> and <see cref="Down"/> are both empty,
    /// and neither model snapshot changes. This migration exists only to put a correct
    /// model record back at the tip of the migration sequence.
    /// <para>
    /// PR #913's migrations were generated at 20:46:32Z on 2026-09-15, 47 minutes before
    /// PR #906 merged, so their .Designer.cs files never contained the
    /// IX_AppDoctorAvailabilities_Slot_Identity unique index that #906 added. That is a
    /// diverged migration tree: the newest Designer described a model that no longer
    /// matched the snapshot beside it.
    /// </para>
    /// <para>
    /// It matters because "dotnet ef migrations remove" does not recompute the snapshot
    /// from the model -- it restores the model held in the preceding migration's designer
    /// metadata. A remove at that tip would therefore have silently dropped a live unique
    /// constraint from the snapshot, in both contexts, with no signal. Appending a
    /// migration regenerates the Designer from the current model and closes that trap
    /// without rewriting any historical file.
    /// </para>
    /// </summary>
    public partial class Fix_DivergedDesignerModelRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
