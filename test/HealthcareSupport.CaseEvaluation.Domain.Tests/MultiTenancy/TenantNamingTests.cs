using System;
using System.Data.Common;
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
    [InlineData("admin", false)]      // reserved
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
}
