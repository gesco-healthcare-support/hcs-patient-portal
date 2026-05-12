using System;

namespace HealthcareSupport.CaseEvaluation.Data;

/// <summary>
/// Stable GUIDs for seed rows that cross-reference each other (e.g. WcabOffice.StateId,
/// Location.StateId, Location.AppointmentTypeId). Tests and seeders read from this class
/// so cross-cap references stay compile-time stable. Only includes IDs that other seeders
/// or tests need to reference by name; bulk-seeded sibling rows generate their own GUIDs
/// inside their own seed contributor.
/// </summary>
public static class CaseEvaluationSeedIds
{
    public static class States
    {
        // California is referenced by WcabOffice (Southern CA offices) and Location (demo clinics).
        public static readonly Guid California = new("a0a00001-0000-4000-9000-00000000ca00");
    }

    public static class AppointmentTypes
    {
        // Qme is referenced by Location (default appointment type for demo clinics).
        public static readonly Guid Qme = new("a0a00002-0000-4000-9000-000000000001");
        public static readonly Guid PanelQme = new("a0a00002-0000-4000-9000-000000000002");
        public static readonly Guid Ame = new("a0a00002-0000-4000-9000-000000000003");
        public static readonly Guid RecordReview = new("a0a00002-0000-4000-9000-000000000004");
        public static readonly Guid Deposition = new("a0a00002-0000-4000-9000-000000000005");
        public static readonly Guid SupplementalMedicalReport = new("a0a00002-0000-4000-9000-000000000006");
    }

    public static class AppointmentLanguages
    {
        public static readonly Guid English = new("a0a00003-0000-4000-9000-000000000001");
    }

    public static class WcabOffices
    {
        public static readonly Guid Anaheim = new("a0a00004-0000-4000-9000-000000000001");
        public static readonly Guid Bakersfield = new("a0a00004-0000-4000-9000-000000000002");
        public static readonly Guid Glendale = new("a0a00004-0000-4000-9000-000000000003");
        public static readonly Guid Irvine = new("a0a00004-0000-4000-9000-000000000004");
        public static readonly Guid Riverside = new("a0a00004-0000-4000-9000-000000000005");
        public static readonly Guid SanBernardino = new("a0a00004-0000-4000-9000-000000000006");
        public static readonly Guid VanNuys = new("a0a00004-0000-4000-9000-000000000007");
    }

    public static class Locations
    {
        public static readonly Guid DemoClinicNorth = new("a0a00005-0000-4000-9000-000000000001");
        public static readonly Guid DemoClinicSouth = new("a0a00005-0000-4000-9000-000000000002");
    }
}
