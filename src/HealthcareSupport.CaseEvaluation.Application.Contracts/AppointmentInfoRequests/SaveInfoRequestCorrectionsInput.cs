using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.AppointmentInfoRequests;

/// <summary>
/// The external user's fix-it corrections for an InfoRequested appointment, as a
/// generic field-key -&gt; raw-value map (QA item L, 2026-06-30). Each key is a
/// flaggable-field key from the send-back-fields registry; the value is the new value
/// as a string (dates ISO/US, state/language ids and gender as their id/enum string),
/// parsed server-side per the field's descriptor. Only keys the open request flagged
/// are accepted (re-locked in <c>SaveCorrectionsAsync</c>); an absent or empty value
/// means "no change". Replaces the prior fixed set of typed properties so the feature
/// covers every flaggable field without per-field DTO churn.
/// </summary>
public class SaveInfoRequestCorrectionsInput
{
    public Dictionary<string, string?> Corrections { get; set; } = new();

    /// <summary>
    /// The corrected Claim Information (injury-detail) rows, when the open request flagged
    /// <c>claimInformation</c> (QA item 11, 2026-07-01). Null means "no change"; a non-null
    /// list is a full REPLACEMENT of the appointment's injury-detail collection (Claim
    /// Information cannot be modelled as scalar key/value pairs). Accepted only when
    /// <c>claimInformation</c> is in the flagged set, then written via direct repository
    /// access -- the corrections endpoint is the trust boundary, so external roles do not
    /// need the gated injury-details CRUD grants.
    /// </summary>
    public List<InjuryDetailCorrectionDto>? InjuryDetails { get; set; }
}
