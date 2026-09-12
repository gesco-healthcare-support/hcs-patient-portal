using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Volo.Abp.DependencyInjection;

namespace HealthcareSupport.CaseEvaluation.Integration.CaseTracker;

/// <summary>
/// Checks the inbound <c>X-Integration-Token</c> against the configured secret.
///
/// <para>This is the ONLY thing between an anonymous caller and a full appointment payload, so it is
/// written to fail closed: if no token is configured, every request is rejected. The alternative --
/// treating "no secret configured" as "no check required" -- would make a fresh or misconfigured
/// deploy serve PHI to anyone who found the URL.</para>
///
/// <para>Comparison is constant-time via <see cref="CryptographicOperations.FixedTimeEquals"/>. A
/// naive <c>==</c> returns as soon as two bytes differ, which leaks the shared secret one character
/// at a time to a caller who can measure response times.</para>
/// </summary>
public class IntegrationTokenValidator : ITransientDependency
{
    private readonly IConfiguration _configuration;

    public IntegrationTokenValidator(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// True only when a token IS configured and <paramref name="presented"/> matches it exactly.
    /// Never logs or returns either value.
    /// </summary>
    public virtual bool IsValid(string? presented)
    {
        var configured = _configuration[CaseTrackerIntegrationConsts.TokenConfigurationKey];

        // Fail closed. Whitespace counts as unconfigured: a blank value in a settings file is a
        // mistake, not an intent to disable authentication.
        if (string.IsNullOrWhiteSpace(configured) || string.IsNullOrEmpty(presented))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(configured);
        var presentedBytes = Encoding.UTF8.GetBytes(presented);

        // FixedTimeEquals requires equal lengths; comparing the lengths first leaks only the length,
        // which the caller already controls.
        if (expectedBytes.Length != presentedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(expectedBytes, presentedBytes);
    }
}
