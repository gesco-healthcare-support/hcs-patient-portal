using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger<CaseEvaluationTenantDatabaseMigrationHandler> _logger;

    public CaseEvaluationTenantDatabaseMigrationHandler(
        IOfficeDatabaseProvisioner officeProvisioner,
        IHostEnvironment hostEnvironment,
        ILogger<CaseEvaluationTenantDatabaseMigrationHandler> logger)
    {
        _officeProvisioner = officeProvisioner;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task HandleEventAsync(TenantCreatedEto eventData)
    {
        await MigrateAndSeedForTenantAsync(
            eventData.Id,
            eventData.Properties.GetOrDefault("AdminEmail") ?? CaseEvaluationConsts.AdminEmailDefaultValue,
            eventData.Properties.GetOrDefault("AdminPassword") ?? CaseEvaluationConsts.AdminPasswordDefaultValue
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
            CaseEvaluationConsts.AdminPasswordDefaultValue
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
            CaseEvaluationConsts.AdminPasswordDefaultValue
        );
    }

    private async Task MigrateAndSeedForTenantAsync(
        Guid tenantId,
        string adminEmail,
        string adminPassword)
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
            await _officeProvisioner.ProvisionAsync(tenantId, adminEmail, adminPassword);
        }
        catch (Exception ex)
        {
            _logger.LogException(ex);
        }
    }
}
