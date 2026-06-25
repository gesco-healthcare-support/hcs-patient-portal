using System;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.Data;

/// <summary>
/// Provisions a single office's database: applies the tenant schema (creating the
/// database if needed) and seeds it (catalogs, the admin user, and the office's
/// doctor). Used by two callers: the synchronous office-creation path
/// (DoctorTenantAppService, runtime/in-process) and the deploy-time event handler
/// (CaseEvaluationTenantDatabaseMigrationHandler, DbMigrator). The operations are
/// idempotent so a retry after a partial failure completes the office.
/// </summary>
public interface IOfficeDatabaseProvisioner
{
    /// <summary>
    /// Migrates + seeds the office database for <paramref name="tenantId"/>, scoped
    /// to that tenant. The admin user is seeded with
    /// <paramref name="adminEmailAddress"/> / <paramref name="adminPassword"/>.
    /// Exceptions propagate so the caller can compensate or surface the failure.
    /// </summary>
    Task ProvisionAsync(Guid tenantId, string adminEmailAddress, string adminPassword);
}
