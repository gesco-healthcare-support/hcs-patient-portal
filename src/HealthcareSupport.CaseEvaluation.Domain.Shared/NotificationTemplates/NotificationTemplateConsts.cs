namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// Constants for the per-tenant <c>NotificationTemplate</c> aggregate.
///
/// <para>The <see cref="Codes"/> nested class is the OLD-verified template-
/// code identifier set used across the system. Two parallel mechanisms
/// existed in OLD (correction logged 2026-05-03 -- the original Phase 1
/// seed listed 23 invented names that do NOT exist in OLD):</para>
///
/// <list type="bullet">
///   <item><b>16 DB-managed codes</b> from
///         <c>P:\PatientPortalOld\PatientAppointment.Models\Enums\TemplateCode.cs</c>
///         (lines 9-27). Stored in the <c>Templates</c> SQL table; IT Admin
///         editable via OLD's Template Management UI; carry Subject +
///         BodyEmail + BodySms.</item>
///   <item><b>43 disk-HTML codes</b> from
///         <c>P:\PatientPortalOld\PatientAppointment.DbEntities\Constants\ApplicationConstants.cs</c>
///         (lines 26-71). HTML files under
///         <c>wwwroot/EmailTemplates/</c>; loaded via
///         <c>ApplicationUtility.GetEmailTemplateFromHTML</c>; email-only;
///         NOT IT-Admin editable in OLD.</item>
/// </list>
///
/// <para>NEW unifies both into the single <c>NotificationTemplates</c> table
/// so all 59 events become IT-Admin editable. This is a strict-parity
/// exception (preserved BEHAVIOR, unified STORAGE) -- the bifurcation in
/// OLD was an accidental legacy artifact, not a designed feature. See the
/// audit doc <c>docs/parity/it-admin-notification-templates.md</c> for the
/// per-event mapping table and Phase 1-vs-deferred subset.</para>
///
/// <para>OLD typos fixed in NEW (verified 2026-05-03; not preserved):</para>
/// <list type="bullet">
///   <item><c>RejectedJoinDeclarationDocument</c> -> <c>RejectedJointDeclarationDocument</c>
///         (missing 't'; "Joint" used 348x elsewhere in OLD)</item>
///   <item><c>AppointmentApprovedStackholderEmails</c> -> <c>AppointmentApprovedStakeholderEmails</c>
///         (entire OLD codebase mis-spells "Stakeholder")</item>
///   <item><c>PatientAppointmentCancellationApprvd</c> + filename
///         <c>Apporved.html</c> -> <c>PatientAppointmentCancellationApproved</c>
///         (inconsistent abbreviation + filename typo)</item>
///   <item>HTML filename <c>User-Registed.html</c> -> body owned by NEW
///         seed; the typo was on the filename only, not the constant
///         <c>UserRegistered</c></item>
/// </list>
///
/// <para>OLD-style local abbreviations (<c>Req</c> in the four
/// <c>...RescheduleReq...</c> entries) are kept as-is when they form a
/// consistent local pattern -- those are stylistic, not typos.</para>
/// </summary>
public static class NotificationTemplateConsts
{
    public const int TemplateCodeMaxLength = 100;
    public const int SubjectMaxLength = 200;
    public const int DescriptionMaxLength = 200;

    /// <summary>
    /// All 59 OLD-verified notification template codes. Phase 1 wires
    /// <see cref="Phase1InScope"/> (33 codes) to handlers in subsequent
    /// per-feature phases; the remaining 26 are seeded but unwired until
    /// post-parity feature phases (Check-In/Out, NoShow, Billing,
    /// SubmitQuery, Internal-User-Mgmt, Audit-Log viewer).
    /// </summary>
    public static class Codes
    {
        // --------------------------------------------------------------
        // A. DB-managed in OLD (TemplateCode int enum, 16 codes).
        //    File: P:\PatientPortalOld\PatientAppointment.Models\Enums\TemplateCode.cs
        // --------------------------------------------------------------

        public const string AppointmentBooked = "AppointmentBooked";
        public const string AppointmentApproved = "AppointmentApproved";
        public const string AppointmentRejected = "AppointmentRejected";
        public const string AppointmentCancelledRequest = "AppointmentCancelledRequest";
        public const string AppointmentCancelledRequestApproved = "AppointmentCancelledRequestApproved";
        public const string AppointmentCancelledRequestRejected = "AppointmentCancelledRequestRejected";
        public const string AppointmentRescheduleRequest = "AppointmentRescheduleRequest";
        public const string AppointmentRescheduleRequestApproved = "AppointmentRescheduleRequestApproved";
        public const string AppointmentRescheduleRequestRejected = "AppointmentRescheduleRequestRejected";
        public const string RejectedPackageDocument = "RejectedPackageDocument";

        /// <summary>FIXED from OLD's <c>RejectedJoinDeclarationDocument</c> (missing 't').</summary>
        public const string RejectedJointDeclarationDocument = "RejectedJointDeclarationDocument";

        public const string AppointmentDueDate = "AppointmentDueDate";
        public const string AppointmentDueDateUploadDocumentLeft = "AppointmentDueDateUploadDocumentLeft";
        public const string SubmitQuery = "SubmitQuery";

        /// <summary>FIXED from OLD's <c>AppointmentApprovedStackholderEmails</c>.</summary>
        public const string AppointmentApprovedStakeholderEmails = "AppointmentApprovedStakeholderEmails";

        public const string AppointmentCancelledByAdmin = "AppointmentCancelledByAdmin";

        // --------------------------------------------------------------
        // B. On-disk HTML in OLD (EmailTemplate static class, 43 codes).
        //    File: P:\PatientPortalOld\PatientAppointment.DbEntities\Constants\ApplicationConstants.cs
        // --------------------------------------------------------------

        public const string AddInternalUser = "AddInternalUser";
        public const string PasswordChange = "PasswordChange";
        public const string ResetPassword = "ResetPassword";
        public const string UserRegistered = "UserRegistered";
        public const string UserQuery = "UserQuery";
        public const string AppointmentRescheduleRequestByAdmin = "AppointmentRescheduleRequestByAdmin";
        public const string AppointmentChangeLogs = "AppointmentChangeLogs";
        public const string PatientAppointmentPending = "PatientAppointmentPending";
        public const string PatientAppointmentApproveReject = "PatientAppointmentApproveReject";
        public const string PatientAppointmentApprovedInternal = "PatientAppointmentApprovedInternal";
        public const string PatientAppointmentApprovedExt = "PatientAppointmentApprovedExt";
        public const string PatientAppointmentRejected = "PatientAppointmentRejected";
        public const string PatientAppointmentCheckedIn = "PatientAppointmentCheckedIn";
        public const string PatientAppointmentCheckedOut = "PatientAppointmentCheckedOut";
        public const string PatientAppointmentNoShow = "PatientAppointmentNoShow";
        public const string PatientAppointmentCancelledNoBill = "PatientAppointmentCancelledNoBill";
        public const string ClinicalStaffCancellation = "ClinicalStaffCancellation";
        public const string AccessorAppointmentBooked = "AccessorAppointmentBooked";
        public const string PatientDocumentAccepted = "PatientDocumentAccepted";
        public const string PatientDocumentRejected = "PatientDocumentRejected";
        public const string PatientDocumentUploaded = "PatientDocumentUploaded";
        public const string PatientNewDocumentAccepted = "PatientNewDocumentAccepted";
        public const string PatientNewDocumentRejected = "PatientNewDocumentRejected";
        public const string PatientNewDocumentUploaded = "PatientNewDocumentUploaded";
        public const string PatientDocumentAcceptedAttachment = "PatientDocumentAcceptedAttachment";
        public const string PatientDocumentAcceptedRemainingDocs = "PatientDocumentAcceptedRemainingDocs";
        public const string PatientDocumentRejectedRemainingDocs = "PatientDocumentRejectedRemainingDocs";
        public const string AppointmentApproveRejectInternal = "AppointmentApproveRejectInternal";
        public const string UploadPendingDocuments = "UploadPendingDocuments";
        public const string AppointmentDueDateReminder = "AppointmentDueDateReminder";
        public const string AppointmentDocumentIncomplete = "AppointmentDocumentIncomplete";
        public const string AppointmentCancelledDueDate = "AppointmentCancelledDueDate";
        public const string AppointmentPendingNextDay = "AppointmentPendingNextDay";

        /// <summary>OLD-style local abbreviation (<c>Req</c>) preserved for naming consistency with the four <c>...RescheduleReq...</c> entries.</summary>
        public const string PatientAppointmentRescheduleReqAdmin = "PatientAppointmentRescheduleReqAdmin";
        public const string PatientAppointmentRescheduleReqApproved = "PatientAppointmentRescheduleReqApproved";
        public const string PatientAppointmentRescheduleReqRejected = "PatientAppointmentRescheduleReqRejected";

        /// <summary>FIXED from OLD's <c>PatientAppointmentCancellationApprvd</c> + filename <c>Apporved.html</c> (inconsistent abbreviation + filename typo).</summary>
        public const string PatientAppointmentCancellationApproved = "PatientAppointmentCancellationApproved";

        public const string PatientAppointmentRescheduleReq = "PatientAppointmentRescheduleReq";
        public const string JointAgreementLetterAccepted = "JointAgreementLetterAccepted";
        public const string JointAgreementLetterUploaded = "JointAgreementLetterUploaded";
        public const string JointAgreementLetterRejected = "JointAgreementLetterRejected";
        public const string AppointmentDocumentAddWithAttachment = "AppointmentDocumentAddWithAttachment";
        public const string PendingAppointmentDailyNotification = "PendingAppointmentDailyNotification";

        /// <summary>
        /// All 59 codes in seed order. Used by
        /// <c>NotificationTemplateDataSeedContributor</c> to ensure each
        /// tenant has a row per code at tenant-create time.
        /// </summary>
        public static readonly string[] All =
        {
            // 16 DB-managed (TemplateCode enum)
            AppointmentBooked, AppointmentApproved, AppointmentRejected,
            AppointmentCancelledRequest, AppointmentCancelledRequestApproved,
            AppointmentCancelledRequestRejected, AppointmentRescheduleRequest,
            AppointmentRescheduleRequestApproved, AppointmentRescheduleRequestRejected,
            RejectedPackageDocument, RejectedJointDeclarationDocument,
            AppointmentDueDate, AppointmentDueDateUploadDocumentLeft, SubmitQuery,
            AppointmentApprovedStakeholderEmails, AppointmentCancelledByAdmin,

            // 43 on-disk HTML (EmailTemplate static class)
            AddInternalUser, PasswordChange, ResetPassword, UserRegistered, UserQuery,
            AppointmentRescheduleRequestByAdmin, AppointmentChangeLogs,
            PatientAppointmentPending, PatientAppointmentApproveReject,
            PatientAppointmentApprovedInternal, PatientAppointmentApprovedExt,
            PatientAppointmentRejected, PatientAppointmentCheckedIn,
            PatientAppointmentCheckedOut, PatientAppointmentNoShow,
            PatientAppointmentCancelledNoBill, ClinicalStaffCancellation,
            AccessorAppointmentBooked, PatientDocumentAccepted, PatientDocumentRejected,
            PatientDocumentUploaded, PatientNewDocumentAccepted, PatientNewDocumentRejected,
            PatientNewDocumentUploaded, PatientDocumentAcceptedAttachment,
            PatientDocumentAcceptedRemainingDocs, PatientDocumentRejectedRemainingDocs,
            AppointmentApproveRejectInternal, UploadPendingDocuments,
            AppointmentDueDateReminder, AppointmentDocumentIncomplete,
            AppointmentCancelledDueDate, AppointmentPendingNextDay,
            PatientAppointmentRescheduleReqAdmin, PatientAppointmentRescheduleReqApproved,
            PatientAppointmentRescheduleReqRejected, PatientAppointmentCancellationApproved,
            PatientAppointmentRescheduleReq, JointAgreementLetterAccepted,
            JointAgreementLetterUploaded, JointAgreementLetterRejected,
            AppointmentDocumentAddWithAttachment, PendingAppointmentDailyNotification,
        };
    }
}
