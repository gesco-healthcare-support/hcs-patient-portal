using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Volo.Abp.Testing;

namespace HealthcareSupport.CaseEvaluation;

public abstract class CaseEvaluationTestBase<TStartupModule> : AbpIntegratedTest<TStartupModule>
    where TStartupModule : IAbpModule
{
    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    protected override void BeforeAddApplication(IServiceCollection services)
    {
        var builder = new ConfigurationBuilder();
        builder.AddJsonFile("appsettings.json", false);
        builder.AddJsonFile("appsettings.secrets.json", true);
        services.ReplaceConfiguration(builder.Build());
    }

    private IDisposable? _ambientTenantScope;

    /// <summary>
    /// Runs the whole test inside <paramref name="tenantId"/>'s scope, and releases it when the
    /// test is disposed. Call it from a test class constructor. The per-office catalog (State,
    /// AppointmentType, Location, WcabOffice, AppointmentLanguage, AppointmentStatus) is seeded
    /// INSIDE a tenant, exactly as production's per-office seeders do (#764), so a test that reads
    /// it has to be inside that tenant too: a host-scope read finds none of it.
    /// </summary>
    protected void UseAmbientTenant(Guid tenantId)
    {
        _ambientTenantScope?.Dispose();
        _ambientTenantScope = ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
    }

    public override void Dispose()
    {
        _ambientTenantScope?.Dispose();
        _ambientTenantScope = null;
        base.Dispose();
    }

    protected virtual Task WithUnitOfWorkAsync(Func<Task> func)
    {
        return WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(), func);
    }

    protected virtual async Task WithUnitOfWorkAsync(AbpUnitOfWorkOptions options, Func<Task> action)
    {
        using (var scope = ServiceProvider.CreateScope())
        {
            var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

            using (var uow = uowManager.Begin(options))
            {
                await action();

                await uow.CompleteAsync();
            }
        }
    }

    protected virtual Task<TResult> WithUnitOfWorkAsync<TResult>(Func<Task<TResult>> func)
    {
        return WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(), func);
    }

    protected virtual async Task<TResult> WithUnitOfWorkAsync<TResult>(AbpUnitOfWorkOptions options, Func<Task<TResult>> func)
    {
        using (var scope = ServiceProvider.CreateScope())
        {
            var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

            using (var uow = uowManager.Begin(options))
            {
                var result = await func();
                await uow.CompleteAsync();
                return result;
            }
        }
    }
}
