namespace HealthcareSupport.CaseEvaluation;

public static class CaseEvaluationDomainErrorCodes
{
    /// <summary>
    /// Raised by <c>SystemParametersAppService.GetAsync / UpdateAsync</c>
    /// when the per-tenant singleton row is missing for the calling tenant
    /// scope. ABP's BusinessException maps this code to the localization
    /// key <c>SystemParameter:NotSeeded</c>.
    /// </summary>
    public const string SystemParameterNotSeeded = "CaseEvaluation:SystemParameter.NotSeeded";

    /// <summary>
    /// Raised by <c>PackageDetailsAppService.CreateAsync / UpdateAsync</c>
    /// when an active <c>PackageDetail</c> already exists for the same
    /// <c>AppointmentTypeId</c>. Mirrors OLD's verbatim validation (see
    /// <c>P:\PatientPortalOld\PatientAppointment.Domain\DocumentManagementModule\PackageDetailDomain.cs</c>:48-53).
    /// Localization key: <c>PackageDetail:OneActivePerAppointmentType</c>.
    /// </summary>
    public const string OneActivePackageDetailPerAppointmentType =
        "CaseEvaluation:PackageDetail.OneActivePerAppointmentType";

    /// <summary>
    /// Raised by <c>DocumentsAppService.DeleteAsync</c> when the catalog row
    /// is still referenced by a <c>DocumentPackage</c>. Forces IT Admin to
    /// unlink before deletion (matches OLD where deletion of a referenced
    /// row would orphan a package row).
    /// </summary>
    public const string DocumentInUse = "CaseEvaluation:Document.InUse";

    /// <summary>
    /// Raised by <c>CustomFieldsAppService.CreateAsync</c> when the IT Admin
    /// tries to create an 11th active row for the same <c>AppointmentTypeId</c>.
    /// Mirrors OLD spec line 543 ("Up to 10 fields per appointment type"),
    /// corrected from OLD's buggy global <c>== 10</c> check
    /// (CustomFieldDomain.cs:38-42) to a per-AppointmentTypeId
    /// <c>&gt;= 10</c> check. Localization key
    /// <c>CustomField:Max10ActivePerAppointmentType</c>.
    /// </summary>
    public const string CustomFieldMax10ActivePerAppointmentType =
        "CaseEvaluation:CustomField.Max10ActivePerAppointmentType";

    /// <summary>
    /// Raised by <c>CustomFieldsAppService.CreateAsync / UpdateAsync</c> when
    /// another active row with the same (FieldLabel, FieldType) already
    /// exists. Mirrors OLD <c>CustomFieldDomain.cs:39 / 69</c>.
    /// </summary>
    public const string CustomFieldDuplicateLabelAndType =
        "CaseEvaluation:CustomField.DuplicateLabelAndType";

    /// <summary>
    /// Phase 7 (2026-05-03) -- raised by
    /// <c>DoctorAvailabilitiesAppService.UpdateAsync</c> when the slot
    /// being updated is currently <c>Reserved</c> or <c>Booked</c>.
    /// Mirrors OLD <c>DoctorsAvailabilityDomain.cs:126-130</c> -- protects
    /// in-flight appointments from having their underlying slot mutated.
    /// Localization key <c>DoctorAvailability:CannotUpdateBookedOrReserved</c>.
    /// </summary>
    public const string DoctorAvailabilityCannotUpdateBookedOrReserved =
        "CaseEvaluation:DoctorAvailability.CannotUpdateBookedOrReserved";

    /// <summary>
    /// Phase 7 (2026-05-03) -- raised by
    /// <c>DoctorAvailabilitiesAppService.DeleteByDateAsync</c> when ANY
    /// slot at the given date + location is <c>Reserved</c> or <c>Booked</c>.
    /// Mirrors OLD <c>DoctorsAvailabilityDomain.cs:143-150</c>. Bulk-delete
    /// must NOT silently drop slots that have appointments tied to them.
    /// Localization key <c>DoctorAvailability:CannotBulkDeleteWithBookedSlots</c>.
    /// </summary>
    public const string DoctorAvailabilityCannotBulkDeleteWithBookedSlots =
        "CaseEvaluation:DoctorAvailability.CannotBulkDeleteWithBookedSlots";

    /// <summary>
    /// Phase 7 (2026-05-03) -- raised by
    /// <c>DoctorAvailabilitiesAppService.DeleteAsync</c> when the slot is
    /// referenced by an existing <c>Appointment</c> or
    /// <c>AppointmentChangeRequest</c>. Mirrors OLD
    /// <c>DoctorsAvailabilityDomain.cs:151-154</c>. Prevents historic-data
    /// FK orphans even if the slot's <c>BookingStatus</c> was somehow
    /// reset to <c>Available</c> by a manual data fix.
    /// Localization key <c>DoctorAvailability:CannotDeleteReferenced</c>.
    /// </summary>
    public const string DoctorAvailabilityCannotDeleteReferenced =
        "CaseEvaluation:DoctorAvailability.CannotDeleteReferenced";

    /// <summary>
    /// Phase 10 (2026-05-03) -- raised by
    /// <c>ExternalAccountAppService.SendPasswordResetCodeAsync</c> when
    /// the target user has not yet confirmed their email address. Mirrors
    /// OLD <c>UserAuthenticationDomain.cs:166-169</c>'s "verified-only
    /// password reset" rule (Adrian's Q1 lock 2026-05-01: strict OLD parity).
    /// Localization key <c>Account:EmailNotConfirmedForPasswordReset</c>.
    /// </summary>
    public const string EmailNotConfirmedForPasswordReset =
        "CaseEvaluation:Account.EmailNotConfirmedForPasswordReset";

    /// <summary>
    /// Phase 10 (2026-05-03) -- raised by
    /// <c>ExternalAccountAppService.SendPasswordResetCodeAsync</c> when
    /// the target user is inactive (<c>IsActive == false</c>). Mirrors OLD
    /// <c>UserAuthenticationDomain.cs:170-173</c>. Localization key
    /// <c>Account:UserInactiveForPasswordReset</c>.
    /// </summary>
    public const string UserInactiveForPasswordReset =
        "CaseEvaluation:Account.UserInactiveForPasswordReset";

    /// <summary>
    /// Phase 10 (2026-05-03) -- raised by
    /// <c>ExternalAccountAppService.ResetPasswordAsync</c> when the reset
    /// token is invalid or already-consumed (mirrors OLD's silent no-op
    /// at <c>UserAuthenticationDomain.cs:244-255</c>; NEW returns a
    /// generic error to avoid info leak). Localization key
    /// <c>Account:ResetPasswordTokenInvalid</c>.
    /// </summary>
    public const string ResetPasswordTokenInvalid =
        "CaseEvaluation:Account.ResetPasswordTokenInvalid";

    /// <summary>
    /// Phase 11b (2026-05-04) -- raised by
    /// <c>AppointmentsAppService.CreateAsync</c> when the chosen slot's
    /// <c>AvailableDate</c> falls inside the per-tenant
    /// <c>SystemParameter.AppointmentLeadTime</c> window. Mirrors OLD
    /// <c>AppointmentDomain.cs</c> Add path's lead-time gate
    /// (<c>ValidationFailedCode.AppointmentBookingDateNotAvailable</c>).
    /// Localization key
    /// <c>Appointment:BookingDateInsideLeadTime</c>.
    /// </summary>
    public const string AppointmentBookingDateInsideLeadTime =
        "CaseEvaluation:Appointment.BookingDateInsideLeadTime";

    /// <summary>
    /// Phase 11b (2026-05-04) -- raised by
    /// <c>AppointmentsAppService.CreateAsync</c> when the chosen slot's
    /// <c>AvailableDate</c> is past the per-AppointmentType max horizon
    /// (<c>SystemParameter.AppointmentMaxTimePQME / AppointmentMaxTimeAME /
    /// AppointmentMaxTimeOTHER</c>). Mirrors OLD <c>AppointmentDomain.cs</c>
    /// Add path's max-time gate.
    /// Localization key
    /// <c>Appointment:BookingDatePastMaxHorizon</c>.
    /// </summary>
    public const string AppointmentBookingDatePastMaxHorizon =
        "CaseEvaluation:Appointment.BookingDatePastMaxHorizon";

    /// <summary>
    /// Phase 18 (2026-05-04) -- raised by
    /// <c>NotificationTemplateRenderer.RenderAsync</c> when the
    /// requested <c>TemplateCode</c> resolves to no row OR the row's
    /// <c>IsActive</c> is false. Treated as a seeding bug rather than
    /// a runtime fallback (per Phase 18 audit decision -- missing
    /// templates should surface loudly so the gap is fixed in seed,
    /// not papered over with hardcoded strings). Localization key
    /// <c>NotificationTemplate:NotFound</c>.
    /// </summary>
    public const string NotificationTemplateNotFound =
        "CaseEvaluation:NotificationTemplate.NotFound";

    /// <summary>
    /// Phase 11e (2026-05-04) -- raised by the Re-Submit path when the
    /// source appointment is not in status <c>Rejected</c>. Mirrors OLD
    /// <c>AppointmentDomain.cs:181</c> ("You not allowed to re apply
    /// appointment"). Localization key
    /// <c>Appointment:ReSubmitSourceNotRejected</c>.
    /// </summary>
    public const string AppointmentReSubmitSourceNotRejected =
        "CaseEvaluation:Appointment.ReSubmitSourceNotRejected";

    /// <summary>
    /// Phase 11e (2026-05-04) -- raised by the Reval path when the source
    /// appointment is not in status <c>Approved</c> AND the caller is NOT
    /// an IT Admin. Mirrors OLD <c>AppointmentDomain.cs:168</c>
    /// ("You can not Re-eval this appointment request because it's not
    /// yet approved. Once it gets approved, You will be able to Re-eval
    /// this appointment request.").
    /// Localization key <c>Appointment:RevalSourceNotApproved</c>.
    /// </summary>
    public const string AppointmentRevalSourceNotApproved =
        "CaseEvaluation:Appointment.RevalSourceNotApproved";

    /// <summary>
    /// Phase 11e (2026-05-04) -- raised by the Reval path when the source
    /// appointment is not in status <c>Approved</c> AND the caller IS an
    /// IT Admin. Mirrors OLD <c>AppointmentDomain.cs:172</c>
    /// ("You can not Re-eval this appointment request because it's not
    /// yet approved. Please approve an appointment and try again."). The
    /// hint to "approve an appointment and try again" is verbatim OLD;
    /// admin-only because non-admin callers see the patient-facing
    /// variant.
    /// Localization key <c>Appointment:RevalSourceNotApprovedAdminHint</c>.
    /// </summary>
    public const string AppointmentRevalSourceNotApprovedAdminHint =
        "CaseEvaluation:Appointment.RevalSourceNotApprovedAdminHint";

    /// <summary>
    /// Phase 12 (2026-05-04) -- raised by
    /// <c>AppointmentApprovalAppService.ApproveAppointmentAsync</c>
    /// when <c>ApproveAppointmentInput.PrimaryResponsibleUserId</c>
    /// is <see cref="System.Guid.Empty"/>. Mirrors OLD's UI gate that
    /// disabled the Approve button until a responsible user was
    /// selected (no equivalent inline error message in OLD source --
    /// the gate was UI-only). Localization key
    /// <c>Appointment:ApprovalRequiresResponsibleUser</c>.
    /// </summary>
    public const string AppointmentApprovalRequiresResponsibleUser =
        "CaseEvaluation:Appointment.ApprovalRequiresResponsibleUser";

    /// <summary>
    /// Phase 12 (2026-05-04) -- raised by
    /// <c>AppointmentApprovalAppService.ApproveAppointmentAsync</c>
    /// when the appointment is not in status <c>Pending</c>. Mirrors
    /// OLD's idempotency string at
    /// <c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDomain.cs</c>:319-325
    /// -- "Appointment Already Approved" / "Appointment Already
    /// Rejected" surface verbatim through this code's localization
    /// value. Localization key <c>Appointment:NotPendingForApproval</c>.
    /// </summary>
    public const string AppointmentNotPendingForApproval =
        "CaseEvaluation:Appointment.NotPendingForApproval";

    /// <summary>
    /// Phase 12 (2026-05-04) -- raised by
    /// <c>AppointmentApprovalAppService.RejectAppointmentAsync</c>
    /// when the appointment is not in status <c>Pending</c>.
    /// Localization key <c>Appointment:NotPendingForRejection</c>.
    /// </summary>
    public const string AppointmentNotPendingForRejection =
        "CaseEvaluation:Appointment.NotPendingForRejection";

    /// <summary>
    /// Phase 12 (2026-05-04) -- raised by
    /// <c>AppointmentApprovalAppService.RejectAppointmentAsync</c>
    /// when <c>RejectAppointmentInput.Reason</c> is null or
    /// whitespace. OLD UI required the rejection-notes textarea before
    /// enabling the Reject button. Localization key
    /// <c>Appointment:RejectionRequiresNotes</c>.
    /// </summary>
    public const string AppointmentRejectionRequiresNotes =
        "CaseEvaluation:Appointment.RejectionRequiresNotes";

    /// <summary>
    /// Phase 11i (2026-05-04) -- raised by
    /// <c>AppointmentAccessorManager.CreateOrLinkAsync</c> when an
    /// existing IdentityUser is found by email but already holds a role
    /// different from the role the booking flow is trying to assign as
    /// accessor. Mirrors OLD <c>AppointmentDomain.cs:188-194</c>:
    /// "Your added accessor '&lt;email&gt;' is already registered in our
    /// system with different user type. Please select proper Accessor
    /// user's type and try again". Localization key
    /// <c>Appointment:AccessorRoleMismatch</c> (with <c>{0}</c> replaced
    /// at render time by the offending email).
    /// </summary>
    public const string AppointmentAccessorRoleMismatch =
        "CaseEvaluation:Appointment.AccessorRoleMismatch";

    /// <summary>
    /// Phase 13 (2026-05-04) -- raised by
    /// <c>AppointmentsAppService.GetAsync / GetWithNavigationPropertiesAsync /
    /// GetByConfirmationNumberAsync</c> when the caller is an external
    /// user who is neither the creator nor an accessor on the target
    /// appointment. Mirrors OLD's behavior of filtering out non-accessible
    /// rows in the stored proc <c>spm.spAppointmentRequestList</c>; per-
    /// appointment lookups land here as the equivalent gate. Internal
    /// users (admin / Clinic Staff / etc.) bypass this check; ABP's
    /// IMultiTenant filter still scopes them to their own tenant.
    /// Localization key <c>Appointment:AccessDenied</c>.
    /// </summary>
    public const string AppointmentAccessDenied =
        "CaseEvaluation:Appointment.AccessDenied";
}
