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
    /// Raised by <c>AppointmentManager.FireTransition</c> when the
    /// caller requests a status transition that is not legal from the
    /// appointment's current state (e.g. Approve from Approved, or
    /// Reject from Rejected). Carries WithData("from", currentStatus)
    /// + WithData("trigger", requestedTrigger) for the SPA to surface
    /// a contextual message. Mapped to HTTP 400 (BUG-024 follow-up,
    /// 2026-05-19) because it is a client-input validation failure,
    /// not an authorization failure -- the caller IS allowed to use
    /// the endpoint, but the state machine rejects the request.
    /// </summary>
    public const string AppointmentInvalidTransition =
        "CaseEvaluation:AppointmentInvalidTransition";

    /// <summary>
    /// 2026-05-21 (OBS-23) -- raised by
    /// <c>AppointmentsAppService.CreateAsync</c> when a non-attorney
    /// external user (Patient or Claim Examiner) attempts to create
    /// an AME or AME-REVAL appointment. Mirrors OLD's
    /// <c>RoleAppointmentType</c> join restriction; NEW uses a
    /// hardcoded attorney allow-list since the join table was not
    /// ported. Mapped to HTTP 400. Localization key
    /// <c>Appointment:AmeRequiresAttorneyRole</c>.
    /// </summary>
    public const string AppointmentAmeRequiresAttorneyRole =
        "CaseEvaluation:Appointment.AmeRequiresAttorneyRole";

    /// <summary>
    /// 2026-05-15 -- raised by <c>InvitationManager.ValidateAsync</c>
    /// when the supplied invite token does not hash to any persisted
    /// <c>Invitation.TokenHash</c>. Treated as the generic-failure
    /// terminal so a tampered URL does not leak whether the token shape
    /// is valid. Localization key
    /// <c>Invitation:InviteInvalid</c>.
    /// </summary>
    public const string InviteInvalid = "CaseEvaluation:Invitation.InviteInvalid";

    /// <summary>
    /// 2026-05-15 -- raised by <c>InvitationManager.ValidateAsync</c>
    /// when the invitation row exists but its <c>ExpiresAt</c> is in
    /// the past. Recipient is shown a friendly "request a new link"
    /// message. Localization key <c>Invitation:InviteExpired</c>.
    /// </summary>
    public const string InviteExpired = "CaseEvaluation:Invitation.InviteExpired";

    /// <summary>
    /// 2026-05-15 -- raised by <c>InvitationManager.ValidateAsync</c>
    /// when the invitation has already been accepted (one-time-use
    /// enforcement). The friendly UX prompts the recipient to sign in
    /// if it was them, otherwise to contact the clinic in case the
    /// link was intercepted. Localization key
    /// <c>Invitation:InviteAlreadyAccepted</c>.
    /// </summary>
    public const string InviteAlreadyAccepted =
        "CaseEvaluation:Invitation.InviteAlreadyAccepted";

    /// <summary>
    /// 2026-05-15 -- raised by <c>InternalUsersAppService.CreateAsync</c>
    /// when the IT Admin requests a role outside the
    /// <c>{Clinic Staff, Staff Supervisor}</c> allow-list. Defense in depth:
    /// the SPA form's dropdown only lists the two creatable roles, but a
    /// tampered request body could try any role name; this gate forbids
    /// the request before any DB write. Localization key
    /// <c>Invitation</c>-style: <c>InternalUser:InvalidRole</c>.
    /// </summary>
    public const string InternalUserInvalidRole =
        "CaseEvaluation:InternalUser.InvalidRole";

    /// <summary>
    /// 2026-05-15 -- raised by <c>InternalUsersAppService.CreateAsync</c>
    /// when the requested role exists in the allow-list but has not been
    /// seeded into the tenant's <c>AbpRoles</c> table. Operationally a
    /// data-seed bug (the role seeder should have run for every tenant);
    /// surfaces as a 400 so the operator can re-seed instead of seeing a
    /// raw 500. Localization key <c>InternalUser:RoleMissing</c>.
    /// </summary>
    public const string InternalUserRoleMissing =
        "CaseEvaluation:InternalUser.RoleMissing";

    /// <summary>
    /// 2026-05-15 -- raised by <c>InternalUsersAppService.CreateAsync</c>
    /// when an <c>IdentityUser</c> with the same email already exists in
    /// the target tenant. Message is intentionally generic (no email
    /// echo) so the response does not leak which addresses are
    /// registered (HIPAA pattern, same as ExternalSignup). Localization
    /// key <c>InternalUser:DuplicateEmail</c>.
    /// </summary>
    public const string InternalUserDuplicateEmail =
        "CaseEvaluation:InternalUser.DuplicateEmail";

    /// <summary>
    /// 2026-05-15 -- raised when <c>IdentityUserManager.CreateAsync</c>
    /// returns a failed <c>IdentityResult</c>. Most commonly a password-
    /// policy violation (the auto-generated password is built to satisfy
    /// the defaults, so this is exceptional). Joined error descriptions
    /// surface via <c>BusinessException.WithData("Errors", ...)</c>.
    /// Localization key <c>InternalUser:CreateFailed</c>.
    /// </summary>
    public const string InternalUserCreateFailed =
        "CaseEvaluation:InternalUser.CreateFailed";

    /// <summary>
    /// 2026-05-15 -- raised when <c>IdentityUserManager.AddToRoleAsync</c>
    /// returns a failed <c>IdentityResult</c> AFTER the user row was
    /// created. The AppService deletes the newly-created user before
    /// throwing so we do not leave an orphan account with no role.
    /// Localization key <c>InternalUser:RoleAssignFailed</c>.
    /// </summary>
    public const string InternalUserRoleAssignFailed =
        "CaseEvaluation:InternalUser.RoleAssignFailed";

    /// <summary>
    /// 2026-05-15 -- raised by <c>InternalUsersAppService.CreateAsync</c>
    /// when the input DTO's <c>TenantId</c> is empty. IT Admin is host-
    /// scoped (admin.localhost), so <c>CurrentTenant.Id</c> is null at
    /// call time; the form's tenant picker is the source of truth for
    /// which tenant the new user belongs to. Localization key
    /// <c>InternalUser:TenantRequired</c>.
    /// </summary>
    public const string InternalUserTenantRequired =
        "CaseEvaluation:InternalUser.TenantRequired";

    /// <summary>
    /// 2026-05-19 -- raised by <c>InternalUsersAppService.CreateAsync</c>
    /// when a tenant-scoped caller (CurrentTenant.Id != null) passes a
    /// non-empty <c>input.TenantId</c> that does not match their own
    /// tenant. Prevents a tenant admin from creating users inside
    /// another tenant by hand-crafting an API call. Localization key
    /// <c>InternalUser:TenantMismatch</c>.
    /// </summary>
    public const string InternalUserTenantMismatch =
        "CaseEvaluation:InternalUser.TenantMismatch";

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
    /// BUG-043 / T8 (2026-05-27) -- raised by
    /// <c>AppointmentManager.ApplyTransitionAsync</c> on the Approve
    /// trigger when the appointment has no Claim Information (injury
    /// detail) rows. Defense-in-depth behind the client-side guard added
    /// in T7: a direct API approve of an injury-less appointment is
    /// rejected here so the requirement cannot be bypassed. Gated only on
    /// the Pending->Approved transition (the create-as-Approved internal
    /// fast-path attaches injuries after creation and is out of scope).
    /// Localization key <c>Appointment:ApprovalRequiresInjuryDetail</c>.
    /// </summary>
    public const string AppointmentApprovalRequiresInjuryDetail =
        "CaseEvaluation:Appointment.ApprovalRequiresInjuryDetail";

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

    /// <summary>
    /// Phase 14 (2026-05-04) -- raised by
    /// <c>DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate</c>
    /// when the appointment status is not Approved or
    /// RescheduleRequested. Mirrors OLD verbatim "Please upload
    /// documents after appointment is approved."
    /// (<c>P:\PatientPortalOld\PatientAppointment.Domain\AppointmentRequestModule\AppointmentDocumentDomain.cs</c>:104).
    /// Localization key <c>Document:UploadAfterApproval</c>.
    /// </summary>
    public const string DocumentUploadAfterApproval =
        "CaseEvaluation:Document.UploadAfterApproval";

    /// <summary>
    /// Phase 14 (2026-05-04) -- raised by
    /// <c>DocumentUploadGate.EnsureAppointmentApprovedAndNotPastDueDate</c>
    /// when the appointment is past its <c>DueDate</c>. Mirrors OLD
    /// verbatim "You can not upload document after specified due
    /// date." (<c>AppointmentDocumentDomain.cs</c>:99). Localization
    /// key <c>Document:UploadAfterDueDate</c>.
    /// </summary>
    public const string DocumentUploadAfterDueDate =
        "CaseEvaluation:Document.UploadAfterDueDate";

    /// <summary>
    /// Phase 14 (2026-05-04) -- raised by
    /// <c>DocumentUploadGate.EnsureAme</c> when a JDF upload is
    /// attempted against a non-AME appointment. Mirrors OLD verbatim
    /// "Appointment type is not valid. Please upload appropriate
    /// document." Localization key
    /// <c>Document:JdfRequiresAmeAppointment</c>.
    /// </summary>
    public const string JdfRequiresAmeAppointment =
        "CaseEvaluation:Document.JdfRequiresAmeAppointment";

    /// <summary>
    /// Phase 14 (2026-05-04) -- raised by
    /// <c>DocumentUploadGate.EnsureCreatorIsAttorney</c> when a JDF
    /// upload is attempted by anyone other than the booking attorney
    /// (or when that user is not in an Applicant/Defense Attorney
    /// role). Localization key
    /// <c>Document:JdfUploaderMustBeBookingAttorney</c>.
    /// </summary>
    public const string JdfUploaderMustBeBookingAttorney =
        "CaseEvaluation:Document.JdfUploaderMustBeBookingAttorney";

    /// <summary>
    /// Phase 14 (2026-05-04) -- raised by
    /// <c>DocumentUploadGate.EnsureNotImmutable</c> when an external
    /// user attempts to mutate an Accepted document. Mirrors OLD's
    /// "approved docs are read-only for external users" rule.
    /// Localization key <c>Document:ImmutableForExternalUser</c>.
    /// </summary>
    public const string DocumentImmutableForExternalUser =
        "CaseEvaluation:Document.ImmutableForExternalUser";

    /// <summary>
    /// Phase 14 (2026-05-04) -- raised by
    /// <c>DocumentUploadGate.EnsureVerificationCodeMatches</c> when
    /// the supplied verification code does not match the document's
    /// stored code, OR the document is missing. Mirrors OLD verbatim
    /// "Un unauthorized user"
    /// (<c>AppointmentDocumentDomain.cs</c>:71). NEW preserves the
    /// OLD wording. Localization key
    /// <c>Document:UnauthorizedVerificationCode</c>.
    /// </summary>
    public const string DocumentUnauthorizedVerificationCode =
        "CaseEvaluation:Document.UnauthorizedVerificationCode";

    /// <summary>
    /// Phase 15 (2026-05-04) -- raised by
    /// <c>AppointmentChangeRequestManager.SubmitCancellationAsync</c>
    /// (and Phase 16's <c>SubmitRescheduleAsync</c>) when the source
    /// appointment is not in status <c>Approved</c>. Mirrors OLD
    /// <c>AppointmentChangeRequestDomain.cs:73-75</c>'s
    /// <c>NoChangeAllowedinAppointment</c>. Localization key
    /// <c>AppointmentChangeRequest:AppointmentNotApproved</c>.
    /// </summary>
    public const string ChangeRequestAppointmentNotApproved =
        "CaseEvaluation:AppointmentChangeRequest.AppointmentNotApproved";

    /// <summary>
    /// Phase 15 (2026-05-04) -- raised when the cancel request is
    /// submitted INSIDE the per-tenant <c>AppointmentCancelTime</c>
    /// window (slot date is closer than the threshold). Mirrors OLD
    /// <c>AppointmentChangeRequestDomain.cs:87-90</c>'s
    /// <c>CannotCancelOrRescheduleAppointment</c>. Localization key
    /// <c>AppointmentChangeRequest:CancelTimeWindow</c>.
    /// </summary>
    public const string ChangeRequestCancelTimeWindow =
        "CaseEvaluation:AppointmentChangeRequest.CancelTimeWindow";

    /// <summary>
    /// Phase 15 (2026-05-04) -- raised when the caller is neither the
    /// appointment creator nor an Edit accessor. Mirrors OLD's intent
    /// per spec line 431; OLD's owner-only check is commented out at
    /// <c>AppointmentChangeRequestDomain.cs:78-81</c>. Localization
    /// key <c>AppointmentChangeRequest:EditAccessRequired</c>.
    /// </summary>
    public const string ChangeRequestEditAccessRequired =
        "CaseEvaluation:AppointmentChangeRequest.EditAccessRequired";

    /// <summary>
    /// Phase 16 (2026-05-04) -- raised by
    /// <c>AppointmentChangeRequestManager.SubmitRescheduleAsync</c>
    /// when the user-picked new slot is not currently in status
    /// <see cref="HealthcareSupport.CaseEvaluation.Enums.BookingStatus.Available"/>.
    /// Mirrors OLD <c>AppointmentChangeRequestDomain.cs:107-110</c>
    /// (<c>AppointmentBookingDateNotAvailable</c>). Localization key
    /// <c>AppointmentChangeRequest:NewSlotNotAvailable</c>.
    /// </summary>
    public const string ChangeRequestNewSlotNotAvailable =
        "CaseEvaluation:AppointmentChangeRequest.NewSlotNotAvailable";

    /// <summary>
    /// Phase 16 (2026-05-04) -- raised when a reschedule request is
    /// submitted without a new slot id (NewDoctorAvailabilityId is
    /// empty). Mirrors OLD <c>AppointmentChangeRequestDomain.cs:103-106</c>
    /// (<c>ProvideNewAppointmentDateTime</c>). Localization key
    /// <c>AppointmentChangeRequest:NewSlotRequired</c>.
    /// </summary>
    public const string ChangeRequestNewSlotRequired =
        "CaseEvaluation:AppointmentChangeRequest.NewSlotRequired";

    /// <summary>
    /// Phase 16 (2026-05-04) -- raised when a reschedule request is
    /// submitted without a reschedule reason. Mirrors OLD
    /// <c>AppointmentChangeRequestDomain.cs:99-102</c>
    /// (<c>ProvideRescheduleReason</c>). Localization key
    /// <c>AppointmentChangeRequest:RescheduleReasonRequired</c>.
    /// </summary>
    public const string ChangeRequestRescheduleReasonRequired =
        "CaseEvaluation:AppointmentChangeRequest.RescheduleReasonRequired";

    /// <summary>
    /// Phase 17 (2026-05-04) -- raised by the change-request approval
    /// AppService when the request is no longer Pending OR when the
    /// optimistic-concurrency gate fires (two supervisors handling the
    /// same row simultaneously). OLD-verbatim wording: "This change
    /// request has already been processed". Localization key
    /// <c>ChangeRequest:AlreadyHandled</c>.
    /// </summary>
    public const string ChangeRequestAlreadyHandled =
        "CaseEvaluation:ChangeRequest.AlreadyHandled";

    /// <summary>
    /// Phase 17 (2026-05-04) -- raised when the cancellation-approval
    /// outcome is not <c>CancelledNoBill</c> or <c>CancelledLate</c>.
    /// Localization key <c>ChangeRequest:InvalidCancellationOutcome</c>.
    /// </summary>
    public const string ChangeRequestInvalidCancellationOutcome =
        "CaseEvaluation:ChangeRequest.InvalidCancellationOutcome";

    /// <summary>
    /// Phase 17 (2026-05-04) -- raised when the reschedule-approval
    /// outcome is not <c>RescheduledNoBill</c> or <c>RescheduledLate</c>.
    /// Localization key <c>ChangeRequest:InvalidRescheduleOutcome</c>.
    /// </summary>
    public const string ChangeRequestInvalidRescheduleOutcome =
        "CaseEvaluation:ChangeRequest.InvalidRescheduleOutcome";

    /// <summary>
    /// Phase 17 (2026-05-04) -- raised when the supervisor overrides
    /// the user-picked slot during reschedule approval but does not
    /// supply <c>AdminReScheduleReason</c>. Mirrors OLD's UI gate.
    /// Localization key <c>ChangeRequest:AdminReasonRequired</c>.
    /// </summary>
    public const string ChangeRequestAdminReasonRequired =
        "CaseEvaluation:ChangeRequest.AdminReasonRequired";

    /// <summary>
    /// Phase 17 (2026-05-04) -- raised when supervisor rejects a
    /// change request without rejection notes. Mirrors OLD's
    /// <c>CancellationRejectionReason</c> /
    /// <c>ReScheduleRejectionReason</c> required-field gates.
    /// Localization key <c>ChangeRequest:RejectionRequiresNotes</c>.
    /// </summary>
    public const string ChangeRequestRejectionRequiresNotes =
        "CaseEvaluation:ChangeRequest.RejectionRequiresNotes";

    /// <summary>
    /// Phase 8 (2026-05-03) -- raised by
    /// <c>ExternalSignupAppService.RegisterAsync</c> when
    /// <c>ConfirmPassword</c> does not equal <c>Password</c>. Mirrors OLD
    /// <c>UserDomain.cs:88</c> (<c>ValidationFailedCode.ConfirmPasswordValidation</c>).
    /// Localization key <c>Registration:ConfirmPasswordMismatch</c>.
    /// </summary>
    public const string RegistrationConfirmPasswordMismatch =
        "CaseEvaluation:Registration.ConfirmPasswordMismatch";

    /// <summary>
    /// Phase 8 (2026-05-03) -- raised by
    /// <c>ExternalSignupAppService.RegisterAsync</c> when
    /// <c>FirmName</c> is missing for an attorney role
    /// (<c>ApplicantAttorney</c> or <c>DefenseAttorney</c>). Mirrors OLD
    /// <c>UserDomain.cs:272</c> (<c>FirmNameValidation</c>) -- with the
    /// OLD-bug-fix that the check now covers BOTH attorney roles, not
    /// just <c>PatientAttorney</c> twice as in the OLD source.
    /// Localization key <c>Registration:FirmNameRequiredForAttorney</c>.
    /// </summary>
    public const string RegistrationFirmNameRequired =
        "CaseEvaluation:Registration.FirmNameRequiredForAttorney";

    /// <summary>
    /// BUG-012 (2026-05-22) -- raised by
    /// <c>AppointmentsAppService.UpsertApplicantAttorneyForAppointmentAsync</c>
    /// and
    /// <c>AppointmentsAppService.UpsertDefenseAttorneyForAppointmentAsync</c>
    /// when the appointment-view/edit save submits an attorney section
    /// with an empty Firm Name. Companion to
    /// <see cref="RegistrationFirmNameRequired"/> but scoped to the
    /// booking flow rather than account registration -- attorneys on an
    /// appointment must carry the same OLD <c>UserDomain.cs:272</c>
    /// firm-name requirement. Mapped to HTTP 400 in
    /// <c>CaseEvaluationHttpApiHostModule</c> via
    /// <c>AbpExceptionHttpStatusCodeOptions</c>. Carries
    /// <c>WithData("AttorneyRole", "ApplicantAttorney" or "DefenseAttorney")</c>
    /// so the SPA can branch the field-highlight without parsing the
    /// message. Localization key
    /// <c>Appointment:AttorneyFirmNameRequired</c>.
    /// </summary>
    public const string AppointmentAttorneyFirmNameRequired =
        "CaseEvaluation:Appointment.AttorneyFirmNameRequired";

    /// <summary>
    /// 2026-05-13 -- raised by <c>ExternalSignupAppService.RegisterAsync</c>
    /// when the submitted email already maps to an existing
    /// <c>IdentityUser</c>. Replaces the prior throw that echoed the
    /// literal input email back (user-enumeration leak in a
    /// HIPAA-sensitive context). The message resolves to a generic
    /// "if-this-is-new-you-will-get-an-email" string, regardless of
    /// whether the account exists. Mapped to HTTP 400 in
    /// <c>CaseEvaluationHttpApiHostModule</c> via
    /// <c>AbpExceptionHttpStatusCodeOptions</c> -- ABP's default 403 for
    /// <c>BusinessException</c> would be semantically wrong (validation
    /// failure, not authorization). Localization key
    /// <c>Registration:DuplicateEmail</c>.
    /// </summary>
    public const string RegistrationDuplicateEmail =
        "CaseEvaluation:Registration.DuplicateEmail";

    /// <summary>
    /// BUG-025 (2026-05-21) -- raised by
    /// <c>AppointmentDocumentsAppService.EnsureFileSizeWithinLimit</c>
    /// when an upload exceeds <c>MaxFileSizeBytes</c> (10 MB). Carries
    /// <c>WithData("MaxBytes", ...)</c> and
    /// <c>WithData("ActualBytes", ...)</c> so the SPA can render a
    /// friendly "file too large" message with both numbers. Mapped to
    /// HTTP 413 Payload Too Large in
    /// <c>CaseEvaluationHttpApiHostModule</c> via
    /// <c>AbpExceptionHttpStatusCodeOptions</c> -- 413 is the RFC 7231
    /// canonical status for size-exceeded; ABP's default 403 for
    /// <c>BusinessException</c> would be semantically wrong (size, not
    /// authorization, is the issue). Localization key
    /// <c>AppointmentDocument:FileTooLarge</c>.
    /// </summary>
    public const string AppointmentDocumentFileTooLarge =
        "CaseEvaluation:AppointmentDocument.FileTooLarge";

    /// <summary>
    /// BUG-025 follow-up (2026-05-21) -- localized replacement for the
    /// previously-hardcoded <c>"File is empty."</c> string at three
    /// upload sites in <c>AppointmentDocumentsAppService</c>. Raised
    /// when the request supplies a zero-byte stream or
    /// <c>fileSize &lt;= 0</c>. Mapped to HTTP 400 Bad Request --
    /// distinct from <see cref="AppointmentDocumentFileTooLarge"/>
    /// (which is 413) so the SPA can branch the user-facing message
    /// without parsing the body. Localization key
    /// <c>AppointmentDocument:FileEmpty</c>.
    /// </summary>
    public const string AppointmentDocumentFileEmpty =
        "CaseEvaluation:AppointmentDocument.FileEmpty";

    /// <summary>
    /// 2026-05-15 -- raised by <c>DoctorsAppService.CreateAsync</c>
    /// when a non-deleted <c>Doctor</c> row already exists for the
    /// caller's tenant. Codifies the one-doctor-per-tenant invariant
    /// (<c>docs/parity/wave-1-parity/_parity-flags.md</c>
    /// PARITY-FLAG-NEW-006). Operators should use the tenant
    /// provisioning flow (<c>DoctorTenantAppService.CreateAsync</c>)
    /// for net-new doctors; the standard CRUD CreateAsync is reserved
    /// for legacy data fixes that should never re-create a duplicate.
    /// Mapped to HTTP 400 Bad Request in
    /// <c>CaseEvaluationHttpApiHostModule</c> (ABP's default 403 for
    /// <c>BusinessException</c> would make the SPA treat it as a
    /// permission failure). Localization key
    /// <c>Doctor:OnePerTenantViolated</c>.
    /// </summary>
    public const string DoctorOnePerTenantViolated =
        "CaseEvaluation:Doctor.OnePerTenantViolated";

    /// <summary>
    /// 2026-05-15 -- raised by <c>DoctorsAppService.DeleteAsync</c>
    /// when the tenant still has downstream rows that depend on the
    /// doctor (<c>DoctorAvailability</c>, <c>Appointment</c>, or an
    /// active <c>DoctorPreferredLocation</c>). Forces the operator to
    /// drain the schedule and reassign or cancel dependents before
    /// removing the Doctor profile. Carries <c>entity</c> + <c>count</c>
    /// via <c>WithData</c> so the SPA can render which bucket is
    /// non-empty. Mapped to HTTP 400 Bad Request. Localization key
    /// <c>Doctor:CannotDeleteWithDependents</c>.
    /// </summary>
    public const string DoctorCannotDeleteWithDependents =
        "CaseEvaluation:Doctor.CannotDeleteWithDependents";

    /// <summary>
    /// 2026-05-15 -- raised by
    /// <c>AppointmentsAppService.ValidateDoctorAvailabilityForBooking</c>
    /// when the slot's active-appointment count has reached or exceeded
    /// its <c>Capacity</c>. Carries <c>capacity</c> + <c>activeCount</c>
    /// via <c>WithData</c>. Mapped to HTTP 400 Bad Request. Localization
    /// key <c>Appointment:BookingSlotFull</c>.
    /// </summary>
    public const string AppointmentBookingSlotFull =
        "CaseEvaluation:Appointment.BookingSlotFull";

    /// <summary>
    /// 2026-05-15 -- raised by
    /// <c>AppointmentsAppService.ValidateDoctorAvailabilityForBooking</c>
    /// when the slot's <c>BookingStatusId</c> is <c>Reserved</c>
    /// (manually closed by the doctor's-admin -- never bookable
    /// regardless of capacity). Mapped to HTTP 400 Bad Request.
    /// Localization key <c>Appointment:BookingSlotClosed</c>.
    /// </summary>
    public const string AppointmentBookingSlotClosed =
        "CaseEvaluation:Appointment.BookingSlotClosed";

    /// <summary>
    /// 2026-05-15 -- raised by
    /// <c>AppointmentsAppService.ValidateDoctorAvailabilityForBooking</c>
    /// when the slot's <c>AppointmentTypes</c> set is non-empty and the
    /// requested <c>AppointmentTypeId</c> is not in it. Empty set means
    /// "any type accepted" and never raises this. Carries <c>requested</c>
    /// + <c>permitted</c> ids via <c>WithData</c>. Mapped to HTTP 400
    /// Bad Request. Localization key
    /// <c>Appointment:BookingSlotTypeMismatch</c>.
    /// </summary>
    public const string AppointmentBookingSlotTypeMismatch =
        "CaseEvaluation:Appointment.BookingSlotTypeMismatch";

    /// <summary>
    /// G-03-01 (2026-06-03) -- raised by
    /// <c>AppointmentDocumentTypeManager.UpdateAsync / DeleteAsync</c> when
    /// the targeted row is a reserved <c>IsSystem</c> category (e.g. the
    /// seeded "Generated Packet" tag auto-applied to generated documents).
    /// System rows are not editable or deletable by admins; the picker hides
    /// them. Mapped to HTTP 400 Bad Request in
    /// <c>CaseEvaluationHttpApiHostModule</c> (ABP's default 403 for
    /// <c>BusinessException</c> would make the SPA treat it as a permission
    /// failure rather than the input-validation failure it is). Localization
    /// key <c>AppointmentDocumentType:SystemReadOnly</c>.
    /// </summary>
    public const string AppointmentDocumentTypeSystemReadOnly =
        "CaseEvaluation:AppointmentDocumentType.SystemReadOnly";

    /// <summary>
    /// G-03-01 (2026-06-03) -- raised by
    /// <c>AppointmentDocumentTypeManager.CreateAsync / UpdateAsync</c> when
    /// another active row already uses the same name for the same
    /// <c>AppointmentTypeId</c> scope (case-insensitive). Restores the name
    /// uniqueness the legacy CRUD never enforced. Mapped to HTTP 400 Bad
    /// Request in <c>CaseEvaluationHttpApiHostModule</c>. Localization key
    /// <c>AppointmentDocumentType:NameAlreadyExists</c>.
    /// </summary>
    public const string AppointmentDocumentTypeNameAlreadyExists =
        "CaseEvaluation:AppointmentDocumentType.NameAlreadyExists";

    /// <summary>
    /// G-03-03 (PR2, 2026-06-04) -- raised by
    /// <c>AppointmentDocumentTypeManager.DeleteAsync</c> when the targeted
    /// category is still referenced by at least one <c>AppointmentDocument</c>
    /// (via <c>AppointmentDocumentTypeId</c>). Forces staff to retire the
    /// category (set inactive) rather than delete it out from under existing
    /// documents, preserving the type label on historical rows. Mapped to
    /// HTTP 409 Conflict in <c>CaseEvaluationHttpApiHostModule</c> (the request
    /// is well-formed and authorized; it conflicts with current state).
    /// Localization key <c>AppointmentDocumentType:InUse</c>.
    /// </summary>
    public const string AppointmentDocumentTypeInUse =
        "CaseEvaluation:AppointmentDocumentType.InUse";
}
