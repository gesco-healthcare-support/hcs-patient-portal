using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.Data;

/// <summary>
/// <see cref="OfficeConnectionStringResolver"/>: an office's connection is composed from the database name
/// on its row and the login in configuration, and an office with no usable row is REFUSED rather than given
/// the global Default (the host database), which is what ABP's stock resolver does.
///
/// <para>Built by hand over the real <see cref="TenantConnectionStringProvider"/> and ABP's real
/// <see cref="CurrentTenant"/>; only the tenant store is substituted, so each row shape is exact.</para>
/// </summary>
public class OfficeConnectionStringResolverTests
{
    private const string HostDefault = "Server=sql-test;Database=CaseEvaluation;User Id=portal_app;Password=TEST-P1;TrustServerCertificate=True";
    private static readonly Guid OfficeId = Guid.Parse("4c2f8f1e-9a51-4d93-9e65-1f0d8f0c0a11");

    private readonly ICurrentTenant _currentTenant = new CurrentTenant(AsyncLocalCurrentTenantAccessor.Instance);
    private readonly ITenantStore _store = Substitute.For<ITenantStore>();

    private OfficeConnectionStringResolver Resolver(string baseConnectionString = HostDefault)
    {
        var options = Substitute.For<IOptionsMonitor<AbpDbConnectionOptions>>();
        var connectionOptions = new AbpDbConnectionOptions();
        connectionOptions.ConnectionStrings.Default = baseConnectionString;
        options.CurrentValue.Returns(connectionOptions);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Default"] = baseConnectionString })
            .Build();
        var services = new ServiceCollection().AddSingleton(_store).BuildServiceProvider();

        return new OfficeConnectionStringResolver(
            options, _currentTenant, services, new TenantConnectionStringProvider(configuration));
    }

    private void OfficeRow(string? storedDefault)
    {
        var office = new TenantConfiguration(OfficeId, "test-office") { ConnectionStrings = new ConnectionStrings() };
        if (storedDefault != null)
        {
            office.ConnectionStrings.Default = storedDefault;
        }

        _store.FindAsync(OfficeId).Returns(office);
#pragma warning disable CS0618 // the sync path is obsolete in ABP but still overridden here
        _store.Find(OfficeId).Returns(office);
#pragma warning restore CS0618
    }

    private static DbConnectionStringBuilder Parse(string connectionString) => new() { ConnectionString = connectionString };

    [Fact]
    public async Task HostContext_GetsTheConfiguredDefault()
    {
        (await Resolver().ResolveAsync()).ShouldBe(HostDefault);
        (await Resolver().ResolveAsync(ConnectionStrings.DefaultConnectionStringName)).ShouldBe(HostDefault);
    }

    [Fact]
    public async Task AnOffice_GetsItsDatabaseWithTheConfiguredLogin()
    {
        OfficeRow(TenantNaming.BuildStoredConnectionString("test-office"));

        using (_currentTenant.Change(OfficeId))
        {
            var resolved = Parse(await Resolver().ResolveAsync());
            resolved["Database"].ShouldBe("CaseEvaluation_test-office");
            resolved["Server"].ShouldBe("sql-test");
            resolved["User Id"].ShouldBe("portal_app");
            resolved["Password"].ShouldBe("TEST-P1");
        }
    }

    [Fact]
    public async Task ARowAnOlderBuildWrote_RoutesToItsDatabase_ButItsServerAndLoginAreIgnored()
    {
        OfficeRow("Server=some-other-server;Database=CaseEvaluation_test-office;User Id=sa;Password=TEST-OLD");

        using (_currentTenant.Change(OfficeId))
        {
            var resolved = Parse(await Resolver().ResolveAsync());
            resolved["Database"].ShouldBe("CaseEvaluation_test-office");
            resolved["Server"].ShouldBe("sql-test");
            resolved["User Id"].ShouldBe("portal_app");
            resolved["Password"].ShouldBe("TEST-P1");
        }
    }

    [Fact]
    public async Task ChangingOnlyTheConfiguredLogin_ChangesEveryOfficeOnItsNextRequest()
    {
        OfficeRow(TenantNaming.BuildStoredConnectionString("test-office"));

        using (_currentTenant.Change(OfficeId))
        {
            var before = Parse(await Resolver().ResolveAsync());
            var after = Parse(await Resolver(HostDefault.Replace("TEST-P1", "TEST-P2")).ResolveAsync());

            before["Password"].ShouldBe("TEST-P1");
            after["Password"].ShouldBe("TEST-P2");
            after["Database"].ShouldBe("CaseEvaluation_test-office");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("AbpIdentity")]
    [InlineData("AbpAuditLogging")]
    [InlineData("Default")]
    public async Task EveryConnectionStringName_ResolvesToTheOfficeDatabase(string? name)
    {
        OfficeRow(TenantNaming.BuildStoredConnectionString("test-office"));

        using (_currentTenant.Change(OfficeId))
        {
            Parse(await Resolver().ResolveAsync(name))["Database"].ShouldBe("CaseEvaluation_test-office");
        }
    }

    [Fact]
    public async Task AnOfficeThatIsNotFound_IsRefused()
    {
        _store.FindAsync(OfficeId).Returns((TenantConfiguration?)null);

        using (_currentTenant.Change(OfficeId))
        {
            var ex = await Should.ThrowAsync<OfficeDatabaseNotConfiguredException>(() => Resolver().ResolveAsync());
            ex.OfficeId.ShouldBe(OfficeId);
            ex.Message.ShouldContain(OfficeId.ToString());
            ex.Message.ShouldContain("no office record");
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task AnOfficeWithNoDatabaseSetting_IsRefused_NotGivenTheHostDatabase(string? stored)
    {
        OfficeRow(stored);

        using (_currentTenant.Change(OfficeId))
        {
            var ex = await Should.ThrowAsync<OfficeDatabaseNotConfiguredException>(() => Resolver().ResolveAsync());
            ex.Message.ShouldContain("no office database setting");
        }
    }

    [Theory]
    [InlineData("Database=CaseEvaluation")]                     // the host catalog
    [InlineData("Server=sql-test;Database=master")]             // not an office database
    [InlineData("Database=CaseEvaluation_admin")]               // a reserved slug
    [InlineData("Database=CaseEvaluation_")]                    // no slug
    [InlineData("Database=CaseEvaluation_Has Space")]           // not a DNS-safe slug
    [InlineData("Server=sql-test;User Id=portal_app")]          // no catalog at all
    [InlineData("=;;=")]                                        // not a connection string
    public async Task ASettingThatNamesNoOfficeDatabase_IsRefused(string stored)
    {
        OfficeRow(stored);

        using (_currentTenant.Change(OfficeId))
        {
            var ex = await Should.ThrowAsync<OfficeDatabaseNotConfiguredException>(() => Resolver().ResolveAsync());
            ex.Message.ShouldContain("names no office database");
        }
    }

    [Fact]
    public async Task TheRefusalNeverEchoesTheStoredValue()
    {
        OfficeRow("Server=sql-test;Database=master;User Id=TEST-markeruser;Password=TEST-markervalue");

        using (_currentTenant.Change(OfficeId))
        {
            var ex = await Should.ThrowAsync<OfficeDatabaseNotConfiguredException>(() => Resolver().ResolveAsync());
            ex.Message.ShouldNotContain("markervalue");
            ex.Message.ShouldNotContain("markeruser");
            ex.Message.ShouldNotContain("master");
        }
    }

    [Fact]
    public void TheSyncPath_RoutesAndRefusesTheSameWay()
    {
        var resolver = Resolver();
#pragma warning disable CS0618 // ABP marks Resolve obsolete; it is still public, so it is overridden and tested
        resolver.Resolve().ShouldBe(HostDefault);

        OfficeRow(TenantNaming.BuildStoredConnectionString("test-office"));
        using (_currentTenant.Change(OfficeId))
        {
            Parse(resolver.Resolve())["Database"].ShouldBe("CaseEvaluation_test-office");
        }

        OfficeRow("Database=CaseEvaluation");
        using (_currentTenant.Change(OfficeId))
        {
            Should.Throw<OfficeDatabaseNotConfiguredException>(() => resolver.Resolve());
        }
#pragma warning restore CS0618
    }
}
