using HealthcareSupport.CaseEvaluation.Enums;
using Volo.Abp.Application.Dtos;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08-01 (2026-06-06) -- filter input for the Appointment Request Report,
/// mirroring the legacy report's quick search plus its advanced filters.
///
/// <para>Legacy parity note: OLD exposed a free-text quick search AND a separate
/// "Patient Name" advanced filter. NEW's <see cref="FilterText"/> already spans
/// panel number, confirmation number, patient first/last name, and booker name
/// (the W1-4 query behavior), so the quick search is a functional superset of
/// OLD's Patient Name box; the redundant dedicated field is intentionally not
/// reproduced. The four remaining advanced filters (type, location, status,
/// date range) are carried explicitly.</para>
/// </summary>
public class GetAppointmentReportInput : PagedAndSortedResultRequestDto
{
    /// <summary>Quick search: panel #, confirmation #, patient name, booker name.</summary>
    public string? FilterText { get; set; }

    public Guid? AppointmentTypeId { get; set; }

    public Guid? LocationId { get; set; }

    public AppointmentStatusType? AppointmentStatus { get; set; }

    public DateTime? AppointmentDateMin { get; set; }

    public DateTime? AppointmentDateMax { get; set; }
}
