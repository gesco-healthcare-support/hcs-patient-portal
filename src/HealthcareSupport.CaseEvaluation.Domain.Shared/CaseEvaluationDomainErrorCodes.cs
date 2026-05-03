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
}
