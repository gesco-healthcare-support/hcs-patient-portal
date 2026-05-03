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
}
