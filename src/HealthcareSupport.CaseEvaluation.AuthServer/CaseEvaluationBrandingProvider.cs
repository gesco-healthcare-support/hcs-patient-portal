using HealthcareSupport.CaseEvaluation.Localization;
using Microsoft.Extensions.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace HealthcareSupport.CaseEvaluation;

/// <summary>
/// AuthServer brand name: always the product name ("Appointment Portal"), on both
/// the host (admin.localhost) and every office sign-in / account page.
///
/// <para>Per the F3 follow-up (2026-06-28) the brand TEXT stays constant everywhere;
/// identity is carried by the LOGO -- the "Evaluators" parent crest at host, the
/// office's uploaded logo or an artistic name-art wordmark at offices (see
/// <c>BrandingHeadViewComponent</c>). An earlier pass returned "Evaluators" at host
/// scope, but that duplicated in text what the crest already conveys.</para>
/// </summary>
[Dependency(ReplaceServices = true)]
public class CaseEvaluationBrandingProvider : DefaultBrandingProvider
{
    private readonly IStringLocalizer<CaseEvaluationResource> _localizer;

    public CaseEvaluationBrandingProvider(IStringLocalizer<CaseEvaluationResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
