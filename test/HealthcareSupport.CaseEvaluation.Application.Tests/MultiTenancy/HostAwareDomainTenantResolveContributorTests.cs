using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Volo.Abp;
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
///
/// <para>B1 (2026-09-25): a Host that names no office is now REFUSED instead of running in
/// host context. Before B1 the contributor abstained on any host that did not fit the
/// template, and abstaining IS host context -- nothing follows it in the chain. Only two
/// things still reach host context on purpose: the reserved <c>admin</c> label and the
/// internal names in <see cref="HostAwareDomainTenantResolveContributor.InternalHosts"/>.
/// Five facts below used to assert that abstention; they now assert the refusal, and their
/// names say so.</para>
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
    public async Task ResolveAsync_refuses_the_other_services_host()
    {
        // The AuthServer's resolver (format "{0}.auth...") must not resolve a ".api." host.
        // nginx routes each service its own subdomain, so a mismatch is a misroute. Before
        // B1 it ran in host context; now it is refused, and still never a wrong-tenant
        // resolution.
        var resolver = new HostAwareDomainTenantResolveContributor(ProdAuthFormat);
        var context = BuildResolveContext("falkinstein.api.portal.example.test");

        await ShouldRefuseAsync(resolver, context, "falkinstein.api.portal.example.test");
    }

    // ---- B1: hosts that name no office are refused ----

    [Theory]
    [InlineData("auth.portal.example.test")]                                  // the bare service host
    [InlineData("evil.example.test")]                                         // a foreign host
    [InlineData("203.0.113.7:8080")]                                          // an IP, with a port
    [InlineData("falkinstein.auth.portal.example.test.evil.example.test")]    // the suffix mid-host
    public async Task ResolveAsync_refuses_a_host_that_names_no_office(string host)
    {
        var resolver = new HostAwareDomainTenantResolveContributor(ProdAuthFormat);
        var context = BuildResolveContext(host);

        await ShouldRefuseAsync(resolver, context, host);
    }

    [Theory]
    [InlineData(ProdAuthFormat, "localhost:8080")]        // the compose health checks
    [InlineData(ProdAuthFormat, "authserver:8080")]       // the API's metadata fetch (MetaAddress)
    [InlineData(ProdAuthFormat, "LOCALHOST:8080")]
    [InlineData(ProdAuthFormat, "AuthServer")]
    [InlineData(HostAwareDomainTenantResolveContributor.DefaultDomainFormat, "localhost:44327")]
    [InlineData(HostAwareDomainTenantResolveContributor.DefaultDomainFormat, "authserver:8080")]
    [InlineData(HostAwareDomainTenantResolveContributor.DefaultDomainFormat, "LOCALHOST:8080")]
    [InlineData(HostAwareDomainTenantResolveContributor.DefaultDomainFormat, "AuthServer")]
    public async Task ResolveAsync_keeps_host_context_for_an_internal_host(string format, string host)
    {
        // Host context exactly as before B1: nothing set, nothing handled. In the real
        // chain nothing follows this contributor, so "left unhandled" IS host context.
        var resolver = new HostAwareDomainTenantResolveContributor(format);
        var context = BuildResolveContext(host);

        await resolver.ResolveAsync(context);

        context.TenantIdOrName.ShouldBeNull();
        context.Handled.ShouldBeFalse();
    }

    [Theory]
    [InlineData("localhost.example.test")]
    [InlineData("authserver.example.test")]
    [InlineData("notlocalhost")]
    [InlineData("evil-authserver")]
    [InlineData("api")]          // D3 (2026-09-25): no caller sends Host `api`, so it is not listed
    [InlineData("api:8080")]
    public async Task ResolveAsync_refuses_a_host_that_only_resembles_an_internal_name(string host)
    {
        // Seeded with what the allow list must reject. A StartsWith / EndsWith / Contains
        // comparison would admit each of these, so each fails that implementation by name.
        var resolver = new HostAwareDomainTenantResolveContributor(ProdAuthFormat);
        var context = BuildResolveContext(host);

        await ShouldRefuseAsync(resolver, context, host);
    }

    [Fact]
    public async Task ResolveAsync_treats_admin_in_any_case_as_host_context()
    {
        var resolver = new HostAwareDomainTenantResolveContributor(ProdAuthFormat);
        var context = BuildResolveContext("ADMIN.auth.portal.example.test");

        await resolver.ResolveAsync(context);

        context.TenantIdOrName.ShouldBeNull();
        context.Handled.ShouldBeFalse();
    }

    // ---- the three properties nothing asserted before phase 3 task 2 (2026-09-04) ----
    //
    // As of 2026-09-04, every host string the 8 tests then above fed was one of:
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
    public async Task ResolveAsync_refuses_a_slug_that_contains_a_dot()
    {
        // Guards `slug.Contains('.') ? null : slug`. Without it a nested host would
        // yield the multi-label slug "falkinstein.extra" as a tenant NAME, so the
        // resolved office would depend on how many labels an attacker prepends.
        // Before B1 the dotted host then ran in host context; nginx's `*.auth` wildcard
        // forwards it, so that was reachable from the network. Now it is refused.
        var resolver = new HostAwareDomainTenantResolveContributor(ProdAuthFormat);
        var context = BuildResolveContext("falkinstein.extra.auth.portal.example.test");

        await ShouldRefuseAsync(resolver, context, "falkinstein.extra.auth.portal.example.test");
    }

    [Fact]
    public async Task ResolveAsync_refuses_a_request_whose_host_header_has_no_value()
    {
        // An empty Host field value is legal under RFC 9112 s3.2 and Kestrel accepts it,
        // so this is reachable from the network. The property pinned is the OUTCOME: an
        // empty host must not select a tenant, and since B1 (decision D4) it must not
        // reach host context either -- nothing internal sends one; curl and HttpClient
        // always send a Host.
        //
        // Still an outcome pin, not a line guard, as it was before B1 (history: an
        // earlier version showed on 2026-09-04 that no single-line deletion failed it).
        // An empty host fails ExtractSlug and is not an internal name, so it reaches the
        // refusal whether or not the Host value is read as null or as "".
        var resolver = new HostAwareDomainTenantResolveContributor(ProdAuthFormat);
        var context = BuildResolveContext(string.Empty);

        await ShouldRefuseAsync(resolver, context, string.Empty);
    }

    [Fact]
    public async Task ResolveAsync_refuses_every_public_host_when_the_template_has_no_placeholder()
    {
        // A misconfigured App:TenantDomainFormat with no {0} must resolve NOTHING rather than
        // treat the whole host as an office name. The host is a real office subdomain, so a
        // resolver that ignored the missing placeholder would have something to resolve.
        // Since B1 that failure is loud: every public host is refused.
        var resolver = new HostAwareDomainTenantResolveContributor("auth.portal.example.test");
        var context = BuildResolveContext("falkinstein.auth.portal.example.test");

        await ShouldRefuseAsync(resolver, context, "falkinstein.auth.portal.example.test");
    }

    [Fact]
    public async Task ResolveAsync_refuses_a_host_whose_office_label_is_empty()
    {
        // The host is exactly the template around an EMPTY slot, e.g. ".auth.portal.example.test".
        // An empty office name must never be handed to the tenant store, and since B1 the
        // request does not fall back to host context either.
        var resolver = new HostAwareDomainTenantResolveContributor(ProdAuthFormat);
        var context = BuildResolveContext(".auth.portal.example.test");

        await ShouldRefuseAsync(resolver, context, ".auth.portal.example.test");
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

    /// <summary>
    /// Asserts the B1 refusal: the contributor throws the host-not-served error, the message
    /// does not echo the Host, and the context is left untouched -- no office named, nothing
    /// marked handled -- so nothing could read the refusal as a resolution.
    /// </summary>
    private static async Task ShouldRefuseAsync(
        HostAwareDomainTenantResolveContributor resolver,
        FakeTenantResolveContext context,
        string host)
    {
        var refusal = await Should.ThrowAsync<BusinessException>(() => resolver.ResolveAsync(context));

        refusal.Code.ShouldBe(HostAwareDomainTenantResolveContributor.HostNotServedErrorCode);
        refusal.Message.ShouldBe(HostAwareDomainTenantResolveContributor.HostNotServedMessage);
        if (host.Length > 0)
        {
            refusal.Message.ShouldNotContain(host.Split(':')[0], Case.Insensitive);
        }

        context.TenantIdOrName.ShouldBeNull();
        context.Handled.ShouldBeFalse();
    }

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
    private static FakeTenantResolveContext BuildResolveContext(
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
