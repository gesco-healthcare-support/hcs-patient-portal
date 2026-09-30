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

        // Two-sided (2026-07-01): pass only when every side whose consent was required
        // (status != NotRequired) is Approved. Both-NotRequired (gating off / no reps /
        // side unresolved) also passes. A No/Expired on any required side blocks finalize
        // and surfaces in the supervisor's mediation bucket.
        if (request.AreAllRequiredSidesGranted())
        {
            return;
        }

        throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotGranted)
            .WithData("sideAConsent", request.SideConsentStatus(ChangeRequestSide.SideA))
            .WithData("sideBConsent", request.SideConsentStatus(ChangeRequestSide.SideB));
    }
}
