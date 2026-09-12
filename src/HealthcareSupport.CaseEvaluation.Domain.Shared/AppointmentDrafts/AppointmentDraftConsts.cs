namespace HealthcareSupport.CaseEvaluation.AppointmentDrafts;

public static class AppointmentDraftConsts
{
    /// <summary>
    /// #15 (2026-06-22): max length of the short, NON-PHI resume label (e.g. the
    /// appointment-type name). The PHI-bearing payload itself is an unbounded
    /// nvarchar(max) blob and is deliberately not length-checked here.
    /// </summary>
    public const int LabelMaxLength = 200;
}
