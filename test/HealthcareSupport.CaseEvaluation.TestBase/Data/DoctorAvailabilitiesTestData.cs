using System;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Deterministic GUIDs for DoctorAvailability entity tests.
///
/// Phase B-6 Tier-1 PR-1B scope: validation-layer + GeneratePreviewAsync
/// slot-math coverage only. DoctorAvailabilityManager and the AppService
/// do not validate FK existence at the application layer -- ABP delegates
/// to the DB constraint on SaveChanges. Since PR-1B does not seed slots
/// into the test DB, we only need stub GUIDs for the three scenarios:
///
/// 1. Guid.Empty guard tests (CreateAsync / UpdateAsync / DeleteBySlot /
///    DeleteByDate / GeneratePreviewAsync item) -- need any non-empty Guid
///    except Guid.Empty so the guard fires correctly.
/// 2. GeneratePreviewAsync slot-math tests -- pass a stub LocationId. The
///    code uses FindAsync which returns null gracefully for a missing id
///    (line 232) so slot generation still returns a valid preview list.
/// 3. UpdateAsync id-parameter tests -- the Guid.Empty guard on LocationId
///    fires before the id is resolved, so any placeholder id works.
///
/// Appointment1Id..NonExistent* seeded slot IDs are deferred to Wave 2
/// of PR-1B (conflict-detection tests requiring seeded existing slots).
/// </summary>
public static class DoctorAvailabilitiesTestData
{
    // Stub IDs for validation tests. Not seeded.
    public static readonly Guid NonExistentSlotId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee01");
    public static readonly Guid NonExistentLocationId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee02");
    public static readonly Guid NonExistentAppointmentTypeId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeee03");
}
