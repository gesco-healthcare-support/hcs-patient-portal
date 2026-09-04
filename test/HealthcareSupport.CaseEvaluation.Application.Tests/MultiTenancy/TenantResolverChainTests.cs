using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.MultiTenancy;

/// <summary>
/// Phase 3 item APP-OWN-01 (2026-09-04) -- pins the tenant-resolver chain in BOTH
/// processes.
///
/// <para>WHY THIS EXISTS. Tenancy is the system's most dangerous boundary: each office
/// is a separate database, so a resolver that honours a caller-supplied value is
/// cross-office data exposure. Both modules call <c>TenantResolvers.Clear()</c> and then
/// register exactly two contributors, which is what makes
/// <c>?__tenant=GUID</c> inert. Until this file existed, that claim rested ENTIRELY on
/// reading two registration sites -- `git grep -ln "TenantResolvers" -- 'test/**/*.cs'`
/// returned nothing across 323 backend test files.</para>
///
/// <para>The failure it is designed to catch is not a developer deleting a line. It is an
/// ABP upgrade re-adding a default <c>__tenant</c> resolver, which changes no file in this
/// repository and would otherwise be invisible.</para>
///
/// <para>Mirrors <c>CaseEvaluationHttpApiHostModuleTests</c>: build a
/// <see cref="ServiceCollection"/>, run the module's internal configuration helper against
/// it, then read the assembled options back out. No host is booted and no database is
/// touched.</para>
///
/// <para><b>WHAT THIS FILE DOES NOT PROVE, stated because a reader who finds "asserts the
/// resolver chain" will assume it covers more.</b> The decoy below is seeded BEFORE the
/// module's callback by construction, so these tests prove that <c>Clear()</c> removes
/// whatever is already present. They do NOT prove that ABP's own default resolvers are
/// present at that moment to be removed -- that depends on module ordering in a real
/// assembled application, which a bare <see cref="ServiceCollection"/> cannot exhibit. If
/// the framework registered its defaults AFTER our callback, they would survive in
/// production and every test here would still be green.</para>
///
/// <para>So this is a strong regression guard against our configuration changing, and only
/// a partial answer to the original question the phase asked -- whether the framework's
/// defaults are actually cleared. Closing that needs the options resolved from a booted
/// application (the repository has <c>AbpIntegratedTest</c> infrastructure for it) and is
/// tracked rather than assumed.</para>
/// </summary>
public class TenantResolverChainTests
{
    /// <summary>
    /// A stand-in for a resolver the framework might already have registered before our
    /// module runs. Named for a caller-supplied source on purpose, so it trips the
    /// "no caller-supplied tenant" theory below as well as the count.
    /// </summary>
    private sealed class QueryStringDecoyTenantResolveContributor : TenantResolveContributorBase
    {
        public override string Name => "QueryStringDecoy";

        public override Task ResolveAsync(ITenantResolveContext context) => Task.CompletedTask;
    }

    /// <summary>Runs a module's multi-tenancy configuration and returns the assembled chain.</summary>
    private static IReadOnlyList<ITenantResolveContributor> ResolversFor(
        Action<ServiceConfigurationContext, IConfiguration> configureMultiTenancy)
    {
        var services = new ServiceCollection();
        services.AddOptions();

        // THE DECOY IS LOAD-BEARING AND MUST NOT BE DELETED AS SETUP NOISE.
        //
        // Without it these tests CANNOT FAIL for the reason they exist. Measured
        // 2026-09-04: with the decoy absent, deleting `TenantResolvers.Clear()` from the
        // module left all 7 tests green -- a bare ServiceCollection has no resolvers, so
        // Clear() is a no-op and "exactly two, in this order" holds either way. That is
        // the same defect as phase 1's full-logout.spec.ts:47, a test that could never
        // have failed.
        //
        // Configure callbacks run in registration order, so this lands BEFORE the
        // module's. If Clear() is removed, the decoy survives and the count, the order
        // and the caller-supplied theory all fail together.
        services.Configure<AbpTenantResolveOptions>(
            options => options.TenantResolvers.Add(new QueryStringDecoyTenantResolveContributor()));

        var context = new ServiceConfigurationContext(services);

        // Empty configuration on purpose: HostAwareDomainTenantResolveContributor
        // .FromConfiguration falls back to the "{0}.localhost" default when
        // App:TenantDomainFormat is unset, and the template is not what this file pins --
        // HostAwareDomainTenantResolveContributorTests already covers that.
        var configuration = new ConfigurationBuilder().Build();

        configureMultiTenancy(context, configuration);

        return services.BuildServiceProvider()
            .GetRequiredService<IOptions<AbpTenantResolveOptions>>()
            .Value.TenantResolvers;
    }

    private static IReadOnlyList<ITenantResolveContributor> HttpApiHostResolvers() =>
        ResolversFor(CaseEvaluationHttpApiHostModule.ConfigureMultiTenancy);

    private static IReadOnlyList<ITenantResolveContributor> AuthServerResolvers() =>
        ResolversFor(CaseEvaluationAuthServerModule.ConfigureMultiTenancy);

    // ---- the chain, per process ----

    [Fact]
    public void HttpApiHost_resolves_tenancy_from_the_current_user_then_the_host_and_nothing_else()
    {
        var resolvers = HttpApiHostResolvers();

        resolvers.Count.ShouldBe(2);
        resolvers[0].ShouldBeOfType<CurrentUserTenantResolveContributor>();
        resolvers[1].ShouldBeOfType<HostAwareDomainTenantResolveContributor>();
    }

    [Fact]
    public void AuthServer_resolves_tenancy_from_the_current_user_then_the_host_and_nothing_else()
    {
        var resolvers = AuthServerResolvers();

        resolvers.Count.ShouldBe(2);
        resolvers[0].ShouldBeOfType<CurrentUserTenantResolveContributor>();
        resolvers[1].ShouldBeOfType<HostAwareDomainTenantResolveContributor>();
    }

    [Fact]
    public void Both_processes_register_the_same_chain_in_the_same_order()
    {
        // ADR-006/007: the AuthServer and the API must agree, or a token minted on one
        // host would resolve to a different office on the other.
        HttpApiHostResolvers().Select(r => r.GetType())
            .ShouldBe(AuthServerResolvers().Select(r => r.GetType()));
    }

    // ---- the property the chain exists to guarantee ----

    [Theory]
    [InlineData("QueryString")]
    [InlineData("Cookie")]
    [InlineData("Route")]
    [InlineData("Header")]
    public void Neither_process_accepts_a_caller_supplied_tenant(string callerSuppliedSource)
    {
        // Strictly implied by the two assertions above -- kept anyway, and deliberately.
        // It states the INTENT those assertions protect, so someone relaxing "exactly two"
        // has to read what they are giving up. ADR-006: dropping the QueryString, Cookie,
        // Route and Header resolvers is what stops ?__tenant=GUID switching offices from
        // the URL bar, and that is HIPAA-relevant rather than merely tidy.
        //
        // Matched on type NAME rather than type: naming the ABP types would import them,
        // and a test that imports the thing it forbids is one refactor away from
        // asserting nothing.
        foreach (var resolvers in new[] { HttpApiHostResolvers(), AuthServerResolvers() })
        {
            resolvers.ShouldNotContain(
                r => r.GetType().Name.Contains(callerSuppliedSource, StringComparison.Ordinal),
                $"a {callerSuppliedSource} tenant resolver would let a caller choose their own office");
        }
    }
}
