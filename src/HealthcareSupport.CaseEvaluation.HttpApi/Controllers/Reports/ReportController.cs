using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.Reports;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers.Reports;

/// <summary>
/// G-08-01 (2026-06-06) -- manual controller for the Appointment Request Report.
/// Thin delegation to <see cref="IReportsAppService"/>; the AppService carries
/// <c>[RemoteService(IsEnabled = false)]</c> so this controller owns the route.
/// </summary>
[RemoteService]
[Area("app")]
[ControllerName("Report")]
[Route("api/app/reports")]
public class ReportController : AbpController, IReportsAppService
{
    private readonly IReportsAppService _reportsAppService;

    public ReportController(IReportsAppService reportsAppService)
    {
        _reportsAppService = reportsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<AppointmentReportRowDto>> GetListAsync(GetAppointmentReportInput input)
    {
        return _reportsAppService.GetListAsync(input);
    }
}
