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

        // Single email address that receives "new appointment request" notifications
        // for the tenant. Used by W1-1f-A-cleanup SubmissionEmailHandler. Default empty
        // (no email sent if unset). Tenant-scoped -- each practice sets their own
        // reception inbox in /setting-management.
        public const string OfficeEmail = Notifications + ".OfficeEmail";

        // Public base URL of the Angular portal, used to build deep-links in
        // outgoing emails (e.g. SendBack notifications referencing
        // /appointments/view/:id). Default http://localhost:4200 for dev; admins
        // override per-tenant in /setting-management for prod / staging URLs.
        public const string PortalBaseUrl = Notifications + ".PortalBaseUrl";
    }

    /// <summary>
    /// W2-10 -- CCR Title 8 Sec. 31.5 / 34(e) reminder windows + appointment-day
    /// reminder T-N day windows. Defaults match CCR text; tenant admins can
    /// shorten / lengthen via /setting-management. Comma-separated integers
    /// stored as a single setting value (parsed by the recurring job at run-time).
    /// </summary>
    public static class RemindersPolicy
    {
        public const string Sec31_5ElapsedDayAnchors = Notifications + ".Reminders.Sec31_5ElapsedDayAnchors";
        public const string Sec34eElapsedDayAnchors = Notifications + ".Reminders.Sec34eElapsedDayAnchors";
        public const string AppointmentDayTMinusAnchors = Notifications + ".Reminders.AppointmentDayTMinusAnchors";
        public const string Sec31_5Cron = Notifications + ".Reminders.Sec31_5Cron";
        public const string Sec34eCron = Notifications + ".Reminders.Sec34eCron";
        public const string AppointmentDayCron = Notifications + ".Reminders.AppointmentDayCron";
        public const string ReminderTimezoneId = Notifications + ".Reminders.TimezoneId";
        public const string RemindersEnabled = Notifications + ".Reminders.Enabled";
        public const string ReminderCcEmail = Notifications + ".Reminders.CcEmail";
        public const string ReminderSignoff = Notifications + ".Reminders.Signoff";
    }
}
