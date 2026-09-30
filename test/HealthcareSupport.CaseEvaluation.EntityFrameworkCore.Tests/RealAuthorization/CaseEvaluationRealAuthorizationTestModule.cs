using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Autofac;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.TextTemplateManagement;
using Volo.Abp.Testing;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.EntityFrameworkCore.RealAuthorization;

/// <summary>
/// #707 LAYER 3 -- the only harness in this repository where a permission check can
/// actually refuse a caller.
///
/// <para>WHY IT EXISTS. Every other test module calls <c>AddAlwaysAllowAuthorization()</c>
/// (<c>CaseEvaluationTestBaseModule.cs:27</c> and
/// <c>CaseEvaluationMultiOfficeTestModule.cs:103</c>), so ABP's authorization interceptor
/// always succeeds and all 243 permission-bearing <c>[Authorize]</c> attributes are inert
/// under test. Layers 1 and 2 (<c>Application.Tests/Authorization/</c>) prove the
/// DECLARATION has not changed and that no public app-service method lost its attribute.
/// Neither proves the mechanism WORKS. This module is where that is proven.</para>
///
/// <para>SCOPE IS DELIBERATELY THE PHI SURFACES, NOT ALL 243. #707 names the expensive
/// mistake explicitly: building a per-permission harness for every attribute is the most
/// effort for the least coverage. What is covered here is the data that must not leak --
/// SSN reveal, appointment documents, appointment packets, and the Case Tracker push.</para>
///
/// <para><b>IT DOES NOT REMOVE ALWAYS-ALLOW; IT NEVER ADDS IT.</b> The first draft of this
/// module depended on <c>CaseEvaluationMultiOfficeTestModule</c> and stripped the
/// always-allow service descriptors afterwards, on the assumption that
/// <c>AddAlwaysAllowAuthorization()</c> appends a winning registration over a real one that
/// stays underneath. Measured 2026-09-16: it does not. Removing its
/// <c>IAuthorizationService</c> descriptor left that service with NO implementation at all,
/// and the harness failed to construct. Composing the module chain directly and simply not
/// calling always-allow is both correct and considerably less machinery -- and it means
/// this harness resolves whatever ABP resolves in production, rather than whatever a
/// hand-maintained list of replacement types says it should.</para>
///
/// <para>The module list below is <c>CaseEvaluationMultiOfficeTestModule</c>'s, and the
/// configuration mirrors it, because the data-layer requirements are identical: named
/// per-database SQLite routed by the tenant's stored connection string. The one difference
/// is the authorization pipeline, which is the entire point of the file.</para>
/// </summary>
[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpTestBaseModule),
    typeof(AbpAuthorizationModule),
    typeof(AbpBackgroundJobsAbstractionsModule),
    typeof(CaseEvaluationApplicationModule),
    typeof(CaseEvaluationEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
)]
public class CaseEvaluationRealAuthorizationTestModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        PreConfigure<AbpSqliteOptions>(x => x.BusyTimeout = null);
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // ABP Pro adds a background worker during initialization whose Logger is resolved
        // through LazyServiceProvider; under the xUnit testhost that provider is not yet
        // attached, crashing StartAsync.
        Configure<AbpBackgroundWorkerOptions>(options => options.IsEnabled = false);
        Configure<AbpBackgroundJobOptions>(options => options.IsJobExecutionEnabled = false);

        // Keep the static feature and template stores out of the database so schema
        // creation and seeding stay lean.
        Configure<FeatureManagementOptions>(options =>
        {
            options.SaveStaticFeaturesToDatabase = false;
            options.IsDynamicFeatureStoreEnabled = false;
        });
        Configure<TextTemplateManagementOptions>(options =>
        {
            options.SaveStaticTemplatesToDatabase = false;
            options.IsDynamicTemplateStoreEnabled = false;
        });
        // #1034: ABP's setting store also saves its static definitions from a BACKGROUND task at
        // start-up. With test classes running in parallel, that task raced the seed on this
        // application's SQLite connection (not thread-safe) and failed a test in its constructor
        // with "Index was out of range" in SqliteConnection.RemoveCommand. Switched off here for
        // the same reason as the three stores above; #1056 did the same in the main EF module.
        Configure<SettingManagementOptions>(options =>
        {
            options.SaveStaticSettingsToDatabase = false;
            options.IsDynamicSettingStoreEnabled = false;
        });

        // PERMISSIONS ARE CONFIGURED DIFFERENTLY HERE THAN IN THE ISOLATION HARNESS, AND
        // DELIBERATELY SO. That module also switches off the DYNAMIC permission store,
        // which is a store of permission DEFINITIONS, not of grants. This harness leaves
        // the defaults alone except for keeping static definitions out of the database:
        // the definitions come from the definition providers in memory, and the GRANTS --
        // the rows this harness seeds and the permission checker reads -- come from the
        // PermissionGrant table either way.
        Configure<PermissionManagementOptions>(options =>
        {
            options.SaveStaticPermissionsToDatabase = false;
        });

        context.Services.AddAlwaysDisableUnitOfWorkTransaction();

        // Two stubs the production host provides, mirroring the other test modules: an
        // IHostEnvironment (ExternalSignupAppService gates dev-only helpers on it) and an
        // Identity "Default" token provider (RegisterAsync generates a confirmation token
        // through it).
        context.Services.AddSingleton<IHostEnvironment>(new HostingEnvironment
        {
            EnvironmentName = Environments.Development,
            ApplicationName = "CaseEvaluationRealAuthorizationTests",
            ContentRootPath = System.IO.Directory.GetCurrentDirectory(),
        });
        context.Services.AddTransient<NoOpTwoFactorTokenProvider>();
        context.Services.Configure<IdentityOptions>(options =>
        {
            options.Tokens.ProviderMap["Default"] =
                new TokenProviderDescriptor(typeof(NoOpTwoFactorTokenProvider));
        });

        // NOTE FOR ANYONE EDITING THIS FILE: there is no AddAlwaysAllowAuthorization() call
        // here and adding one would silently turn every test in this namespace green while
        // proving nothing. RealAuthorizationHarnessSelfValidationTests asserts against
        // exactly that, so the suite would fail rather than quietly lose its teeth.

        RealAuthorizationTestDatabase.EnsureInitialized();

        // Route every DbContext to the per-request resolved connection string: host scope
        // gets ConnectionStrings:Default (set to this harness's host database in the test
        // base), a tenant gets that tenant's stored office connection string.
        Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(configurationContext =>
            {
                configurationContext.UseSqlite();
            });
        });
    }
}
