using System;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Deterministic GUIDs for Appointment entity tests. The Appointment* IDs
/// are reserved for appointments the seed contributor inserts once
/// DoctorAvailability / Patient / IdentityUser seeds land in PR-1B, 1C,
/// and the eventual IdentityUser contributor. For this PR (CreateAsync /
/// UpdateAsync validation coverage), only the NonExistent* IDs are used.
/// </summary>
public static class AppointmentsTestData
{
    // Reserved for future seed expansion once upstream FK seeds exist.
    public static readonly Guid Appointment1Id = Guid.Parse("c1111111-1111-1111-1111-111111111111");
    public static readonly Guid Appointment2Id = Guid.Parse("c2222222-2222-2222-2222-222222222222");

    // IDs guaranteed NOT to resolve against any seeded entity. Used by the
    // "FK target does not exist" test paths that exercise AppointmentsAppService's
    // `FindAsync(...)` null-check branches.
    public static readonly Guid NonExistentPatientId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd01");
    public static readonly Guid NonExistentIdentityUserId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd02");
    public static readonly Guid NonExistentAppointmentTypeId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd03");
    public static readonly Guid NonExistentLocationId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd04");
    public static readonly Guid NonExistentDoctorAvailabilityId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddd05");
}
