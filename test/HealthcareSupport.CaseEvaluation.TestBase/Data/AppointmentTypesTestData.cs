using System;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Hardcoded synthetic GUIDs + names for the AppointmentType host-scoped lookup
/// entity seeded by <see cref="HealthcareSupport.CaseEvaluation.Testing.CaseEvaluationIntegrationTestSeedContributor"/>.
///
/// AppointmentType is `FullAuditedEntity&lt;Guid&gt;` (host-only -- NOT
/// IMultiTenant; NOT AggregateRoot). 4 inbound FKs (Appointment, DoctorAvailability,
/// Location, DoctorAppointmentType). The M2M with Doctor is managed by
/// DoctorsAppService.SetAppointmentTypesAsync, NOT by AppointmentTypesAppService.
///
/// Backward-compatibility note: <see cref="LocationsTestData.AppointmentType1Id"/>
/// + <see cref="LocationsTestData.AppointmentType1Name"/> were declared by Tier-1
/// PR-1D and are referenced by existing seeds (Location1 + DoctorAvailability1/3).
/// The values are re-exposed here under the more semantically-correct
/// `AppointmentTypesTestData` namespace; the orchestrator extracts them into
/// `SeedAppointmentTypesAsync` so the seed phase is grouped with its entity.
/// Tests added from PR-3B onward should reference this class.
///
/// AppointmentType2 exercises the optional `Description` field (max 200 chars).
/// </summary>
public static class AppointmentTypesTestData
{
    // --- AppointmentType1: shared with Tier-1 (TEST-IME-Eval; null Description) ---
    public static readonly Guid AppointmentType1Id = LocationsTestData.AppointmentType1Id;
    public const string AppointmentType1Name = LocationsTestData.AppointmentType1Name;

    // --- AppointmentType2: TEST-Orthopedic with populated Description (Tier-3) ---
    public static readonly Guid AppointmentType2Id = Guid.Parse("31111111-1111-1111-1111-111111111111");
    public const string AppointmentType2Name = "TEST-Orthopedic";
    public const string AppointmentType2Description = "TEST-Orthopedic-Description";
}
