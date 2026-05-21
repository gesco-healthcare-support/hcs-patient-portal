using System.Text.RegularExpressions;

namespace HealthcareSupport.CaseEvaluation.Notifications;

/// <summary>
/// Task A (BUG-014 fix, 2026-05-20) -- composes tenant-prefixed URLs at
/// the email-rendering boundary. The setting-resolver pipeline returns
/// tenant-less base URLs (e.g. "http://localhost:4200" sourced from the
/// <c>Settings__CaseEvaluation__Notifications__PortalBaseUrl</c> docker
/// env var via ABP's <see cref="Volo.Abp.Settings.ConfigurationSettingValueProvider"/>);
/// this helper rewrites the bare-localhost host token to
/// <c>{tenantName}.localhost</c> using the booker's <c>ICurrentTenant.Name</c>.
///
/// <para>The regex <c>(^|//)localhost(?=([:/]|$))</c> is lifted byte-for-byte
/// from <c>angular/src/tenant-bootstrap.ts:99</c> so the frontend
/// (subdomain bootstrap) and backend (email URL rendering) share one
/// substitution rule. Both anchor on the host-token position (start-of-string
/// or after <c>//</c>) followed by a port/path/end delimiter, so accidental
/// substrings like <c>my-localhost-server.example.com</c> are not matched.</para>
///
/// <para>Idempotent: URLs that already carry a subdomain (e.g.
/// <c>http://falkinstein.localhost:4200</c> set by a tenant admin via
/// <c>/setting-management</c>) pass through unchanged because the regex
/// requires a bare <c>localhost</c> host token.</para>
/// </summary>
internal static class TenantUrlComposer
{
    private static readonly Regex LocalhostHost = new(
        @"(^|//)localhost(?=([:/]|$))",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Rewrites the bare-localhost host token in <paramref name="baseUrl"/>
    /// to <c>{tenantName}.localhost</c>. Returns the input unchanged when
    /// the URL has no bare-localhost token, the URL is already prefixed,
    /// or <paramref name="tenantName"/> is null/empty (host scope).
    /// Returns null when <paramref name="baseUrl"/> is null.
    /// </summary>
    public static string? ComposeForTenant(string? baseUrl, string? tenantName)
    {
        if (string.IsNullOrEmpty(baseUrl)) return baseUrl;
        if (string.IsNullOrEmpty(tenantName)) return baseUrl;
        return LocalhostHost.Replace(baseUrl, $"$1{tenantName!.ToLowerInvariant()}.localhost");
    }
}
