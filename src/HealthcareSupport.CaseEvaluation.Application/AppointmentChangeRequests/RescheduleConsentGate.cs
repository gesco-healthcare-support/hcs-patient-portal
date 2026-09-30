using Volo.Abp;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Phase 4c (2026-08-05) -- gate that blocks a reschedule FINALIZE until the current consent
/// round's solicited sides have all agreed. Pure/static, matching
/// <see cref="OpposingConsentValidator"/>, which this deliberately does NOT replace:
/// cancellation consent still lives on the request's flat columns and keeps using that one.
///
/// <para>Why a separate gate rather than reusing the parent's: on a reschedule the parent's two
/// consent sides are both <c>NotRequired</c>, and
/// <see cref="AppointmentChangeRequest.AreAllRequiredSidesGranted"/> returns TRUE for
/// both-NotRequired -- "nothing to consent". That is correct for a cancellation with no
/// representatives, but for a reschedule it would wave through a finalize with no consent
/// recorded at all. The round-shaped gate treats a MISSING round as a hard block instead.</para>
/// </summary>
internal static class RescheduleConsentGate
{
    /// <summary>
    /// Throws <c>ChangeRequestConsentNotGranted</c> when consent gating is enabled and either
    /// no date has been confirmed yet (<paramref name="currentRound"/> is null) or the current
    /// round has a solicited side that is not Approved. A Rejected / Expired side therefore
    /// blocks finalize and surfaces in the supervisor's mediation bucket, exactly as the cancel
    /// gate does -- staff resolve it by confirming a NEW date, which opens the next round.
    /// No-op when gating is disabled (feature flag off).
    /// </summary>
    public static void EnsureRoundConsentGranted(
        ChangeRequestConsentRound? currentRound,
        bool consentGatingEnabled)
    {
        if (!consentGatingEnabled)
        {
            return;
        }

        if (currentRound == null)
        {
            // No round means staff never confirmed a date, so there is nothing to move the
            // appointment TO and nobody has been asked anything.
            throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotGranted)
                .WithData("reason", "NoConfirmedDate");
        }

        if (currentRound.AreAllRequiredSidesGranted())
        {
            return;
        }

        throw new BusinessException(CaseEvaluationDomainErrorCodes.ChangeRequestConsentNotGranted)
            .WithData("roundNumber", currentRound.RoundNumber)
            .WithData("sideAConsent", currentRound.SideConsentStatus(ChangeRequestSide.SideA))
            .WithData("sideBConsent", currentRound.SideConsentStatus(ChangeRequestSide.SideB));
    }
}
