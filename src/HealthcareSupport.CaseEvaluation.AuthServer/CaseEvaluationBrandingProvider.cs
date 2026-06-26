using HealthcareSupport.CaseEvaluation.Localization;
using Microsoft.Extensions.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Ui.Branding;

namespace HealthcareSupport.CaseEvaluation;

/// <summary>
/// AuthServer brand name. Host (no tenant) = the "Evaluators" parent brand
/// (admin.localhost); offices keep the product name ("Appointment Portal").
///
/// <para>Per F1 (2026-06-26) the OFFICE identity on the login / account pages is
/// shown by the logo (or, with no uploaded logo, an artistic name-art wordmark) --
/// see <c>BrandingHeadViewComponent</c> -- and must NOT replace the product name in
/// the brand text + browser title. The office's own display name still drives the
/// post-login SPA chrome (Angular BrandingService) and the login logo, just not this
/// AppName.</para>
/// </summary>
[Dependency(ReplaceServices = true)]
public class CaseEvaluationBrandingProvider : DefaultBrandingProvider
{
    private readonly IStringLocalizer<CaseEvaluationResource> _localizer;
    private readonly ICurrentTenant _currentTenant;

    public CaseEvaluationBrandingProvider(
        IStringLocalizer<CaseEvaluationResource> localizer,
        ICurrentTenant currentTenant)
    {
        _localizer = localizer;
        _currentTenant = currentTenant;
    }

    public override string AppName =>
        _currentTenant.Id == null ? "Evaluators" : _localizer["AppName"];
}
