using HealthcareSupport.CaseEvaluation.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace HealthcareSupport.CaseEvaluation.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class CaseEvaluationController : AbpControllerBase
{
    protected CaseEvaluationController()
    {
        LocalizationResource = typeof(CaseEvaluationResource);
    }
}
