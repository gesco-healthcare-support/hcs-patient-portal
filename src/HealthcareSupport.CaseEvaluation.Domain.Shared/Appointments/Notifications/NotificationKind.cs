namespace HealthcareSupport.CaseEvaluation.Appointments.Notifications;

/// <summary>
/// W2-10: per-event semantics tag passed to the recipient resolver so it
/// can emit different recipient sets per event (e.g. <c>OfficeAdmin</c>
/// receives Submitted notifications; <c>Patient</c> doesn't unless they
/// are also the booker). Also drives subject/body template selection.
/// </summary>
public enum NotificationKind
{
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    RequestSchedulingReminder = 5,
    CancellationRescheduleReminder = 6,
    AppointmentDayReminder = 7,

    // Phase 14b (2026-05-04) -- document-flow recipient routing.
    DocumentUploaded = 8,
    DocumentAccepted = 9,
    DocumentRejected = 10,
    JdfAutoCancelled = 11,
    PackageDocumentReminder = 12,

    // Category 7 (2026-05-10) -- OLD SchedulerDomain reminder fan-outs.
    // #1 OLD :72 -- daily digest of all pending requests to the per-tenant clinic-staff inbox.
    PendingDailyDigest = 13,
    // #2 OLD :87 -- per-internal-staff queue counts (PendingCount + ApprovedCount).
    InternalStaffQueueDigest = 14,
    // #4 OLD :152 -- per-stakeholder due-date approaching reminder (14 / 7 / 3 days).
    DueDateApproachingReminder = 15,
    // #5 OLD :176 -- per-stakeholder documents-incomplete-and-due-date-approaching reminder.
    DueDateDocumentIncompleteReminder = 16,

    // Category 4 (2026-05-10) -- per-recipient packet email fan-out
    // (AppointmentDocumentAddWithAttachment). Used by AttyCE packet
    // handler so recipient resolution can be tagged distinctly from the
    // status-change Approved email.
    PacketAttyCEDelivery = 17,
}
