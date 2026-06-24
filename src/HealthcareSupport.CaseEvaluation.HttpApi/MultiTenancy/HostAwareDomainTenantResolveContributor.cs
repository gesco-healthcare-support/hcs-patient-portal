using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
/// </summary>
public class HostAwareDomainTenantResolveContributor : TenantResolveContributorBase
{
    public const string ContributorName = "HostAwareDomain";

    /// <summary>The single reserved subdomain that maps to Host context.</summary>
    public const string ReservedHostSlug = "admin";

    public override string Name => ContributorName;

    public string DomainFormat { get; }

    public HostAwareDomainTenantResolveContributor(string domainFormat)
    {
        DomainFormat = domainFormat;
    }

    public override Task ResolveAsync(ITenantResolveContext context)
    {
        var httpContext = context.ServiceProvider
            .GetService<IHttpContextAccessor>()?.HttpContext;
        if (httpContext == null || !httpContext.Request.Host.HasValue)
        {
            return Task.CompletedTask;
        }

        var slug = ExtractSlug(httpContext.Request.Host.Value, DomainFormat);
        if (string.IsNullOrEmpty(slug))
        {
            return Task.CompletedTask;
        }

        if (string.Equals(slug, ReservedHostSlug, StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        context.TenantIdOrName = slug;
        context.Handled = true;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Extracts the {0} portion of <paramref name="host"/> against
    /// <paramref name="format"/>. Strips port. Returns null when the host
    /// does not match the format. Mirrors the parse intent of ABP's
    /// <c>FormatStringValueExtracter</c> for the single-placeholder case.
    /// </summary>
    private static string? ExtractSlug(string host, string format)
    {
        var colonIndex = host.IndexOf(':');
        var hostWithoutPort = colonIndex >= 0 ? host.Substring(0, colonIndex) : host;

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
