namespace HealthcareSupport.CaseEvaluation.ExternalSignups;

/// <summary>
/// External-user role enum for the public registration flow.
///
/// <para>OLD parity (Phase 8, 2026-05-03; <c>Roles.cs:14-17</c>) defines four
/// external roles: Patient, Adjuster, PatientAttorney (renamed
/// <c>ApplicantAttorney</c> in NEW per <c>_old-docs-index.md</c> naming
/// override), and DefenseAttorney.</para>
///
/// <para><c>ClaimExaminer</c> is a NEW deviation that contradicts the locked
/// memory <c>project_role-model.md</c> ("Claim Examiner is metadata not a
/// role"). It is retained for now because Session A's tenant-invite flow
/// (<c>InviteExternalUserAsync</c>) and the Angular invite component
/// reference it. Phase 8 audit gap G1 tracks the eventual cleanup; for the
/// moment, the value persists with a documented deviation.</para>
///
/// <para>Numeric values: do NOT renumber. Existing rows in test seeds and
/// Angular references hardcode 1-4. <c>Adjuster = 5</c> is new in Phase 8;
/// adding at the end preserves wire compatibility.</para>
/// </summary>
public enum ExternalUserType
{
    Patient = 1,
    ClaimExaminer = 2,
    ApplicantAttorney = 3,
    DefenseAttorney = 4,
    Adjuster = 5,
}
