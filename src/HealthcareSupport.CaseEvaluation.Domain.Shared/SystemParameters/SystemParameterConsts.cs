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
    public const int DefaultAppointmentMaxTimePQME = 60;
    public const int DefaultAppointmentMaxTimeAME = 90;
    public const int DefaultAppointmentMaxTimeOTHER = 60;
    public const int DefaultAppointmentCancelTime = 2;
    public const int DefaultAppointmentDueDays = 14;
    public const int DefaultAppointmentDurationTime = 60;
    public const int DefaultAutoCancelCutoffTime = 7;
    public const int DefaultJointDeclarationUploadCutoffDays = 7;
    public const int DefaultPendingAppointmentOverDueNotificationDays = 3;
    public const int DefaultReminderCutoffTime = 7;
    public const bool DefaultIsCustomField = false;
}
