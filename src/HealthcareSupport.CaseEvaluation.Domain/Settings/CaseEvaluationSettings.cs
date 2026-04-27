namespace HealthcareSupport.CaseEvaluation.Settings;

/// <summary>
/// Setting name constants for the CaseEvaluation policy surface. Replaces OLD's
/// `SystemParameter` entity (per Q8 lock 2026-04-24); each setting maps to a row in
/// `AbpSettings` resolved per-user / per-tenant / per-host via ABP's
/// <c>ISettingProvider</c> chain.
///
/// Reference values come from OLD's `spm.SystemParameters` PROD row when supplied;
/// placeholder defaults are documented inline. Legal-staff sign-off on the legally-
/// driven values (CCR Title 8 Sections 31.5/33/34) is queued and may adjust the
/// defaults later.
/// </summary>
public static class CaseEvaluationSettings
{
    private const string Prefix = "CaseEvaluation";
    private const string Booking = Prefix + ".Booking";
    private const string Scheduling = Prefix + ".Scheduling";
    private const string Documents = Prefix + ".Documents";
    private const string Notifications = Prefix + ".Notifications";

    public static class BookingPolicy
    {
        // Minimum minutes between request-submit and proposed appointment time.
        // Default 1440 minutes (1 day).
        public const string LeadTimeMinutes = Booking + ".LeadTimeMinutes";

        // Maximum minutes from now an appointment of a given type may be scheduled.
        // Defaults: 90 days (129,600 minutes) for QME / AME / Other.
        public const string MaxHorizonQmeMinutes   = Booking + ".MaxHorizonQmeMinutes";
        public const string MaxHorizonAmeMinutes   = Booking + ".MaxHorizonAmeMinutes";
        public const string MaxHorizonOtherMinutes = Booking + ".MaxHorizonOtherMinutes";

        // Default appointment duration. Default 60 minutes.
        public const string AppointmentDurationMinutes = Booking + ".AppointmentDurationMinutes";

        // Days a Pending request may sit before the office is nudged. Default 7 days.
        public const string AppointmentDueDays = Booking + ".AppointmentDueDays";
    }

    public static class SchedulingPolicy
    {
        // Minutes before a slot during which a party-initiated cancel still counts as
        // "on time" (vs late). Default 2880 minutes (2 days).
        public const string CancelWindowMinutes = Scheduling + ".CancelWindowMinutes";

        // Auto-cancel cutoff: minutes past which a Pending request auto-rejects.
        // Default 1440 minutes (1 day).
        public const string AutoCancelCutoffMinutes = Scheduling + ".AutoCancelCutoffMinutes";

        // Cutoff for the day-before reminder job. Default 1440 minutes (1 day).
        public const string ReminderCutoffMinutes = Scheduling + ".ReminderCutoffMinutes";

        // Days a Pending request triggers an "overdue" notification. Default 3 days.
        public const string PendingAppointmentOverdueNotificationDays = Scheduling + ".PendingAppointmentOverdueNotificationDays";
    }

    public static class DocumentsPolicy
    {
        // Days before the appointment by which joint declarations must be uploaded.
        // Default 7 days.
        public const string JointDeclarationUploadCutoffDays = Documents + ".JointDeclarationUploadCutoffDays";
    }

    public static class NotificationsPolicy
    {
        // CSV string of email addresses CC'd on every all-parties notification email.
        // Default empty (no CC); admins set per-tenant.
        public const string CcEmailAddresses = Notifications + ".CcEmailAddresses";
    }
}
