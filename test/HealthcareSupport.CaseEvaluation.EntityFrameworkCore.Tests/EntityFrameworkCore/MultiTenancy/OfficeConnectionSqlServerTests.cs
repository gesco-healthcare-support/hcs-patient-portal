using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Integration.CaseTracker.SqlServer;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// <see cref="OfficeConnectionStringResolver"/> against a real SQL Server: an office row that stores only
/// its database name opens that database with the login from configuration, and changing that login's
/// password needs a configuration change only. No office row is touched. Docker must be running.
///
/// <para>Uses the SHARED container (<see cref="SqlServerCollection"/>), never a second one. Each test
/// creates its own throwaway login and <c>CaseEvaluation_probe...</c> database through the container's
/// admin connection and drops both afterwards, so nothing it does is visible to the other classes in the
/// collection.</para>
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class OfficeConnectionSqlServerTests
{
    private static readonly Guid OfficeId = Guid.Parse("7b0e2d55-3c1a-4f6e-9d2b-5a8c1e0f4d21");

    private readonly SqlServerFeedFixture _sql;
    private readonly ICurrentTenant _currentTenant = new CurrentTenant(AsyncLocalCurrentTenantAccessor.Instance);
    private readonly ITenantStore _store = Substitute.For<ITenantStore>();

    public OfficeConnectionSqlServerTests(SqlServerFeedFixture sql)
    {
        _sql = sql;
    }

    [Fact]
    public async Task AnOfficeOpensItsDatabase_AndAPasswordChangeNeedsAConfigurationChangeOnly()
    {
        var probe = await CreateProbeAsync();
        try
        {
            OfficeRow(TenantNaming.BuildStoredConnectionString(probe.Slug));

            string before;
            using (_currentTenant.Change(OfficeId))
            {
                before = await Resolver(probe.Login, probe.FirstPassword).ResolveAsync();
            }

            (await WhoAmIAsync(before)).ShouldBe((probe.Database, probe.Login));

            await SqlServerFeedFixture.ExecuteAsync(
                _sql.FeedDatabase, $"ALTER LOGIN [{probe.Login}] WITH PASSWORD = '{probe.SecondPassword}';");
            SqlConnection.ClearAllPools();

            // The connection built from the old configuration no longer opens...
            var refused = await Should.ThrowAsync<SqlException>(() => WhoAmIAsync(before));
            refused.Number.ShouldBe(18456);

            // ...and the new password in configuration is all it takes. The office row is the same value.
            using (_currentTenant.Change(OfficeId))
            {
                var after = await Resolver(probe.Login, probe.SecondPassword).ResolveAsync();
                (await WhoAmIAsync(after)).ShouldBe((probe.Database, probe.Login));
            }

            await _store.Received().FindAsync(OfficeId);
        }
        finally
        {
            await DropProbeAsync(probe);
        }
    }

    [Fact]
    public async Task AnOfficeWhoseRowIsGone_IsRefused_RatherThanGivenTheHostConnection()
    {
        _store.FindAsync(OfficeId).Returns((TenantConfiguration?)null);
        var resolver = Resolver("TEST_unused", "Tt0!unused");

        // Control: host context gets the configured host connection.
        new SqlConnectionStringBuilder(await resolver.ResolveAsync()).InitialCatalog.ShouldBe("CaseEvaluation");

        using (_currentTenant.Change(OfficeId))
        {
            await Should.ThrowAsync<OfficeDatabaseNotConfiguredException>(() => resolver.ResolveAsync());
        }
    }

    private OfficeConnectionStringResolver Resolver(string login, string password)
    {
        var hostDefault = new SqlConnectionStringBuilder(_sql.FeedDatabase)
        {
            InitialCatalog = "CaseEvaluation",
            UserID = login,
            Password = password,
            Encrypt = true,
            TrustServerCertificate = true,
        }.ConnectionString;

        var options = Substitute.For<IOptionsMonitor<AbpDbConnectionOptions>>();
        var connectionOptions = new AbpDbConnectionOptions();
        connectionOptions.ConnectionStrings.Default = hostDefault;
        options.CurrentValue.Returns(connectionOptions);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = hostDefault })
            .Build();
        var services = new ServiceCollection().AddSingleton(_store).BuildServiceProvider();

        return new OfficeConnectionStringResolver(
            options, _currentTenant, services, new TenantConnectionStringProvider(configuration));
    }

    private void OfficeRow(string storedDefault)
    {
        var office = new TenantConfiguration(OfficeId, "probe-office") { ConnectionStrings = new ConnectionStrings() };
        office.ConnectionStrings.Default = storedDefault;
        _store.FindAsync(OfficeId).Returns(office);
    }

    private static async Task<(string Database, string Login)> WhoAmIAsync(string connectionString)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT DB_NAME(), SUSER_SNAME();", connection);
        await using var reader = await command.ExecuteReaderAsync();
        await reader.ReadAsync();
        return (reader.GetString(0), reader.GetString(1));
    }

    private async Task<Probe> CreateProbeAsync()
    {
        var hex = Guid.NewGuid().ToString("N")[..10];
        var probe = new Probe(
            Slug: "probe" + hex,
            Login: "TEST_probe_" + hex,
            FirstPassword: "Tt1!" + Guid.NewGuid().ToString("N"),
            SecondPassword: "Tt2!" + Guid.NewGuid().ToString("N"));

        await SqlServerFeedFixture.ExecuteAsync(_sql.FeedDatabase,
            $"CREATE LOGIN [{probe.Login}] WITH PASSWORD = '{probe.FirstPassword}', CHECK_POLICY = OFF; "
            + $"CREATE DATABASE [{probe.Database}];");
        await SqlServerFeedFixture.ExecuteAsync(Catalog(probe.Database),
            $"CREATE USER [{probe.Login}] FOR LOGIN [{probe.Login}];");
        return probe;
    }

    private async Task DropProbeAsync(Probe probe)
    {
        SqlConnection.ClearAllPools();
        await SqlServerFeedFixture.ExecuteAsync(_sql.FeedDatabase,
            $"IF DB_ID(N'{probe.Database}') IS NOT NULL BEGIN "
            + $"ALTER DATABASE [{probe.Database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{probe.Database}]; END; "
            + $"IF SUSER_ID(N'{probe.Login}') IS NOT NULL DROP LOGIN [{probe.Login}];");
    }

    private string Catalog(string name) =>
        new SqlConnectionStringBuilder(_sql.FeedDatabase) { InitialCatalog = name }.ConnectionString;

    private sealed record Probe(string Slug, string Login, string FirstPassword, string SecondPassword)
    {
        public string Database => TenantNaming.DatabaseNamePrefix + Slug;
    }
}
