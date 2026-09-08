using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.MultiTenancy;

/// <summary>
/// T1 (in-house hosting, 2026-07-09) -- the tenant-resolver host template is now
/// config-driven (App:TenantDomainFormat) so production can serve per-service
/// subdomains (e.g. "{0}.auth.portal.example.com") while local dev keeps
/// "{0}.localhost". These tests pin:
///   (a) FromConfiguration reads the key and falls back to the localhost default;
///   (b) ResolveAsync still extracts the office slug, treats "admin" as host context,
///       and leaves an unknown slug for ABP's middleware to 404 -- now against a
///       production-shaped, multi-label template.
/// Real usage: the contributor reads the Host header from an IHttpContextAccessor
/// resolved off the ITenantResolveContext service provider, exactly as ABP drives it.
/// </summary>
public class HostAwareDomainTenantResolveContributorTests
{
    private const string ProdAuthFormat = "{0}.auth.portal.example.test";

    // ---- FromConfiguration: config wiring ----

    [Fact]
    public void FromConfiguration_uses_the_configured_template()
    {
        var configuration = BuildConfiguration(
            (HostAwareDomainTenantResolveContributor.DomainFormatConfigKey, ProdAuthFormat));

        var resolver = HostAwareDomainTenantResolveContributor.FromConfiguration(configuration);

        resolver.DomainFormat.ShouldBe(ProdAuthFormat);
    }

    [Fact]
    public void FromConfiguration_falls_back_to_localhost_when_unset()
    {
        var resolver = HostAwareDomainTenantResolveContributor.FromConfiguration(BuildConfiguration());

        resolver.DomainFormat.ShouldBe(HostAwareDomainTenantResolveContributor.DefaultDomainFormat);
        resolver.DomainFormat.ShouldBe("{0}.localhost");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void FromConfiguration_falls_back_to_localhost_when_blank(string blank)
    {
        var configuration = BuildConfiguration(
            (HostAwareDomainTenantResolveContributor.DomainFormatConfigKey, blank));

        HostAwareDomainTenantResolveContributor.FromConfiguration(configuration)
            .DomainFormat.ShouldBe(HostAwareDomainTenantResolveContributor.DefaultDomainFormat);
    }

    // ---- ResolveAsync: production-shaped template ----

    [Fact]
    public async Task ResolveAsync_extracts_office_slug_from_a_production_subdomain()
    {
        var resolver = new HostAwareDomainTenantResolveContributor(ProdAuthFormat);
        var context = BuildResolveContext("falkinstein.auth.portal.example.test");

        await resolver.ResolveAsync(context);

        context.TenantIdOrName.ShouldBe("falkinstein");
        context.Handled.ShouldBeTrue();
    }

    [Fact]
    public async Task ResolveAsync_treats_admin_as_host_context()
    {
        var resolver = new HostAwareDomainTenantResolveContributor(ProdAuthFormat);
        var context = BuildResolveContext("admin.auth.portal.example.test");

        await resolver.ResolveAsync(context);

        context.TenantIdOrName.ShouldBeNull();
        context.Handled.ShouldBeFalse();
    }

    [Fact]
    public async Task ResolveAsync_passes_an_unknown_slug_to_the_store_for_a_404()
    {
        // A typo'd office is set as TenantIdOrName so ABP's MultiTenancyMiddleware
        // looks it up and 404s "Tenant not found!" -- ADR-007 typo protection preserved.
        var resolver = new HostAwareDomainTenantResolveContributor(ProdAuthFormat);
        var context = BuildResolveContext("falkinstien.auth.portal.example.test");

        await resolver.ResolveAsync(context);

        context.TenantIdOrName.ShouldBe("falkinstien");
        context.Handled.ShouldBeTrue();
    }

    [Fact]
    public async Task ResolveAsync_still_resolves_on_the_localhost_default()
    {
        var resolver = new HostAwareDomainTenantResolveContributor(
            HostAwareDomainTenantResolveContributor.DefaultDomainFormat);
        var context = BuildResolveContext("falkinstein.localhost");

        await resolver.ResolveAsync(context);

        context.TenantIdOrName.ShouldBe("falkinstein");
        context.Handled.ShouldBeTrue();
    }

    [Fact]
    public async Task ResolveAsync_ignores_a_host_that_does_not_match_the_template()
    {
        // The AuthServer's resolver (format "{0}.auth...") must not resolve a ".api." host.
        // nginx routes each service its own subdomain, so a mismatch means host context,
        // never a wrong-tenant resolution.
        var resolver = new HostAwareDomainTenantResolveContributor(ProdAuthFormat);
        var context = BuildResolveContext("falkinstein.api.portal.example.test");

        await resolver.ResolveAsync(context);

        context.TenantIdOrName.ShouldBeNull();
        context.Handled.ShouldBeFalse();
    }

    // ---- the three properties nothing asserted before phase 3 task 2 (2026-09-04) ----
    //
    // Every host string the 8 tests above feed is one of:
    //   admin.auth.portal.example.test        falkinstein.api.portal.example.test
    //   falkinstein.auth.portal.example.test  falkinstein.localhost
    //   falkinstien.auth.portal.example.test  (deliberate typo, unknown-slug case)
    //
    // NONE carries a port, NONE is empty, and NONE puts a dot in the slug position.
    // So three branches of ExtractSlug and the abstention guard were unasserted, and
    // each is a silent cross-office exposure if it regresses: a surviving port or an
    // accepted dotted slug both produce a tenant name that is not the one the URL
    // names.
    //
    // ALL THREE WERE SEEN TO FAIL, but not equally. The port and dot tests each fail
    // to a single-line deletion of the branch they guard. The empty-host test needs
    // BOTH of its defences removed before it fails -- see its own comment. That
    // asymmetry is stated rather than smoothed over, because "all three verified"
    // would imply three guards of the same strength.

    [Fact]
    public async Task ResolveAsync_strips_the_port_before_matching_the_template()
    {
        // Guards the colonIndex/Substring pair. Local dev and the in-house LAN box
        // both serve on explicit ports, so this path runs constantly and a
        // regression would 404 every office rather than fail loudly in CI.
        var resolver = new HostAwareDomainTenantResolveContributor(ProdAuthFormat);
        var context = BuildResolveContext("falkinstein.auth.portal.example.test:44368");

        await resolver.ResolveAsync(context);

        context.TenantIdOrName.ShouldBe("falkinstein");
        context.Handled.ShouldBeTrue();
    }

    [Fact]
    public async Task ResolveAsync_rejects_a_slug_that_contains_a_dot()
    {
        // Guards `slug.Contains('.') ? null : slug`. Without it a nested host would
        // yield the multi-label slug "falkinstein.extra" as a tenant NAME, so the
        // resolved office would depend on how many labels an attacker prepends.
        var resolver = new HostAwareDomainTenantResolveContributor(ProdAuthFormat);
        var context = BuildResolveContext("falkinstein.extra.auth.portal.example.test");

        await resolver.ResolveAsync(context);

        context.TenantIdOrName.ShouldBeNull();
        context.Handled.ShouldBeFalse();
    }

    [Fact]
    public async Task ResolveAsync_selects_no_tenant_when_the_host_header_has_no_value()
    {
        // WHAT THIS GUARDS, MEASURED RATHER THAN CLAIMED. An empty Host field value
        // is legal under RFC 9112 s3.2 and Kestrel accepts it, so this is reachable
        // from the network. The property worth pinning is the OUTCOME: an empty host
        // must not select a tenant.
        //
        // It is NOT a guard on `!httpContext.Request.Host.HasValue` (`:69-72`), and
        // an earlier version of this comment said it was. Two independent mechanisms
        // produce the same outcome, so NO SINGLE-LINE DELETION FAILS THIS TEST --
        // proven 2026-09-04 by four experiments:
        //
        //   remove the HasValue abstention alone     -> 12/12 still pass
        //     (ExtractSlug returns null for "", so the outcome is unchanged)
        //   make ExtractSlug yield a slug for "" alone -> 12/12 still pass
        //     (the abstention returns first, so the change is unreachable)
        //   remove BOTH                              -> THIS TEST FAILS, alone
        //
        // Also worth knowing: deleting the abstention does not even COMPILE on its
        // own. Nullable flow analysis then flags `Host.Value` (CS8604) and
        // TreatWarningsAsErrors turns that into a build failure, so the realistic
        // regression has to silence it with `!` deliberately.
        //
        // BUT THAT THIRD DEFENCE IS A SETTING, NOT A PROPERTY OF THE LANGUAGE, and
        // it is not permanent. It rests on Directory.Build.props:17 <Nullable>enable
        // and :21 <TreatWarningsAsErrors>true. That second one has been FALSE
        // before -- the file's own note at :9 records it being "flipped to true in
        // Phase B-6 PR-0 after B-2.1 closed out the 480 nullability warnings". Relax
        // it again and CS8604 drops back to a warning, the compiler stops refusing
        // the deletion, and nothing reports that the defence went. So this is two
        // defences plus a setting, not three permanent ones.
        //
        // So this is defence in depth, and the test is an outcome pin rather than a
        // line guard. Recorded precisely because "guards the abstention" would read
        // as more protection than exists.
        var resolver = new HostAwareDomainTenantResolveContributor(ProdAuthFormat);
        var context = BuildResolveContext(string.Empty);

        await resolver.ResolveAsync(context);

        context.TenantIdOrName.ShouldBeNull();
        context.Handled.ShouldBeFalse();
    }

    // ---- APP-OWN-03: what happens when the token and the hostname disagree ----

    [Fact]
    public async Task An_office_A_token_presented_to_office_B_resolves_from_the_token()
    {
        // CHARACTERIZATION. This records what the system does today. It does NOT say
        // whether that is correct -- that depends on whether anything downstream
        // trusts the hostname for authorisation, which is an OPEN QUESTION and is not
        // settled here, in this name, or in any assertion message below.
        //
        // Task 1 (TenantResolverChainTests) asserts CurrentUserTenantResolveContributor
        // is FIRST in both processes. This asserts what being first means: the two
        // contributors genuinely disagree about the same request, so order decides.
        var officeA = Guid.NewGuid();
        var currentUser = AuthenticatedUserOfOffice(officeA);
        const string officeBHost = "officeb.auth.portal.example.test";

        var fromToken = BuildResolveContext(officeBHost, currentUser);
        await new CurrentUserTenantResolveContributor().ResolveAsync(fromToken);

        fromToken.TenantIdOrName.ShouldBe(officeA.ToString());

        // The same request, resolved by the host contributor alone, names office B.
        // Asserted rather than assumed: without it, "the token wins" would rest on
        // the belief that the host would have said something different.
        var fromHost = BuildResolveContext(officeBHost, currentUser);
        await new HostAwareDomainTenantResolveContributor(ProdAuthFormat).ResolveAsync(fromHost);

        fromHost.TenantIdOrName.ShouldBe("officeb");
    }

    // ---- helpers ----

    private static IConfiguration BuildConfiguration(params (string Key, string Value)[] values)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in values)
        {
            dict[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    // `currentUser` is OPTIONAL and defaults to not being registered at all, so every
    // test written before 2026-09-04 behaves exactly as it did. A required parameter
    // would have touched all 12 of them and made the diff read as a rewrite of a file
    // whose other tests were not under review.
    private static ITenantResolveContext BuildResolveContext(
        string host,
        ICurrentUser? currentUser = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString(host);

        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor>(
            new HttpContextAccessor { HttpContext = httpContext });

        if (currentUser is not null)
        {
            services.AddSingleton(currentUser);
        }

        return new FakeTenantResolveContext { ServiceProvider = services.BuildServiceProvider() };
    }

    /// <summary>
    /// An authenticated caller whose token carries office A's tenant id.
    ///
    /// <para>FIRST substitution of <c>ICurrentUser</c> in this test tree, so it is the
    /// pattern whoever copies it next will follow. Both members are configured
    /// DELIBERATELY: NSubstitute returns a stub rather than null for unconfigured
    /// members, so an unconfigured substitute would quietly supply a plausible value
    /// and a green test would prove nothing about what the contributor read.</para>
    /// </summary>
    private static ICurrentUser AuthenticatedUserOfOffice(Guid officeId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAuthenticated.Returns(true);
        currentUser.TenantId.Returns(officeId);
        return currentUser;
    }

    // The concrete Volo.Abp.MultiTenancy.TenantResolveContext is defined in two
    // referenced assemblies (Abstractions + main), so referencing it directly is an
    // ambiguous CS0433. The contributor only reads ServiceProvider and writes
    // TenantIdOrName / Handled, so a minimal ITenantResolveContext double is enough.
    private sealed class FakeTenantResolveContext : ITenantResolveContext
    {
        public IServiceProvider ServiceProvider { get; init; } = default!;
        public string? TenantIdOrName { get; set; }
        public bool Handled { get; set; }
    }
}
