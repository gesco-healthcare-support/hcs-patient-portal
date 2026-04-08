using HealthcareSupport.CaseEvaluation.Localization;
using Volo.Abp.Application.Services;

namespace HealthcareSupport.CaseEvaluation;

/* Inherit your application services from this class.
 */
public abstract class CaseEvaluationAppService : ApplicationService
{
    protected CaseEvaluationAppService()
    {
        LocalizationResource = typeof(CaseEvaluationResource);
    }
}
