using System;

namespace HealthcareSupport.CaseEvaluation.TestData;

/// <summary>
/// Test-tenant identity references. Like the other TestData classes, the tenant ids
/// are fixed <c>Guid.Parse</c> values, and every test application seeds its tenants
/// with exactly these ids.
///
/// Why fixed rather than captured at seed time (#1034):
///   These used to be static properties that each test application's seed overwrote
///   with the id <c>ITenantManager.CreateAsync</c> had just generated. Every EF Core
///   test builds its own application and its own database, so once test classes run
///   in parallel, application X would seed, application Y would overwrite the static,
///   and X would then read a tenant id that does not exist in X's database. With
///   fixed ids every application writes the same value, so there is nothing to race.
///
/// How the seed honours them:
///   <c>Volo.Saas.Tenants.Tenant</c> has only non-public constructors, so the
///   orchestrator still creates tenants through <c>ITenantManager.CreateAsync(name)</c>
///   (name validation and normalization included), then sets the fixed id on the
///   new, not-yet-tracked entity with ABP's <c>EntityHelper.TrySetId</c>, and fails
///   loudly if the id did not take.
///
/// Tenant names are fixed too, because <c>ITenantManager</c> enforces name
/// uniqueness and tests assert on them.
/// </summary>
public static class TenantsTestData
{
    public const string TenantAName = "TEST-tenant-a";
    public const string TenantBName = "TEST-tenant-b";

    /// <summary>
    /// Id of the tenant named <see cref="TenantAName"/> in every test application's
    /// database, seeded by
    /// <see cref="HealthcareSupport.CaseEvaluation.Testing.CaseEvaluationIntegrationTestSeedContributor"/>.
    /// </summary>
    public static Guid TenantARef { get; } = Guid.Parse("d1a00000-0000-0000-0000-00000000000a");

    /// <summary>
    /// Id of the tenant named <see cref="TenantBName"/>. See <see cref="TenantARef"/>.
    /// </summary>
    public static Guid TenantBRef { get; } = Guid.Parse("d1b00000-0000-0000-0000-00000000000b");
}
