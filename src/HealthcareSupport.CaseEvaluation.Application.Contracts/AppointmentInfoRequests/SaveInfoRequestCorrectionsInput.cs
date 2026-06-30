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
}
