using System;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Test-tenant identity references. Unlike other TestData classes in this project,
/// the Tenant GUIDs here are NOT `Guid.Parse` constants -- they are static
/// properties populated at seed time by the orchestrator when it creates tenants
/// through <c>ITenantManager.CreateAsync(name)</c>.
///
/// Why non-deterministic:
///   <c>Volo.Saas.Tenants.Tenant</c> exposes only non-public constructors, so
///   tests cannot pre-allocate a GUID and construct a Tenant entity directly.
///   The production path (used here too) is <c>ITenantManager.CreateAsync</c>,
///   which generates its own Guid. We capture that generated Id into
///   <see cref="TenantARef"/> / <see cref="TenantBRef"/> at seed time.
///
/// Deterministic within a run (the orchestrator is a singleton; all tests in a
/// single test-run see the same captured GUIDs). Non-deterministic across runs.
/// Tests assert against the captured property, not a hardcoded GUID.
///
/// Tenant names ARE deterministic constants because <c>ITenantManager</c>
/// enforces name uniqueness and tests may assert on them.
/// </summary>
public static class TenantsTestData
{
    public const string TenantAName = "TEST-tenant-a";
    public const string TenantBName = "TEST-tenant-b";

    /// <summary>
    /// Populated by <see cref="HealthcareSupport.CaseEvaluation.Testing.CaseEvaluationIntegrationTestSeedContributor"/>
    /// during its SeedTenantsAsync step. Guid.Empty before seed has run; do not
    /// reference this property from static initializers.
    /// </summary>
    public static Guid TenantARef { get; internal set; } = Guid.Empty;

    /// <summary>
    /// Populated by the orchestrator at seed time. See remarks on <see cref="TenantARef"/>.
    /// </summary>
    public static Guid TenantBRef { get; internal set; } = Guid.Empty;
}
