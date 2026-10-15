using HealthcareSupport.CaseEvaluation.Localization;
using HealthcareSupport.CaseEvaluation.PhiAccess;
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

    /// <summary>
    /// The PHI access log. A service that returns patient data records the read here, once the
    /// caller is authorised and before the data is returned. Resolved lazily, so a service that
    /// never reads PHI pays nothing for it.
    /// </summary>
    protected PhiAccessRecorder PhiAccessRecorder => LazyServiceProvider.LazyGetRequiredService<PhiAccessRecorder>();
}
