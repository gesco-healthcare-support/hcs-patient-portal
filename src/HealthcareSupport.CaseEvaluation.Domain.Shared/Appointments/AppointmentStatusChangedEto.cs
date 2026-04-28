using System;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Local event published by <c>AppointmentManager</c> after a successful
/// status transition. Subscribers fan out side effects: slot cascade flips
/// <c>DoctorAvailability.BookingStatusId</c> per the T11 sync table; email
/// handler renders the matching template; future change-log audit handler
/// persists the transition row.
///
/// Lives in <c>Domain.Shared</c> so subscribers across projects can reference
/// it without creating a layering violation.
/// </summary>
public class AppointmentStatusChangedEto
{
    public Guid AppointmentId { get; set; }

    public Guid? TenantId { get; set; }

    public AppointmentStatusType FromStatus { get; set; }

    public AppointmentStatusType ToStatus { get; set; }

    public Guid? ActingUserId { get; set; }

    public string? Reason { get; set; }

    public DateTime OccurredAt { get; set; }

    public AppointmentStatusChangedEto()
    {
    }

    public AppointmentStatusChangedEto(
        Guid appointmentId,
        Guid? tenantId,
        AppointmentStatusType fromStatus,
        AppointmentStatusType toStatus,
        Guid? actingUserId,
        string? reason,
        DateTime occurredAt)
    {
        AppointmentId = appointmentId;
        TenantId = tenantId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActingUserId = actingUserId;
        Reason = reason;
        OccurredAt = occurredAt;
    }
}
