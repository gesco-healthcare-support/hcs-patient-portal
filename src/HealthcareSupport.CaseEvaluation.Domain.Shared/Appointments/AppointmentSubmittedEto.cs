using System;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Local event published by <c>AppointmentsAppService.CreateAsync</c> after a
/// new appointment row is persisted. Distinct from
/// <see cref="AppointmentStatusChangedEto"/> -- that event covers transitions,
/// this one covers initial creation. Subscribers fan out the office-side
/// "new request" email and the booker's "request received" confirmation.
///
/// Lives in <c>Domain.Shared</c> for the same reason as the status-changed
/// ETO -- subscribers across projects can reference it without a layering
/// violation.
/// </summary>
public class AppointmentSubmittedEto
{
    public Guid AppointmentId { get; set; }

    public Guid? TenantId { get; set; }

    public Guid BookerUserId { get; set; }

    public Guid PatientId { get; set; }

    public string RequestConfirmationNumber { get; set; } = string.Empty;

    public DateTime AppointmentDate { get; set; }

    public DateTime SubmittedAt { get; set; }

    public AppointmentSubmittedEto()
    {
    }

    public AppointmentSubmittedEto(
        Guid appointmentId,
        Guid? tenantId,
        Guid bookerUserId,
        Guid patientId,
        string requestConfirmationNumber,
        DateTime appointmentDate,
        DateTime submittedAt)
    {
        AppointmentId = appointmentId;
        TenantId = tenantId;
        BookerUserId = bookerUserId;
        PatientId = patientId;
        RequestConfirmationNumber = requestConfirmationNumber;
        AppointmentDate = appointmentDate;
        SubmittedAt = submittedAt;
    }
}
