using System;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Hardcoded synthetic GUIDs for Patient entities seeded by the integration-test
/// orchestrator (<see cref="HealthcareSupport.CaseEvaluation.Testing.CaseEvaluationIntegrationTestSeedContributor"/>).
/// The tenant GUIDs live here rather than in IdentityUsersTestData because the
/// Patient entity is the primary Tier-1 surface that asserts against them — the
/// HIPAA-critical cross-tenant visibility tests in PR-1C turn on the fact that
/// Patient has a TenantId column but does not implement IMultiTenant, so the
/// multi-tenant filter does not apply (see
/// src/HealthcareSupport.CaseEvaluation.Domain/Patients/CLAUDE.md).
///
/// This file ships in the seed-infra PR so PR-1C can add SeedPatientsAsync
/// without also renaming the GUID references across new test classes.
/// </summary>
public static class PatientsTestData
{
    public static readonly Guid Patient1Id = Guid.Parse("c1111111-1111-1111-1111-111111111111");
    public static readonly Guid Patient2Id = Guid.Parse("c2222222-2222-2222-2222-222222222222");

    // Deterministic tenant GUIDs consumed by the cross-tenant visibility tests.
    // Not wired into SaaS Tenant rows yet; PR-1C decides whether to seed actual
    // Tenant entities or assert against the raw GUID column.
    public static readonly Guid TenantAId = Guid.Parse("b1111111-1111-1111-1111-111111111111");
    public static readonly Guid TenantBId = Guid.Parse("b2222222-2222-2222-2222-222222222222");
}
