using Microsoft.Extensions.Localization;
using HealthcareSupport.CaseEvaluation.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace HealthcareSupport.CaseEvaluation;

[Dependency(ReplaceServices = true)]
public class CaseEvaluationBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<CaseEvaluationResource> _localizer;

    public CaseEvaluationBrandingProvider(IStringLocalizer<CaseEvaluationResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
