using Volo.Abp.TextTemplating;

namespace HealthcareSupport.CaseEvaluation.Emailing;

/// <summary>
/// Empty placeholder so Wave 1 capabilities (templates-email-sms, scheduler-notifications,
/// account-self-service, appointment-documents email flows) have a registered home for
/// their template definitions. ABP's <c>TextTemplateManagementDomainModule</c> picks this
/// up via DI auto-discovery -- no module-level registration required.
///
/// Per pre-Wave-0 plan E1, real templates land alongside the consumer that needs them
/// (e.g. magic-link patient invite belongs to the Wave 1 patient-onboarding cap).
/// </summary>
public class CaseEvaluationTemplateDefinitionProvider : TemplateDefinitionProvider
{
    public override void Define(ITemplateDefinitionContext context)
    {
        // Wave 1 capabilities populate this body. Intentionally empty at Wave 0.
    }
}
