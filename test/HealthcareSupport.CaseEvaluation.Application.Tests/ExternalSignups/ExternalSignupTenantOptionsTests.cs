using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.TestData;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// The seam: the two anonymous office-discovery endpoints the AuthServer register overlay calls
/// before anyone is signed in -- <c>GetTenantOptionsAsync</c> (the host-only office picker) and
/// <c>ResolveTenantByNameAsync</c> (turning a <c>?__tenant=&lt;Name&gt;</c> query string into the
/// GUID the register POST needs).
///
/// <para>Why the emptiness assertion below is real and not an empty fixture:
/// <c>Volo.Saas.Tenants.Tenant</c> does NOT implement <c>IMultiTenant</c> -- the string
/// "IMultiTenant" does not appear anywhere in Volo.Saas.Domain.dll's metadata -- so an in-office
/// query against the tenant registry is UNFILTERED and would hand an in-office caller the full
/// list of every other office. The guard is the only thing stopping that, and the host-scope half
/// of the test proves the rows are there to be handed out.</para>
///
/// <para>NOT pinned here: the <c>CurrentTenant.Change(null)</c> inside
/// <c>ResolveTenantByNameAsync</c>. That switch exists for CONNECTION ROUTING under
/// database-per-office (the registry lives in the management database); this rig runs one SQLite
/// connection, and the entity is not filtered, so removing the switch would change nothing
/// observable here. The test below claims case-insensitivity, null-on-miss and the blank guard,
/// and nothing about host context. Do not read more into it.</para>
///
/// <para>Also not pinned: authorization, which always-allow neutralises; and the Take(200) cap,
/// which would need 200 synthetic offices in a rig that never rolls back.</para>
/// </summary>
public abstract class ExternalSignupTenantOptionsTests<TStartupModule>
    : CaseEvaluationApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IExternalSignupAppService _appService;
    private readonly ICurrentTenant _currentTenant;

    protected ExternalSignupTenantOptionsTests()
    {
        _appService = GetRequiredService<IExternalSignupAppService>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    private static string NewToken() => Guid.NewGuid().ToString("N")[..8];

    /// <summary>
    /// The office picker is a HOST surface. Someone already inside an office must not be able to
    /// enumerate the others through it.
    /// </summary>
    [Fact]
    public async Task GetTenantOptionsAsync_InsideAnOffice_ReturnsNothing()
    {
        // At host scope the offices ARE enumerable, so the emptiness below is the guard.
        var atHostScope = await _appService.GetTenantOptionsAsync(TenantsTestData.TenantAName);
        atHostScope.Items.ShouldContain(i => i.Id == TenantsTestData.TenantARef);

        using (_currentTenant.Change(TenantsTestData.TenantARef))
        {
            var insideAnOffice = await _appService.GetTenantOptionsAsync(TenantsTestData.TenantAName);
            insideAnOffice.Items.ShouldBeEmpty();
        }
    }

    /// <summary>
    /// The picker narrows by name as the operator types. Asserted by membership and a predicate
    /// over every returned row, never by a count: the rig accumulates across the whole run.
    /// </summary>
    [Fact]
    public async Task GetTenantOptionsAsync_HostScope_FiltersByName()
    {
        var filtered = await _appService.GetTenantOptionsAsync(TenantsTestData.TenantAName);

        filtered.Items.ShouldContain(i => i.Id == TenantsTestData.TenantARef);
        filtered.Items.ShouldAllBe(i => i.DisplayName.Contains(TenantsTestData.TenantAName));

        // The office that must be excluded EXISTS: it comes back when the filter is dropped.
        var unfiltered = await _appService.GetTenantOptionsAsync(null);
        unfiltered.Items.ShouldContain(i => i.Id == TenantsTestData.TenantBRef);
    }

    /// <summary>
    /// The invite link may carry the office name in any casing a mail client or a human typed, so
    /// the match is case-insensitive; an unknown or blank name is a miss, not an error, because
    /// the overlay falls back to the office picker.
    /// </summary>
    [Fact]
    public async Task ResolveTenantByNameAsync_MatchesCaseInsensitivelyAndReturnsNullOnMiss()
    {
        var upperCased = await _appService.ResolveTenantByNameAsync(
            TenantsTestData.TenantAName.ToUpperInvariant());
        upperCased.ShouldNotBeNull();
        upperCased!.Id.ShouldBe(TenantsTestData.TenantARef);
        upperCased.DisplayName.ShouldBe(TenantsTestData.TenantAName);

        // Surrounding whitespace is trimmed rather than treated as part of the name.
        var padded = await _appService.ResolveTenantByNameAsync($"  {TenantsTestData.TenantAName}  ");
        padded.ShouldNotBeNull();
        padded!.Id.ShouldBe(TenantsTestData.TenantARef);

        (await _appService.ResolveTenantByNameAsync($"TEST-no-such-office-{NewToken()}")).ShouldBeNull();
        (await _appService.ResolveTenantByNameAsync("   ")).ShouldBeNull();
    }
}
