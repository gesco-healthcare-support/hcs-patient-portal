using System;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Deterministic GUIDs + field constants for Appointment entities.
///
/// Extended in Phase B-6 Tier 2 PR-2A (Wave-2 seed infrastructure): the
/// orchestrator's <c>SeedAppointmentsAsync</c> inserts 2 appointments so
/// downstream Tier-2 entities (AppointmentAccessors,
/// AppointmentApplicantAttorneys, AppointmentEmployerDetails) have valid
/// <c>AppointmentId</c> FKs.
///
/// Appointment distribution:
///   Appointment1 -- TenantA, Pending, FK'd to Patient1 + Slot1
///   Appointment2 -- TenantB, Approved, FK'd to Patient2 + Slot3
///
/// Confirmation numbers use the A9xxxx range to avoid collision with the
/// natural A0xxxx auto-generation range that <c>AppointmentsAppService.CreateAsync</c>
/// uses in production.
///
/// PK uniqueness is per-table, so the Appointment1Id / Appointment2Id Guid
/// values are intentionally identical to <see cref="PatientsTestData.Patient1Id"/>
/// / <see cref="PatientsTestData.Patient2Id"/>: no cross-table conflict at the
/// DB level, and keeping the parallel numbering is a readability aid.
///
/// The pre-existing NonExistent* Guids remain for PR-1A's Wave-1 validation
/// tests; they are guaranteed NOT to resolve against any seeded row.
/// </summary>
public static class AppointmentsTestData
{
    // --- Seeded appointments (Wave-2, PR-2A) ---
    public static readonly Guid Appointment1Id = Guid.Parse("c1111111-1111-1111-1111-111111111111");
    public static readonly Guid Appointment2Id = Guid.Parse("c2222222-2222-2222-2222-222222222222");

    public static readonly DateTime Appointment1Date = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime Appointment2Date = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc);

    // A9xxxx range avoids collision with the natural A0xxxx range that
    // AppointmentsAppService.CreateAsync auto-generates for new appointments.
    public const string Appointment1RequestConfirmationNumber = "A90001";
    public const string Appointment2RequestConfirmationNumber = "A90002";

    public const AppointmentStatusType Appointment1Status = AppointmentStatusType.Pending;
    public const AppointmentStatusType Appointment2Status = AppointmentStatusType.Approved;

    // --- Wave-1 validation-only Guids (unchanged) ---

    // IDs guaranteed NOT to resolve against any seeded entity. Used by the
    // "FK target does not exist" test paths that exercise AppointmentsAppService's
    // `FindAsync(...)` null-check branches.
    public static readonly Guid NonExistentPatientId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd01");
    public static readonly Guid NonExistentIdentityUserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd02");
    public static readonly Guid NonExistentAppointmentTypeId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd03");
    public static readonly Guid NonExistentLocationId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd04");
    public static readonly Guid NonExistentDoctorAvailabilityId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd05");
}
