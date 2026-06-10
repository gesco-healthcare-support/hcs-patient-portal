using HealthcareSupport.CaseEvaluation.AppointmentDocuments;
using HealthcareSupport.CaseEvaluation.Appointments;
using HealthcareSupport.CaseEvaluation.Patients;
using HealthcareSupport.CaseEvaluation.Permissions;
using HealthcareSupport.CaseEvaluation.Reports.Pdf;
using Microsoft.AspNetCore.Authorization;
using QuestPDF.Fluent;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08-01 (2026-06-06) -- the Appointment Request Report: a cross-appointment,
/// PHI-masked operational worklist for internal staff. Reuses the shared
/// appointment query (filters + paging), applies the legacy report's guards
/// (at least one filter; a both-or-neither date range) and its default sort
/// (confirmation number descending), and redacts every row (SSN to last 4,
/// date of birth to birth year) before it leaves the service.
///
/// <para>Internal-only via the Reports permission, which is granted solely to
/// IT Admin / Staff Supervisor / Intake Staff. Internal callers see every
/// appointment in their tenant (no visibility narrowing), matching the legacy
/// report's audience. The full SSN is never emitted here -- only the masked
/// last 4; a full reveal still routes through the audited
/// <c>Patients.RevealSsn</c> endpoint.</para>
/// </summary>
[RemoteService(IsEnabled = false)]
[Authorize(CaseEvaluationPermissions.Reports.Default)]
public class ReportsAppService : CaseEvaluationAppService, IReportsAppService
{
    private readonly IAppointmentRepository _appointmentRepository;

    public ReportsAppService(IAppointmentRepository appointmentRepository)
    {
        _appointmentRepository = appointmentRepository;
    }

    public virtual async Task<PagedResultDto<AppointmentReportRowDto>> GetListAsync(GetAppointmentReportInput input)
    {
        if (!ReportFilterValidator.HasAnyFilter(input))
        {
            throw new UserFriendlyException(L["Report:EnterAtLeastOneFilter"]);
        }

        if (!ReportFilterValidator.IsDateRangeValid(input.AppointmentDateMin, input.AppointmentDateMax))
        {
            throw new UserFriendlyException(L["Report:InvalidDateRange"]);
        }

        var sorting = ReportFilterValidator.ResolveSorting(input.Sorting);

        var totalCount = await _appointmentRepository.GetCountAsync(
            filterText: input.FilterText,
            appointmentDateMin: input.AppointmentDateMin,
            appointmentDateMax: input.AppointmentDateMax,
            appointmentTypeId: input.AppointmentTypeId,
            locationId: input.LocationId,
            appointmentStatus: input.AppointmentStatus);

        var items = await _appointmentRepository.GetListWithNavigationPropertiesAsync(
            filterText: input.FilterText,
            appointmentDateMin: input.AppointmentDateMin,
            appointmentDateMax: input.AppointmentDateMax,
            appointmentTypeId: input.AppointmentTypeId,
            locationId: input.LocationId,
            appointmentStatus: input.AppointmentStatus,
            sorting: sorting,
            maxResultCount: input.MaxResultCount,
            skipCount: input.SkipCount);

        var rows = items.Select(ToMaskedRow).ToList();

        return new PagedResultDto<AppointmentReportRowDto>
        {
            TotalCount = totalCount,
            Items = rows,
        };
    }

    [Authorize(CaseEvaluationPermissions.Reports.Export)]
    public virtual async Task<DownloadResult> GetReportPdfAsync(GetAppointmentReportInput input)
    {
        if (!ReportFilterValidator.HasAnyFilter(input))
        {
            throw new UserFriendlyException(L["Report:EnterAtLeastOneFilter"]);
        }

        if (!ReportFilterValidator.IsDateRangeValid(input.AppointmentDateMin, input.AppointmentDateMax))
        {
            throw new UserFriendlyException(L["Report:InvalidDateRange"]);
        }

        var sorting = ReportFilterValidator.ResolveSorting(input.Sorting);

        // Export the FULL filtered set (no paging), unlike the paged grid.
        var items = await _appointmentRepository.GetListWithNavigationPropertiesAsync(
            filterText: input.FilterText,
            appointmentDateMin: input.AppointmentDateMin,
            appointmentDateMax: input.AppointmentDateMax,
            appointmentTypeId: input.AppointmentTypeId,
            locationId: input.LocationId,
            appointmentStatus: input.AppointmentStatus,
            sorting: sorting);

        var rows = items.Select(ToMaskedRow).ToList();
        var bytes = new AppointmentReportPdfDocument(rows).GeneratePdf();

        return new DownloadResult
        {
            Content = new MemoryStream(bytes),
            ContentType = "application/pdf",
            FileName = "appointment-request-report.pdf",
        };
    }

    private static AppointmentReportRowDto ToMaskedRow(AppointmentWithNavigationProperties nav)
    {
        var patient = nav.Patient;
        var source = new ReportRowSource
        {
            AppointmentId = nav.Appointment.Id,
            RequestConfirmationNumber = nav.Appointment.RequestConfirmationNumber,
            AppointmentTypeName = nav.AppointmentType?.Name,
            LocationName = nav.Location?.Name,
            AppointmentDate = nav.Appointment.AppointmentDate,
            AppointmentStatus = nav.Appointment.AppointmentStatus,
            PatientName = ComposePatientName(patient),
            DateOfBirth = patient?.DateOfBirth,
            Email = patient?.Email,
            PhoneNumber = patient?.PhoneNumber,
            SocialSecurityNumber = patient?.SocialSecurityNumber,
        };

        return ReportRowRedactor.ToMaskedDto(source);
    }

    private static string? ComposePatientName(Patient? patient)
    {
        if (patient is null)
        {
            return null;
        }

        // Legacy report displays the patient as "Last, First Middle".
        var name = $"{patient.LastName}, {patient.FirstName}";
        return string.IsNullOrWhiteSpace(patient.MiddleName) ? name : $"{name} {patient.MiddleName}";
    }
}
