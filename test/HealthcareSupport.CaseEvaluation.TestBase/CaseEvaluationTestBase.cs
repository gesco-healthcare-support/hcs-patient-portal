using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using HealthcareSupport.CaseEvaluation.Timing;
using Volo.Abp.Uow;
using Volo.Abp.Testing;

namespace HealthcareSupport.CaseEvaluation;

public abstract class CaseEvaluationTestBase<TStartupModule> : AbpIntegratedTest<TStartupModule>
    where TStartupModule : IAbpModule
{
    // Issue #1031 measurement (draft PR, never merged). A field initializer runs BEFORE the base
    // constructor, so this stamps the start of the whole ABP app build.
    private readonly PhaseRecord _phaseRecord = PhaseClock.Start();

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    // Stamped at the start rather than in AfterAddApplication, because a test class may override
    // that hook without calling base (AppointmentDocumentsServiceFlowTests does).
    protected override IServiceProvider CreateServiceProvider(IServiceCollection services)
    {
        _phaseRecord.MarkProviderStart();
        var provider = base.CreateServiceProvider(services);
        _phaseRecord.MarkProviderEnd();
        return provider;
    }

    protected override void AfterInitialize()
    {
        base.AfterInitialize();
        _phaseRecord.MarkAfterInitialize();
    }

    public override void Dispose()
    {
        _phaseRecord.MarkDisposeStart();
        base.Dispose();
        _phaseRecord.MarkDisposeEnd();
        PhaseClock.Finish(_phaseRecord);
    }

    protected override void BeforeAddApplication(IServiceCollection services)
    {
        _phaseRecord.MarkBeforeAdd(GetType().FullName ?? GetType().Name, typeof(TStartupModule).Name);
        var builder = new ConfigurationBuilder();
        builder.AddJsonFile("appsettings.json", false);
        builder.AddJsonFile("appsettings.secrets.json", true);
        services.ReplaceConfiguration(builder.Build());
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
