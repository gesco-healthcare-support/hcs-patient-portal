using System;
using HealthcareSupport.CaseEvaluation.Enums;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Local event published by <c>AppointmentManager</c> after a successful
/// status transition AND by <c>AppointmentsAppService</c> on initial create
/// + delete. Subscribers fan out side effects: slot cascade flips
/// <c>DoctorAvailability.BookingStatusId</c> per the T11 sync table; email
/// handler renders the matching template; future change-log audit handler
/// persists the transition row.
///
/// W2-3 expanded the type to model the full lifecycle:
///  - <see cref="FromStatus"/> is nullable so an initial-create publish
///    (no prior status) can fire the same handler chain.
///  - <see cref="ToStatus"/> is nullable so a hard-delete publish (no
///    successor status) can free the slot through the cascade.
///  - <see cref="DoctorAvailabilityId"/> snapshots the appointment's current
///    slot so handlers don't have to re-fetch the (possibly already-deleted)
///    appointment to find which slot to flip.
///  - <see cref="OldDoctorAvailabilityId"/> captures the prior slot when the
///    appointment's slot pointer changes during a reschedule. The cascade
///    handler swaps both slots when this is set; null (the common case) means
///    "no swap, just flip the current slot."
///
/// Lives in <c>Domain.Shared</c> so subscribers across projects can reference
/// it without creating a layering violation.
/// </summary>
public class AppointmentStatusChangedEto
{
    public Guid AppointmentId { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>Prior status. Null when the event represents an initial create.</summary>
    public AppointmentStatusType? FromStatus { get; set; }

    /// <summary>New status. Null when the event represents a hard delete.</summary>
    public AppointmentStatusType? ToStatus { get; set; }

    /// <summary>
    /// Slot ID the appointment is currently pointed at. Snapshot at publish time
    /// so subscribers never need to re-fetch the appointment.
    /// </summary>
    public Guid? DoctorAvailabilityId { get; set; }

    /// <summary>
    /// Prior slot ID when the appointment's slot pointer changed in this
    /// transition (reschedule). Null when the slot was unchanged.
    /// </summary>
    public Guid? OldDoctorAvailabilityId { get; set; }

    public Guid? ActingUserId { get; set; }

    public string? Reason { get; set; }

    public DateTime OccurredAt { get; set; }

    public AppointmentStatusChangedEto()
    {
    }

    public AppointmentStatusChangedEto(
        Guid appointmentId,
        Guid? tenantId,
        AppointmentStatusType? fromStatus,
        AppointmentStatusType? toStatus,
        Guid? actingUserId,
        string? reason,
        DateTime occurredAt,
        Guid? doctorAvailabilityId = null,
        Guid? oldDoctorAvailabilityId = null)
    {
        AppointmentId = appointmentId;
        TenantId = tenantId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        ActingUserId = actingUserId;
        Reason = reason;
        OccurredAt = occurredAt;
        DoctorAvailabilityId = doctorAvailabilityId;
        OldDoctorAvailabilityId = oldDoctorAvailabilityId;
    }
}
