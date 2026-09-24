using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HealthChecks.UI.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;
using UiSettings = global::HealthChecks.UI.Configuration.Settings;

namespace HealthcareSupport.CaseEvaluation.HealthChecks;

/// <summary>
/// What each host registers for health checks: the checks themselves, the address the health UI polls,
/// and the three routes (<c>/health-status</c>, <c>/health-ui</c>, <c>/health-api</c>).
///
/// <para>Both hosts define <c>HealthcareSupport.CaseEvaluation.HealthChecks.HealthChecksBuilderExtensions</c>,
/// so the type name is ambiguous from this project, which references both. Each is therefore reached by
/// reflection from its OWN host's assembly. The UI's configured endpoints sit in an <c>internal</c> list
/// on HealthChecks.UI's <c>Settings</c>, which is read by reflection too. If a package upgrade moves
/// either member, these tests fail naming it, rather than passing without checking.</para>
/// </summary>
public class HealthCheckRegistrationTests
{
    public enum Host
    {
        ApiHost,
        AuthServer,
    }

    [Fact]
    public void ApiHost_RegistersTheDatabaseCheck_WithItsTag()
    {
        var registrations = BuildProvider(Host.ApiHost, new Dictionary<string, string?>())
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        var check = registrations.ShouldHaveSingleItem();
        check.Name.ShouldBe("CaseEvaluation DbContext Check");
        check.Tags.ShouldBe(new[] { "database" });
    }

    [Fact]
    public void AuthServer_RegistersNoChecksOfItsOwn()
    {
        // The auth server exposes the status route and the UI but has no database check: the
        // registration list is empty, beside the API host's single check above.
        BuildProvider(Host.AuthServer, new Dictionary<string, string?>())
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .ShouldBeEmpty();
    }

    [Theory]
    [InlineData(Host.ApiHost, "CaseEvaluation Health Status")]
    [InlineData(Host.AuthServer, "CaseEvaluation AuthServer Health Status")]
    public void TheUiPolls_ItsOwnUrl_WhenOneIsConfigured(Host host, string expectedName)
    {
        var endpoint = UiEndpoint(host, new Dictionary<string, string?>
        {
            ["App:HealthUiCheckUrl"] = "https://TEST-ui.test.local/health-status",
            ["App:HealthCheckUrl"] = "https://TEST-api.test.local/hc",
        });

        endpoint.Name.ShouldBe(expectedName);
        endpoint.Uri.ShouldBe("https://TEST-ui.test.local/health-status");
    }

    [Theory]
    [InlineData(Host.ApiHost)]
    [InlineData(Host.AuthServer)]
    public void TheUiPolls_TheHealthCheckUrl_WhenNoUiUrlIsConfigured(Host host)
    {
        var endpoint = UiEndpoint(host, new Dictionary<string, string?>
        {
            ["App:HealthCheckUrl"] = "https://TEST-api.test.local/hc",
        });

        endpoint.Uri.ShouldBe("https://TEST-api.test.local/hc");
    }

    [Theory]
    [InlineData(Host.ApiHost, null)]
    [InlineData(Host.ApiHost, "")]
    [InlineData(Host.AuthServer, null)]
    [InlineData(Host.AuthServer, "")]
    public void TheUiPolls_TheLocalStatusRoute_WhenNeitherUrlIsSet(Host host, string? healthCheckUrl)
    {
        var endpoint = UiEndpoint(host, new Dictionary<string, string?>
        {
            ["App:HealthCheckUrl"] = healthCheckUrl,
        });

        endpoint.Uri.ShouldBe("/health-status");
    }

    [Theory]
    [InlineData(Host.ApiHost)]
    [InlineData(Host.AuthServer)]
    public void EachHost_MapsTheStatusUiAndApiRoutes(Host host)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "TEST-healthchecks" });
        AddHealthChecks(host, builder.Services);
        var app = builder.Build();

        var actions = app.Services.GetRequiredService<IOptions<AbpEndpointRouterOptions>>().Value.EndpointConfigureActions;
        actions.Count.ShouldBe(2);
        foreach (var configure in actions)
        {
            configure(new EndpointRouteBuilderContext(app, app.Services));
        }

        var routes = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty)
            .ToList();
        routes.ShouldContain("/health-status");
        routes.ShouldContain(route => route.StartsWith("/health-ui", StringComparison.Ordinal));
        routes.ShouldContain(route => route.StartsWith("/health-api", StringComparison.Ordinal));
    }

    // ------------------------------------------------------------------------

    private static HealthCheckSetting UiEndpoint(Host host, Dictionary<string, string?> settings)
    {
        var uiSettings = BuildProvider(host, settings).GetRequiredService<IOptions<UiSettings>>().Value;
        var list = typeof(UiSettings).GetProperty("HealthChecks", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "HealthChecks.UI's Settings no longer has the internal HealthChecks list this test reads.");
        return ((IEnumerable<HealthCheckSetting>)list.GetValue(uiSettings)!).ShouldHaveSingleItem();
    }

    private static ServiceProvider BuildProvider(Host host, Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        AddHealthChecks(host, services);
        return services.BuildServiceProvider();
    }

    private static void AddHealthChecks(Host host, IServiceCollection services)
    {
        var (assembly, methodName) = host switch
        {
            Host.ApiHost => (typeof(CaseEvaluationHttpApiHostModule).Assembly, "AddCaseEvaluationHealthChecks"),
            Host.AuthServer => (typeof(CaseEvaluationAuthServerModule).Assembly, "AddCaseEvaluationAuthServerHealthChecks"),
            _ => throw new ArgumentOutOfRangeException(nameof(host)),
        };

        var extensions = assembly.GetType("HealthcareSupport.CaseEvaluation.HealthChecks.HealthChecksBuilderExtensions", throwOnError: true)!;
        var method = extensions.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{assembly.GetName().Name} no longer has {methodName}.");
        method.Invoke(null, new object[] { services });
    }
}
