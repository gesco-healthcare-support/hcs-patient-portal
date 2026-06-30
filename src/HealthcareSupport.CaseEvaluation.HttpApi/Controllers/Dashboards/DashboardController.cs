using System.Collections.Generic;
using System.Threading.Tasks;
using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.Dashboards;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Dashboards;

[RemoteService(Name = "Default")]
[Area("app")]
[ControllerName("Dashboard")]
[Route("api/app/dashboard")]
[ApiVersion("1.0")]
public class DashboardController : AbpController, IDashboardAppService
{
    private readonly IDashboardAppService _appService;

    public DashboardController(IDashboardAppService appService)
    {
        _appService = appService;
    }

    [HttpGet]
    public Task<DashboardCountersDto> GetAsync()
    {
        return _appService.GetAsync();
    }

    [HttpGet("overview")]
    public Task<DashboardDto> GetDashboardAsync([FromQuery] DashboardRange range)
    {
        return _appService.GetDashboardAsync(range);
    }

    [HttpGet("tenant-summaries")]
    public Task<List<TenantSummaryDto>> GetTenantSummariesAsync()
    {
        return _appService.GetTenantSummariesAsync();
    }

    // 2026-06-30 (QA item B): paged Offices/Tenants list (edition + activation +
    // per-office counts in one call) for the reusable host Offices table.
    [HttpGet("offices")]
    public Task<PagedResultDto<OfficeListDto>> GetOfficesAsync([FromQuery] GetOfficesInput input)
    {
        return _appService.GetOfficesAsync(input);
    }

    // 2026-06-30 (QA item B): paged per-office breakdown for the host dashboard
    // table, independent of the monolithic overview payload.
    [HttpGet("tenant-breakdown")]
    public Task<PagedResultDto<DashboardTenantRowDto>> GetTenantBreakdownAsync(
        [FromQuery] GetTenantBreakdownInput input)
    {
        return _appService.GetTenantBreakdownAsync(input);
    }
}
