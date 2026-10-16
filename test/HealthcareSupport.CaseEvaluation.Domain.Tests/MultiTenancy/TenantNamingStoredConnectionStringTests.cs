using System;
using Shouldly;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.MultiTenancy;

/// <summary>
/// What an office row stores (<see cref="TenantNaming.BuildStoredConnectionString"/>) and how it is read
/// back (<see cref="TenantNaming.TryGetSlugFromStoredConnectionString"/>): the database name and nothing
/// else, and only an office database name counts.
/// </summary>
public class TenantNamingStoredConnectionStringTests
{
    [Fact]
    public void TheStoredValue_IsTheDatabaseNameOnly()
    {
        var stored = TenantNaming.BuildStoredConnectionString("falkinstein");

        stored.ShouldBe("Database=CaseEvaluation_falkinstein");
        stored.ShouldNotContain("Server", Case.Insensitive);
        stored.ShouldNotContain("Password", Case.Insensitive);
        stored.ShouldNotContain("User", Case.Insensitive);
    }

    [Fact]
    public void AnInvalidSlug_CannotBeStored()
    {
        Should.Throw<ArgumentException>(() => TenantNaming.BuildStoredConnectionString("admin"));
        Should.Throw<ArgumentException>(() => TenantNaming.BuildStoredConnectionString("Not A Slug"));
    }

    [Theory]
    [InlineData("Database=CaseEvaluation_falkinstein", "falkinstein")]
    [InlineData("Initial Catalog=CaseEvaluation_test-office", "test-office")]
    [InlineData("server=sql-server;Database=CaseEvaluation_test-office;User Id=sa;Password=x", "test-office")]
    [InlineData("Server=a;Initial Catalog=CaseEvaluation_other;Database=CaseEvaluation_wins", "wins")]
    [InlineData("database=caseevaluation_falkinstein", "falkinstein")]
    public void TheSlug_IsReadBackFromTheCatalogAlone(string stored, string slug)
    {
        TenantNaming.TryGetSlugFromStoredConnectionString(stored, out var read).ShouldBeTrue();
        read.ShouldBe(slug);
    }

    [Fact]
    public void BuildThenRead_RoundTrips()
    {
        TenantNaming.TryGetSlugFromStoredConnectionString(
            TenantNaming.BuildStoredConnectionString("f-office-a"), out var slug).ShouldBeTrue();
        slug.ShouldBe("f-office-a");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("Database=CaseEvaluation")]
    [InlineData("Database=master")]
    [InlineData("Database=CaseEvaluation_")]
    [InlineData("Database=CaseEvaluation_admin")]
    [InlineData("Database=CaseEvaluation_api")]
    [InlineData("Database=CaseEvaluation_UPPER")]
    [InlineData("Database=CaseEvaluation_two words")]
    [InlineData("Server=sql-server;User Id=sa")]
    [InlineData("=;;=")]
    public void AnythingElse_NamesNoOfficeDatabase(string? stored)
    {
        TenantNaming.TryGetSlugFromStoredConnectionString(stored, out var slug).ShouldBeFalse();
        slug.ShouldBeEmpty();
    }
}
