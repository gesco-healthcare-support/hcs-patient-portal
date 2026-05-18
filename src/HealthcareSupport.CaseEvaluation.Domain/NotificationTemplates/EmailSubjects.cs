using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.NotificationTemplates;

/// <summary>
/// OLD-verbatim email subjects for the demo-critical lifecycle codes.
/// Subjects are short one-liners so a const-string map is the right
/// shape (versus the multi-line HTML bodies, which live as embedded
/// .html resources -- see <see cref="EmailBodyResources"/>).
///
/// <para>The bracketed patient-details prefix that OLD constructs at
/// <c>AppointmentDocumentDomain.cs</c>:921
/// (<c>"(" + patientName + injuryDetails + ")"</c>) is expressed via the
/// <c>##EmailSubjectIdentity##</c> token so the dispatcher can do the
/// same substitution as OLD without us encoding the bracket-and-dash
/// concatenation logic per-template.</para>
///
/// <para><b>Bug-fix from OLD:</b> the registration subject in OLD reads
/// <c>"Your have registered successfully"</c> (sic). Corrected here to
/// <c>"You have registered successfully"</c> per CLAUDE.md
/// "Clear bug -- fix it" rule.</para>
/// </summary>
internal static class EmailSubjects
{
    /// <summary>
    /// OLD <c>UserDomain.cs</c>:321. Typo "Your" -> "You" fixed.
    /// </summary>
    public const string UserRegistered =
        "You have registered successfully - Patient Appointment portal";

    /// <summary>OLD <c>AppointmentDomain.cs</c>:926.</summary>
    public const string PatientAppointmentPending =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Your appointment request has been Pending.";

    /// <summary>OLD <c>AppointmentDomain.cs</c>:940.</summary>
    public const string PatientAppointmentApproveReject =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Approve or Reject New Appointment Request";

    /// <summary>OLD <c>AppointmentDomain.cs</c>:957.</summary>
    public const string PatientAppointmentApprovedInternal =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Your appointment request has been approved successfully.";

    /// <summary>
    /// OLD <c>AppointmentDomain.cs</c>:970. Same string as the Internal-side
    /// variant -- OLD uses identical subject for both branches.
    /// </summary>
    public const string PatientAppointmentApprovedExt =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Your appointment request has been approved successfully.";

    /// <summary>OLD <c>AppointmentDomain.cs</c>:985.</summary>
    public const string PatientAppointmentRejected =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Your appointment request has been rejected by our clinic staff.";

    /// <summary>
    /// OLD <c>UserAuthenticationDomain.cs</c>:209.
    /// Phase 1.B (Category 1, 2026-05-08): wired through
    /// <c>CaseEvaluationAccountEmailer.SendPasswordResetLinkAsync</c>.
    /// </summary>
    public const string ResetPassword =
        "Patient Appointment Portal - Reset Password";

    /// <summary>
    /// OLD <c>UserDomain.cs</c>:337 / <c>UserAuthenticationDomain.cs</c>:313.
    /// Phase 1.C (Category 1, 2026-05-08): security-receipt confirmation
    /// fired after a successful password change (in-app or post-reset).
    /// </summary>
    public const string PasswordChange =
        "Your password has been successfully changed - Patient Appointment portal";

    // ----------------------------------------------------------------------
    // Phase 2.A (Category 2, 2026-05-08): per-recipient "Appointment Requested"
    // subjects. Replaces OLD's PatientAppointmentPending subject for the
    // stakeholder fan-out path. Adrian directive 2026-05-08 (rename Pending
    // -> Requested). NO bracketed identity prefix here -- the per-recipient
    // template handler builds a tighter subject using just the confirmation
    // number for cleaner inboxes.
    // ----------------------------------------------------------------------

    // ----------------------------------------------------------------------
    // Phase 2.C (Category 2, 2026-05-08): four deferred status-change
    // subjects. Match the simplified-HTML pattern (no bracketed identity
    // prefix; subject keys reference ##AppointmentRequestConfirmationNumber##).
    // ----------------------------------------------------------------------

    /// <summary>OLD <c>AppointmentDomain.cs</c>:993. Decision 4 fixes the OLD bug surfacing rejection notes on a check-in event.</summary>
    public const string PatientAppointmentCheckedIn =
        "Patient Appointment Portal - Your appointment has been checked in - ##AppointmentRequestConfirmationNumber##";

    /// <summary>OLD <c>AppointmentDomain.cs</c>:1005. Decision 4 same RejectionNotes-skip as CheckedIn.</summary>
    public const string PatientAppointmentCheckedOut =
        "Patient Appointment Portal - Your appointment has been checked out - ##AppointmentRequestConfirmationNumber##";

    /// <summary>OLD <c>AppointmentDomain.cs</c>:1017. Decision 5 internal staff only.</summary>
    public const string PatientAppointmentNoShow =
        "Patient Appointment Portal - Appointment ##AppointmentRequestConfirmationNumber## marked No-Show";

    /// <summary>OLD <c>AppointmentDomain.cs</c>:1030.</summary>
    public const string PatientAppointmentCancelledNoBill =
        "Patient Appointment Portal - Your appointment has been cancelled - ##AppointmentRequestConfirmationNumber##";

    // ----------------------------------------------------------------------
    // Phase 6.C (Category 6, 2026-05-08): document-flow subjects.
    // OLD wording verified at AppointmentDocumentDomain.cs:258 / :275 / :291
    // (package + ad-hoc share the same subjects). JointAgreementLetter*
    // subjects taken from AppointmentChangeRequestDomain.cs:976 / :987 / :994
    // -- OLD's awkward "Uploaded Accepted" / "Uploaded Rejected" wording
    // is FIXED here per the "clear bug -- fix it" rule.
    // ----------------------------------------------------------------------

    /// <summary>OLD <c>AppointmentDocumentDomain.cs</c>:258.</summary>
    public const string PatientDocumentAccepted =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Appointment document is Accepted.";

    /// <summary>OLD <c>AppointmentDocumentDomain.cs</c>:275.</summary>
    public const string PatientDocumentRejected =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Appointment document is Rejected.";

    /// <summary>OLD <c>AppointmentDocumentDomain.cs</c>:291.</summary>
    public const string PatientDocumentUploaded =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Appointment document is uploaded by user.";

    /// <summary>OLD <c>AppointmentDocumentDomain.cs</c>:340 -- ad-hoc-document accept variant. Same subject as the package variant; only the template body differs.</summary>
    public const string PatientNewDocumentAccepted =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Appointment document is Accepted.";

    /// <summary>OLD <c>AppointmentDocumentDomain.cs</c>:362.</summary>
    public const string PatientNewDocumentRejected =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Appointment document is Rejected.";

    /// <summary>OLD <c>AppointmentDocumentDomain.cs</c>:378.</summary>
    public const string PatientNewDocumentUploaded =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Appointment document is uploaded by user.";

    /// <summary>OLD <c>AppointmentChangeRequestDomain.cs</c>:976. OLD's "Joint Agreement Letter Uploaded Accepted" wording is fixed.</summary>
    public const string JointAgreementLetterAccepted =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Joint Agreement Letter Accepted.";

    /// <summary>OLD <c>AppointmentChangeRequestDomain.cs</c>:987.</summary>
    public const string JointAgreementLetterUploaded =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Joint Agreement Letter Uploaded.";

    /// <summary>OLD <c>AppointmentChangeRequestDomain.cs</c>:994. OLD's "Uploaded Rejected" wording is fixed.</summary>
    public const string JointAgreementLetterRejected =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Joint Agreement Letter Rejected.";

    /// <summary>Office mailbox subject when a new request lands.</summary>
    public const string AppointmentRequestedOffice =
        "New appointment request ##AppointmentRequestConfirmationNumber##";

    /// <summary>Registered party / patient subject -- "log in to view" body inside.</summary>
    public const string AppointmentRequestedRegistered =
        "Appointment Requested - ##AppointmentRequestConfirmationNumber##";

    /// <summary>Unregistered party subject -- "register as [role]" body inside.</summary>
    public const string AppointmentRequestedUnregistered =
        "Appointment Requested - register to view ##AppointmentRequestConfirmationNumber##";

    // ----------------------------------------------------------------------
    // Phase 7 (Category 7, 2026-05-10): OLD SchedulerDomain reminder subjects.
    // Wording from OLD SchedulerDomain.cs SendSMTPMail call sites:
    //   #1 :84  -> "Pending Appointment Request"
    //   #2 :113 -> "Updated Appointment Request"
    //   #3 :146 -> "Please Upload Pending Documents"
    //   #4 :171 -> "Appointment Due Date Approaching"
    //   #5 :199 -> "Appointment Document Incomplete"
    // ----------------------------------------------------------------------

    /// <summary>OLD <c>SchedulerDomain.cs</c>:84.</summary>
    public const string PendingAppointmentDailyNotification =
        "Pending Appointment Request";

    /// <summary>OLD <c>SchedulerDomain.cs</c>:113.</summary>
    public const string AppointmentApproveRejectInternal =
        "Updated Appointment Request";

    /// <summary>OLD <c>SchedulerDomain.cs</c>:146 + :229 (JDF reuses the same subject).</summary>
    public const string UploadPendingDocuments =
        "Please Upload Pending Documents";

    /// <summary>OLD <c>SchedulerDomain.cs</c>:171.</summary>
    public const string AppointmentDueDateReminder =
        "Appointment Due Date Approaching";

    /// <summary>OLD <c>SchedulerDomain.cs</c>:199.</summary>
    public const string AppointmentDocumentIncomplete =
        "Appointment Document Incomplete";

    // ----------------------------------------------------------------------
    // Phase 4 (Category 4, 2026-05-10): per-recipient packet email subject.
    // OLD wording verified at AppointmentDocumentDomain.cs:513 / :670 / :806:
    //   "Appointment Request Approved " + patientDetailsEmailSubject
    // The bracketed-identity tail is preserved via ##EmailSubjectIdentity##
    // so the dispatcher does the same substitution as the lifecycle emails.
    // ----------------------------------------------------------------------

    /// <summary>OLD <c>AppointmentDocumentDomain.cs</c>:513 / :670 / :806.</summary>
    public const string AppointmentDocumentAddWithAttachment =
        "Appointment Request Approved - ##EmailSubjectIdentity##";

    // ----------------------------------------------------------------------
    // Phase 5 (Category 5, 2026-05-10): document Accepted/Rejected with
    // remaining-docs branch. OLD scaffolded these in
    // AppointmentJointDeclarationDomain.cs:235 / :257 -- subjects match the
    // active Accepted/Rejected emails verbatim (only the BODY differs).
    // ----------------------------------------------------------------------

    /// <summary>OLD <c>AppointmentJointDeclarationDomain.cs</c>:235. Same subject string as PatientDocumentAccepted; only the body differs.</summary>
    public const string PatientDocumentAcceptedRemainingDocs =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Appointment document is Accepted.";

    /// <summary>OLD <c>AppointmentJointDeclarationDomain.cs</c>:257. Same subject string as PatientDocumentRejected; only the body differs.</summary>
    public const string PatientDocumentRejectedRemainingDocs =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Appointment document is Rejected.";

    // ----------------------------------------------------------------------
    // Phase 3 (Category 3, 2026-05-10): change-request flow subjects.
    // OLD wording verified from AppointmentChangeRequestDomain.cs:
    //   :658 ClinicalStaffCancellation - "Appointment request has been cancelled"
    //   :709 RescheduleReqAdmin        - "Reschedule request has been changed by our team"
    //   :723 RescheduleReqApproved     - "Your reschedule request has been approved"
    //   :735 RescheduleReqRejected     - "Your reschedule request has been rejected"
    //   :744 CancellationApproved      - "cancellation request has been accepted"
    //   :760 RescheduleReq             - "Your have successfully requested for reschedule" (typo fixed to "You have")
    //
    // Adrian Decision (2026-05-10): single template for approved (no admin-
    // override fork). Subject carries ##ApprovedSubjectQualifier## so the
    // handler can pick between "approved" and "changed by our team" wording.
    // ----------------------------------------------------------------------

    /// <summary>OLD has no explicit cancel-submit subject (NEW-only -- OLD did not email on cancel submit).</summary>
    public const string AppointmentCancelledRequest =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Cancellation request received";

    /// <summary>OLD <c>AppointmentChangeRequestDomain.cs</c>:744. Typo-fixed to capitalize first word.</summary>
    public const string AppointmentCancelledRequestApproved =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Cancellation request has been accepted";

    /// <summary>OLD <c>AppointmentChangeRequestDomain.cs</c>:752 (commented out). NEW-only.</summary>
    public const string AppointmentCancelledRequestRejected =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Cancellation request has been rejected";

    /// <summary>OLD <c>AppointmentChangeRequestDomain.cs</c>:760. Typo "Your have" fixed.</summary>
    public const string AppointmentRescheduleRequest =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - You have successfully requested a reschedule";

    /// <summary>OLD <c>AppointmentChangeRequestDomain.cs</c>:709 or :723 selected by ##ApprovedSubjectQualifier## (Cat 3 Adrian Decision: single template, variable-driven copy).</summary>
    public const string AppointmentRescheduleRequestApproved =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - ##ApprovedSubjectQualifier##";

    /// <summary>OLD <c>AppointmentChangeRequestDomain.cs</c>:735.</summary>
    public const string AppointmentRescheduleRequestRejected =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Your reschedule request has been rejected";

    /// <summary>OLD <c>AppointmentChangeRequestDomain.cs</c>:658.</summary>
    public const string ClinicalStaffCancellation =
        "Patient Appointment Portal - ##EmailSubjectIdentity## - Appointment request has been cancelled";

    /// <summary>
    /// 2026-05-15 -- admin-issued invite email. ##TenantName## is the
    /// per-tenant clinic display name; the dispatcher substitutes it
    /// in both subject and body before send.
    /// </summary>
    public const string InviteExternalUser =
        "You have been invited to register at ##TenantName##";

    /// <summary>
    /// 2026-05-15 -- welcome email for a newly-created internal user.
    /// Fixes OLD's literal-string subject typo (<c>"Welcome to socal"</c>)
    /// by substituting the per-tenant clinic name. ##TenantName## is
    /// resolved by the dispatcher at render time.
    /// </summary>
    public const string InternalUserCreated =
        "Welcome to ##TenantName##";

    /// <summary>
    /// Single source of truth for the per-code subject lookup. The seed
    /// contributor and any future migration walk this map; codes without
    /// an entry fall back to a stub subject.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ByCode =
        new Dictionary<string, string>(System.StringComparer.Ordinal)
        {
            [NotificationTemplateConsts.Codes.UserRegistered] = UserRegistered,
            [NotificationTemplateConsts.Codes.PatientAppointmentPending] = PatientAppointmentPending,
            [NotificationTemplateConsts.Codes.PatientAppointmentApproveReject] = PatientAppointmentApproveReject,
            [NotificationTemplateConsts.Codes.PatientAppointmentApprovedInternal] = PatientAppointmentApprovedInternal,
            [NotificationTemplateConsts.Codes.PatientAppointmentApprovedExt] = PatientAppointmentApprovedExt,
            [NotificationTemplateConsts.Codes.PatientAppointmentRejected] = PatientAppointmentRejected,
            [NotificationTemplateConsts.Codes.ResetPassword] = ResetPassword,
            [NotificationTemplateConsts.Codes.PasswordChange] = PasswordChange,
            [NotificationTemplateConsts.Codes.AppointmentRequestedOffice] = AppointmentRequestedOffice,
            [NotificationTemplateConsts.Codes.AppointmentRequestedRegistered] = AppointmentRequestedRegistered,
            [NotificationTemplateConsts.Codes.AppointmentRequestedUnregistered] = AppointmentRequestedUnregistered,
            [NotificationTemplateConsts.Codes.PatientAppointmentCheckedIn] = PatientAppointmentCheckedIn,
            [NotificationTemplateConsts.Codes.PatientAppointmentCheckedOut] = PatientAppointmentCheckedOut,
            [NotificationTemplateConsts.Codes.PatientAppointmentNoShow] = PatientAppointmentNoShow,
            [NotificationTemplateConsts.Codes.PatientAppointmentCancelledNoBill] = PatientAppointmentCancelledNoBill,
            [NotificationTemplateConsts.Codes.PatientDocumentAccepted] = PatientDocumentAccepted,
            [NotificationTemplateConsts.Codes.PatientDocumentRejected] = PatientDocumentRejected,
            [NotificationTemplateConsts.Codes.PatientDocumentUploaded] = PatientDocumentUploaded,
            [NotificationTemplateConsts.Codes.PatientNewDocumentAccepted] = PatientNewDocumentAccepted,
            [NotificationTemplateConsts.Codes.PatientNewDocumentRejected] = PatientNewDocumentRejected,
            [NotificationTemplateConsts.Codes.PatientNewDocumentUploaded] = PatientNewDocumentUploaded,
            [NotificationTemplateConsts.Codes.JointAgreementLetterAccepted] = JointAgreementLetterAccepted,
            [NotificationTemplateConsts.Codes.JointAgreementLetterUploaded] = JointAgreementLetterUploaded,
            [NotificationTemplateConsts.Codes.JointAgreementLetterRejected] = JointAgreementLetterRejected,

            // Phase 7 (Category 7, 2026-05-10): OLD SchedulerDomain reminder subjects.
            [NotificationTemplateConsts.Codes.PendingAppointmentDailyNotification] = PendingAppointmentDailyNotification,
            [NotificationTemplateConsts.Codes.AppointmentApproveRejectInternal] = AppointmentApproveRejectInternal,
            [NotificationTemplateConsts.Codes.UploadPendingDocuments] = UploadPendingDocuments,
            [NotificationTemplateConsts.Codes.AppointmentDueDateReminder] = AppointmentDueDateReminder,
            [NotificationTemplateConsts.Codes.AppointmentDocumentIncomplete] = AppointmentDocumentIncomplete,

            // Phase 4 (Category 4, 2026-05-10): packet email subject.
            [NotificationTemplateConsts.Codes.AppointmentDocumentAddWithAttachment] = AppointmentDocumentAddWithAttachment,

            // Phase 5 (Category 5, 2026-05-10): document remaining-docs variants.
            [NotificationTemplateConsts.Codes.PatientDocumentAcceptedRemainingDocs] = PatientDocumentAcceptedRemainingDocs,
            [NotificationTemplateConsts.Codes.PatientDocumentRejectedRemainingDocs] = PatientDocumentRejectedRemainingDocs,

            // Phase 3 (Category 3, 2026-05-10): change-request flow subjects.
            [NotificationTemplateConsts.Codes.AppointmentCancelledRequest] = AppointmentCancelledRequest,
            [NotificationTemplateConsts.Codes.AppointmentCancelledRequestApproved] = AppointmentCancelledRequestApproved,
            [NotificationTemplateConsts.Codes.AppointmentCancelledRequestRejected] = AppointmentCancelledRequestRejected,
            [NotificationTemplateConsts.Codes.AppointmentRescheduleRequest] = AppointmentRescheduleRequest,
            [NotificationTemplateConsts.Codes.AppointmentRescheduleRequestApproved] = AppointmentRescheduleRequestApproved,
            [NotificationTemplateConsts.Codes.AppointmentRescheduleRequestRejected] = AppointmentRescheduleRequestRejected,
            [NotificationTemplateConsts.Codes.ClinicalStaffCancellation] = ClinicalStaffCancellation,

            // 2026-05-15 -- admin-issued invitation.
            [NotificationTemplateConsts.Codes.InviteExternalUser] = InviteExternalUser,

            // 2026-05-15 -- IT Admin internal-user welcome email.
            [NotificationTemplateConsts.Codes.InternalUserCreated] = InternalUserCreated,
        };
}
