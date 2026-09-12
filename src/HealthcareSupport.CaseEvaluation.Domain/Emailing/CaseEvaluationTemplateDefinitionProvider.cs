using Volo.Abp.TextTemplating;

namespace HealthcareSupport.CaseEvaluation.Emailing;

/// <summary>
/// Placeholder for ABP-managed text templates. Stays empty at MVP -- the
/// Transition emails (Approved / Rejected) build
/// their HTML body inline in
/// <see cref="HealthcareSupport.CaseEvaluation.Appointments.Handlers.StatusChangeEmailHandler"/>
/// rather than going through ABP's <c>TextTemplateManagement</c> machinery.
/// Inline strings are simpler for MVP demo; localization + admin-editable
/// templates + Razor partials are deferred to post-MVP cleanup (see
/// docs/plans/deferred-from-mvp.md).
///
/// Future wave consumers (scheduler-notifications recurring reminders,
/// magic-link patient invite, joint-declaration reminders) will start
/// populating this provider when they need richer template management.
/// </summary>
public class CaseEvaluationTemplateDefinitionProvider : TemplateDefinitionProvider
{
    public override void Define(ITemplateDefinitionContext context)
    {
        // Intentionally empty at MVP. Inline strings live in
        // StatusChangeEmailHandler. Future template definitions land here.
    }
}
