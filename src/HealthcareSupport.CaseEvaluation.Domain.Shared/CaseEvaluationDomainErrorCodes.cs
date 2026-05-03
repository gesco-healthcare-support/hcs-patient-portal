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
}
