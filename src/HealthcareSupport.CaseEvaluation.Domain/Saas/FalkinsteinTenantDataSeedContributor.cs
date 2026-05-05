using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Saas.Tenants;

namespace HealthcareSupport.CaseEvaluation.Saas;

/// <summary>
/// ADR-006 Phase 1A (2026-05-05) -- seeds the single demo tenant
/// "Falkinstein" with a dedicated database (CaseEvaluation_Falkinstein)
/// so subdomain-based tenant routing has something to resolve to in dev.
///
/// Runs only when ASPNETCORE_ENVIRONMENT=Development. Idempotent: if a
/// tenant already exists with the slug "falkinstein" the contributor is
/// a no-op. Reserved-name guard: tenants whose slug is "admin" are
/// rejected runtime-side in DoctorTenantAppService.CreateAsync because
/// `admin.localhost` is the host-context surface.
///
/// Tenant database connection string is derived from the host's
/// "ConnectionStrings:Default" by replacing the database name with
/// CaseEvaluation_Falkinstein. Keeps server, credentials, and
/// trust-cert flags consistent with the host string so a single
/// MSSQL_SA_PASSWORD env var continues to drive both connections.
///
/// After this contributor seeds the SaasTenants row, ABP fires
/// TenantConnectionStringUpdatedEto (because we set the default
/// connection string) and CaseEvaluationDbMigrationService iterates
/// the tenant list to migrate + seed its database. That is where
/// InternalUsersDataSeedContributor + ExternalUserRoleDataSeedContributor
/// + DemoExternalUsersDataSeedContributor populate the tenant DB.
/// </summary>
public class FalkinsteinTenantDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string TenantName = "Falkinstein";
    public const string TenantSlug = "falkinstein";
    public const string TenantDatabaseName = "CaseEvaluation_Falkinstein";

    private readonly ITenantManager _tenantManager;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IConfiguration _configuration;
    private readonly ILogger<FalkinsteinTenantDataSeedContributor> _logger;

    public FalkinsteinTenantDataSeedContributor(
        ITenantManager tenantManager,
        IRepository<Tenant, Guid> tenantRepository,
        ICurrentTenant currentTenant,
        IConfiguration configuration,
        ILogger<FalkinsteinTenantDataSeedContributor> logger)
    {
        _tenantManager = tenantManager;
        _tenantRepository = tenantRepository;
        _currentTenant = currentTenant;
        _configuration = configuration;
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

        // Temporary T4-WIP gate: when SKIP_FALKINSTEIN_SEED=true, no-op.
        // Lets the host-DB migration finish cleanly while
        // CaseEvaluationTenantDbContext is being completed (Patient + Location
        // move-to-tenant per ADR-006 T4). Remove this gate once T4 lands and
        // the tenant DbContext + matching tenant migration can apply.
        if (IsSkipped())
        {
            _logger.LogWarning(
                "FalkinsteinTenantDataSeedContributor: skipping (SKIP_FALKINSTEIN_SEED set); host-only mode.");
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

            var connectionString = BuildTenantConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                _logger.LogWarning(
                    "FalkinsteinTenantDataSeedContributor: host 'ConnectionStrings:Default' is empty; cannot derive tenant connection string. Skipping seed.");
                return;
            }

            var tenant = await _tenantManager.CreateAsync(TenantName);
            tenant.SetDefaultConnectionString(connectionString);
            await _tenantRepository.InsertAsync(tenant, autoSave: true);

            _logger.LogInformation(
                "FalkinsteinTenantDataSeedContributor: created tenant '{Name}' ({Id}) with database {Database}.",
                TenantName, tenant.Id, TenantDatabaseName);
        }
    }

    private string? BuildTenantConnectionString()
    {
        var hostConnectionString = _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(hostConnectionString))
        {
            return null;
        }

        // Swap the Database= token. The host string is built by
        // docker-compose / appsettings as
        // "Server=...;Database=CaseEvaluation;User Id=sa;Password=...;TrustServerCertificate=True".
        // Match the segment between "Database=" and the next ';' (or end of
        // string) without breaking other keys that share substring tokens.
        var segments = hostConnectionString.Split(';', StringSplitOptions.None);
        for (var i = 0; i < segments.Length; i++)
        {
            var trimmed = segments[i].TrimStart();
            if (trimmed.StartsWith("Database=", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
            {
                var keyEnd = segments[i].IndexOf('=');
                segments[i] = segments[i].Substring(0, keyEnd + 1) + TenantDatabaseName;
                return string.Join(';', segments);
            }
        }

        // Host string has no Database= key (LocalDB default-instance shape).
        // Append one. Trim trailing semicolons before append to avoid `;;`.
        return hostConnectionString.TrimEnd(';') + ";Database=" + TenantDatabaseName;
    }

    private static bool IsDevelopment()
    {
        var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
        return string.Equals(environmentName, "Development", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSkipped()
    {
        var raw = Environment.GetEnvironmentVariable("SKIP_FALKINSTEIN_SEED");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }
        return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "1", StringComparison.OrdinalIgnoreCase);
    }
}
