using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp.MultiTenancy;
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

    private static ITenantResolveContext BuildResolveContext(string host)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString(host);

        var services = new ServiceCollection();
        services.AddSingleton<IHttpContextAccessor>(
            new HttpContextAccessor { HttpContext = httpContext });

        return new FakeTenantResolveContext { ServiceProvider = services.BuildServiceProvider() };
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
