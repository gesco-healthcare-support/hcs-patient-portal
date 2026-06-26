using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Testing;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Base class for tests that run on the multi-office isolation harness. Overrides the
/// host "Default" connection string to point at the in-memory host database, then
/// exposes the same WithUnitOfWorkAsync helpers as the standard test base. Each office
/// connection string is stored on its tenant record by the test itself.
/// </summary>
public abstract class CaseEvaluationMultiOfficeTestBase
    : AbpIntegratedTest<CaseEvaluationMultiOfficeTestModule>
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    protected override void BeforeAddApplication(IServiceCollection services)
    {
        var builder = new ConfigurationBuilder();
        builder.AddJsonFile("appsettings.json", optional: false);
        builder.AddJsonFile("appsettings.secrets.json", optional: true);
        // Point host scope (CurrentTenant == null) at the in-memory host database so
        // UseSqlite(context.ConnectionString) opens it rather than the SQL Server
        // string baked into appsettings.json.
        builder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = MultiOfficeTestDatabase.HostConnectionString,
        });
        services.ReplaceConfiguration(builder.Build());
    }

    // requiresNew starts a fresh, independent unit of work -- needed when crossing
    // into a different tenant's context so the office connection re-resolves (per ABP
    // GitHub #16357 / the project's B9 finding).
    protected virtual async Task WithUnitOfWorkAsync(Func<Task> action, bool requiresNew = false)
    {
        using var scope = ServiceProvider.CreateScope();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew);
        await action();
        await uow.CompleteAsync();
    }

    protected virtual async Task<TResult> WithUnitOfWorkAsync<TResult>(
        Func<Task<TResult>> func, bool requiresNew = false)
    {
        using var scope = ServiceProvider.CreateScope();
        var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        using var uow = uowManager.Begin(new AbpUnitOfWorkOptions(), requiresNew);
        var result = await func();
        await uow.CompleteAsync();
        return result;
    }
}
