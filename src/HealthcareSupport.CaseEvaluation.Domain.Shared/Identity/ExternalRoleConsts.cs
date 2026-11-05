using System.Collections.Generic;

namespace HealthcareSupport.CaseEvaluation.Identity;

/// <summary>
/// The external roles, defined ONCE (#692).
/// </summary>
/// <remarks>
/// <para>These four names were hardcoded in three places, none referencing
/// another: <c>ExternalUserRoleDataSeedContributor</c> created them, the same
/// method eleven lines later granted the booking baseline to a second literal,
/// and <c>AppointmentAccessorRules.RecognizedExternalRoles</c> decided which
/// roles the application recognises as external accessors.</para>
///
/// <para>The lists could disagree silently. Removing "Defense Attorney" from
/// the grant loop alone left 1,919 tests passing: the role was still created
/// and still assignable, and held nothing. A user granted it logs in
/// successfully and finds every booking action refused, which presents as a
/// permissions bug on a correctly-configured-looking role -- so the
/// investigation starts in the wrong place.</para>
///
/// <para>Lives in Domain.Shared because the two consumers sit in different
/// namespaces of Domain (Identity and AppointmentAccessors) and neither owns
/// the list more than the other.</para>
///
/// <para>ROLE-NAMING RECONCILIATION, 2026-05-04. OLD has exactly four external
/// roles (verified at
/// <c>P:\PatientPortalOld\PatientAppointment.Models\Enums\Roles.cs</c>):
/// Patient=4, Adjuster=5, PatientAttorney=6, DefenseAttorney=7. NEW renamed two
/// for clarity -- Adjuster to Claim Examiner, aligning with the
/// AppointmentClaimExaminer entity, and PatientAttorney to Applicant Attorney.
/// "Adjuster" and "Claim Examiner" are the SAME role; an earlier audit listed
/// both and was reconciled. OLD has no fifth role; the two paralegal roles
/// (2026-11-04) are NEW's own, added because most bookers are a paralegal.</para>
///
/// <para>UNIFYING THE THIRD LIST COUPLES TWO QUESTIONS, and that was decided
/// deliberately (Adrian, 2026-09-04). Which roles get PROVISIONED and which the
/// application RECOGNISES as external accessors are related but not identical;
/// they are the same six names today. If a future requirement needs a role
/// recognised without booking grants, or seeded without being an accessor, the
/// answer is to split these apart deliberately -- NOT to quietly add a fourth
/// literal somewhere, which is the state this replaced.</para>
///
/// <para>DO NOT point <c>MultiOfficeExternalRoleGrantsTests</c> at this list.
/// Its role names are a deliberate independent literal: if the assertion's
/// source and the code under test share a definition they move together, and
/// the test can never fail. Removing that duplication looks like finishing this
/// job and would silently destroy the protection it exists to preserve.</para>
/// </remarks>
public static class ExternalRoleConsts
{
    /// <summary>Patient.</summary>
    public const string Patient = "Patient";

    /// <summary>Claim Examiner (OLD "Adjuster").</summary>
    public const string ClaimExaminer = "Claim Examiner";

    /// <summary>Applicant Attorney (OLD "PatientAttorney").</summary>
    public const string ApplicantAttorney = "Applicant Attorney";

    /// <summary>Defense Attorney.</summary>
    public const string DefenseAttorney = "Defense Attorney";

    /// <summary>
    /// A law office's paralegal on the applicant side (2026-11-04). Granted
    /// exactly what <see cref="ApplicantAttorney"/> holds, but NOT the attorney
    /// of record: never matched by the appointment's party email columns, so it
    /// reaches a case only as its creator or an accessor.
    /// </summary>
    public const string ApplicantParalegal = "Applicant Paralegal";

    /// <summary>The defense-side counterpart of <see cref="ApplicantParalegal"/>.</summary>
    public const string DefenseParalegal = "Defense Paralegal";

    /// <summary>
    /// Every external role. The order is not significant -- both consumers
    /// iterate, and both tests assert membership rather than sequence.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Patient,
        ClaimExaminer,
        ApplicantAttorney,
        DefenseAttorney,
        ApplicantParalegal,
        DefenseParalegal,
    };

    /// <summary>
    /// The applicant side's law-office roles: the attorney and the paralegal.
    /// Use where "an attorney of the applicant side" means the office's PEOPLE
    /// (booking, change-request side, accessor management, the attorney packet),
    /// never where it means the attorney OF RECORD on an appointment.
    /// </summary>
    public static readonly IReadOnlyList<string> ApplicantLawOffice = new[]
    {
        ApplicantAttorney,
        ApplicantParalegal,
    };

    /// <summary>The defense side's law-office roles; see <see cref="ApplicantLawOffice"/>.</summary>
    public static readonly IReadOnlyList<string> DefenseLawOffice = new[]
    {
        DefenseAttorney,
        DefenseParalegal,
    };
}
