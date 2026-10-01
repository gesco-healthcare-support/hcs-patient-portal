using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Logging;

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

    /// <summary>
    /// Applies the decision to the two <c>IdentityModelEventSource</c> switches.
    ///
    /// <para>Each host calls this instead of branching at its own call site, so the decision and the
    /// act of applying it are one tested unit. A host module cannot be exercised by a test, so a
    /// branch written there is a branch nothing can check.</para>
    /// </summary>
    public static void Apply(IHostEnvironment environment, IConfiguration configuration)
    {
        Apply(ShouldEnable(environment, configuration));
    }

    /// <summary>
    /// Writes both switches unconditionally rather than only turning them on.
    ///
    /// <para>The switches are process-wide statics that default to off, so the old
    /// <c>if (enable) { on }</c> shape and this one behave alike on a fresh host. This shape also
    /// states the off case, so a host that has already had them set cannot inherit it.</para>
    /// </summary>
    internal static void Apply(bool enable)
    {
        IdentityModelEventSource.ShowPII = enable;
        IdentityModelEventSource.LogCompleteSecurityArtifact = enable;
    }
}
