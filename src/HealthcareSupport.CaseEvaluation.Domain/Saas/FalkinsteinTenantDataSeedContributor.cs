using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Saas;

/// <summary>
/// ADR-006 Phase 1A (2026-05-05) -- seeds the single demo tenant
/// "Falkinstein" so subdomain-based tenant routing has something to
/// resolve to in dev.
///
/// Phase 1A scope (2026-05-05 demo): Falkinstein has NO separate
/// connection string. All tenant data lives in the host database and
/// is scoped at row level by ABP's IMultiTenant filter (Patient now
/// implements IMultiTenant per FEAT-09). Physical DB-per-tenant
/// isolation is deferred to Phase 1B because:
///   1. The dual-DbContext infrastructure (CaseEvaluationTenantDbContext)
///      has no migrations -- activating it requires generating a
///      first-ever tenant migration that creates ~25 tenant tables.
///   2. The demo flows (login, slot booking, approve/reject, packet)
///      run identically against a single DB with row-level filtering.
///   3. HIPAA cross-tenant read protection is delivered by the
///      IMultiTenant filter; physical separation is a hardening step,
///      not a correctness requirement.
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
    private readonly ILogger<FalkinsteinTenantDataSeedContributor> _logger;

    public FalkinsteinTenantDataSeedContributor(
        ITenantManager tenantManager,
        IRepository<Tenant, Guid> tenantRepository,
        ICurrentTenant currentTenant,
        ILogger<FalkinsteinTenantDataSeedContributor> logger)
    {
        _tenantManager = tenantManager;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
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

            // Phase 1A: no SetDefaultConnectionString call. Without a
            // tenant-side connection string, ABP's MultiTenantConnectionStringResolver
            // falls back to the host's "Default" connection string for
            // every tenant query, so all tenant data lives in the same DB
            // with row-level filtering. When Phase 1B activates dual-DbContext,
            // restore the SetDefaultConnectionString call here.
            var tenant = await _tenantManager.CreateAsync(TenantName);
            await _tenantRepository.InsertAsync(tenant, autoSave: true);

            _logger.LogInformation(
                "FalkinsteinTenantDataSeedContributor: created tenant '{Name}' ({Id}) using host DB.",
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
