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
}
