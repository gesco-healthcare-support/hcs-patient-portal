using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08-01 / G-08-03 (2026-06-06) -- the read-only Appointment Request Report for
/// internal staff, plus its PDF export. Gated by <c>CaseEvaluation.Reports</c>
/// (export by <c>Reports.Export</c>); every row is PHI-redacted server-side (SSN
/// masked to last 4, date of birth to the birth year) before it leaves this
/// service -- the same redaction feeds the grid and the PDF.
/// </summary>
public interface IReportsAppService : IApplicationService
{
    /// <summary>Paged, filtered, sorted appointment-request rows (the report grid).</summary>
    Task<PagedResultDto<AppointmentReportRowDto>> GetListAsync(GetAppointmentReportInput input);

    /// <summary>
    /// The full filtered result set (no paging) rendered as a PDF, for the
    /// "Export to PDF" action. Same filters/guards as the grid; reuses the row
    /// redaction so masking cannot diverge.
    /// </summary>
    Task<DownloadResult> GetReportPdfAsync(GetAppointmentReportInput input);

    /// <summary>
    /// Per-status counts for the report's filter set, with the status filter itself
    /// ignored so the summary cards span every status. Raw <see cref="AppointmentStatusType"/>
    /// counts -- the Angular report buckets them into its pills via the shared
    /// appointmentStatusToPill, keeping cards + grid in lockstep.
    /// </summary>
    Task<List<AppointmentStatusCountDto>> GetStatusCountsAsync(GetAppointmentReportInput input);

    /// <summary>
    /// The full filtered result set (no paging) rendered as a CSV, for the
    /// "Export to CSV" action. Same filters/guards/redaction as the PDF.
    /// </summary>
    Task<DownloadResult> GetReportCsvAsync(GetAppointmentReportInput input);
}
