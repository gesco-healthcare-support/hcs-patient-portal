using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08-01 (2026-06-06) -- one row of the Appointment Request Report grid,
/// mirroring the legacy report's ten columns. PHI is masked at the Application
/// boundary BEFORE this DTO is built (see ReportRowRedactor): SSN shows the
/// last 4 only and DateOfBirth is the birth year only. Name / Email /
/// PhoneNumber are shown in full for the internal worklist (Adrian's HIPAA
/// call 2026-06-06). The full SSN is never carried here -- it is available
/// only via the audited Patients.RevealSsn reveal endpoint.
/// </summary>
public class AppointmentReportRowDto
{
    /// <summary>Target of the "Confirmation No" link (-> /appointments/{id}).</summary>
    public Guid AppointmentId { get; set; }

    public string RequestConfirmationNumber { get; set; } = null!;

    public string? AppointmentTypeName { get; set; }

    public string? LocationName { get; set; }

    public DateTime AppointmentDate { get; set; }

    public AppointmentStatusType AppointmentStatus { get; set; }

    public string? PatientName { get; set; }

    /// <summary>Birth year only (e.g. "1985"); never the full date of birth.</summary>
    public string? DateOfBirth { get; set; }

    public string? Email { get; set; }

    public string? PhoneNumber { get; set; }

    /// <summary>Masked to the last 4 digits (e.g. "***-**-1234").</summary>
    public string? SocialSecurityNumber { get; set; }
}
