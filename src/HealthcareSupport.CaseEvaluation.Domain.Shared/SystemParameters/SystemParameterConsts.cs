namespace HealthcareSupport.CaseEvaluation.SystemParameters;

/// <summary>
/// Constants for the per-tenant <c>SystemParameter</c> singleton. Default
/// values mirror OLD's seed (Phase 1.1, 2026-05-01) so booking, cancel,
/// reschedule, and reminder gates fire with parity-correct windows out of
/// the box. IT Admin can override per tenant via the system-parameters page.
/// </summary>
public static class SystemParameterConsts
{
    public const int CcEmailIdsMaxLength = 500;

    public const int DefaultAppointmentLeadTime = 3;
    // AF1 (2026-06-03) made every per-type horizon 60 days. On 2026-11-08 AME went back to 90 at
    // the product owner's request: AME may be booked up to 90 days ahead, PQME and OTHER stay at
    // 60. 90 is also the internal-staff ceiling, so the AME default now equals the ceiling. The
    // minimum lead time (3 days) is unchanged. These are only the defaults for a NEW office's
    // seeded row; an existing office keeps its stored value and edits it in System Parameters.
    public const int DefaultAppointmentMaxTimePQME = 60;
    public const int DefaultAppointmentMaxTimeAME = 90;
    public const int DefaultAppointmentMaxTimeOTHER = 60;
    // 2026-06-11: internal staff may book further out than external users. External users are
    // capped by the per-type AppointmentMaxTime{PQME,AME,OTHER} horizon (60 for PQME and OTHER, 90 for
    // AME since 2026-11-08); internal staff are
    // capped by this single horizon (90). 90 is the absolute ceiling -- nobody books beyond it.
    public const int DefaultAppointmentMaxTimeInternal = 90;
    public const int DefaultAppointmentCancelTime = 2;
    public const int DefaultAppointmentDueDays = 14;
    public const int DefaultAppointmentDurationTime = 60;
    public const int DefaultAutoCancelCutoffTime = 7;
    public const int DefaultJointDeclarationUploadCutoffDays = 7;
    public const int DefaultPendingAppointmentOverDueNotificationDays = 3;
    public const int DefaultReminderCutoffTime = 7;
    public const bool DefaultIsCustomField = false;
}
