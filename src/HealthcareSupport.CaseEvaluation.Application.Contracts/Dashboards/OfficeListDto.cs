using System;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// 2026-06-30 (QA item B) -- one office row for the host Offices/Tenants table.
/// Consolidates tenant identity + edition + activation (formerly from the Volo
/// SaaS list) with the per-office user/appointment counts (formerly a separate
/// <c>getTenantSummaries</c> call), so the client renders a page with one request
/// instead of a forkJoin.
/// </summary>
public class OfficeListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Lowercased office name -- the subdomain label (e.g. "falkinstein").</summary>
    public string Subdomain { get; set; } = string.Empty;
    public Guid? EditionId { get; set; }
    public string EditionName { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public int AppointmentCount { get; set; }
    /// <summary>False only when the tenant's activation state is Passive.</summary>
    public bool IsActive { get; set; }
    /// <summary>Tenant concurrency stamp, so the edit modal can update without a refetch.</summary>
    public string? ConcurrencyStamp { get; set; }
}
