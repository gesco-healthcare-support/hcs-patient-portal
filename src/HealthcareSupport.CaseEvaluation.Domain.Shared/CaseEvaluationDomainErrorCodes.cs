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
}
