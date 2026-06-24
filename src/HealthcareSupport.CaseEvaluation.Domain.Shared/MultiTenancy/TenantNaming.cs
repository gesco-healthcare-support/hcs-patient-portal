using System;
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
/// </summary>
public static class TenantNaming
{
    /// <summary>Office database name prefix; the host database is "CaseEvaluation".</summary>
    public const string DatabaseNamePrefix = "CaseEvaluation_";

    /// <summary>Subdomain reserved for the host-context surface; never an office slug.</summary>
    public const string ReservedSlug = "admin";

    /// <summary>DNS label length limit; also bounds the database name.</summary>
    public const int MaxSlugLength = 63;

    // Lowercase DNS label: alphanumeric ends, optional internal hyphens.
    private static readonly Regex SlugPattern = new(
        "^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$",
        RegexOptions.CultureInvariant);

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

        if (string.Equals(slug, ReservedSlug, StringComparison.Ordinal))
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
