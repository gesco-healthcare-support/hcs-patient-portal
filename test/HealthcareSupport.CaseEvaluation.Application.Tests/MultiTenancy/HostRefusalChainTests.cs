using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.MultiTenancy;

/// <summary>
/// B1 (2026-09-25) -- what a refused Host does to the WHOLE resolver chain, run through ABP's
/// real <see cref="TenantResolver"/> over the chain each process's own
/// <c>ConfigureMultiTenancy</c> builds.
///
/// <para>The contributor tests prove the contributor throws. This file proves what that
/// means once it is inside the chain: resolution STOPS. A refusal that merely declined to
/// handle the request would let the next resolver decide the office -- and in the
/// production chain, where nothing follows, it would silently be host context, which is
/// the exact behaviour B1 removes.</para>
///
/// <para>WHAT THIS DOES NOT PROVE: the HTTP response. ABP's <c>MultiTenancyMiddleware</c>
/// turns the exception into a 404 with <c>Abp-Tenant-Resolve-Error</c> (ABP 10.0.2 source,
/// <c>MultiTenancyMiddleware.InvokeAsync</c> and the default
/// <c>MultiTenancyMiddlewareErrorPageBuilder</c>); no test project here runs the HTTP
/// pipeline, so that step is verified against a running stack, not here.</para>
/// </summary>
public class HostRefusalChainTests
{
    private const string ApiFormat = "{0}.api.portal.example.test";
    private const string AuthFormat = "{0}.auth.portal.example.test";

    /// <summary>
    /// THE DECOY IS LOAD-BEARING AND MUST NOT BE DELETED AS SETUP NOISE.
    ///
    /// <para>It stands for any resolver a later change could append after ours -- ABP's own
    /// query-string one is the realistic case. It names an office whenever it is reached, so a
    /// test can see whether resolution got past our contributor. It is registered AFTER the
    /// module's callback on purpose: registered before, the module's <c>Clear()</c> would
    /// remove it (which <see cref="TenantResolverChainTests"/> already proves), and it could
    /// then observe nothing.</para>
    /// </summary>
    private sealed class FallThroughDecoyTenantResolveContributor : TenantResolveContributorBase
    {
        public const string DecoyOffice = "TEST-decoy-office";

        public override string Name => "FallThroughDecoy";

        public bool WasAsked { get; private set; }

        public override Task ResolveAsync(ITenantResolveContext context)
        {
            WasAsked = true;
            context.TenantIdOrName = DecoyOffice;
            context.Handled = true;
            return Task.CompletedTask;
        }
    }

    public static TheoryData<string> Processes => new() { "HttpApiHost", "AuthServer" };

    [Theory]
    [MemberData(nameof(Processes))]
    public async Task A_refused_host_stops_resolution_before_any_later_resolver(string process)
    {
        var decoy = new FallThroughDecoyTenantResolveContributor();
        var resolver = Build(process, DottedHost(process), Anonymous(), decoy);

        var refusal = await Should.ThrowAsync<BusinessException>(() => resolver.ResolveTenantIdOrNameAsync());

        refusal.Code.ShouldBe(HostAwareDomainTenantResolveContributor.HostNotServedErrorCode);
        decoy.WasAsked.ShouldBeFalse();
    }

    [Theory]
    [InlineData("HttpApiHost", "localhost:8080")]
    [InlineData("HttpApiHost", "authserver:8080")]
    [InlineData("AuthServer", "localhost:8080")]
    [InlineData("AuthServer", "authserver:8080")]
    public async Task An_internal_host_is_left_unhandled_so_the_chain_continues(string process, string host)
    {
        // "Asked" means our contributor left the request unhandled. In production nothing
        // follows it, so that is host context -- asserted directly in the next test.
        var decoy = new FallThroughDecoyTenantResolveContributor();
        var resolver = Build(process, host, Anonymous(), decoy);

        await resolver.ResolveTenantIdOrNameAsync();

        decoy.WasAsked.ShouldBeTrue();
    }

    [Theory]
    [InlineData("HttpApiHost", "localhost:8080")]
    [InlineData("AuthServer", "authserver:8080")]
    public async Task An_internal_host_runs_in_host_context_in_the_production_chain(string process, string host)
    {
        var resolver = Build(process, host, Anonymous(), decoy: null);

        var result = await resolver.ResolveTenantIdOrNameAsync();

        result.TenantIdOrName.ShouldBeNull();
        result.AppliedResolvers.ShouldBe(new[]
        {
            CurrentUserTenantResolveContributor.ContributorName,
            HostAwareDomainTenantResolveContributor.ContributorName,
        });
    }

    [Theory]
    [MemberData(nameof(Processes))]
    public async Task An_office_host_resolves_its_office_and_never_reaches_the_decoy(string process)
    {
        var decoy = new FallThroughDecoyTenantResolveContributor();
        var resolver = Build(process, OfficeHost(process), Anonymous(), decoy);

        var result = await resolver.ResolveTenantIdOrNameAsync();

        result.TenantIdOrName.ShouldBe("falkinstein");
        decoy.WasAsked.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(Processes))]
    public async Task A_signed_in_callers_token_decides_before_the_host_is_read(string process)
    {
        // CHARACTERIZATION of B1's limit, stated so nobody reads B1 as more than it is: the
        // CurrentUser resolver runs first and handles every authenticated request, so a
        // refused host never reaches our contributor when a token is present. B1 governs
        // anonymous requests only.
        var officeA = Guid.NewGuid();
        var decoy = new FallThroughDecoyTenantResolveContributor();
        var resolver = Build(process, DottedHost(process), SignedInToOffice(officeA), decoy);

        var result = await resolver.ResolveTenantIdOrNameAsync();

        result.TenantIdOrName.ShouldBe(officeA.ToString());
        result.AppliedResolvers.ShouldBe(new[] { CurrentUserTenantResolveContributor.ContributorName });
        decoy.WasAsked.ShouldBeFalse();
    }

    // ---- helpers ----

    private static string DottedHost(string process) =>
        process == "AuthServer" ? "a.b.auth.portal.example.test" : "a.b.api.portal.example.test";

    private static string OfficeHost(string process) =>
        process == "AuthServer" ? "falkinstein.auth.portal.example.test" : "falkinstein.api.portal.example.test";

    /// <summary>
    /// Builds ABP's real resolver over the chain the named process's own
    /// <c>ConfigureMultiTenancy</c> registers, with the request Host and caller given.
    /// </summary>
    private static TenantResolver Build(
        string process,
        string host,
        ICurrentUser currentUser,
        FallThroughDecoyTenantResolveContributor? decoy)
    {
        var services = new ServiceCollection();
        services.AddOptions();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [HostAwareDomainTenantResolveContributor.DomainFormatConfigKey] =
                    process == "AuthServer" ? AuthFormat : ApiFormat,
            })
            .Build();

        Action<ServiceConfigurationContext, IConfiguration> configureMultiTenancy = process == "AuthServer"
            ? CaseEvaluationAuthServerModule.ConfigureMultiTenancy
            : CaseEvaluationHttpApiHostModule.ConfigureMultiTenancy;
        configureMultiTenancy(new ServiceConfigurationContext(services), configuration);

        if (decoy is not null)
        {
            // Registered after the module's callback, so it lands after our contributor.
            services.Configure<AbpTenantResolveOptions>(options => options.TenantResolvers.Add(decoy));
        }

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Host = new HostString(host);
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = httpContext });
        services.AddSingleton(currentUser);

        var provider = services.BuildServiceProvider();
        return new TenantResolver(provider.GetRequiredService<IOptions<AbpTenantResolveOptions>>(), provider);
    }

    /// <summary>
    /// An anonymous caller. Both members are configured DELIBERATELY: NSubstitute returns a
    /// stub rather than null for unconfigured members, so leaving them would supply a value
    /// nobody chose.
    /// </summary>
    private static ICurrentUser Anonymous()
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAuthenticated.Returns(false);
        currentUser.TenantId.Returns((Guid?)null);
        return currentUser;
    }

    private static ICurrentUser SignedInToOffice(Guid officeId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.IsAuthenticated.Returns(true);
        currentUser.TenantId.Returns(officeId);
        return currentUser;
    }
}
