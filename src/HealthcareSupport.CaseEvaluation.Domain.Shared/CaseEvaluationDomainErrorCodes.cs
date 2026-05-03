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
    /// Raised by <c>NotificationTemplatesAppService.GetAsync</c> /
    /// <c>GetByCodeAsync</c> when the template id or code does not resolve
    /// to an active row in the current tenant scope. Localization key
    /// <c>NotificationTemplate:NotFound</c>.
    /// </summary>
    public const string NotificationTemplateNotFound =
        "CaseEvaluation:NotificationTemplate.NotFound";

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
}
