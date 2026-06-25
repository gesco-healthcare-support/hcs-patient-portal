using System;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.Data;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Saas;

/// <summary>
/// ADR-006 (2026-05-05) -- seeds the single demo tenant "Falkinstein"
/// so subdomain-based tenant routing has something to resolve to in dev.
///
/// Phase 1B (db-per-office): Falkinstein gets its own connection string
/// (database "CaseEvaluation_falkinstein"), derived from the host
/// "Default" via the B3 secret seam. ABP's
/// MultiTenantConnectionStringResolver then routes its queries to that
/// database, and the migrator loop (CaseEvaluationDbMigrationService)
/// creates + migrates + seeds it. This supersedes the original Phase 1A
/// demo (no connection string; all tenant data in the host DB with
/// row-level IMultiTenant filtering), which was a deliberate stop-gap
/// until the tenant DbContext had a migration -- now in place (Phase A).
///
/// Runs only when ASPNETCORE_ENVIRONMENT=Development. Idempotent: if a
/// tenant named Falkinstein already exists the contributor is a no-op.
/// Reserved-name guard: tenants whose name is "admin" are rejected
/// runtime-side in DoctorTenantAppService.CreateAsync because
/// `admin.localhost` is the host-context surface in the SPA redirect.
/// </summary>
public class FalkinsteinTenantDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string TenantName = "Falkinstein";
    public const string TenantSlug = "falkinstein";

    private readonly ITenantManager _tenantManager;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ITenantConnectionStringProvider _connectionStringProvider;
    private readonly ILogger<FalkinsteinTenantDataSeedContributor> _logger;

    public FalkinsteinTenantDataSeedContributor(
        ITenantManager tenantManager,
        IRepository<Tenant, Guid> tenantRepository,
        ICurrentTenant currentTenant,
        ITenantConnectionStringProvider connectionStringProvider,
        ILogger<FalkinsteinTenantDataSeedContributor> logger)
    {
        _tenantManager = tenantManager;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _connectionStringProvider = connectionStringProvider;
        _logger = logger;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (!IsDevelopment())
        {
            _logger.LogInformation(
                "FalkinsteinTenantDataSeedContributor: skipping (not Development environment).");
            return;
        }

        // SaasTenants rows live in host scope; switch to host context so the
        // multi-tenant filter does not hide an existing row.
        if (context?.TenantId != null)
        {
            return;
        }

        using (_currentTenant.Change(null))
        {
            var existing = await _tenantRepository.FirstOrDefaultAsync(
                t => t.Name == TenantName);
            if (existing != null)
            {
                _logger.LogInformation(
                    "FalkinsteinTenantDataSeedContributor: tenant '{Name}' already exists ({Id}); skipping.",
                    TenantName, existing.Id);
                return;
            }

            // Phase 1B (db-per-office): give Falkinstein its own connection
            // string so ABP's MultiTenantConnectionStringResolver routes its
            // queries to "CaseEvaluation_falkinstein" and the migrator loop
            // (CaseEvaluationDbMigrationService) provisions + seeds that
            // database. The connection string is derived from the host
            // "Default" (B3 secret seam) -- never built from hardcoded
            // credentials. ABP encrypts it at rest on the tenant record.
            var tenant = await _tenantManager.CreateAsync(TenantName);
            tenant.SetDefaultConnectionString(
                _connectionStringProvider.BuildConnectionString(TenantSlug));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);

            _logger.LogInformation(
                "FalkinsteinTenantDataSeedContributor: created tenant '{Name}' ({Id}) with its own database.",
                TenantName, tenant.Id);
        }
    }

    private static bool IsDevelopment()
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }
}
