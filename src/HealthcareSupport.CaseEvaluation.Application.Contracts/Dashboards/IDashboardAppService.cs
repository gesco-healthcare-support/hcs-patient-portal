using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
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

    /// <summary>
    /// 2026-06-30 (QA item B) -- paged Offices/Tenants list with edition,
    /// activation, and per-office user/appointment counts in one host-scoped call
    /// (replaces the client forkJoin of the Volo SaaS list + getTenantSummaries).
    /// Counts are computed only for the returned page. Gated by <c>Saas.Tenants</c>.
    /// </summary>
    Task<PagedResultDto<OfficeListDto>> GetOfficesAsync(GetOfficesInput input);

    /// <summary>
    /// 2026-06-30 (QA item B) -- paged per-office breakdown for the host dashboard
    /// table (appointments / pending / approved / this-week), so it pages
    /// independently of the monolithic <see cref="GetDashboardAsync"/> payload.
    /// Host-only; gated by <c>Dashboard.Host</c>.
    /// </summary>
    Task<PagedResultDto<DashboardTenantRowDto>> GetTenantBreakdownAsync(GetTenantBreakdownInput input);
}
