using HealthcareSupport.CaseEvaluation.Enums;
using HealthcareSupport.CaseEvaluation.Patients;

namespace HealthcareSupport.CaseEvaluation.Reports;

/// <summary>
/// G-08-01 (2026-06-06) -- the raw, pre-redaction projection of one report row,
/// assembled by ReportsAppService from the appointment + patient nav graph. It
/// carries the RAW SSN and full DateOfBirth and never leaves the Application
/// layer: ReportRowRedactor turns it into the masked, wire-safe
/// <see cref="AppointmentReportRowDto"/>.
/// </summary>
internal sealed class ReportRowSource
{
    public Guid AppointmentId { get; init; }
    public string RequestConfirmationNumber { get; init; } = null!;
    public string? AppointmentTypeName { get; init; }
    public string? LocationName { get; init; }
    public DateTime AppointmentDate { get; init; }
    public AppointmentStatusType AppointmentStatus { get; init; }
    public string? PatientName { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string? SocialSecurityNumber { get; init; }
}

/// <summary>
/// G-08-01 (2026-06-06) -- the single PHI-redaction seam shared by the report
/// grid and (later) its PDF export, so masking can never diverge between the
/// two surfaces. SSN -> last 4 (<see cref="SsnVisibility"/>); DateOfBirth ->
/// birth year (<see cref="DobVisibility"/>); name / email / phone pass through
/// in full because the internal worklist needs them to identify and contact
/// (Adrian's HIPAA call 2026-06-06). Pure (no DI); unit-tested via
/// InternalsVisibleTo.
/// </summary>
internal static class ReportRowRedactor
{
    internal static AppointmentReportRowDto ToMaskedDto(ReportRowSource source)
    {
        return new AppointmentReportRowDto
        {
            AppointmentId = source.AppointmentId,
            RequestConfirmationNumber = source.RequestConfirmationNumber,
            AppointmentTypeName = source.AppointmentTypeName,
            LocationName = source.LocationName,
            AppointmentDate = source.AppointmentDate,
            AppointmentStatus = source.AppointmentStatus,
            PatientName = source.PatientName,
            DateOfBirth = DobVisibility.ToYearOnly(source.DateOfBirth),
            Email = source.Email,
            PhoneNumber = source.PhoneNumber,
            SocialSecurityNumber = SsnVisibility.MaskToLast4(source.SocialSecurityNumber),
        };
    }
}
