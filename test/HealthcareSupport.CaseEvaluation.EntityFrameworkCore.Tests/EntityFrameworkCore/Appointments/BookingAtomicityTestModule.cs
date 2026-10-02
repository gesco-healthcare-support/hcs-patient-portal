using HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.Appointments;

/// <summary>
/// The multi-office harness, moved onto real SQL Server with transactions switched back ON, so that
/// a unit of work that fails can actually roll back (#732).
///
/// <para>Two things in <see cref="CaseEvaluationMultiOfficeTestModule"/> make rollback impossible
/// there, and this module reverses both. It calls <c>AddAlwaysDisableUnitOfWorkTransaction</c>, so no
/// unit of work ever opens a transaction; and it routes every DbContext to a named shared-cache
/// SQLite database, opening a connection per resolved string. Here ABP's stock unit-of-work
/// manager and behaviour provider are restored, and every DbContext is pointed at SQL Server.</para>
///
/// <para>Everything else -- disabled background workers, the static stores kept out of the
/// database, the identity and host-environment stubs, always-allow authorization -- is inherited
/// unchanged, so a booking runs through the same application services the multi-office tests use.
/// Authorization is not under test here; atomicity is.</para>
/// </summary>
[DependsOn(typeof(CaseEvaluationMultiOfficeTestModule))]
public class BookingAtomicityTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Undo AddAlwaysDisableUnitOfWorkTransaction, which does TWO things, both reversed here.
        // Measured 2026-10-01 by resolving the services from this module: it replaces the unit-of-work
        // MANAGER with AlwaysDisableTransactionsUnitOfWorkManager, which forces IsTransactional to
        // false even on an explicit Begin(isTransactional: true), and it registers a behaviour
        // provider that disables transactions for the interceptor. Replacing only the provider left
        // the manager in place, and a transactional unit of work that threw still kept its row.
        // This is why #732's one-row control failed: the harness switched transactions off, not
        // SQLite. Both are put back to ABP's stock registrations.
        context.Services.Replace(ServiceDescriptor.Singleton<IUnitOfWorkManager, UnitOfWorkManager>());
        context.Services.Replace(ServiceDescriptor.Singleton<IUnitOfWorkTransactionBehaviourProvider, NullUnitOfWorkTransactionBehaviourProvider>());

        // Registered after the multi-office module's UseSqlite, so this one is the default that
        // applies. The parameterless overload reads the already-resolved connection string: the
        // host database for host scope, the office's stored string inside the office.
        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(configurationContext => configurationContext.UseSqlServer());
        });
    }
}
