using System;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Logging;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// <see cref="OfficeDatabaseSettingRewriter"/>: an office row an older build wrote is brought to the
/// database-name-only form, a row already in that form is left alone, and a row that names no office
/// database is left unchanged and reported by id. Nothing it logs carries a stored value.
///
/// <para>Every EF test gets its own database (guard G5), so the counts below are exact: the rig's two
/// seeded offices store no setting and are never rewritten.</para>
/// </summary>
public class OfficeDatabaseSettingRewriterTests : CaseEvaluationEntityFrameworkCoreTestBase
{
    private readonly ITenantManager _tenantManager;
    private readonly ITenantRepository _tenants;
    private readonly ICurrentTenant _currentTenant;
    private readonly RecordingLogger<OfficeDatabaseSettingRewriter> _log = new();

    public OfficeDatabaseSettingRewriterTests()
    {
        _tenantManager = GetRequiredService<ITenantManager>();
        _tenants = GetRequiredService<ITenantRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    [Fact]
    public async Task ARowAnOlderBuildWrote_IsRewrittenToTheDatabaseNameOnly_AndItsOtherNamedStringsAreRemoved()
    {
        var slug = NewSlug();
        var id = await CreateOfficeAsync(slug,
            $"Server=sql-server;Database=CaseEvaluation_{slug};User Id=TEST-olduser;Password=TEST-oldvalue",
            ("AbpAuditLogging", $"Server=sql-server;Database=CaseEvaluation_{slug};User Id=TEST-olduser;Password=TEST-oldvalue"));

        (await RewriteAsync()).ShouldBe(1);

        var office = await InHostAsync(() => _tenants.GetAsync(id));
        office.FindDefaultConnectionString().ShouldBe(TenantNaming.BuildStoredConnectionString(slug));
        office.ConnectionStrings.Select(c => c.Name).ShouldBe(new[] { ConnectionStrings.DefaultConnectionStringName });
        NothingLoggedContains("TEST-oldvalue", "TEST-olduser", "sql-server");
    }

    [Fact]
    public async Task ARowAlreadyInTheNewForm_IsLeftAlone_AndASecondRunRewritesNothing()
    {
        var current = NewSlug();
        var older = NewSlug();
        await CreateOfficeAsync(current, TenantNaming.BuildStoredConnectionString(current));
        await CreateOfficeAsync(older, $"Server=sql-server;Initial Catalog=CaseEvaluation_{older};User Id=TEST-u;Password=TEST-p");

        (await RewriteAsync()).ShouldBe(1);
        (await RewriteAsync()).ShouldBe(0);

        var rows = await InHostAsync(() => _tenants.GetListAsync(includeDetails: true));
        rows.Single(t => t.Name == current).FindDefaultConnectionString().ShouldBe(TenantNaming.BuildStoredConnectionString(current));
        rows.Single(t => t.Name == older).FindDefaultConnectionString().ShouldBe(TenantNaming.BuildStoredConnectionString(older));
    }

    [Fact]
    public async Task ARowThatNamesNoOfficeDatabase_IsLeftUnchanged_AndReportedByIdWithoutItsValue()
    {
        const string unusable = "Server=sql-test;Database=master;User Id=TEST-markeruser;Password=TEST-markervalue";
        var id = await CreateOfficeAsync(NewSlug(), unusable);

        (await RewriteAsync()).ShouldBe(0);

        (await InHostAsync(() => _tenants.GetAsync(id))).FindDefaultConnectionString().ShouldBe(unusable);
        var error = _log.Entries.Where(e => e.Level == LogLevel.Error).ShouldHaveSingleItem();
        error.Message.ShouldContain(id.ToString());
        error.Message.ShouldContain("names no office database");
        NothingLoggedContains("TEST-markervalue", "TEST-markeruser", "master", "sql-test");
    }

    [Fact]
    public async Task AnOfficeWithNoSetting_IsLeftAlone_AndNotReportedHere()
    {
        var id = await CreateOfficeAsync(NewSlug(), storedDefault: null);

        (await RewriteAsync()).ShouldBe(0);

        (await InHostAsync(() => _tenants.GetAsync(id))).FindDefaultConnectionString().ShouldBeNull();
        // The migrator's office loop reports it, as it reports the rig's own offices; the rewriter does not.
        _log.Entries.ShouldNotContain(e => e.Level >= LogLevel.Warning);
    }

    private async Task<int> RewriteAsync()
    {
        var rewriter = GetRequiredService<OfficeDatabaseSettingRewriter>();
        rewriter.Logger = _log;
        return await rewriter.RewriteAsync();
    }

    private void NothingLoggedContains(params string[] fragments)
    {
        _log.Entries.ShouldNotBeEmpty();
        foreach (var entry in _log.Entries)
        {
            foreach (var fragment in fragments)
            {
                entry.Message.ShouldNotContain(fragment);
            }
        }
    }

    private Task<Guid> CreateOfficeAsync(string slug, string? storedDefault, params (string Name, string Value)[] named) =>
        InHostAsync(async () =>
        {
            var office = await _tenantManager.CreateAsync(slug);
            if (storedDefault != null)
            {
                office.SetDefaultConnectionString(storedDefault);
            }

            foreach (var (name, value) in named)
            {
                office.SetConnectionString(name, value);
            }

            await _tenants.InsertAsync(office, autoSave: true);
            return office.Id;
        });

    private Task<T> InHostAsync<T>(Func<Task<T>> action) =>
        WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                return await action();
            }
        });

    private static string NewSlug() => "testoffice" + Guid.NewGuid().ToString("N")[..10];
}
