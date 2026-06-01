namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// External-user role enum for the public registration + invitation flows.
/// Lives in Domain.Shared (since 2026-05-15) because the
/// <see cref="Invitations.Invitation"/> domain entity references it.
///
/// <para>Four external roles, matching OLD's <c>Roles.cs:14-17</c>: Patient,
/// Adjuster (renamed <c>ClaimExaminer</c> in NEW), PatientAttorney (renamed
/// <c>ApplicantAttorney</c>), and DefenseAttorney. The single value-5
/// claim-examiner party is modeled once, as <c>ClaimExaminer = 2</c>.</para>
///
/// <para>G-06-05 (2026-06-01): the stray <c>Adjuster = 5</c> value was removed.
/// It was a redundant second name for the role NEW already calls
/// <c>ClaimExaminer</c> -- the role seeder, every role classifier, and the demo
/// data only ever used "Claim Examiner", so "Adjuster" was an unseeded,
/// permission-less dead-end. Do NOT renumber the remaining values; test seeds
/// and Angular references hardcode 1-4.</para>
/// </summary>
public enum ExternalUserType
{
    Patient = 1,
    ClaimExaminer = 2,
    ApplicantAttorney = 3,
    DefenseAttorney = 4,
}
