using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthcareSupport.CaseEvaluation.EntityFrameworkCore;
using HealthcareSupport.CaseEvaluation.SystemParameters;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace HealthcareSupport.CaseEvaluation.SystemParameters;

public class EfCoreSystemParameterRepository
    : EfCoreRepository<CaseEvaluationDbContext, SystemParameter, Guid>, ISystemParameterRepository
{
    public EfCoreSystemParameterRepository(IDbContextProvider<CaseEvaluationDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<SystemParameter?> GetCurrentTenantAsync(CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await queryable.FirstOrDefaultAsync(cancellationToken);
    }
}
