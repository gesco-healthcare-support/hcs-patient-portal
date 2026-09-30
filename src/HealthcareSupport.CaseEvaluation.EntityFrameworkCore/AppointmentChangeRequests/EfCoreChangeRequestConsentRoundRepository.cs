using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.AppointmentChangeRequests;

/// <summary>
/// EF implementation of <see cref="IChangeRequestConsentRoundRepository"/> (epic phase 4c,
/// 2026-08-05). Uses the same <c>CaseEvaluationDbContext</c> as the other custom per-tenant
/// repositories; ABP resolves the office connection at runtime, so one registration serves
/// the host and every office database.
/// </summary>
public class EfCoreChangeRequestConsentRoundRepository
    : EfCoreRepository<CaseEvaluationDbContext, ChangeRequestConsentRound, Guid>,
        IChangeRequestConsentRoundRepository
{
    public EfCoreChangeRequestConsentRoundRepository(
        IDbContextProvider<CaseEvaluationDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<ChangeRequestConsentRound?> FindByTokenHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenHash))
        {
            return null;
        }

        // Both hash columns are indexed, so this is two index seeks rather than a scan.
        var queryable = await GetQueryableAsync();
        return await AsyncExecuter.FirstOrDefaultAsync(
            queryable.Where(x =>
                x.SideAConsentTokenHash == tokenHash || x.SideBConsentTokenHash == tokenHash),
            GetCancellationToken(cancellationToken));
    }

    public virtual async Task<ChangeRequestConsentRound?> GetCurrentAsync(
        Guid appointmentChangeRequestId,
        CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await AsyncExecuter.FirstOrDefaultAsync(
            queryable
                .Where(x => x.AppointmentChangeRequestId == appointmentChangeRequestId
                    && x.SupersededAt == null)
                .OrderByDescending(x => x.RoundNumber),
            GetCancellationToken(cancellationToken));
    }
}
