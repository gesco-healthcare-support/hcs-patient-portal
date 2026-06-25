using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace HealthcareSupport.CaseEvaluation.Data;

/// <summary>
/// Default <see cref="IOfficeDatabaseProvisioner"/>. Mirrors the migrate-then-seed
/// sequence the DbMigrator uses per tenant: a non-transactional unit of work for
/// schema migration (the DDL auto-commits / cannot be rolled back) followed by a
/// transactional unit of work for seeding. Scoped to the office tenant so the
/// schema migrator resolves the office connection string and writes to the office
/// database. Unlike the deploy-time handler, this does NOT swallow exceptions --
/// the synchronous caller needs failures to surface so it can compensate.
/// </summary>
public class OfficeDatabaseProvisioner : IOfficeDatabaseProvisioner, ITransientDependency
{
    private readonly IEnumerable<ICaseEvaluationDbSchemaMigrator> _dbSchemaMigrators;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IDataSeeder _dataSeeder;
    private readonly ITenantStore _tenantStore;
    private readonly ILogger<OfficeDatabaseProvisioner> _logger;

    public OfficeDatabaseProvisioner(
        IEnumerable<ICaseEvaluationDbSchemaMigrator> dbSchemaMigrators,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IDataSeeder dataSeeder,
        ITenantStore tenantStore,
        ILogger<OfficeDatabaseProvisioner> logger)
    {
        _dbSchemaMigrators = dbSchemaMigrators;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
        _dataSeeder = dataSeeder;
        _tenantStore = tenantStore;
        _logger = logger;
    }

    public async Task ProvisionAsync(Guid tenantId, string adminEmailAddress, string adminPassword)
    {
        using (_currentTenant.Change(tenantId))
        {
            // Schema migration creates the office database if needed. Non-transactional:
            // SQL DDL can't participate in a rollback, and MigrateAsync manages its own.
            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
            {
                var tenantConfiguration = await _tenantStore.FindAsync(tenantId);
                if (tenantConfiguration?.ConnectionStrings != null &&
                    !tenantConfiguration.ConnectionStrings.Default.IsNullOrWhiteSpace())
                {
                    foreach (var migrator in _dbSchemaMigrators)
                    {
                        await migrator.MigrateAsync();
                    }
                }
                else
                {
                    // No office connection string -> the office has no separate database to
                    // provision (would resolve to the host DB). Surface rather than silently
                    // seed into the host database.
                    throw new AbpException(
                        $"Cannot provision office {tenantId}: it has no Default connection string.");
                }

                await uow.CompleteAsync();
            }

            // Seed catalogs + admin user + the office doctor into the office database.
            using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true))
            {
                await _dataSeeder.SeedAsync(
                    new DataSeedContext(tenantId)
                        .WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, adminEmailAddress)
                        .WithProperty(IdentityDataSeedContributor.AdminPasswordPropertyName, adminPassword)
                );

                await uow.CompleteAsync();
            }
        }
    }
}
