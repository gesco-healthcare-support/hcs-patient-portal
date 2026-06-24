using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Dashboards;

public interface IDashboardAppService : IApplicationService
{
    Task<DashboardCountersDto> GetAsync();

    /// <summary>Rich composite payload for the redesigned internal dashboard.</summary>
    Task<DashboardDto> GetDashboardAsync(DashboardRange range);

    /// <summary>
    /// 2026-06-16 (A-B4) -- host-only per-tenant user + appointment counts for
    /// the Tenants management table. Gated by <c>Saas.Tenants</c>.
    /// </summary>
    Task<List<TenantSummaryDto>> GetTenantSummariesAsync();
}
