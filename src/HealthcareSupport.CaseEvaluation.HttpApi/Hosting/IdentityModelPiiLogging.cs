using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace HealthcareSupport.CaseEvaluation.Hosting;

/// <summary>
/// Decides whether IdentityModel may log personal data and whole security tokens
/// (<c>IdentityModelEventSource.ShowPII</c> and <c>LogCompleteSecurityArtifact</c>).
///
/// <para><b>Development only, decided in code.</b> Both hosts used to switch these ON unless
/// <c>App:DisablePII</c> was true, and that key is set in no deployed configuration, so every
/// environment ran with them on: a rejected sign-in token was written to the container log whole,
/// claims included. Keying on a setting fails OPEN wherever the setting is forgotten. Keying on the
/// environment fails closed: a new server, a staging slot or the Azure host is safe with no
/// configuration at all.</para>
///
/// <para><c>App:DisablePII=true</c> still turns it off in Development, for a developer who wants
/// Production-shaped logs locally. No value of it turns logging ON outside Development.</para>
/// </summary>
public static class IdentityModelPiiLogging
{
    public const string DisableKey = "App:DisablePII";

    public static bool ShouldEnable(IHostEnvironment environment, IConfiguration configuration)
    {
        return environment.IsDevelopment() && !configuration.GetValue<bool>(DisableKey);
    }
}
