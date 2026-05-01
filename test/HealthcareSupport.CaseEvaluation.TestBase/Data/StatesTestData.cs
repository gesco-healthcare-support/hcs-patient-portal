using System;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Hardcoded synthetic GUIDs + names for the State host-scoped lookup entity
/// seeded by <see cref="HealthcareSupport.CaseEvaluation.Testing.CaseEvaluationIntegrationTestSeedContributor"/>.
///
/// State is `FullAuditedAggregateRoot&lt;Guid&gt;` (host-only -- NOT IMultiTenant).
/// 5 inbound FKs (Patient, ApplicantAttorney, AppointmentEmployerDetail,
/// Location, Doctor) all SetNull; tests must avoid deleting seeded states or
/// the Tier-1/2 nav-prop assertions will see null FKs.
///
/// Backward-compatibility note: <see cref="LocationsTestData.State1Id"/> +
/// <see cref="LocationsTestData.State1Name"/> were declared by Tier-1 PR-1D
/// and are referenced by existing tests (Locations, AppointmentEmployerDetails).
/// The values are re-exposed here under the more semantically-correct
/// `StatesTestData` namespace; the orchestrator extracts them into
/// `SeedStatesAsync` so the seed phase is grouped with its entity. Tests added
/// from PR-3A onward should reference this class; legacy references via
/// <c>LocationsTestData</c> continue to work by aliasing the same Guids.
///
/// GUID prefix scheme: Tier-3 uses digit-only prefixes (2/3/4/5/6) since
/// hex letters a-f are already claimed by Tier-1/2 TestData files
/// (a=Locations, b=ApplicantAttorneys, c=Appointments+Joins, d=Slots,
/// e=Employer/State1, f=AppointmentType1). Letters g-z are not valid hex.
/// </summary>
public static class StatesTestData
{
    // --- State1: shared with Tier-1 (LocationsTestData.State1Id = "TEST-California") ---
    public static readonly Guid State1Id = LocationsTestData.State1Id;
    public const string State1Name = LocationsTestData.State1Name;

    // --- State2: TEST-Nevada (Tier-3 addition; supports Name-filter test) ---
    public static readonly Guid State2Id = Guid.Parse("21111111-1111-1111-1111-111111111111");
    public const string State2Name = "TEST-Nevada";

    // --- State3: TEST-Oregon (Tier-3 addition; supports multi-state list test) ---
    public static readonly Guid State3Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public const string State3Name = "TEST-Oregon";
}
