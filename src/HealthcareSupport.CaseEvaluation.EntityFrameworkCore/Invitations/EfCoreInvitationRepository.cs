using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.Invitations;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.Invitations;

/// <summary>
/// EF Core implementation of <see cref="IInvitationRepository"/>. Adds the
/// hash-keyed lookup used by the InvitationManager; everything else
/// inherits from <see cref="EfCoreRepository{TDbContext, TEntity, TKey}"/>.
/// </summary>
public class EfCoreInvitationRepository
    : EfCoreRepository<CaseEvaluationDbContext, Invitation, Guid>,
      IInvitationRepository
{
    public EfCoreInvitationRepository(
        IDbContextProvider<CaseEvaluationDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<Invitation?> FindByTokenHashAsync(
        string tokenHash, CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .Where(x => x.TokenHash == tokenHash)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }
}
