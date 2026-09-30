using System;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

/// <summary>
/// 2026-06-16 (Prompt 16, A-B4) -- one row of the host Tenants-management table:
/// the tenant's id + name plus its live user and appointment counts. Subdomain,
/// edition, and activation state come from the stock Volo SaaS TenantService on
/// the client; this endpoint supplies only the per-tenant aggregates Volo SaaS
/// does not expose. Aggregate counts of PHI rows are not PHI under HIPAA Safe
/// Harbor.
/// </summary>
public class TenantSummaryDto
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int UserCount { get; set; }
    public int AppointmentCount { get; set; }
}
