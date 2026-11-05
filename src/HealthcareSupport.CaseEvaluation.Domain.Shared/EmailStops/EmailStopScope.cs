namespace HealthcareSupport.CaseEvaluation.EmailStops;

/// <summary>
/// 2026-11-05 (office decision 2) -- how far a staff "Stop emails" reaches. <see cref="Case"/> silences one
/// address on one appointment (office database); <see cref="AllCases"/> silences it on every case at every
/// practice (one host-level list). Integer values travel on the wire; never renumber.
/// </summary>
public enum EmailStopScope
{
    Case = 1,
    AllCases = 2,
}
