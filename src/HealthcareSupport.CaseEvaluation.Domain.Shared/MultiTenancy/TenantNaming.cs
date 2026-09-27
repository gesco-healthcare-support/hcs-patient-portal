using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.RegularExpressions;

namespace HealthcareSupport.CaseEvaluation.MultiTenancy;

/// <summary>
/// The per-office naming convention for database-per-office multi-tenancy.
///
/// An office's subdomain slug IS its lowercased name, and its database is
/// "CaseEvaluation_{slug}". <see cref="DeriveSlug"/> VALIDATES rather than
/// transforms: the subdomain resolver
/// (HostAwareDomainTenantResolveContributor) matches the resolved slug back to
/// the tenant's stored Name, so a transformed slug (spaces -> hyphens, etc.)
/// would no longer resolve. A non-slug-safe office name therefore fails fast.
///
/// The reserved slug "admin" maps to the host-context surface (admin.localhost)
/// and can never be an office. (Same value guarded at the request boundary by
/// HostAwareDomainTenantResolveContributor.ReservedHostSlug and at office
/// creation by DoctorTenantAppService.ReservedTenantNameAdmin; this is the
/// lowest-layer copy so Domain/Application can validate without referencing
/// the host projects.)
///
/// <see cref="ProxyReservedSlugs"/> reserves a SECOND, unrelated group: the
/// single-label hosts the reverse proxy answers itself. Those are not host-context
/// aliases and never reach the application, which is exactly why they need a guard
/// here -- nothing downstream would ever see the request and complain.
/// </summary>
public static class TenantNaming
{
    /// <summary>Office database name prefix; the host database is "CaseEvaluation".</summary>
    public const string DatabaseNamePrefix = "CaseEvaluation_";

    /// <summary>Subdomain reserved for the host-context surface; never an office slug.</summary>
    public const string ReservedSlug = "admin";

    /// <summary>
    /// Single-label hosts the reverse proxy claims with an EXACT <c>server_name</c>, which nginx
    /// ranks above every wildcard whatever the file order. An office by one of these names is
    /// accepted everywhere else and then simply unreachable: its SPA host
    /// <c>{slug}.{BASE_DOMAIN}</c> is captured by that exact block instead of falling through to
    /// <c>*.{BASE_DOMAIN}</c>, so the office has no front door. The request never reaches the
    /// application, so this is the only layer that can refuse it.
    ///
    /// <para>Kept SEPARATE from <see cref="ReservedSlug"/> rather than merged into one list,
    /// because the two are reserved for different reasons and only "admin" carries host-context
    /// meaning. Merging them would invite adding these to
    /// <c>HostAwareDomainTenantResolveContributor.ReservedHostSlug</c>, where they do not belong.</para>
    ///
    /// <para>Sourced from <c>docker/nginx-proxy/default.conf.template</c>: <c>api</c> and
    /// <c>auth</c> at its <c>server_name api.${BASE_DOMAIN} auth.${BASE_DOMAIN}</c> block (#1021),
    /// and <c>minio</c> at <c>server_name minio.${BASE_DOMAIN}</c>, whose own comment already
    /// noted it "becomes a RESERVED office slug, like `admin`" without anything enforcing it.
    /// <c>health</c> (the load-balancer probe host, answered only on <c>/health-status</c>) and
    /// <c>www</c> (redirected to the apex) joined with the production-hosting plan's C1.
    /// A new exact-name block in that file needs a matching entry here.</para>
    /// </summary>
    public static readonly IReadOnlySet<string> ProxyReservedSlugs =
        new HashSet<string>(StringComparer.Ordinal) { "api", "auth", "minio", "health", "www" };

    /// <summary>DNS label length limit; also bounds the database name.</summary>
    public const int MaxSlugLength = 63;

    // Lowercase DNS label: alphanumeric ends, optional internal hyphens.
    // A match timeout is supplied as defense-in-depth (ReDoS hardening) even though
    // this pattern has no catastrophic-backtracking risk and IsValidSlug rejects
    // anything over MaxSlugLength before the pattern ever runs.
    private static readonly Regex SlugPattern = new(
        "^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    /// <summary>
    /// Derives the office slug from its name by lowercasing + trimming, then
    /// validating it is DNS/SQL safe and not reserved. Throws
    /// <see cref="ArgumentException"/> when the name cannot be a subdomain
    /// (blank, reserved, or containing characters outside [a-z0-9-]).
    /// </summary>
    public static string DeriveSlug(string officeName)
    {
        if (string.IsNullOrWhiteSpace(officeName))
        {
            throw new ArgumentException("Office name is required.", nameof(officeName));
        }

        var slug = officeName.Trim().ToLowerInvariant();

        if (string.Equals(slug, ReservedSlug, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Office name '{ReservedSlug}' is reserved for the host-context surface.",
                nameof(officeName));
        }

        if (ProxyReservedSlugs.Contains(slug))
        {
            // Distinct message from the one above: this name is refused because the proxy owns
            // the hostname, not because it means something in host context. Telling an admin
            // "reserved for the host-context surface" about `minio` would send them looking in
            // the wrong place.
            throw new ArgumentException(
                $"Office name '{slug}' is reserved by the reverse proxy, which answers that " +
                "hostname itself, so the office would have no reachable address.",
                nameof(officeName));
        }

        if (!IsValidSlug(slug))
        {
            throw new ArgumentException(
                "Office name must be a single DNS-safe token (lowercase letters, " +
                $"digits, and internal hyphens; max {MaxSlugLength} characters) so it " +
                "can be the office subdomain.",
                nameof(officeName));
        }

        return slug;
    }

    /// <summary>
    /// True when <paramref name="slug"/> is a valid, non-reserved office slug:
    /// already lowercase, 1..<see cref="MaxSlugLength"/> chars, DNS-label shaped.
    /// </summary>
    public static bool IsValidSlug(string? slug)
    {
        if (string.IsNullOrEmpty(slug) || slug.Length > MaxSlugLength)
        {
            return false;
        }

        if (string.Equals(slug, ReservedSlug, StringComparison.Ordinal) ||
            ProxyReservedSlugs.Contains(slug))
        {
            return false;
        }

        return SlugPattern.IsMatch(slug);
    }

    /// <summary>
    /// Composes the office database name "CaseEvaluation_{slug}". Throws
    /// <see cref="ArgumentException"/> when <paramref name="slug"/> is invalid.
    /// </summary>
    public static string GetDatabaseName(string slug)
    {
        if (!IsValidSlug(slug))
        {
            throw new ArgumentException("Slug is not a valid office slug.", nameof(slug));
        }

        return DatabaseNamePrefix + slug;
    }

    /// <summary>
    /// Builds an office connection string from a base connection string (the host
    /// "Default", or an optional per-environment override) by pointing it at the
    /// office database "CaseEvaluation_{slug}" -- keeping the base server, auth, and
    /// options. Deriving from the base avoids duplicating the SQL credentials into
    /// a second config key (they stay in the single secret-managed Default).
    /// The catalog is set via the synonym-safe "Database" keyword; any existing
    /// "Database"/"Initial Catalog" on the base is replaced. Throws
    /// <see cref="ArgumentException"/> for a blank base or an invalid slug.
    /// </summary>
    public static string BuildConnectionString(string baseConnectionString, string slug)
    {
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            throw new ArgumentException("Base connection string is required.", nameof(baseConnectionString));
        }

        var databaseName = GetDatabaseName(slug);

        var builder = new DbConnectionStringBuilder { ConnectionString = baseConnectionString };
        builder.Remove("Initial Catalog");
        builder.Remove("Database");
        builder["Database"] = databaseName;

        return builder.ConnectionString;
    }
}
