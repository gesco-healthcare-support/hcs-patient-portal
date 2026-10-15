using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using HealthcareSupport.CaseEvaluation.Identity.AdminPasswords;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;

namespace HealthcareSupport.CaseEvaluation.Data;

public class CaseEvaluationTenantDatabaseMigrationHandler :
    IDistributedEventHandler<TenantCreatedEto>,
    IDistributedEventHandler<TenantConnectionStringUpdatedEto>,
    IDistributedEventHandler<ApplyDatabaseMigrationsEto>,
    ITransientDependency
{
    private readonly IOfficeDatabaseProvisioner _officeProvisioner;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IAdminPasswordStore _adminPasswordStore;
    private readonly ILogger<CaseEvaluationTenantDatabaseMigrationHandler> _logger;

    public CaseEvaluationTenantDatabaseMigrationHandler(
        IOfficeDatabaseProvisioner officeProvisioner,
        IHostEnvironment hostEnvironment,
        IAdminPasswordStore adminPasswordStore,
        ILogger<CaseEvaluationTenantDatabaseMigrationHandler> logger)
    {
        _officeProvisioner = officeProvisioner;
        _hostEnvironment = hostEnvironment;
        _adminPasswordStore = adminPasswordStore;
        _logger = logger;
    }

    public async Task HandleEventAsync(TenantCreatedEto eventData)
    {
        var suppliedPassword = eventData.Properties.GetOrDefault("AdminPassword");

        // Refused here, BEFORE the DbMigrator check below, on purpose: offices are created in the
        // API, where this handler otherwise does nothing, and this is the only place a typed
        // published default is caught on that path.
        RefuseAKnownDefault(suppliedPassword);

        await MigrateAndSeedForTenantAsync(
            eventData.Id,
            eventData.Properties.GetOrDefault("AdminEmail") ?? CaseEvaluationConsts.AdminEmailDefaultValue,
            () => suppliedPassword.IsNullOrWhiteSpace()
                ? _adminPasswordStore.GetOrCreateAsync(eventData.Id)
                : Task.FromResult(suppliedPassword)
        );
    }

    public async Task HandleEventAsync(TenantConnectionStringUpdatedEto eventData)
    {
        if (eventData.ConnectionStringName != ConnectionStrings.DefaultConnectionStringName ||
            eventData.NewValue.IsNullOrWhiteSpace())
        {
            return;
        }

        await MigrateAndSeedForTenantAsync(
            eventData.Id,
            CaseEvaluationConsts.AdminEmailDefaultValue,
            () => _adminPasswordStore.GetOrCreateAsync(eventData.Id)
        );

        /* You may want to move your data from the old database to the new database!
         * It is up to you. If you don't make it, new database will be empty
         * (and tenant's admin password is reset to the default).
         */
    }

    public async Task HandleEventAsync(ApplyDatabaseMigrationsEto eventData)
    {
        if (eventData.TenantId == null)
        {
            return;
        }

        await MigrateAndSeedForTenantAsync(
            eventData.TenantId.Value,
            CaseEvaluationConsts.AdminEmailDefaultValue,
            () => _adminPasswordStore.GetOrCreateAsync(eventData.TenantId.Value)
        );
    }

    /// <summary>
    /// B12 -- the password a newly created office is seeded with.
    ///
    /// <para>An operator creating an office through the SaaS screen may TYPE a password, and that
    /// is kept: they chose it and they are the one who will hand it over. What is refused is a
    /// typed password that happens to be one this product publishes -- accepting it would put a
    /// brand new office on a password anybody can read in the framework source or in this
    /// repository, by the one route that bypasses the generated store entirely.</para>
    ///
    /// <para>With nothing typed, the stored password for that database is used, generated on first
    /// call -- and only in the DbMigrator, see <see cref="MigrateAndSeedForTenantAsync"/>.</para>
    /// </summary>
    private static void RefuseAKnownDefault(string? suppliedPassword)
    {
        if (AdminPasswordPolicy.IsKnownDefault(suppliedPassword))
        {
            throw new BusinessException(CaseEvaluationDomainErrorCodes.AdminPasswordIsAKnownDefault);
        }
    }

    /// <param name="resolveAdminPassword">
    /// Called only once this process is known to be the DbMigrator. It used to be evaluated by the
    /// caller, so the API -- where this handler does nothing else -- wrote a store entry for every
    /// office it created or re-pointed, holding a password no account was ever given.
    /// </param>
    private async Task MigrateAndSeedForTenantAsync(
        Guid tenantId,
        string adminEmail,
        Func<Task<string>> resolveAdminPassword)
    {
        // Smoke-test 2026-05-04 (G0c): the duplicate-key race on
        // AbpLocalizationResources came from this handler running concurrently
        // in HttpApi.Host AND AuthServer (each subscribes via ITransientDependency
        // when the Domain module loads). The DbMigrator already runs the central
        // IDataSeeder pipeline, so non-migrator hosts can no-op safely. The
        // synchronous runtime path (DoctorTenantAppService) provisions office
        // databases in-process via IOfficeDatabaseProvisioner; this broadcast-event
        // handler covers only the deploy/bulk path under the DbMigrator.
        // Detect via IHostEnvironment.ApplicationName, which ASP.NET Core sets
        // from the entry-assembly name (HealthcareSupport.CaseEvaluation.DbMigrator
        // for the migrator console, AuthServer / HttpApi.Host otherwise).
        if (!_hostEnvironment.ApplicationName.Contains("DbMigrator", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug(
                "CaseEvaluationTenantDatabaseMigrationHandler: skipping tenant migration + seed " +
                "(host '{ApplicationName}' is not the DbMigrator).",
                _hostEnvironment.ApplicationName);
            return;
        }

        try
        {
            await _officeProvisioner.ProvisionAsync(tenantId, adminEmail, await resolveAdminPassword());
        }
        catch (Exception ex)
        {
            _logger.LogException(ex);
        }
    }
}
