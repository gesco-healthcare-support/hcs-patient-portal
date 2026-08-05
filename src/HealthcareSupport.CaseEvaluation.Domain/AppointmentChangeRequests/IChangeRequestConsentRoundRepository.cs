using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// Reads over <see cref="ChangeRequestConsentRound"/> (epic phase 4c, 2026-08-05). Two
/// lookups the generic repository cannot express cheaply: resolving a consent token to the
/// round that owns it, and finding which round is CURRENT for a change request.
/// </summary>
public interface IChangeRequestConsentRoundRepository : IRepository<ChangeRequestConsentRound, Guid>
{
    /// <summary>
    /// The round holding <paramref name="tokenHash"/> on either side, or null when no round
    /// does. Superseded rounds are deliberately still matched: a party may click a link from
    /// a date that has since been replaced, and the landing page must be able to tell them
    /// that rather than show "invalid token".
    /// </summary>
    Task<ChangeRequestConsentRound?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The change request's current round -- the highest <c>RoundNumber</c> that has not been
    /// superseded -- or NULL when staff have not confirmed a date yet.
    ///
    /// <para>Named <c>Get</c> rather than <c>Find</c> to match the plan's contract, but it
    /// genuinely returns null and the nullable return type makes the compiler enforce the
    /// check at every call site. "No current round" is a normal state, not an error: it is
    /// exactly what a freshly submitted reschedule looks like.</para>
    /// </summary>
    Task<ChangeRequestConsentRound?> GetCurrentAsync(
        Guid appointmentChangeRequestId,
        CancellationToken cancellationToken = default);
}
