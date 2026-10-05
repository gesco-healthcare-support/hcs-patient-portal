using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace HealthcareSupport.CaseEvaluation.ApiDocumentation;

/// <summary>
/// Decides whether the API documentation (the OpenAPI document and its UI) is served, and applies
/// that decision to the request pipeline.
///
/// <para>Development ONLY. The document lists every route, DTO and permission of a PHI API,
/// including <c>/api/public/*</c>, <c>/api/integration/*</c> and the auto-generated twin routes, and it
/// is served without signing in. It used to be mapped in every environment. The decision and the act of
/// applying it live together here, outside the host module, because nothing reaches the module's
/// <c>OnApplicationInitialization</c> in a test: a guard written inline there cannot be checked.</para>
///
/// <para>Deliberately NOT a configuration flag. An environment name is harder to misconfigure than a
/// setting someone can leave switched on, and no deployment was found that needs the UI outside
/// Development.</para>
/// </summary>
internal static class ApiDocumentationPipeline
{
    /// <summary>True only when the host runs in the Development environment.</summary>
    internal static bool ShouldServe(IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        return environment.IsDevelopment();
    }

    /// <summary>
    /// Maps the OpenAPI document and the UI when <see cref="ShouldServe"/> allows it; otherwise adds
    /// nothing to the pipeline. Returns whether anything was mapped.
    /// </summary>
    internal static bool Apply(IApplicationBuilder app, IHostEnvironment environment, string? oauthClientId)
    {
        ArgumentNullException.ThrowIfNull(app);
        if (!ShouldServe(environment))
        {
            return false;
        }

        app.UseSwagger();
        app.UseAbpSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "CaseEvaluation API");
            options.OAuthClientId(oauthClientId);
        });
        return true;
    }
}
