using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore;

/// <summary>
/// Boots a graph that runs the API host's <c>ConfigureMultiTenancy</c> AFTER the framework's own
/// modules, so the assembled <c>AbpTenantResolveOptions</c> can be read.
/// </summary>
/// <remarks>
/// ABP configures a module's dependencies BEFORE the module itself, so depending on
/// <see cref="CaseEvaluationEntityFrameworkCoreTestModule"/> puts this callback last -- which is
/// the ordering under test. This mirrors the real call site at
/// <c>CaseEvaluationHttpApiHostModule.cs:110</c>.
/// </remarks>
[DependsOn(typeof(CaseEvaluationEntityFrameworkCoreTestModule))]
public class BootedApiTenantResolverOrderingTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        CaseEvaluationHttpApiHostModule.ConfigureMultiTenancy(
            context,
            context.Services.GetConfiguration());
    }
}

/// <summary>
/// The AuthServer half. Separate graph, because both helpers configure the SAME options type --
/// running them in one application would let the second <c>Clear()</c> erase the first's work and
/// neither could be attributed. Mirrors the real call site at
/// <c>CaseEvaluationAuthServerModule.cs:504</c>.
/// </summary>
[DependsOn(typeof(CaseEvaluationEntityFrameworkCoreTestModule))]
public class BootedAuthTenantResolverOrderingTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        CaseEvaluationAuthServerModule.ConfigureMultiTenancy(
            context,
            context.Services.GetConfiguration());
    }
}

/// <summary>
/// Phase 3 task 9, second half -- our <c>Clear()</c> runs AFTER the framework's registration and
/// wins, asserted on a BOOTED application.
///
/// <para><b>THE GAP THIS CLOSES, and it is the last one in this chain.</b>
/// <c>TenantResolverChainTests</c> (task 1) runs the helper against a bare
/// <c>ServiceCollection</c>: it proves <c>Clear()</c> removes what is present, but it seeds its own
/// decoy, so it cannot show ABP's defaults were there to be removed.
/// <c>BootedTenantResolverDefaultsTests</c> (#716) proves the framework really does contribute
/// <c>CurrentUser</c> to a booted graph, but that graph never runs our helper, so it cannot show
/// ours wins. <b>Neither can see the ordering. This file boots a graph that runs both, so the
/// assembled result is the answer.</b></para>
///
/// <para><b>WHY THE ORDERING IS A REAL QUESTION rather than an obvious yes.</b> If the framework
/// registered its defaults AFTER our callback, production would carry a resolver we believe we
/// removed -- and every existing test would stay green, because none of them boots an application.
/// In a database-per-tenant system a surviving caller-supplied resolver (<c>?__tenant=</c> in a
/// query string, header, cookie or route) is direct cross-office PHI exposure, not a privilege
/// nuisance.</para>
///
/// <para><b>WHAT MAKES THE ORDERING CLAIM PRECISE.</b>
/// <see cref="OptionsType_HasNoResolversOfItsOwn_SoAnythingPresentCameFromAConfigureAction"/>
/// pins that <c>AbpTenantResolveOptions</c> is EMPTY on construction. That matters: if the
/// framework's <c>CurrentUser</c> came from the options constructor rather than from a configure
/// action, then any <c>Clear()</c> would beat it regardless of module order and the word
/// "ordering" would be wrong. It does not, so the sequence really is
/// framework-configures-then-we-clear.</para>
///
/// <para><b>THE HONEST LIMIT, stated so nobody reads this as more than it is.</b> Today the only
/// framework default is <c>CurrentUser</c>, and our helper re-adds a contributor of that same
/// name. So a missing <c>Clear()</c> currently produces a harmless DUPLICATE rather than an
/// exposure. <b>The value of this test is prospective:</b> it is the thing that fails on the day an
/// ABP upgrade contributes a caller-supplied resolver, which is a change to no file in this
/// repository. Read the duplicate as the SYMPTOM, not as the risk.</para>
///
/// <para><b>SEEN TO FAIL 2026-09-08.</b> <c>options.TenantResolvers.Clear()</c> was deleted from
/// BOTH host modules -- <c>CaseEvaluationHttpApiHostModule.cs:414</c> and
/// <c>CaseEvaluationAuthServerModule.cs:543</c> -- and rebuilt. <c>build_exit=0</c> was asserted
/// before the run, so these results are a fresh binary and not a stale one.
/// <b>Failed: 4, Passed: 1</b>, both graphs failing independently:</para>
/// <code>
/// should be ["CurrentUser", "HostAwareDomain"]
/// but was   ["CurrentUser", "CurrentUser", "HostAwareDomain"]
///
/// names.Distinct().Count() should be 3 but was 2
/// </code>
/// <para>The one pass is
/// <see cref="OptionsType_HasNoResolversOfItsOwn_SoAnythingPresentCameFromAConfigureAction"/>,
/// which is the CONTROL: it does not depend on our helper at all, so its staying green is what
/// shows the break disabled this guarantee rather than breaking the whole graph. Both production
/// files were then restored and verified BYTE-IDENTICAL with <c>git hash-object</c>.</para>
///
/// <para><b>The break CONFIRMS the ordering rather than merely detecting a change.</b> With
/// <c>Clear()</c> gone the framework's <c>CurrentUser</c> is still present and ours is appended
/// AFTER it. That sequence is only possible if the framework registered first and our callback ran
/// second -- which is precisely the fact task 1 and #716 could each only half-establish.</para>
///
/// <para><b>Both host modules are covered deliberately.</b> The AuthServer mints the token that
/// <c>CurrentUserTenantResolveContributor</c> later reads, so a default re-added on the login path
/// is at least as serious as one on the API path, and its module is the one that declares no
/// multi-tenancy module of its own.</para>
/// </summary>
/// <remarks>
/// The <c>[Collection]</c> attribute is REQUIRED, not decoration -- see
/// <c>BootedTenantResolverDefaultsTests</c>, which records the FOREIGN KEY failure that omitting it
/// produces in a full run while passing under a filter.
/// </remarks>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class BootedApiTenantResolverOrderingTests
    : CaseEvaluationTestBase<BootedApiTenantResolverOrderingTestModule>
{
    /// <summary>
    /// The whole point: after a real boot, the set is exactly ours, in our order.
    /// </summary>
    [Fact]
    public void BootedApiGraph_AfterOurConfigureMultiTenancy_ContainsExactlyOurTwoResolvers()
    {
        var resolvers = GetRequiredService<IOptions<AbpTenantResolveOptions>>().Value.TenantResolvers;

        var expected = new[] { "CurrentUser", "HostAwareDomain" };
        resolvers.Select(r => r.Name).ShouldBe(
            expected,
            "the API host's ConfigureMultiTenancy ran after the framework's modules, so the "
            + "assembled options must contain exactly the two contributors it registers. If a "
            + "THIRD entry is present, Clear() did not remove what the framework had already "
            + "registered -- either it was deleted, or a framework module now configures these "
            + "options after we do. Either way a resolver we believe we removed is live in "
            + "production, and this repository would otherwise show nothing.");
    }

    /// <summary>
    /// The precise failure a missing <c>Clear()</c> produces today, named as itself.
    /// </summary>
    [Fact]
    public void BootedApiGraph_NoResolverNameAppearsTwice()
    {
        var names = GetRequiredService<IOptions<AbpTenantResolveOptions>>()
            .Value.TenantResolvers.Select(r => r.Name).ToArray();

        names.Distinct().Count().ShouldBe(
            names.Length,
            "a duplicated resolver name is what a missing Clear() looks like on today's ABP: the "
            + "framework's CurrentUser survives and ours is appended beside it. The duplicate is "
            + "harmless in itself -- it is the evidence that Clear() is not running or not "
            + "winning, which is the guarantee that stops a FUTURE framework default surviving.");
    }

    /// <summary>
    /// Establishes that the resolvers seen above came from configure actions rather than from the
    /// options object's own construction, which is what makes "ordering" the right word.
    /// </summary>
    [Fact]
    public void OptionsType_HasNoResolversOfItsOwn_SoAnythingPresentCameFromAConfigureAction()
    {
        new AbpTenantResolveOptions().TenantResolvers.ShouldBeEmpty(
            "AbpTenantResolveOptions must start empty. If ABP ever seeds resolvers in the "
            + "constructor instead, Clear() would beat them regardless of module ordering, and "
            + "the ordering guarantee these tests assert would be proving something weaker than "
            + "it claims. This fact is what keeps that claim honest.");
    }
}

/// <summary>
/// The AuthServer half of the same guarantee. See
/// <see cref="BootedApiTenantResolverOrderingTests"/> for the full reasoning; it is not repeated
/// here so the two cannot drift.
/// </summary>
[Collection(CaseEvaluationTestConsts.CollectionDefinitionName)]
public class BootedAuthTenantResolverOrderingTests
    : CaseEvaluationTestBase<BootedAuthTenantResolverOrderingTestModule>
{
    [Fact]
    public void BootedAuthGraph_AfterOurConfigureMultiTenancy_ContainsExactlyOurTwoResolvers()
    {
        var resolvers = GetRequiredService<IOptions<AbpTenantResolveOptions>>().Value.TenantResolvers;

        var expected = new[] { "CurrentUser", "HostAwareDomain" };
        resolvers.Select(r => r.Name).ShouldBe(
            expected,
            "the AuthServer's ConfigureMultiTenancy ran after the framework's modules, so the "
            + "assembled options must contain exactly the two contributors it registers. This is "
            + "the login path: the AuthServer mints the token CurrentUserTenantResolveContributor "
            + "later reads, so a framework default surviving here is the more dangerous half.");
    }

    [Fact]
    public void BootedAuthGraph_NoResolverNameAppearsTwice()
    {
        var names = GetRequiredService<IOptions<AbpTenantResolveOptions>>()
            .Value.TenantResolvers.Select(r => r.Name).ToArray();

        names.Distinct().Count().ShouldBe(
            names.Length,
            "a duplicated resolver name is what a missing Clear() looks like on today's ABP. See "
            + "BootedApiTenantResolverOrderingTests for why the duplicate is the symptom rather "
            + "than the risk.");
    }
}
