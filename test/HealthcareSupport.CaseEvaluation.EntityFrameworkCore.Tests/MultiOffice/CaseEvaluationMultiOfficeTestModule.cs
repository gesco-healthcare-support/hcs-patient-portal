using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.TextTemplateManagement;
using Volo.Abp.Testing;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.MultiOffice;

/// <summary>
/// Multi-office isolation harness (Phase F / F1). Unlike
/// <see cref="CaseEvaluationEntityFrameworkCoreTestModule"/>, which routes every tenant
/// to ONE shared in-memory SQLite connection (good for row-filter tests, blind to
/// separate-database routing), this module gives the host and each office their OWN
/// named in-memory database and lets ABP's stock MultiTenantConnectionStringResolver
/// route by the connection string stored on each tenant record. That is what makes a
/// genuine physical cross-office isolation assertion possible.
///
/// It deliberately does NOT depend on the CaseEvaluation test-base chain: that chain
/// runs the full integration seeder (which seeds catalogs at host scope and wires
/// cross-entity FKs assuming a single database -- both of which break once each office
/// is a separate database). Tests built on this module seed only what they need.
/// </summary>
[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpBackgroundJobsAbstractionsModule),
    typeof(CaseEvaluationEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
)]
public class CaseEvaluationMultiOfficeTestModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<AbpSqliteOptions>(x => x.BusyTimeout = null);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // ABP Pro adds a background worker during initialization whose Logger is
        // resolved through LazyServiceProvider; under the xUnit testhost that
        // provider is not yet attached, crashing StartAsync. Disable the worker
        // manager for the test run (same workaround as the single-connection module).
        Configure<AbpBackgroundWorkerOptions>(options => options.IsEnabled = false);
        Configure<AbpBackgroundJobOptions>(options => options.IsJobExecutionEnabled = false);

        // Keep the static permission/feature/template stores out of the database so
        // schema creation and seeding stay lean.
        Configure<FeatureManagementOptions>(options =>
        {
            options.SaveStaticFeaturesToDatabase = false;
            options.IsDynamicFeatureStoreEnabled = false;
        });
        Configure<PermissionManagementOptions>(options =>
        {
            options.SaveStaticPermissionsToDatabase = false;
            options.IsDynamicPermissionStoreEnabled = false;
        });
        Configure<TextTemplateManagementOptions>(options =>
        {
            options.SaveStaticTemplatesToDatabase = false;
            options.IsDynamicTemplateStoreEnabled = false;
        });

        context.Services.AddAlwaysDisableUnitOfWorkTransaction();

        // The F1 self-validation is a data-layer proof; authorization is irrelevant
        // here. The behavioral-authz harness (F2) is a separate module that seeds real
        // permission grants and does NOT bypass authorization.
        context.Services.AddAlwaysAllowAuthorization();

        // Open the keeper connections and create the schema in each named database
        // before any unit of work runs.
        MultiOfficeTestDatabase.EnsureInitialized();

        // Route EVERY DbContext to the per-request resolved connection string instead
        // of a single fixed connection. For host scope the resolver returns
        // ConnectionStrings:Default (set to the host database in the test base); for a
        // tenant it returns that tenant's stored office connection string. The string
        // MUST exactly match a keeper's named URI or SQLite would open a fresh empty
        // database -- a false-positive isolation pass (F-8).
        Configure<AbpDbContextOptions>(options =>
        {
            // The parameterless ABP overload reads the already-resolved
            // context.ConnectionString (the per-tenant office string, or host Default),
            // so each DbContext opens the database that string names.
            options.Configure(configurationContext =>
            {
                configurationContext.UseSqlite();
            });
        });
    }
}
