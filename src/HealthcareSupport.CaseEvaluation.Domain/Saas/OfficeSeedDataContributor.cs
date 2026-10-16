using HealthcareSupport.CaseEvaluation.Branding;
using HealthcareSupport.CaseEvaluation.MultiTenancy;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Saas;

/// <summary>
/// Registers every office in <see cref="OfficeSeedData"/> (one synthetic TEST office) as a Volo.Saas
/// tenant whose row names its own database (CaseEvaluation_{slug}; the server and login are composed
/// from configuration at runtime) and seeds each office's host-side branding display
/// name. The migrator loop (CaseEvaluationDbMigrationService) then creates + migrates +
/// seeds each office database. Replaces the single-office FalkinsteinTenantDataSeedContributor.
///
/// Runs in EVERY environment, production included: a fresh deployment gets one synthetic office to
/// exercise end to end. Idempotent: tenants/branding already present are left alone.
/// Tenant Name resolves the subdomain; the slug drives the database name; the branding
/// DisplayName is the brand shown to users (logos are uploaded in-app per office).
/// </summary>
public class OfficeSeedDataContributor : IDataSeedContributor, ITransientDependency
{
    private readonly ITenantManager _tenantManager;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IRepository<OfficeBranding, Guid> _brandingRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<OfficeSeedDataContributor> _logger;

    public OfficeSeedDataContributor(
        ITenantManager tenantManager,
        IRepository<Tenant, Guid> tenantRepository,
        ICurrentTenant currentTenant,
        IRepository<OfficeBranding, Guid> brandingRepository,
        IGuidGenerator guidGenerator,
        ILogger<OfficeSeedDataContributor> logger)
    {
        _tenantManager = tenantManager;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _brandingRepository = brandingRepository;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        // SaaS tenant + branding rows live in host scope; the per-office databases are
        // provisioned + seeded by the migrator loop once a tenant holds a connection string.
        if (context?.TenantId != null)
        {
            return;
        }

        using (_currentTenant.Change(null))
        {
            foreach (var office in OfficeSeedData.Offices)
            {
                var tenant = await _tenantRepository.FirstOrDefaultAsync(t => t.Name == office.TenantName);
                if (tenant == null)
                {
                    // The row stores the office's database name and nothing else. NOT encrypted at
                    // rest (an older comment here said it was); it no longer needs to be, because the
                    // server and login are composed from configuration on every request
                    // (OfficeConnectionStringResolver).
                    tenant = await _tenantManager.CreateAsync(office.TenantName);
                    tenant.SetDefaultConnectionString(TenantNaming.BuildStoredConnectionString(office.Slug));
                    await _tenantRepository.InsertAsync(tenant, autoSave: true);
                    _logger.LogInformation(
                        "OfficeSeedDataContributor: created tenant '{Name}' ({Id}) with its own database.",
                        office.TenantName, tenant.Id);
                }

                var existingBranding = await _brandingRepository.FirstOrDefaultAsync(b => b.OfficeId == tenant.Id);
                if (existingBranding == null)
                {
                    var branding = new OfficeBranding(_guidGenerator.Create(), tenant.Id);
                    branding.SetDisplayName(office.DisplayName);
                    await _brandingRepository.InsertAsync(branding, autoSave: true);
                    _logger.LogInformation(
                        "OfficeSeedDataContributor: seeded branding '{DisplayName}' for office {Name}.",
                        office.DisplayName, office.TenantName);
                }
            }
        }
    }
}
