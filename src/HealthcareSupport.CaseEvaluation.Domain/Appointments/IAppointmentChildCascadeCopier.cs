using System;
using System.Threading.Tasks;

namespace HealthcareSupport.CaseEvaluation.Appointments;

/// <summary>
/// Copies every child row of one appointment onto another (phase 4d, 2026-08-05). Used when
/// finalizing a reschedule, which now closes the old appointment and creates a new one rather than
/// moving a single row.
///
/// <para>WHY THIS EXISTS AT ALL. The epic originally locked "reuse the create pipeline" for the new
/// appointment. That cannot work: <c>AppointmentCreateDto</c> carries 16 scalars plus custom field
/// values and NO child collections, because the child cascade is CLIENT-SIDE -- the Angular booking
/// wizard fires six further POSTs after create. Finalize is a server-side staff action with no
/// wizard in the loop, so reusing that path would yield an appointment with custom field values and
/// nothing else.</para>
///
/// <para>WHY THE PER-GROUP RESULT. Bug F18 was a cascade copier that silently dropped 2 of 8 child
/// groups. Returning a count per group is what lets a caller and a test assert each group
/// individually instead of trusting one "it worked" boolean.</para>
///
/// <para>The implementation lives in the EntityFrameworkCore project because it clones rows through
/// EF's <c>CurrentValues</c>, which copies every mapped column automatically -- a column added to a
/// child entity later is carried with no change here. A hand-written property copy would reproduce
/// F18 one level down, at field granularity.</para>
/// </summary>
public interface IAppointmentChildCascadeCopier
{
    /// <summary>
    /// Copies all child groups from <paramref name="sourceAppointmentId"/> to
    /// <paramref name="targetAppointmentId"/>. Does NOT save; the caller's unit of work commits.
    /// </summary>
    Task<CopiedGroupCounts> CopyAllAsync(
        Guid sourceAppointmentId,
        Guid targetAppointmentId,
        Guid? tenantId);
}

/// <summary>
/// How many rows were copied per child group. Every group is named explicitly so a dropped group
/// shows up as a zero rather than hiding inside a total.
/// </summary>
public sealed record CopiedGroupCounts(
    int Accessors,
    int ApplicantAttorneys,
    int DefenseAttorneys,
    int ClaimExaminers,
    int EmployerDetails,
    int InjuryDetails,
    int BodyParts,
    int PrimaryInsurances,
    int CustomFieldValues,
    int Documents)
{
    /// <summary>Total rows copied across every group; for logging, never for assertions.</summary>
    public int Total =>
        Accessors + ApplicantAttorneys + DefenseAttorneys + ClaimExaminers + EmployerDetails +
        InjuryDetails + BodyParts + PrimaryInsurances + CustomFieldValues + Documents;
}
