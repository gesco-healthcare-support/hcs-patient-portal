using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.Hosting;

/// <summary>
/// T12 (in-house hosting, 2026-07-09) -- fail-fast startup validation of required production
/// configuration. Called from the AuthServer + HttpApi.Host modules' ConfigureServices.
///
/// <para>In a non-Development environment it asserts that the secrets/config the app cannot run
/// correctly without are present and are not still placeholder values, and throws a single
/// <see cref="AbpException"/> listing every offending KEY. OWASP: the values are never included
/// in the message or logged. No-op in Development, where the ABP + LocalDb defaults are used on
/// purpose.</para>
/// </summary>
public static class HostingConfigValidator
{
    /// <summary>Tokens that mark an unfilled placeholder rather than a real value.</summary>
    private static readonly string[] PlaceholderMarkers = { "REPLACE_ME", "CHANGE_ME", "FILL_ME" };

    /// <summary>
    /// Throws <see cref="AbpException"/> when a required production setting is missing or still a
    /// placeholder; no-op in Development. Set <paramref name="requireSigningCertificate"/> for the
    /// AuthServer (it needs the openiddict.pfx passphrase); leave it false for the API.
    /// </summary>
    public static void ValidateOrThrow(
        IConfiguration configuration,
        bool isDevelopment,
        bool requireSigningCertificate)
    {
        if (isDevelopment)
        {
            return;
        }

        // (key, predicate) pairs filtered via LINQ so the "any invalid?" check below reads over a
        // materialized list. This also sidesteps Sonar S2583, whose engine cannot see a local
        // function mutating a captured list and so wrongly reports the throw below as unreachable.
        var checks = new List<(string Key, Func<string?, bool> IsInvalid)>
        {
            // A LocalDb connection string is the dev default leaking into a container -- treat it as
            // unset even though it is technically non-blank.
            ("ConnectionStrings:Default",
                v => IsBlankOrPlaceholder(v) || v!.Contains("(LocalDb)", StringComparison.OrdinalIgnoreCase)),
            ("StringEncryption:DefaultPassPhrase", IsBlankOrPlaceholder),
            ("Redis:Configuration", IsBlankOrPlaceholder),
            ("AuthServer:Authority", IsBlankOrPlaceholder),
            ("App:SelfUrl", IsBlankOrPlaceholder),
        };

        if (requireSigningCertificate)
        {
            checks.Add(("AuthServer:CertificatePassPhrase", IsBlankOrPlaceholder));
        }

        var invalid = checks
            .Where(check => check.IsInvalid(configuration[check.Key]))
            .Select(check => check.Key)
            .ToList();

        if (invalid.Count > 0)
        {
            throw new AbpException(
                "Production configuration is invalid. The following required settings are missing or " +
                "still placeholder values: " + string.Join(", ", invalid) + ". Set them via environment " +
                "variables (see env.prod.example / secrets/env.prod) before starting. The values are not " +
                "shown here on purpose.");
        }
    }

    private static bool IsBlankOrPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return PlaceholderMarkers.Any(marker =>
            value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
