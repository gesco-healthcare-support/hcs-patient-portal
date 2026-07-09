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

        var invalid = new List<string>();

        void Require(string key, Func<string?, bool> isInvalid)
        {
            if (isInvalid(configuration[key]))
            {
                invalid.Add(key);
            }
        }

        // A LocalDb connection string is the dev default leaking into a container -- treat it as
        // unset even though it is technically non-blank.
        Require("ConnectionStrings:Default",
            v => IsBlankOrPlaceholder(v) || v!.Contains("(LocalDb)", StringComparison.OrdinalIgnoreCase));
        Require("StringEncryption:DefaultPassPhrase", IsBlankOrPlaceholder);
        Require("Redis:Configuration", IsBlankOrPlaceholder);
        Require("AuthServer:Authority", IsBlankOrPlaceholder);
        Require("App:SelfUrl", IsBlankOrPlaceholder);

        if (requireSigningCertificate)
        {
            Require("AuthServer:CertificatePassPhrase", IsBlankOrPlaceholder);
        }

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
