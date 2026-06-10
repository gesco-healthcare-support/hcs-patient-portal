using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Group D (2026-06-09) -- gate that blocks the Staff Supervisor finalize until the
/// opposing side has granted consent. Pure/static, matching the existing
/// <see cref="ChangeRequestApprovalValidator"/> pattern.
/// </summary>
public static class OpposingConsentValidator
{
    /// <summary>
    /// Throws <c>ChangeRequestConsentNotGranted</c> when consent gating is enabled and
    /// the request's consent is not <see cref="ChangeRequestConsentStatus.Approved"/>.
    /// No-op when gating is disabled (feature flag off) or the request never required
    /// consent (<see cref="ChangeRequestConsentStatus.NotRequired"/> -- the defensive
    /// "routed straight to staff" path). A <c>Rejected</c> / <c>Expired</c> consent
    /// therefore still blocks auto-finalize, surfacing in the supervisor's
    /// "needs mediation" bucket; staff reject it through the normal reject path.
    /// </summary>
    public static void EnsureConsentGranted(AppointmentChangeRequest request, bool consentGatingEnabled)
    {
        Check.NotNull(request, nameof(request));

        if (!consentGatingEnabled)
        {
            return;
        }
        if (request.ConsentStatus == ChangeRequestConsentStatus.NotRequired)
        {
            return;
        }
        if (request.IsConsentGranted())
        {
            return;
        }

        throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotGranted)
            .WithData("consentStatus", request.ConsentStatus);
    }
}
