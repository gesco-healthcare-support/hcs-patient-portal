using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Data;
using HealthcareSupport.CaseEvaluation.Logging;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;
using Xunit;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiTenancy;

/// <summary>Records which scope (host = null, or an office id) each schema migration and seed ran in.</summary>
public sealed class MigrationRecorder : ICaseEvaluationDbSchemaMigrator, IDataSeeder
{
    private readonly ICurrentTenant _currentTenant;

    public MigrationRecorder(ICurrentTenant currentTenant) => _currentTenant = currentTenant;

    public List<Guid?> Migrated { get; } = new();

    public List<Guid?> Seeded { get; } = new();

    public Task MigrateAsync()
    {
        Migrated.Add(_currentTenant.Id);
        return Task.CompletedTask;
    }

    public Task SeedAsync(DataSeedContext context)
    {
        Seeded.Add(context.TenantId);
        return Task.CompletedTask;
    }
}

/// <summary>
/// The EF test graph with every schema migrator and the data seeder replaced by one
/// <see cref="MigrationRecorder"/>, so <see cref="CaseEvaluationDbMigrationService.MigrateAsync"/> runs for
/// real (the rewriter, the office loop, the admin-password steps) without migrating or seeding anything.
/// </summary>
[DependsOn(typeof(CaseEvaluationEntityFrameworkCoreTestModule))]
public class OfficeMigrationSkipTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddSingleton<MigrationRecorder>();
        context.Services.RemoveAll<ICaseEvaluationDbSchemaMigrator>();
        context.Services.AddTransient<ICaseEvaluationDbSchemaMigrator>(sp => sp.GetRequiredService<MigrationRecorder>());
        context.Services.Replace(ServiceDescriptor.Transient<IDataSeeder>(sp => sp.GetRequiredService<MigrationRecorder>()));
    }
}

/// <summary>
/// The DbMigrator's office loop migrates and seeds an office only when its row names an office database.
/// The loop it replaced seeded an office with no setting regardless, and with no office database to go
/// to, that seed landed in the HOST database. Now such an office is skipped and reported by id.
///
/// <para>The rig's two seeded offices store no setting, so they are skipped too; the assertions name every
/// scope that ran, so they cover them.</para>
/// </summary>
public class OfficeMigrationSkipTests : CaseEvaluationTestBase<OfficeMigrationSkipTestModule>
{
    private readonly ITenantManager _tenantManager;
    private readonly ITenantRepository _tenants;
    private readonly ICurrentTenant _currentTenant;
    private readonly MigrationRecorder _recorder;

    public OfficeMigrationSkipTests()
    {
        _tenantManager = GetRequiredService<ITenantManager>();
        _tenants = GetRequiredService<ITenantRepository>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _recorder = GetRequiredService<MigrationRecorder>();
    }

    [Fact]
    public async Task OnlyTheHostAndOfficesWithAnOfficeDatabase_AreMigratedAndSeeded()
    {
        var current = await CreateOfficeAsync(slug => TenantNaming.BuildStoredConnectionString(slug));
        var older = await CreateOfficeAsync(slug => $"Server=sql-server;Database=CaseEvaluation_{slug};User Id=TEST-u;Password=TEST-p");
        var none = await CreateOfficeAsync(_ => null);
        var unusable = await CreateOfficeAsync(_ => "Server=sql-server;Database=CaseEvaluation;User Id=TEST-u;Password=TEST-p");
        var log = new RecordingLogger<CaseEvaluationDbMigrationService>();
        var service = GetRequiredService<CaseEvaluationDbMigrationService>();
        service.Logger = log;

        await service.MigrateAsync();

        var expected = new Guid?[] { null, current, older };
        _recorder.Migrated.Distinct().ShouldBe(expected, ignoreOrder: true);
        _recorder.Seeded.ShouldBe(expected, ignoreOrder: true);

        var errors = log.Entries.Where(e => e.Level == LogLevel.Error).Select(e => e.Message).ToList();
        errors.ShouldContain(m => m.Contains(none.ToString()) && m.Contains("NOT migrated or seeded"));
        errors.ShouldContain(m => m.Contains(unusable.ToString()) && m.Contains("NOT migrated or seeded"));
        errors.ShouldNotContain(m => m.Contains(current.ToString()) || m.Contains(older.ToString()));
        log.Entries.ShouldNotContain(e => e.Message.Contains("TEST-p") || e.Message.Contains("sql-server"));
    }

    [Fact]
    public async Task TheMigrator_RewritesAnOlderRow_BeforeItsOfficeLoop()
    {
        string slug = string.Empty;
        var older = await CreateOfficeAsync(s =>
        {
            slug = s;
            return $"Server=sql-server;Database=CaseEvaluation_{s};User Id=TEST-u;Password=TEST-p";
        });

        await GetRequiredService<CaseEvaluationDbMigrationService>().MigrateAsync();

        (await InHostAsync(() => _tenants.GetAsync(older))).FindDefaultConnectionString()
            .ShouldBe(TenantNaming.BuildStoredConnectionString(slug));
        _recorder.Seeded.ShouldContain(older);
    }

    private Task<Guid> CreateOfficeAsync(Func<string, string?> storedDefault) =>
        InHostAsync(async () =>
        {
            var slug = "testoffice" + Guid.NewGuid().ToString("N")[..10];
            var office = await _tenantManager.CreateAsync(slug);
            var stored = storedDefault(slug);
            if (stored != null)
            {
                office.SetDefaultConnectionString(stored);
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
}
