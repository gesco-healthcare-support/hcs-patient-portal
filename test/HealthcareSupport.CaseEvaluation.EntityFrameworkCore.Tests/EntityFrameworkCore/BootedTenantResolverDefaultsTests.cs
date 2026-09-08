using System.Linq;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

/// <summary>
/// Phase 3 task 9 -- the half of the tenancy question a bare <c>ServiceCollection</c> cannot
/// answer, closed by reading the options out of a BOOTED application.
///
/// <para><b>THE GAP THIS CLOSES.</b> <c>TenantResolverChainTests</c> (task 1) proves our modules
/// clear whatever is in <c>AbpTenantResolveOptions</c> and register exactly two contributors. Its
/// own docstring is careful to say what it cannot prove: that ABP's defaults are actually
/// PRESENT at that moment to be removed. It seeds its own decoy, so if the framework registered
/// its defaults AFTER our callback they would survive in production and every test there would
/// still be green. Answering that needs a real assembled application, which is what this file
/// boots.</para>
///
/// <para><b>MEASURED 2026-09-08.</b> A booted graph that does NOT call either host module's
/// <c>ConfigureMultiTenancy</c> contains exactly one framework default:</para>
/// <code>
/// count=1; [CurrentUser=CurrentUserTenantResolveContributor]
/// </code>
/// <para>Verified first that nothing in the harness seeds this -- neither
/// <c>CaseEvaluationTestBaseModule</c> nor <c>CaseEvaluationMultiOfficeTestModule</c> mentions
/// <c>TenantResolvers</c> or <c>AbpTenantResolveOptions</c>. So the framework really does
/// contribute a resolver of its own accord, and <c>Clear()</c> in the host modules is removing
/// something rather than being decorative. <b>That is the fact task 1 could not exhibit.</b></para>
///
/// <para><b>WHAT THIS TEST IS FOR.</b> Not our code -- ABP's. The failure it exists to catch is
/// the one task 1 names as the real risk: <i>an ABP upgrade re-adding a default resolver, which
/// changes no file in this repository and would otherwise be invisible.</i> A framework that
/// starts contributing a caller-supplied resolver (query string, header, cookie, route) is a
/// cross-office exposure risk in a database-per-tenant system, and nothing else in the suite
/// would notice. This asserts the exact set, so an addition fails rather than passing quietly.
/// Asserting the SET and not just the count is deliberate: a swap would keep the count at one.</para>
///
/// <para><b>SEEN TO FAIL 2026-09-08.</b> One extra resolver was registered into the booted graph
/// -- exactly what an ABP upgrade would do -- and both facts failed independently:</para>
/// <code>
/// should be ["CurrentUser"] but was ["CurrentUser", "QueryString"]
/// callerSupplied should be empty but had 1 item
/// </code>
/// <para>Two facts rather than one on purpose: the first catches ANY change to the set, the
/// second only the dangerous kind. The first would also fail on a harmless addition, so the
/// second is what tells a reader whether a failure is a nuisance or an exposure.</para>
///
/// <para><b>WHAT THIS TEST DOES NOT PROVE -- stated so nobody reads it as more.</b> This graph
/// does not include either host module, so it does NOT show that our <c>Clear()</c> runs after
/// the framework's registration and wins. Closing that needs a booted graph carrying our own
/// <c>ConfigureMultiTenancy</c>, and today there is no route to one: the helper is
/// <c>internal</c> with <c>InternalsVisibleTo</c> granted only to
/// <c>HealthcareSupport.CaseEvaluation.Application.Tests</c>, which cannot boot this graph (its
/// integration tests are abstract and are concretised HERE, so the reference runs this way and
/// cannot be reversed). Extending that attribute is a change to production source, so it is a
/// decision rather than something to absorb inside a characterization pass. Recorded, not
/// worked around.</para>
///
/// <para><b>Do not "improve" this by importing the ASP.NET hosting stack into the test project
/// to reach the host modules.</b> The host graphs carry ASP.NET multi-tenancy modules
/// (<c>CaseEvaluationHttpApiHostModule.cs:69</c> declares
/// <c>AbpAspNetCoreMvcUiMultiTenancyModule</c>; the AuthServer declares no multi-tenancy module
/// at all and its resolved set is UNMEASURED). Forcing hosting into the harness to chase them
/// would be a large change to how every test in this project boots, made for one assertion.</para>
/// </summary>
/// <remarks>
/// The <c>[Collection]</c> attribute is REQUIRED, not decoration. Every other test class in this
/// project carries it, and without it xUnit runs this class in parallel with them; they share one
/// SQLite database, so two applications initialize and seed at once. Measured 2026-09-08: omitting
/// it made both facts below fail in the FULL suite with
/// <c>SqliteException: SQLite Error 19: 'FOREIGN KEY constraint failed'</c> thrown from this
/// class's constructor, while passing when run under a filter. <b>A filtered run cannot detect
/// this</b> -- if you verify a new test class here only with <c>--filter</c>, you have not
/// verified it.
/// </remarks>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class BootedTenantResolverDefaultsTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    /// <summary>
    /// The framework's contribution, pinned by name.
    /// </summary>
    [Fact]
    public void BootedApplication_FrameworkTenantResolverDefaults_AreExactlyCurrentUser()
    {
        var resolvers = GetRequiredService<IOptions<AbpTenantResolveOptions>>().Value.TenantResolvers;

        resolvers.Select(r => r.Name).ShouldBe(
            new[] { "CurrentUser" },
            "a booted graph without our ConfigureMultiTenancy must contain exactly ABP's one "
            + "default resolver. If this list GREW, the framework has started contributing a "
            + "resolver we never registered -- and if the new one honours a caller-supplied "
            + "value (__tenant in a query string, header, cookie or route) that is cross-office "
            + "PHI exposure in a database-per-tenant system. If it SHRANK to empty, the premise "
            + "of TenantResolverChainTests is gone: Clear() would be removing nothing, and that "
            + "test's decoy would be the only thing it ever removes.");
    }

    /// <summary>
    /// The framework default must not be one that honours a caller-supplied tenant. This is the
    /// property that actually matters, asserted independently of the exact name above so a
    /// rename in ABP does not quietly turn the guarantee off.
    /// </summary>
    [Fact]
    public void BootedApplication_NoFrameworkDefault_HonoursACallerSuppliedTenant()
    {
        var resolvers = GetRequiredService<IOptions<AbpTenantResolveOptions>>().Value.TenantResolvers;

        var callerSupplied = resolvers
            .Select(r => r.Name)
            .Where(n => n is "QueryString" or "Header" or "Cookie" or "Route" or "Form")
            .ToArray();

        callerSupplied.ShouldBeEmpty(
            "these resolvers read the tenant from the request, so any of them in a booted graph "
            + "means a caller can select which office's database to read. The portal is "
            + "database-per-tenant, so that is not a privilege bug, it is direct PHI exposure.");
    }
}
