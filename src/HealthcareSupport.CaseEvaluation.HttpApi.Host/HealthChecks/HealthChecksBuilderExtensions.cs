using System;
using HealthChecks.UI.Client;
using HealthcareSupport.CaseEvaluation.Permissions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace HealthcareSupport.CaseEvaluation.HealthChecks;

public static class HealthChecksBuilderExtensions
{
    private static readonly string[] DatabaseTags = { "database" };

    public static void AddCaseEvaluationHealthChecks(this IServiceCollection services)
    {
        // Add your health checks here
        var healthChecksBuilder = services.AddHealthChecks();
        healthChecksBuilder.AddCheck<CaseEvaluationDatabaseCheck>("CaseEvaluation DbContext Check", tags: DatabaseTags);

        services.ConfigureHealthCheckEndpoint("/health-status");

        var configuration = services.GetConfiguration();
        var healthCheckUrl = configuration["App:HealthCheckUrl"];

        if (string.IsNullOrEmpty(healthCheckUrl))
        {
            healthCheckUrl = "/health-status";
        }
        var healthChecksUiBuilder = services.AddHealthChecksUI(settings =>
        {
            settings.AddHealthCheckEndpoint("CaseEvaluation Health Status", configuration["App:HealthUiCheckUrl"] ?? healthCheckUrl);
        });

        // Set your HealthCheck UI Storage here
        healthChecksUiBuilder.AddInMemoryStorage();

        services.MapHealthChecksUiEndpoints(options =>
        {
            options.UIPath = "/health-ui";
            options.ApiPath = "/health-api";
        });
    }

    private static void ConfigureHealthCheckEndpoint(this IServiceCollection services, string path)
    {
        services.Configure<AbpEndpointRouterOptions>(options =>
        {
            options.EndpointConfigureActions.Add(endpointContext =>
            {
                endpointContext.Endpoints.MapHealthChecks(
                    new PathString(path.EnsureStartsWith('/')),
                    new HealthCheckOptions
                    {
                        Predicate = _ => true,
                        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
                        AllowCachingResponses = false,
                    });
            });
        });
    }

    /// <summary>
    /// Maps the health UI and its API. Outside Development both need a signed-in host user holding
    /// <see cref="CaseEvaluationPermissions.BackgroundJobsDashboard.Default"/>; this host authenticates
    /// by bearer token only, so a browser gets 401 there. The <c>/health-status</c> probe mapped above
    /// stays open for the proxy and monitoring.
    /// </summary>
    private static void MapHealthChecksUiEndpoints(this IServiceCollection services, Action<global::HealthChecks.UI.Configuration.Options>? setupOption = null)
    {
        services.Configure<AbpEndpointRouterOptions>(routerOptions =>
        {
            routerOptions.EndpointConfigureActions.Add(endpointContext =>
            {
                var healthUi = endpointContext.Endpoints.MapHealthChecksUI(setupOption);
                if (!endpointContext.ScopeServiceProvider.GetRequiredService<IWebHostEnvironment>().IsDevelopment())
                {
                    healthUi.RequireAuthorization(CaseEvaluationPermissions.BackgroundJobsDashboard.Default);
                }
            });
        });
    }
}
