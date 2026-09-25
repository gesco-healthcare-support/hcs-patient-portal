using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthcareSupport.CaseEvaluation.Migrations
{
    /// <summary>
    /// 2026-06-07 -- reduce the selectable appointment types to AME / IME / PQME.
    /// The seeder already seeds only those three; this one-way data migration
    /// removes the four legacy types (QME, Record Review, Deposition,
    /// Supplemental Medical Report) that linger only in databases seeded by
    /// older code. Referencing rows are reassigned/removed first (the
    /// AppAppointments and AppDoctorAvailabilityAppointmentType FKs are
    /// NO_ACTION and would otherwise block the delete). Reclassification:
    /// QME -> PQME; Record Review / Deposition / Supplemental Medical Report
    /// -> IME. Every statement is idempotent and a no-op on a fresh DB (0 legacy
    /// rows), so it is safe to run on any environment.
    /// </summary>
    public partial class Reduce_AppointmentTypes_To_Three : Migration
    {
        // Canonical seeded type ids (CaseEvaluationSeedIds.AppointmentTypes).
        private const string Ime = "a0a00002-0000-4000-9000-000000000007";
        private const string Pqme = "a0a00002-0000-4000-9000-000000000002";
        // Legacy types to remove.
        private const string Qme = "a0a00002-0000-4000-9000-000000000001";
        private const string RecordReview = "a0a00002-0000-4000-9000-000000000004";
        private const string Deposition = "a0a00002-0000-4000-9000-000000000005";
        private const string Supplemental = "a0a00002-0000-4000-9000-000000000006";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var legacyList = $"'{Qme}','{RecordReview}','{Deposition}','{Supplemental}'";

            migrationBuilder.Sql($@"
                -- Reassign historical appointments off the legacy types.
                UPDATE [AppAppointments] SET [AppointmentTypeId] = '{Pqme}'
                    WHERE [AppointmentTypeId] = '{Qme}';
                UPDATE [AppAppointments] SET [AppointmentTypeId] = '{Ime}'
                    WHERE [AppointmentTypeId] IN ('{RecordReview}','{Deposition}','{Supplemental}');

                -- Drop slot<->type offerings for legacy types (NO_ACTION FK).
                DELETE FROM [AppDoctorAvailabilityAppointmentType]
                    WHERE [AppointmentTypeId] IN ({legacyList});

                -- Doctor<->type links and per-type field configs (CASCADE on type
                -- delete; removed explicitly to stay deterministic + provider-safe).
                DELETE FROM [AppDoctorAppointmentType]
                    WHERE [AppointmentTypeId] IN ({legacyList});
                DELETE FROM [AppAppointmentTypeFieldConfigs]
                    WHERE [AppointmentTypeId] IN ({legacyList});

                -- Null any location default-type pointing at a legacy type (SET_NULL FK).
                UPDATE [AppLocations] SET [AppointmentTypeId] = NULL
                    WHERE [AppointmentTypeId] IN ({legacyList});

                -- Finally remove the four legacy appointment types.
                DELETE FROM [AppAppointmentTypes]
                    WHERE [Id] IN ({legacyList});
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way data cleanup: deleted legacy types + their reassigned
            // appointment links cannot be faithfully restored, and the seeder no
            // longer seeds them. Down is intentionally a no-op.
        }
    }
}
