using System;
using System.Net;
using System.Text.RegularExpressions;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Composes tenant-prefixed URLs at the email-rendering boundary. The setting-resolver
/// pipeline returns an office-LESS base URL (e.g. "http://localhost:4200" in dev,
/// "https://portal.example.com" in prod, sourced from the
/// <c>Settings__CaseEvaluation__Notifications__PortalBaseUrl</c> setting whose default is the
/// <c>App:AngularUrl</c> config value); this helper prepends the office slug as the leftmost
/// host label so links land on the office SPA (e.g. "https://falkinstein.portal.example.com").
///
/// <para>T10/G3 (in-house hosting, 2026-07-09): prepends the office slug after the scheme,
/// matching the frontend prependSlug rule in <c>angular/src/tenant-bootstrap.ts</c> so the SPA
/// subdomain bootstrap and the backend email URL rendering share one substitution rule. This
/// replaces the earlier bare-localhost-only swap, which could not prefix a real production base
/// host. Skips IP-address hosts (an IP cannot be subdomained) and is idempotent when the host
/// already starts with the office slug (a tenant admin may have set an already-prefixed URL via
/// <c>/setting-management</c>).</para>
/// </summary>
internal static class TenantUrlComposer
{
    // Captures the scheme and the host[:port] -- everything up to the first '/', '?' or '#'.
    // The match timeout is a defensive ReDoS guard (Sonar S6444); the pattern is linear so it
    // never trips in practice, but an explicit bound is cheap insurance at the email boundary.
    private static readonly Regex SchemeAndHost = new(
        @"^(?<scheme>https?://)(?<host>[^/?#]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Prepends "{tenantName}." (lowercased) as the leftmost host label of
    /// <paramref name="baseUrl"/>. Returns the input unchanged when the URL has no http(s) host,
    /// the host is an IP address, the host already starts with the office slug, or
    /// <paramref name="tenantName"/> is null/empty (host scope). Returns null when
    /// <paramref name="baseUrl"/> is null.
    /// </summary>
    public static string? ComposeForTenant(string? baseUrl, string? tenantName)
    {
        if (string.IsNullOrEmpty(baseUrl)) return baseUrl;
        if (string.IsNullOrEmpty(tenantName)) return baseUrl;

        var slug = tenantName.ToLowerInvariant();
        return SchemeAndHost.Replace(baseUrl, match =>
        {
            var host = match.Groups["host"].Value;
            var hostname = host.Split(':')[0];

            if (IPAddress.TryParse(hostname, out _) ||
                hostname.StartsWith(slug + ".", StringComparison.OrdinalIgnoreCase))
            {
                return match.Value;
            }

            return $"{match.Groups["scheme"].Value}{slug}.{host}";
        });
    }
}
