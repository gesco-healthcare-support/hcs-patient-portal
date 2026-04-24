using System;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Deterministic GUIDs + field constants for DoctorAvailability entities.
///
/// Extended in Phase B-6 Tier 2 PR-2A (Wave-2 seed infrastructure): the
/// orchestrator's <c>SeedDoctorAvailabilitiesAsync</c> now inserts 3 slots
/// so downstream Tier-2 entities (AppointmentAccessors,
/// AppointmentApplicantAttorneys, AppointmentEmployerDetails) have valid
/// <c>DoctorAvailabilityId</c> FKs through their parent Appointments.
///
/// Slot distribution:
///   Slot1 -- TenantA, Booked, linked to Appointment1 (see AppointmentsTestData)
///   Slot2 -- TenantA, Available, free / unused (reserved for future Wave-2
///            DoctorAvailability CRUD tests that need a non-linked slot)
///   Slot3 -- TenantB, Booked, linked to Appointment2
///
/// The pre-existing NonExistent* Guids remain for PR-1B's Wave-1 validation
/// tests; they are guaranteed NOT to resolve against any seeded row.
/// </summary>
public static class DoctorAvailabilitiesTestData
{
    // --- Seeded slots (Wave-2, PR-2A) ---
    public static readonly Guid Slot1Id = Guid.Parse("d1111111-1111-1111-1111-111111111111");
    public static readonly Guid Slot2Id = Guid.Parse("d2222222-2222-2222-2222-222222222222");
    public static readonly Guid Slot3Id = Guid.Parse("d3333333-3333-3333-3333-333333333333");

    // Fixed future dates (not today, per HIPAA test-data rule; safely 2026-06 so
    // no-one mistakes them for a real clinical appointment window).
    public static readonly DateTime Slot1AvailableDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime Slot2AvailableDate = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime Slot3AvailableDate = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc);

    public static readonly TimeOnly Slot1FromTime = new TimeOnly(9, 0);
    public static readonly TimeOnly Slot1ToTime = new TimeOnly(10, 0);
    public static readonly TimeOnly Slot2FromTime = new TimeOnly(14, 0);
    public static readonly TimeOnly Slot2ToTime = new TimeOnly(15, 0);
    public static readonly TimeOnly Slot3FromTime = new TimeOnly(10, 0);
    public static readonly TimeOnly Slot3ToTime = new TimeOnly(11, 0);

    public const BookingStatus Slot1BookingStatus = BookingStatus.Booked;
    public const BookingStatus Slot2BookingStatus = BookingStatus.Available;
    public const BookingStatus Slot3BookingStatus = BookingStatus.Booked;

    // --- Wave-1 validation-only Guids (unchanged) ---

    // Stub IDs guaranteed NOT to resolve against any seeded row. Used by the
    // Guid.Empty-guard and FK-not-found-guard paths in DoctorAvailability's
    // AppService / GeneratePreviewAsync.
    public static readonly Guid NonExistentSlotId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01");
    public static readonly Guid NonExistentLocationId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02");
    public static readonly Guid NonExistentAppointmentTypeId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee03");
}
