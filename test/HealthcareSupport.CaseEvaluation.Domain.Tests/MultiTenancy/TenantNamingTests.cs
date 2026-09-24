using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.MultiTenancy;

/// <summary>
/// Pure unit tests for <see cref="TenantNaming"/> (no DB / DI).
///
/// Pins the per-office naming convention used by db-per-office provisioning:
/// the office subdomain slug is the lowercased office name, and the office
/// database is "CaseEvaluation_{slug}". Derivation VALIDATES rather than
/// transforms, because the subdomain resolver
/// (HostAwareDomainTenantResolveContributor) matches the slug back to the
/// tenant's stored Name -- a transformed slug (e.g. spaces -> hyphens) would
/// no longer resolve. So a non-slug-safe office name fails fast.
/// </summary>
public class TenantNamingTests
{
    // ---- DeriveSlug: happy path ----

    [Fact]
    public void DeriveSlug_lowercases_the_name()
        => TenantNaming.DeriveSlug("Falkinstein").ShouldBe("falkinstein");

    [Fact]
    public void DeriveSlug_trims_surrounding_whitespace()
        => TenantNaming.DeriveSlug("  Falkinstein  ").ShouldBe("falkinstein");

    [Fact]
    public void DeriveSlug_allows_digits_and_internal_hyphens()
        => TenantNaming.DeriveSlug("Dr-Smith2").ShouldBe("dr-smith2");

    // ---- DeriveSlug: fail-fast validation ----

    [Theory]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData(" Admin ")]
    public void DeriveSlug_rejects_the_reserved_admin_slug(string name)
        => Should.Throw<ArgumentException>(() => TenantNaming.DeriveSlug(name));

    [Theory]
    [InlineData("api")]
    [InlineData("API")]
    [InlineData(" Api ")]
    [InlineData("auth")]
    [InlineData("minio")]
    [InlineData("MinIO")]
    public void DeriveSlug_rejects_slugs_the_reverse_proxy_answers_itself(string name)
        => Should.Throw<ArgumentException>(() => TenantNaming.DeriveSlug(name));

    /// <summary>
    /// The two reserved groups are refused for unrelated reasons, and the message is the only
    /// thing that tells an administrator which. Being told "reserved for the host-context
    /// surface" about `minio` sends them to look at tenant resolution, where there is nothing
    /// to find. Pins that the two stay distinguishable.
    /// </summary>
    [Fact]
    public void DeriveSlug_blames_the_proxy_rather_than_host_context_for_a_proxy_slug()
    {
        Should.Throw<ArgumentException>(() => TenantNaming.DeriveSlug("minio"))
            .Message.ShouldContain("reverse proxy");

        Should.Throw<ArgumentException>(() => TenantNaming.DeriveSlug("admin"))
            .Message.ShouldContain("host-context");
    }

    [Theory]
    [InlineData("Dr Smith")]      // internal space -> not a valid subdomain
    [InlineData("dr_smith")]      // underscore not DNS-safe
    [InlineData("dr.smith")]      // dot would split the subdomain
    [InlineData("dr!smith")]      // punctuation
    [InlineData("-leading")]      // leading hyphen
    [InlineData("trailing-")]     // trailing hyphen
    public void DeriveSlug_rejects_non_slug_safe_names(string name)
        => Should.Throw<ArgumentException>(() => TenantNaming.DeriveSlug(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeriveSlug_rejects_null_or_blank(string? name)
        => Should.Throw<ArgumentException>(() => TenantNaming.DeriveSlug(name!));

    [Fact]
    public void DeriveSlug_rejects_names_longer_than_the_dns_label_limit()
        => Should.Throw<ArgumentException>(
            () => TenantNaming.DeriveSlug(new string('a', TenantNaming.MaxSlugLength + 1)));

    [Fact]
    public void DeriveSlug_allows_the_dns_label_limit_exactly()
    {
        var name = new string('a', TenantNaming.MaxSlugLength);
        TenantNaming.DeriveSlug(name).ShouldBe(name);
    }

    // ---- IsValidSlug ----

    [Theory]
    [InlineData("falkinstein", true)]
    [InlineData("dr-smith2", true)]
    [InlineData("a", true)]
    [InlineData("admin", false)]      // reserved for host context
    [InlineData("api", false)]        // claimed by the proxy
    [InlineData("auth", false)]       // claimed by the proxy
    [InlineData("minio", false)]      // claimed by the proxy
    [InlineData("Falkinstein", false)] // uppercase: a SLUG is already-lowercased
    [InlineData("dr smith", false)]
    [InlineData("-foo", false)]
    [InlineData("foo-", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidSlug_matches_the_convention(string? slug, bool expected)
        => TenantNaming.IsValidSlug(slug).ShouldBe(expected);

    // ---- GetDatabaseName ----

    [Fact]
    public void GetDatabaseName_prefixes_the_slug()
        => TenantNaming.GetDatabaseName("falkinstein").ShouldBe("CaseEvaluation_falkinstein");

    [Fact]
    public void GetDatabaseName_rejects_an_invalid_slug()
        => Should.Throw<ArgumentException>(() => TenantNaming.GetDatabaseName("Dr Smith"));

    [Fact]
    public void GetDatabaseName_rejects_a_slug_the_reverse_proxy_claims()
        => Should.Throw<ArgumentException>(() => TenantNaming.GetDatabaseName("api"));

    [Fact]
    public void DeriveSlug_then_GetDatabaseName_round_trips_falkinstein()
        => TenantNaming.GetDatabaseName(TenantNaming.DeriveSlug("Falkinstein"))
            .ShouldBe("CaseEvaluation_falkinstein");

    // ---- BuildConnectionString ----

    private const string LocalDbBase =
        "Server=(LocalDb)\\MSSQLLocalDB;Database=CaseEvaluation;Trusted_Connection=True;TrustServerCertificate=true";

    [Fact]
    public void BuildConnectionString_points_at_the_office_database()
    {
        var result = TenantNaming.BuildConnectionString(LocalDbBase, "falkinstein");

        var parsed = new DbConnectionStringBuilder { ConnectionString = result };
        parsed["Database"].ShouldBe("CaseEvaluation_falkinstein");
    }

    [Fact]
    public void BuildConnectionString_preserves_server_and_auth_options()
    {
        var result = TenantNaming.BuildConnectionString(LocalDbBase, "falkinstein");

        var parsed = new DbConnectionStringBuilder { ConnectionString = result };
        parsed["Server"].ShouldBe("(LocalDb)\\MSSQLLocalDB");
        parsed["Trusted_Connection"].ShouldBe("True");
        parsed["TrustServerCertificate"].ShouldBe("true");
    }

    [Fact]
    public void BuildConnectionString_replaces_an_initial_catalog_synonym()
    {
        var baseWithInitialCatalog =
            "Data Source=db,1433;Initial Catalog=CaseEvaluation;User Id=sa;Password=p;TrustServerCertificate=true";

        var result = TenantNaming.BuildConnectionString(baseWithInitialCatalog, "drsmith");

        var parsed = new DbConnectionStringBuilder { ConnectionString = result };
        parsed["Database"].ShouldBe("CaseEvaluation_drsmith");
        parsed.ContainsKey("Initial Catalog").ShouldBeFalse();
        parsed["User Id"].ShouldBe("sa");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void BuildConnectionString_rejects_a_blank_base(string? baseConnectionString)
        => Should.Throw<ArgumentException>(
            () => TenantNaming.BuildConnectionString(baseConnectionString!, "falkinstein"));

    [Fact]
    public void BuildConnectionString_rejects_an_invalid_slug()
        => Should.Throw<ArgumentException>(
            () => TenantNaming.BuildConnectionString(LocalDbBase, "Dr Smith"));

    // ---- Drift guard: the nginx template is the source of these names ----

    private const string ProxyTemplateRelativePath = "docker/nginx-proxy/default.conf.template";

    /// <summary>
    /// The list of proxy-claimed slugs lives in C# but is DECIDED in the nginx template, and
    /// nothing else couples the two files. This is the coupling: add an exact single-label
    /// `server_name` over there without adding it to
    /// <see cref="TenantNaming.ProxyReservedSlugs"/>, and this fails.
    ///
    /// <para>That drift is not hypothetical. `minio` became an exact host in August and its own
    /// comment said it "becomes a RESERVED office slug, like `admin`", yet nothing enforced it
    /// until this test; `api` and `auth` joined it in #1021 with the same gap.</para>
    ///
    /// <para>Compared as a SET in both directions rather than a containment check, so a stale
    /// entry left behind after a block is removed fails just as loudly as a missing one.</para>
    /// </summary>
    [Fact]
    public void ProxyReservedSlugs_matches_the_exact_single_label_hosts_in_the_nginx_template()
    {
        var templatePath = FindProxyTemplateOrThrow();
        var claimed = ParseExactSingleLabelHosts(File.ReadAllText(templatePath));

        claimed.ShouldNotBeEmpty(
            $"Parsed no exact single-label hosts from {templatePath}. The template always has " +
            "at least one, so this means the parser stopped matching the file's syntax -- which " +
            "would leave this guard permanently and silently green.");

        claimed.ShouldBe(
            TenantNaming.ProxyReservedSlugs.OrderBy(x => x, StringComparer.Ordinal).ToList(),
            $"The exact single-label hosts in {ProxyTemplateRelativePath} and " +
            "TenantNaming.ProxyReservedSlugs have drifted. Every such host captures " +
            "'{label}.${BASE_DOMAIN}' ahead of the '*.${BASE_DOMAIN}' wildcard, so an office " +
            "by that name would be created successfully and then have no reachable front door. " +
            "Reconcile the two lists.");
    }

    /// <summary>
    /// Single-label hosts nginx answers with an EXACT name. Wildcards are deliberately excluded:
    /// `*.api.${BASE_DOMAIN}` routes offices TO the API and reserves nothing, while
    /// `api.${BASE_DOMAIN}` consumes the label itself.
    /// </summary>
    private static List<string> ParseExactSingleLabelHosts(string template)
    {
        var found = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var rawLine in template.Split('\n'))
        {
            // Strip comments FIRST. The template discusses server_name in prose above several
            // blocks, and counting that prose would reserve labels nginx never claims.
            var hash = rawLine.IndexOf('#');
            var line = hash >= 0 ? rawLine.Substring(0, hash) : rawLine;

            foreach (Match directive in ServerNameDirective.Matches(line))
            {
                var hosts = directive.Groups[1].Value.Split(
                    (char[]?)null, StringSplitOptions.RemoveEmptyEntries);

                foreach (var host in hosts)
                {
                    var match = ExactSingleLabelHost.Match(host);
                    if (match.Success)
                    {
                        found.Add(match.Groups[1].Value);
                    }
                }
            }
        }

        return found.ToList();
    }

    /// <summary>
    /// Walks up from this source file to the repository root. Throws rather than skipping when
    /// the template cannot be found: a guard that quietly passes because its input went missing
    /// is the exact failure this repo has shipped before (#901, #902).
    /// </summary>
    private static string FindProxyTemplateOrThrow([CallerFilePath] string callerPath = "")
    {
        var directory = Directory.GetParent(callerPath);

        while (directory != null)
        {
            var candidate = Path.Combine(
                directory.FullName, "docker", "nginx-proxy", "default.conf.template");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new ShouldAssertException(
            $"Could not find {ProxyTemplateRelativePath} walking up from '{callerPath}'. " +
            "This test is a drift guard and must fail loudly when it cannot read its input.");
    }

    private static readonly Regex ServerNameDirective = new(
        @"(?:^|\s)server_name\s+([^;]+);",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));

    // ${BASE_DOMAIN} is matched literally: it is the template's own placeholder, not a value.
    private static readonly Regex ExactSingleLabelHost = new(
        @"^([a-z0-9][a-z0-9-]*)\.\$\{BASE_DOMAIN\}$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(5));
}
