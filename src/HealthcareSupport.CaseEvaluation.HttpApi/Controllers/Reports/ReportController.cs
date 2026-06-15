using Asp.Versioning;
using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
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

    // Interface member, hidden from routing: the PDF is exposed as a file stream
    // by ExportPdfAsync below, mirroring the packet-download split.
    [NonAction]
    public virtual Task<DownloadResult> GetReportPdfAsync(GetAppointmentReportInput input)
    {
        return _reportsAppService.GetReportPdfAsync(input);
    }

    [HttpGet("export-pdf")]
    public virtual async Task<IActionResult> ExportPdfAsync([FromQuery] GetAppointmentReportInput input)
    {
        var result = await _reportsAppService.GetReportPdfAsync(input);
        return File(result.Content, result.ContentType, result.FileName);
    }

    [HttpGet("status-counts")]
    public virtual Task<List<AppointmentStatusCountDto>> GetStatusCountsAsync(GetAppointmentReportInput input)
    {
        return _reportsAppService.GetStatusCountsAsync(input);
    }

    // Interface member, hidden from routing: the CSV is exposed as a file stream
    // by ExportCsvAsync below, mirroring the PDF split.
    [NonAction]
    public virtual Task<DownloadResult> GetReportCsvAsync(GetAppointmentReportInput input)
    {
        return _reportsAppService.GetReportCsvAsync(input);
    }

    [HttpGet("export-csv")]
    public virtual async Task<IActionResult> ExportCsvAsync([FromQuery] GetAppointmentReportInput input)
    {
        var result = await _reportsAppService.GetReportCsvAsync(input);
        return File(result.Content, result.ContentType, result.FileName);
    }
}
