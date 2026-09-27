using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.MultiTenancy;

/// <summary>
/// Resolves the current tenant from the Host header against
/// <see cref="DomainFormat"/> (default "{0}.localhost"), with one exception:
/// the slug "admin" is reserved for the Volo SaaS Host surface and is NOT
/// looked up in the tenant store. Reserved-slug requests fall through with
/// no tenant set, so the request runs in Host context.
///
/// ADR-007 (2026-05-11) supersedes ADR-006's incorrect premise that ABP's
/// stock <c>DomainTenantResolveContributor</c> returns null when the slug
/// does not match a tenant. It does not: it sets
/// <c>context.TenantIdOrName</c> from the host, and ABP's
/// <c>MultiTenancyMiddleware</c> writes HTTP 404 with header
/// <c>Abp-Tenant-Resolve-Error: Tenant not found!</c> when the slug is not
/// a row in the tenant store. This contributor preserves that typo
/// protection for unknown tenant slugs while letting "admin" pass through.
///
/// B1 (2026-09-25): a Host that names no office is REFUSED rather than run in
/// Host context -- see <see cref="ResolveAsync"/>. Host context is now reached
/// only on purpose: the reserved "admin" label, or an internal name listed in
/// <see cref="InternalHosts"/>.
/// </summary>
public class HostAwareDomainTenantResolveContributor : TenantResolveContributorBase
{
    public const string ContributorName = "HostAwareDomain";

    /// <summary>The single reserved subdomain that maps to Host context.</summary>
    public const string ReservedHostSlug = "admin";

    /// <summary>
    /// Configuration key holding the host template ("{0}.suffix"). Set per service in
    /// production (e.g. "{0}.auth.portal.example.com" on the AuthServer,
    /// "{0}.api.portal.example.com" on the API). Unset in local dev.
    /// </summary>
    public const string DomainFormatConfigKey = "App:TenantDomainFormat";

    /// <summary>Template used when <see cref="DomainFormatConfigKey"/> is unset (local dev).</summary>
    public const string DefaultDomainFormat = "{0}.localhost";

    /// <summary>
    /// Error code of the refusal. Kept here rather than in <c>CaseEvaluationDomainErrorCodes</c>:
    /// the refusal is written by ABP's multi-tenancy middleware, which uses the exception's
    /// message verbatim and never goes through the localized exception handling that the
    /// codes in that class (and their <c>en.json</c> entries) exist for.
    /// </summary>
    public const string HostNotServedErrorCode = "CaseEvaluation:MultiTenancy.HostNotServed";

    /// <summary>
    /// Fixed refusal text. It never includes the request's Host: that value is caller-controlled
    /// and would otherwise reach both a response header and the logs.
    /// </summary>
    public const string HostNotServedMessage = "This host does not serve an office.";

    /// <summary>
    /// Host names (without port, compared ignoring case) that keep host context although
    /// they name no office, because a caller inside the deployment sends them:
    /// <c>localhost</c> -- the container health checks and the HealthChecks UI poll
    /// (<c>docker-compose.prod.yml</c> <c>curl -f http://localhost:8080/health-status</c>,
    /// <c>App__HealthUiCheckUrl</c>) and local development; <c>authserver</c> -- internal calls
    /// to the AuthServer by its container name (<c>AuthServer__MetaAddress: http://authserver:8080</c>).
    /// Measured 2026-09-25 on the dev stack: the metadata and signing-key endpoints themselves
    /// are answered by OpenIddict during authentication, BEFORE tenant resolution, so that fetch
    /// works either way; this entry keeps any other call on that Host out of the refusal.
    /// Anything else that names no office is refused.
    /// </summary>
    public static readonly IReadOnlyList<string> InternalHosts = ["localhost", "authserver"];

    public override string Name => ContributorName;

    public string DomainFormat { get; }

    public HostAwareDomainTenantResolveContributor(string domainFormat)
    {
        DomainFormat = domainFormat;
    }

    /// <summary>
    /// Builds the contributor from configuration: reads <see cref="DomainFormatConfigKey"/> and
    /// falls back to <see cref="DefaultDomainFormat"/> when it is missing or blank, so local dev
    /// (and any environment without the key) keeps "{0}.localhost" while production supplies its
    /// own per-service host template. ADR-007 flagged this config seam for the production hosts.
    /// </summary>
    public static HostAwareDomainTenantResolveContributor FromConfiguration(IConfiguration configuration)
    {
        var format = configuration[DomainFormatConfigKey];
        return new HostAwareDomainTenantResolveContributor(
            string.IsNullOrWhiteSpace(format) ? DefaultDomainFormat : format);
    }

    /// <summary>
    /// Resolves the office from the Host, in this order:
    /// <list type="number">
    /// <item><description>No HTTP request at all (a background job, a console host): nothing
    /// to read, so abstain.</description></item>
    /// <item><description>A single office label under <see cref="DomainFormat"/>: the reserved
    /// <c>admin</c> label keeps host context; any other label is handed to ABP, whose middleware
    /// answers 404 for an office that does not exist (ADR-007).</description></item>
    /// <item><description>An <see cref="InternalHosts"/> name: host context, for the health
    /// checks and the metadata fetch.</description></item>
    /// <item><description>Anything else -- an empty or dotted label, the bare or the other
    /// service's host, a foreign host, an IP, an empty Host: REFUSED (B1, 2026-09-25).</description></item>
    /// </list>
    /// <para>The refusal is a throw on purpose. ABP 10.0.2's <c>TenantResolver</c> does not catch
    /// contributor exceptions, and <c>MultiTenancyMiddleware.InvokeAsync</c> catches any exception
    /// from tenant resolution and hands it to the default
    /// <c>MultiTenancyMiddlewareErrorPageBuilder</c>, which answers 404 with an
    /// <c>Abp-Tenant-Resolve-Error</c> header -- the same shape as an unknown office, and with no
    /// tenant-store lookup. Abstaining instead would BE host context: nothing follows this
    /// contributor in either process's chain.</para>
    /// <para>This governs anonymous requests only. <c>CurrentUserTenantResolveContributor</c>
    /// runs first and handles every authenticated request from its token.</para>
    /// </summary>
    public override Task ResolveAsync(ITenantResolveContext context)
    {
        var httpContext = context.ServiceProvider
            .GetService<IHttpContextAccessor>()?.HttpContext;
        if (httpContext == null)
        {
            return Task.CompletedTask;
        }

        var hostWithoutPort = StripPort(httpContext.Request.Host.Value ?? string.Empty);

        var slug = ExtractSlug(hostWithoutPort, DomainFormat);
        if (slug != null)
        {
            if (!string.Equals(slug, ReservedHostSlug, StringComparison.OrdinalIgnoreCase))
            {
                context.TenantIdOrName = slug;
                context.Handled = true;
            }

            return Task.CompletedTask;
        }

        if (IsInternalHost(hostWithoutPort))
        {
            return Task.CompletedTask;
        }

        throw new BusinessException(HostNotServedErrorCode, HostNotServedMessage);
    }

    /// <summary>Exact, case-insensitive match against <see cref="InternalHosts"/>.</summary>
    private static bool IsInternalHost(string hostWithoutPort) =>
        InternalHosts.Any(internalHost =>
            string.Equals(hostWithoutPort, internalHost, StringComparison.OrdinalIgnoreCase));

    private static string StripPort(string host)
    {
        var colonIndex = host.IndexOf(':');
        return colonIndex >= 0 ? host.Substring(0, colonIndex) : host;
    }

    /// <summary>
    /// Extracts the {0} portion of <paramref name="hostWithoutPort"/> against
    /// <paramref name="format"/>. Returns null when the host does not match the
    /// format, when the {0} slot is empty, and when it holds more than one label.
    /// Mirrors the parse intent of ABP's <c>FormatStringValueExtracter</c> for the
    /// single-placeholder case.
    /// </summary>
    private static string? ExtractSlug(string hostWithoutPort, string format)
    {
        var placeholderIndex = format.IndexOf("{0}", StringComparison.Ordinal);
        if (placeholderIndex < 0)
        {
            return null;
        }

        var prefix = format.Substring(0, placeholderIndex);
        var suffix = format.Substring(placeholderIndex + "{0}".Length);

        if (!hostWithoutPort.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            !hostWithoutPort.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var startIndex = prefix.Length;
        var endIndex = hostWithoutPort.Length - suffix.Length;
        if (endIndex <= startIndex)
        {
            return null;
        }

        var slug = hostWithoutPort.Substring(startIndex, endIndex - startIndex);
        return slug.Contains('.', StringComparison.Ordinal) ? null : slug;
    }
}
