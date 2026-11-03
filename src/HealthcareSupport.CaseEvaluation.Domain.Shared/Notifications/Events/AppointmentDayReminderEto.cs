using System;

namespace HealthcareSupport.CaseEvaluation.Notifications.Events;

/// <summary>
/// 2026-11-03 (email routing, row 15) -- raised by <c>AppointmentDayReminderJob</c> for each Approved
/// appointment whose date falls on a reminder anchor (default 7 and 1 days ahead). Subscriber
/// <c>AppointmentDayReminderEmailHandler</c> sends the reminder to the patient, the office inbox and the
/// Intake Staff through the notification dispatcher and the editable <c>AppointmentDayReminder</c>
/// template.
/// </summary>
public class AppointmentDayReminderEto
{
    public Guid AppointmentId { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>Days from today (Pacific) to the appointment date: 7 or 1 by default.</summary>
    public int DaysUntil { get; set; }

    public DateTime OccurredAt { get; set; }
}
